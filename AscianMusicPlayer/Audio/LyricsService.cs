using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AscianMusicPlayer.Audio
{
    public class LyricsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, Task<List<LyricLine>>> _fetchCache = new();
        private readonly ConcurrentDictionary<string, List<LyricLine>> _lyricsCache = new();

        public LyricsService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AscianMusicPlayer/1.0");
        }

        public Task<List<LyricLine>> FetchSyncedLyricsAsync(Song song)
        {
            string cacheKey = $"{song.Artist}|{song.Title}|{song.Album}";
            return _fetchCache.GetOrAdd(cacheKey, _ => FetchSyncedLyricsInternalAsync(song));
        }

        private async Task<List<LyricLine>> FetchSyncedLyricsInternalAsync(Song song)
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
                    var empty = new List<LyricLine>();
                    _lyricsCache[cacheKey] = empty;
                    return empty;
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
                else
                {
                    if (lrcData?.PlainLyrics != null)
                        Plugin.Log.Information($"Found plain lyrics (no timestamps) for: {song.Title}");
                    else
                        Plugin.Log.Information($"No lyrics found for: {song.Title}");

                    var empty = new List<LyricLine>();
                    _lyricsCache[cacheKey] = empty;
                    return empty;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error fetching lyrics for {song.Title}: {ex.Message}");
                _fetchCache.TryRemove(cacheKey, out _);
                return new List<LyricLine>();
            }
        }

        public void ClearCache()
        {
            _fetchCache.Clear();
            _lyricsCache.Clear();
        }

        public async Task CheckLyricsAvailabilityInBackground(List<Song> songs, CancellationToken ct)
        {
            foreach (var song in songs)
            {
                if (ct.IsCancellationRequested) return;
                if (song.HasSyncedLyrics || song.LrcLibLyricsAvailable) continue;

                try
                {
                    string cacheKey = $"{song.Artist}|{song.Title}|{song.Album}";
                    List<LyricLine> lyrics;

                    if (_lyricsCache.TryGetValue(cacheKey, out var cached))
                    {
                        lyrics = cached;
                    }
                    else
                    {
                        var trackName = HttpUtility.UrlEncode(song.Title);
                        var artistName = HttpUtility.UrlEncode(song.Artist);
                        var albumName = HttpUtility.UrlEncode(song.Album);
                        var duration = (int)song.Duration.TotalSeconds;

                        var url = $"https://lrclib.net/api/get?track_name={trackName}&artist_name={artistName}&album_name={albumName}&duration={duration}";

                        var response = await _httpClient.GetAsync(url, ct);

                        if (!response.IsSuccessStatusCode)
                        {
                            _lyricsCache[cacheKey] = new List<LyricLine>();
                            await Task.Delay(200, ct);
                            continue;
                        }

                        var json = await response.Content.ReadAsStringAsync(ct);
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var lrcData = System.Text.Json.JsonSerializer.Deserialize<LrcLibResponse>(json, options);

                        if (lrcData?.SyncedLyrics != null)
                        {
                            lyrics = AudioController.ParseSyncedLyricsStatic(lrcData.SyncedLyrics);
                        }
                        else
                        {
                            lyrics = new List<LyricLine>();
                        }

                        _lyricsCache[cacheKey] = lyrics;
                        await Task.Delay(200, ct);
                    }

                    if (lyrics.Count > 0)
                    {
                        song.LrcLibLyricsAvailable = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warning($"Failed to check LRCLIB availability for {song.Title}: {ex.Message}");
                }
            }

            Plugin.Log.Information("Finished background lyrics availability check");
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
