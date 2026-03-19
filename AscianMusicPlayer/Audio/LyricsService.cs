using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace AscianMusicPlayer.Audio
{
    public class LyricsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<LyricLine>> _lyricsCache = new();

        public LyricsService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AscianMusicPlayer/1.0");
        }

        public async Task<List<LyricLine>> FetchSyncedLyricsAsync(Song song)
        {
            string cacheKey = $"{song.Artist}|{song.Title}|{song.Album}";
            if (_lyricsCache.TryGetValue(cacheKey, out var cachedLyrics))
            {
                Plugin.Log.Debug($"Using cached lyrics for: {song.Title}");
                return cachedLyrics;
            }

            try
            {
                var trackName = HttpUtility.UrlEncode(song.Title);
                var artistName = HttpUtility.UrlEncode(song.Artist);
                var albumName = HttpUtility.UrlEncode(song.Album);
                var duration = (int)song.Duration.TotalSeconds;

                var url = $"https://lrclib.net/api/get?track_name={trackName}&artist_name={artistName}&album_name={albumName}&duration={duration}";

                Plugin.Log.Information($"Fetching lyrics from LRCLIB for: {song.Title} - {song.Artist}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Plugin.Log.Warning($"LRCLIB API returned status {response.StatusCode} for: {song.Title}");
                    _lyricsCache[cacheKey] = new List<LyricLine>();
                    return new List<LyricLine>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var lrcData = System.Text.Json.JsonSerializer.Deserialize<LrcLibResponse>(json, options);

                if (lrcData?.SyncedLyrics != null)
                {
                    Plugin.Log.Information($"Found synced lyrics for: {song.Title}");
                    var lyrics = AudioController.ParseSyncedLyricsStatic(lrcData.SyncedLyrics);
                    _lyricsCache[cacheKey] = lyrics;
                    return lyrics;
                }
                else if (lrcData?.PlainLyrics != null)
                {
                    Plugin.Log.Information($"Found plain lyrics (no timestamps) for: {song.Title}");
                    _lyricsCache[cacheKey] = new List<LyricLine>();
                    return new List<LyricLine>();
                }
                else
                {
                    Plugin.Log.Information($"No lyrics found for: {song.Title}");
                    _lyricsCache[cacheKey] = new List<LyricLine>();
                    return new List<LyricLine>();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error fetching lyrics for {song.Title}: {ex.Message}");
                return new List<LyricLine>();
            }
        }

        public void ClearCache()
        {
            _lyricsCache.Clear();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private class LrcLibResponse
        {
            [JsonPropertyName("syncedLyrics")]
            public string? SyncedLyrics { get; set; }

            [JsonPropertyName("plainLyrics")]
            public string? PlainLyrics { get; set; }
        }
    }
}
