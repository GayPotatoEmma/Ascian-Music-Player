using Dalamud.Configuration;
using System.Collections.Generic;
using AscianMusicPlayer.Audio;

namespace AscianMusicPlayer
{
    public class Settings : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public string MediaFolder = string.Empty;
        public bool SearchSubfolders = false;
        public uint MusicChannel = 180;
        public bool MuteBgmWhenPlaying = true;

        public bool BindToGameVolume = true;
        public float MusicVolume = 100f;

        public float TitleColumnWidth = 0;
        public float ArtistColumnWidth = 100;
        public float AlbumColumnWidth = 100;
        public float LengthColumnWidth = 85;

        public bool ShowArtistColumn = true;
        public bool ShowAlbumColumn = true;
        public bool ShowLengthColumn = true;

        public bool ShowInDtr = false;
        public bool PrintSongToChat = false;

        public float CrossfadeDuration = 0f;

        public List<Playlist> Playlists { get; set; } = new();

        public const uint ChannelBgm = 175;
        public const uint ChannelSe = 176;
        public const uint ChannelVoice = 177;
        public const uint ChannelEnv = 178;
        public const uint ChannelSystem = 179;
        public const uint ChannelPerform = 180;

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
