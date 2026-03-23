using System;
using System.Collections.Generic;

namespace AscianMusicPlayer.Audio
{
    public class LyricLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class Song
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string Album { get; set; } = "Unknown Album";
        public string AlbumArtist { get; set; } = "Unknown Artist";
        public uint TrackNumber { get; set; } = 0;
        public TimeSpan Duration { get; set; }
        public List<LyricLine> SyncedLyrics { get; set; } = new();
        public int LyricsOffsetMs { get; set; } = 0;

        public string FormattedDuration => $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
        public bool HasSyncedLyrics => SyncedLyrics.Count > 0;
        public bool LrcLibLyricsAvailable { get; set; } = false;
    }
}