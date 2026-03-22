using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AscianMusicPlayer.Data
{
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private bool _disposed;

        public DatabaseService(string pluginConfigDirectory)
        {
            var dbPath = Path.Combine(pluginConfigDirectory, "AscianMusicPlayer.db");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Playlists (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PlaylistSongs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlaylistId TEXT NOT NULL,
                    SongPath TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    AddedAt TEXT NOT NULL,
                    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                    UNIQUE(PlaylistId, SongPath)
                );

                CREATE INDEX IF NOT EXISTS IX_PlaylistSongs_PlaylistId ON PlaylistSongs(PlaylistId);

                CREATE TABLE IF NOT EXISTS SongSettings (
                    SongPath TEXT PRIMARY KEY,
                    LyricsOffsetMs INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS SongMetadataCache (
                    SongPath TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    Artist TEXT NOT NULL,
                    Album TEXT NOT NULL,
                    AlbumArtist TEXT NOT NULL,
                    TrackNumber INTEGER NOT NULL,
                    DurationTicks INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL
                );

                PRAGMA journal_mode=WAL;
            ";
            command.ExecuteNonQuery();
        }

        private SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public List<PlaylistDto> GetAllPlaylists()
        {
            var playlists = new List<PlaylistDto>();

            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, CreatedAt, UpdatedAt FROM Playlists ORDER BY Name";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                playlists.Add(new PlaylistDto
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    CreatedAt = DateTime.Parse(reader.GetString(2)),
                    UpdatedAt = DateTime.Parse(reader.GetString(3))
                });
            }

            return playlists;
        }

        public List<string> GetPlaylistSongs(Guid playlistId)
        {
            var songs = new List<string>();

            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SongPath FROM PlaylistSongs WHERE PlaylistId = @PlaylistId ORDER BY SortOrder";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                songs.Add(reader.GetString(0));
            }

            return songs;
        }

        public Guid CreatePlaylist(string name)
        {
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");

            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Playlists (Id, Name, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @CreatedAt, @UpdatedAt)";
            command.Parameters.AddWithValue("@Id", id.ToString());
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@CreatedAt", now);
            command.Parameters.AddWithValue("@UpdatedAt", now);
            command.ExecuteNonQuery();

            return id;
        }

        public void DeletePlaylist(Guid playlistId)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId; DELETE FROM Playlists WHERE Id = @PlaylistId";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());
            command.ExecuteNonQuery();
        }

        public void RenamePlaylist(Guid playlistId, string newName)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Playlists SET Name = @Name, UpdatedAt = @UpdatedAt WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", playlistId.ToString());
            command.Parameters.AddWithValue("@Name", newName);
            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        public void AddSongToPlaylist(Guid playlistId, string songPath)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO PlaylistSongs (PlaylistId, SongPath, SortOrder, AddedAt)
                VALUES (@PlaylistId, @SongPath, (SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM PlaylistSongs WHERE PlaylistId = @PlaylistId), @AddedAt)";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());
            command.Parameters.AddWithValue("@SongPath", songPath);
            command.Parameters.AddWithValue("@AddedAt", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();

            UpdatePlaylistTimestamp(connection, playlistId);
        }

        public void RemoveSongFromPlaylist(Guid playlistId, string songPath)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongPath = @SongPath";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());
            command.Parameters.AddWithValue("@SongPath", songPath);
            command.ExecuteNonQuery();

            UpdatePlaylistTimestamp(connection, playlistId);
        }

        public int GetPlaylistSongCount(Guid playlistId)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM PlaylistSongs WHERE PlaylistId = @PlaylistId";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public bool PlaylistContainsSong(Guid playlistId, string songPath)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongPath = @SongPath";
            command.Parameters.AddWithValue("@PlaylistId", playlistId.ToString());
            command.Parameters.AddWithValue("@SongPath", songPath);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public void MigrateFromConfig(List<Audio.Playlist> playlists)
        {
            if (playlists.Count == 0) return;

            using var connection = CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var playlist in playlists)
                {
                    using var checkCommand = connection.CreateCommand();
                    checkCommand.CommandText = "SELECT COUNT(*) FROM Playlists WHERE Id = @Id";
                    checkCommand.Parameters.AddWithValue("@Id", playlist.Id.ToString());

                    if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
                        continue;

                    var now = DateTime.UtcNow.ToString("O");

                    using var insertPlaylist = connection.CreateCommand();
                    insertPlaylist.CommandText = "INSERT INTO Playlists (Id, Name, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @CreatedAt, @UpdatedAt)";
                    insertPlaylist.Parameters.AddWithValue("@Id", playlist.Id.ToString());
                    insertPlaylist.Parameters.AddWithValue("@Name", playlist.Name);
                    insertPlaylist.Parameters.AddWithValue("@CreatedAt", now);
                    insertPlaylist.Parameters.AddWithValue("@UpdatedAt", now);
                    insertPlaylist.ExecuteNonQuery();

                    for (int i = 0; i < playlist.SongPaths.Count; i++)
                    {
                        using var insertSong = connection.CreateCommand();
                        insertSong.CommandText = "INSERT INTO PlaylistSongs (PlaylistId, SongPath, SortOrder, AddedAt) VALUES (@PlaylistId, @SongPath, @SortOrder, @AddedAt)";
                        insertSong.Parameters.AddWithValue("@PlaylistId", playlist.Id.ToString());
                        insertSong.Parameters.AddWithValue("@SongPath", playlist.SongPaths[i]);
                        insertSong.Parameters.AddWithValue("@SortOrder", i);
                        insertSong.Parameters.AddWithValue("@AddedAt", now);
                        insertSong.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static void UpdatePlaylistTimestamp(SqliteConnection connection, Guid playlistId)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Playlists SET UpdatedAt = @UpdatedAt WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", playlistId.ToString());
            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        public int GetLyricsOffset(string songPath)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT LyricsOffsetMs FROM SongSettings WHERE SongPath = @SongPath";
            command.Parameters.AddWithValue("@SongPath", songPath);

            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public Dictionary<string, int> GetAllLyricsOffsets(List<string> filePaths)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (filePaths.Count == 0) return result;

            using var connection = CreateConnection();
            connection.Open();

            const int chunkSize = 900;
            for (int offset = 0; offset < filePaths.Count; offset += chunkSize)
            {
                var chunk = filePaths.GetRange(offset, Math.Min(chunkSize, filePaths.Count - offset));
                using var command = connection.CreateCommand();
                var paramNames = new List<string>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramName = $"@p{i}";
                    paramNames.Add(paramName);
                    command.Parameters.AddWithValue(paramName, chunk[i]);
                }

                command.CommandText = $"SELECT SongPath, LyricsOffsetMs FROM SongSettings WHERE SongPath IN ({string.Join(",", paramNames)})";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result[reader.GetString(0)] = reader.GetInt32(1);
                }
            }

            return result;
        }

        public void SetLyricsOffset(string songPath, int offsetMs)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SongSettings (SongPath, LyricsOffsetMs) VALUES (@SongPath, @LyricsOffsetMs)
                ON CONFLICT(SongPath) DO UPDATE SET LyricsOffsetMs = @LyricsOffsetMs";
            command.Parameters.AddWithValue("@SongPath", songPath);
            command.Parameters.AddWithValue("@LyricsOffsetMs", offsetMs);
            command.ExecuteNonQuery();
        }

        public void CleanupMetadataCache(List<string> activeFilePaths)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                using var createTemp = connection.CreateCommand();
                createTemp.CommandText = "CREATE TEMP TABLE IF NOT EXISTS ActiveSongs (SongPath TEXT PRIMARY KEY)";
                createTemp.ExecuteNonQuery();

                using var clearTemp = connection.CreateCommand();
                clearTemp.CommandText = "DELETE FROM ActiveSongs";
                clearTemp.ExecuteNonQuery();

                const int chunkSize = 900;
                for (int offset = 0; offset < activeFilePaths.Count; offset += chunkSize)
                {
                    var chunk = activeFilePaths.GetRange(offset, Math.Min(chunkSize, activeFilePaths.Count - offset));
                    using var insertCmd = connection.CreateCommand();
                    var values = new List<string>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var paramName = $"@p{i}";
                        values.Add($"({paramName})");
                        insertCmd.Parameters.AddWithValue(paramName, chunk[i]);
                    }
                    insertCmd.CommandText = $"INSERT OR IGNORE INTO ActiveSongs (SongPath) VALUES {string.Join(",", values)}";
                    insertCmd.ExecuteNonQuery();
                }

                using var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM SongMetadataCache WHERE SongPath NOT IN (SELECT SongPath FROM ActiveSongs)";
                var deleted = deleteCmd.ExecuteNonQuery();

                using var dropTemp = connection.CreateCommand();
                dropTemp.CommandText = "DROP TABLE IF EXISTS ActiveSongs";
                dropTemp.ExecuteNonQuery();

                transaction.Commit();

                if (deleted > 0)
                    Plugin.Log.Information($"Cleaned up {deleted} stale metadata cache entries");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public Dictionary<string, SongMetadataDto> GetCachedMetadata(List<string> filePaths)
        {
            var result = new Dictionary<string, SongMetadataDto>(StringComparer.OrdinalIgnoreCase);
            if (filePaths.Count == 0) return result;

            using var connection = CreateConnection();
            connection.Open();

            const int chunkSize = 900;
            for (int offset = 0; offset < filePaths.Count; offset += chunkSize)
            {
                var chunk = filePaths.GetRange(offset, Math.Min(chunkSize, filePaths.Count - offset));

                using var command = connection.CreateCommand();
                var paramNames = new List<string>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var paramName = $"@p{i}";
                    paramNames.Add(paramName);
                    command.Parameters.AddWithValue(paramName, chunk[i]);
                }

                command.CommandText = $"SELECT SongPath, Title, Artist, Album, AlbumArtist, TrackNumber, DurationTicks, LastModified FROM SongMetadataCache WHERE SongPath IN ({string.Join(",", paramNames)})";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result[reader.GetString(0)] = new SongMetadataDto
                    {
                        Title = reader.GetString(1),
                        Artist = reader.GetString(2),
                        Album = reader.GetString(3),
                        AlbumArtist = reader.GetString(4),
                        TrackNumber = (uint)reader.GetInt64(5),
                        DurationTicks = reader.GetInt64(6),
                        LastModified = reader.GetInt64(7)
                    };
                }
            }

            return result;
        }

        public void SaveMetadataCache(List<(Audio.Song Song, long LastModifiedTicks)> entries)
        {
            if (entries.Count == 0) return;

            using var connection = CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var (song, lastModified) in entries)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO SongMetadataCache (SongPath, Title, Artist, Album, AlbumArtist, TrackNumber, DurationTicks, LastModified)
                        VALUES (@SongPath, @Title, @Artist, @Album, @AlbumArtist, @TrackNumber, @DurationTicks, @LastModified)";
                    command.Parameters.AddWithValue("@SongPath", song.FilePath);
                    command.Parameters.AddWithValue("@Title", song.Title);
                    command.Parameters.AddWithValue("@Artist", song.Artist);
                    command.Parameters.AddWithValue("@Album", song.Album);
                    command.Parameters.AddWithValue("@AlbumArtist", song.AlbumArtist);
                    command.Parameters.AddWithValue("@TrackNumber", (long)song.TrackNumber);
                    command.Parameters.AddWithValue("@DurationTicks", song.Duration.Ticks);
                    command.Parameters.AddWithValue("@LastModified", lastModified);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    public class PlaylistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SongMetadataDto
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumArtist { get; set; } = string.Empty;
        public uint TrackNumber { get; set; }
        public long DurationTicks { get; set; }
        public long LastModified { get; set; }
    }
}
