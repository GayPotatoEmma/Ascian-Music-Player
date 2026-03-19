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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public class PlaylistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
