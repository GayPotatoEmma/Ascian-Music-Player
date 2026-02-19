using System;

namespace AscianMusicPlayer.Audio
{
    public class Song
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string Album { get; set; } = "Unknown Album";
        public TimeSpan Duration { get; set; }

        public string FormattedDuration => $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
    }
}