using Dalamud.Configuration;

namespace AscianMusicPlayer
{
    public class Settings : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public string MediaFolder { get; set; } = string.Empty;
        public bool SearchSubfolders { get; set; } = false;
        public uint MusicChannel { get; set; } = 180;
        public bool MuteBgmWhenPlaying { get; set; } = true;

        public float TitleColumnWidth { get; set; } = 0;
        public float ArtistColumnWidth { get; set; } = 100;
        public float AlbumColumnWidth { get; set; } = 100;
        public float LengthColumnWidth { get; set; } = 85;

        public bool ShowArtistColumn { get; set; } = true;
        public bool ShowAlbumColumn { get; set; } = true;
        public bool ShowLengthColumn { get; set; } = true;

        public bool ShowInDtr { get; set; } = false;

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
