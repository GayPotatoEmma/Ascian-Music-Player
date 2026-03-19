using System;
using System.Collections.Generic;
using System.Linq;
using AscianMusicPlayer.Data;

namespace AscianMusicPlayer.Audio
{
    public class PlaylistManager
    {
        private readonly DatabaseService _database;
        private List<PlaylistInfo> _playlistCache = new();

        public IReadOnlyList<PlaylistInfo> Playlists => _playlistCache;

        public PlaylistManager(DatabaseService database)
        {
            _database = database;
            RefreshCache();
        }

        private void RefreshCache()
        {
            var dbPlaylists = _database.GetAllPlaylists();
            _playlistCache = dbPlaylists.Select(p => new PlaylistInfo
            {
                Id = p.Id,
                Name = p.Name,
                SongCount = _database.GetPlaylistSongCount(p.Id)
            }).ToList();
        }

        public PlaylistInfo CreatePlaylist(string name)
        {
            var id = _database.CreatePlaylist(name);
            var playlist = new PlaylistInfo { Id = id, Name = name, SongCount = 0 };
            _playlistCache.Add(playlist);
            return playlist;
        }

        public void DeletePlaylist(Guid playlistId)
        {
            _database.DeletePlaylist(playlistId);
            _playlistCache.RemoveAll(p => p.Id == playlistId);
        }

        public void RenamePlaylist(Guid playlistId, string newName)
        {
            _database.RenamePlaylist(playlistId, newName);
            var playlist = _playlistCache.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.Name = newName;
            }
        }

        public void AddSongToPlaylist(Guid playlistId, string songPath)
        {
            if (_database.PlaylistContainsSong(playlistId, songPath))
                return;

            _database.AddSongToPlaylist(playlistId, songPath);
            var playlist = _playlistCache.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.SongCount++;
            }
        }

        public void RemoveSongFromPlaylist(Guid playlistId, string songPath)
        {
            _database.RemoveSongFromPlaylist(playlistId, songPath);
            var playlist = _playlistCache.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.SongCount = Math.Max(0, playlist.SongCount - 1);
            }
        }

        public PlaylistInfo? GetPlaylist(Guid playlistId)
        {
            return _playlistCache.FirstOrDefault(p => p.Id == playlistId);
        }

        public List<string> GetPlaylistSongPaths(Guid playlistId)
        {
            return _database.GetPlaylistSongs(playlistId);
        }

        public List<Song> GetPlaylistSongs(Guid playlistId, List<Song> allSongs)
        {
            var songPaths = _database.GetPlaylistSongs(playlistId);
            if (songPaths.Count == 0) return new List<Song>();

            var songPathSet = new HashSet<string>(songPaths);
            return allSongs.Where(s => songPathSet.Contains(s.FilePath)).ToList();
        }

        public bool PlaylistContainsSong(Guid playlistId, string songPath)
        {
            return _database.PlaylistContainsSong(playlistId, songPath);
        }

        public void MigrateFromConfig(List<Playlist> legacyPlaylists)
        {
            if (legacyPlaylists.Count == 0) return;

            _database.MigrateFromConfig(legacyPlaylists);
            RefreshCache();
        }
    }

    public class PlaylistInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SongCount { get; set; }
    }
}

