using System;
using System.Collections.Generic;

namespace AscianMusicPlayer.Audio
{
    [Serializable]
    public class Playlist
    {
        public string Name { get; set; } = "New Playlist";
        public List<string> SongPaths { get; set; } = new();
        public Guid Id { get; set; } = Guid.NewGuid();

        public Playlist()
        {
        }

        public Playlist(string name)
        {
            Name = name;
        }
    }
}
