using Dalamud.Configuration;
using System.Collections.Generic;
using AscianMusicPlayer.Audio;

namespace AscianMusicPlayer
{
    public class Settings : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool HasCompletedFirstLaunch = false;

        public string MediaFolder = string.Empty;
        public bool SearchSubfolders = false;
        public uint MusicChannel = 180;
        public bool MuteBgmWhenPlaying = true;

        public bool BindToGameVolume = true;
        public float MusicVolume = 100f;

        public bool ShowArtistColumn = true;
        public bool ShowAlbumColumn = true;
        public bool ShowLengthColumn = true;
        public bool ShowTrackNumberColumn = false;
        public bool ShowHasLyricsColumn = false;

        public bool ShowInDtr = false;
        public bool PrintSongToChat = false;

        public bool HideWithGameUi = true;
        public bool MiniPlayerTextScrolling = true;
        public int LyricsDisplayMode = 0;
        public bool LyricsLipSync = false;
        public int LipSyncStyle = 1;
        public int ChatBubbleStyleIndex = 4;
        public bool FetchLyricsOnline = false;
        public uint FlyTextLyricColor = 0xFF0000FF;

        public static readonly (string Name, ushort LogKind)[] ChatBubbleStyles =
        [
            ("Say",                   10),
            ("Yell",                  30),
            ("Shout",                 11),
            ("Tell",                  12),
            ("Party",                 14),
            ("Alliance",              15),
            ("Free Company",          24),
            ("PvP Team",              36),
            ("Cross-world Linkshell", 101),
            ("Linkshell",             16),
            ("Novice Network",        27),
        ];

        public float CrossfadeDuration = 0f;

        public int LyricsWindowWidth = 500;
        public int LyricsWindowHeight = 150;
        public int LyricsNextLineCount = 2;
        public float LyricsCurrentLineScale = 1.3f;
        public float LyricsNextLineScale = 0.9f;
        public float LyricsHorizontalAlignment = 0.5f;
        public float LyricsVerticalAlignment = 0.5f;
        public float LyricsWindowBgAlpha = 0.9f;
        public bool LyricsWindowClickthrough = false;

        public string LyricsSystemFontName = string.Empty;
        public uint LyricsCurrentLineColor = 0xFFFFFFFF;
        public uint LyricsNextLineColor = 0xFF999999;

        public bool LyricsCurrentLineOutlineEnabled = false;
        public int LyricsCurrentLineOutlineWidth = 1;
        public uint LyricsCurrentLineOutlineColor = 0xFF000000;

        public bool LyricsNextLineOutlineEnabled = false;
        public int LyricsNextLineOutlineWidth = 1;
        public uint LyricsNextLineOutlineColor = 0xFF000000;

        public bool LyricsShadowEnabled = false;
        public float LyricsShadowOpacity = 50f;

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
