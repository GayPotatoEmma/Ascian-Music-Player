using System;
using System.Collections.Generic;
using System.Linq;

namespace AscianMusicPlayer.Audio
{
    public class PlaylistManager
    {
        private readonly List<Playlist> _playlists;

        public IReadOnlyList<Playlist> Playlists => _playlists;

        public PlaylistManager(List<Playlist> playlists)
        {
            _playlists = playlists;
        }

        public Playlist CreatePlaylist(string name)
        {
            var playlist = new Playlist(name);
            _playlists.Add(playlist);
            return playlist;
        }

        public void DeletePlaylist(Guid playlistId)
        {
            _playlists.RemoveAll(p => p.Id == playlistId);
        }

        public void RenamePlaylist(Guid playlistId, string newName)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.Name = newName;
            }
        }

        public void AddSongToPlaylist(Guid playlistId, string songPath)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null && !playlist.SongPaths.Contains(songPath))
            {
                playlist.SongPaths.Add(songPath);
            }
        }

        public void RemoveSongFromPlaylist(Guid playlistId, string songPath)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.SongPaths.Remove(songPath);
            }
        }

        public Playlist? GetPlaylist(Guid playlistId)
        {
            return _playlists.FirstOrDefault(p => p.Id == playlistId);
        }

        public List<Song> GetPlaylistSongs(Guid playlistId, List<Song> allSongs)
        {
            var playlist = GetPlaylist(playlistId);
            if (playlist == null) return new List<Song>();

            var songPathSet = new HashSet<string>(playlist.SongPaths);
            return allSongs.Where(s => songPathSet.Contains(s.FilePath)).ToList();
        }
    }
}
