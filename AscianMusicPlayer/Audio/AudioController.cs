using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NAudio.Wave;
using Dalamud.Game.Config;

namespace AscianMusicPlayer.Audio
{
    public class AudioController : IDisposable
    {
        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;
        private float _currentVolume = 1.0f;
        private bool _weMutedGame = false;
        private uint _originalBgmVolume = 100;
        private DateTime _bgmUnmuteTime = DateTime.MinValue;
        private bool _pendingBgmUnmute = false;

        private IWavePlayer? _nextOutputDevice;
        private AudioFileReader? _nextAudioFile;
        private bool _isCrossfading = false;
        private DateTime _crossfadeStartTime;
        private Song? _nextSong;

        public event EventHandler? SongEnded;
        public event EventHandler<Song>? SongChanged;

        public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _outputDevice?.PlaybackState == PlaybackState.Paused;
        public bool HasAudio => _audioFile != null;

        public TimeSpan CurrentTime => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public void SetPosition(TimeSpan position)
        {
            if (_audioFile != null && position >= TimeSpan.Zero && position <= _audioFile.TotalTime)
            {
                _audioFile.CurrentTime = position;
            }
        }

        public void Pause()
        {
            _outputDevice?.Pause();
            if (Plugin.Settings.MuteBgmWhenPlaying)
            {
                MuteBgm(false);
            }
        }

        public void Resume()
        {
            _outputDevice?.Play();
            if (Plugin.Settings.MuteBgmWhenPlaying)
            {
                MuteBgm(true);
            }
        }

        public static List<LyricLine> ParseSyncedLyricsStatic(string lyricsText)
        {
            return ParseSyncedLyrics(lyricsText);
        }

        private static List<LyricLine> ParseSyncedLyrics(string lyricsText)
        {
            var lines = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(lyricsText)) return lines;

            var lrcPattern = @"\[(\d{1,2}):(\d{2})\.(\d{2,3})\](.*)";
            var matches = Regex.Matches(lyricsText, lrcPattern);

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value);
                    int seconds = int.Parse(match.Groups[2].Value);
                    int centiseconds = int.Parse(match.Groups[3].Value);

                    int milliseconds = match.Groups[3].Value.Length == 3 
                        ? centiseconds 
                        : centiseconds * 10;

                    string text = match.Groups[4].Value.Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(new LyricLine
                        {
                            Time = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                            Text = text
                        });
                    }
                }
            }

            return lines.OrderBy(l => l.Time).ToList();
        }

        public static List<Song> LoadSongs(string folderPath)
        {
            var songs = new List<Song>();
            if (!Directory.Exists(folderPath)) return songs;

            var searchOption = Plugin.Settings.SearchSubfolders 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            var files = Directory.EnumerateFiles(folderPath, "*.*", searchOption)
                .Where(s => 
                {
                    var ext = Path.GetExtension(s);
                    return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);
                });

            foreach (var file in files)
            {
                try
                {
                    var tfile = TagLib.File.Create(file);
                    var song = new Song
                    {
                        FilePath = file,
                        Title = tfile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                        Artist = tfile.Tag.FirstPerformer ?? "Unknown",
                        Album = tfile.Tag.Album ?? "Unknown",
                        AlbumArtist = tfile.Tag.FirstAlbumArtist ?? tfile.Tag.FirstPerformer ?? "Unknown",
                        TrackNumber = tfile.Tag.Track,
                        Duration = tfile.Properties.Duration
                    };

                    var lrcPath = Path.ChangeExtension(file, ".lrc");
                    if (File.Exists(lrcPath))
                    {
                        try
                        {
                            var lrcContent = File.ReadAllText(lrcPath);
                            song.SyncedLyrics = ParseSyncedLyrics(lrcContent);
                            if (song.SyncedLyrics.Count > 0)
                            {
                                Plugin.Log.Information($"Loaded {song.SyncedLyrics.Count} synced lyric lines from .lrc file for: {song.Title}");
                            }
                        }
                        catch (Exception lrcEx)
                        {
                            Plugin.Log.Warning($"Failed to load .lrc file for {song.Title}: {lrcEx.Message}");
                        }
                    }

                    songs.Add(song);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Failed to load metadata for {file}: {ex.Message}");
                }
            }
            return songs;
        }

        public void Play(Song song)
        {
            _pendingBgmUnmute = false;

            CleanupNextTrack();

            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            try
            {
                _audioFile = new AudioFileReader(song.FilePath);
                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(_audioFile);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;

                UpdateVolume();

                _outputDevice.Play();

                if (Plugin.Settings.MuteBgmWhenPlaying)
                {
                    MuteBgm(true);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Error playing audio: {song.FilePath}");

                Plugin.NotificationManager.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
                {
                    Content = $"Failed to play: {song.Title}\nError: {ex.Message}",
                    Type = Dalamud.Interface.ImGuiNotification.NotificationType.Error,
                    Minimized = false
                });

                SongEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetNextSong(Song? song)
        {
            _nextSong = song;
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception == null)
            {
                SongEnded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Plugin.Log.Error($"Playback stopped with error: {e.Exception.Message}");
            }
        }

        public void Stop()
        {
            CleanupNextTrack();

            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            MuteBgm(false);
        }

        public void UpdateVolume()
        {
            try
            {
                float vol;

                if (Plugin.Settings.BindToGameVolume)
                {
                    if (Plugin.GameConfig.TryGet((SystemConfigOption)Plugin.Settings.MusicChannel, out uint channelVol))
                    {
                        vol = channelVol / 100.0f;
                    }
                    else
                    {
                        vol = 1.0f;
                    }
                }
                else
                {
                    vol = Plugin.Settings.MusicVolume / 100.0f;
                }

                _currentVolume = vol;

                if (_audioFile != null && !_isCrossfading)
                {
                    _audioFile.Volume = _currentVolume;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Failed to update volume: {ex.Message}");
            }
        }

        private void MuteBgm(bool mute)
        {
            try
            {
                if (mute)
                {
                    _pendingBgmUnmute = false;

                    if (Plugin.GameConfig.TryGet(SystemConfigOption.SoundBgm, out uint currentVolume))
                    {
                        if (currentVolume > 0)
                        {
                            _originalBgmVolume = currentVolume;
                            Plugin.GameConfig.Set(SystemConfigOption.SoundBgm, 0u);
                            Plugin.Log.Information($"Muted game BGM (was {currentVolume})");
                        }
                        _weMutedGame = true;
                    }
                }
                else
                {
                    if (_weMutedGame)
                    {
                        _pendingBgmUnmute = true;
                        _bgmUnmuteTime = DateTime.UtcNow.AddMilliseconds(300);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to mute game BGM: {ex.Message}");
            }
        }

        public void CheckBgmUnmute()
        {
            if (_pendingBgmUnmute && DateTime.UtcNow >= _bgmUnmuteTime)
            {
                _pendingBgmUnmute = false;
                if (_weMutedGame)
                {
                    try
                    {
                        Plugin.GameConfig.Set(SystemConfigOption.SoundBgm, _originalBgmVolume);
                        _weMutedGame = false;
                        Plugin.Log.Information($"Restored game BGM to {_originalBgmVolume}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"Failed to restore game BGM: {ex.Message}");
                    }
                }
            }
        }

        public void UpdateCrossfade()
        {
            if (Plugin.Settings.CrossfadeDuration <= 0 || !IsPlaying || _audioFile == null)
                return;

            var timeRemaining = TotalTime - CurrentTime;

            if (!_isCrossfading && timeRemaining.TotalSeconds <= Plugin.Settings.CrossfadeDuration && _nextSong != null)
            {
                StartCrossfade(_nextSong);
            }

            if (_isCrossfading && _nextAudioFile != null && _audioFile != null)
            {
                var elapsed = (DateTime.UtcNow - _crossfadeStartTime).TotalSeconds;
                var progress = Math.Min(elapsed / Plugin.Settings.CrossfadeDuration, 1.0);

                _audioFile.Volume = _currentVolume * (float)(1.0 - progress);
                _nextAudioFile.Volume = _currentVolume * (float)progress;

                if (progress >= 1.0)
                {
                    CompleteCrossfade();
                }
            }
        }

        private void StartCrossfade(Song nextSong)
        {
            if (_isCrossfading || _nextOutputDevice != null)
                return;

            try
            {
                _nextAudioFile = new AudioFileReader(nextSong.FilePath);
                _nextOutputDevice = new WaveOutEvent();
                _nextOutputDevice.Init(_nextAudioFile);
                _nextOutputDevice.PlaybackStopped += OnNextPlaybackStopped;

                _nextAudioFile.Volume = 0f;

                _nextOutputDevice.Play();

                _isCrossfading = true;
                _crossfadeStartTime = DateTime.UtcNow;

                Plugin.Log.Debug($"Started crossfade to: {nextSong.Title}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to start crossfade to: {nextSong.FilePath}");
                CleanupNextTrack();
            }
        }

        private void CompleteCrossfade()
        {
            var completedSong = _nextSong;

            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            _outputDevice = _nextOutputDevice;
            _audioFile = _nextAudioFile;
            _nextOutputDevice = null;
            _nextAudioFile = null;

            if (_outputDevice != null)
            {
                _outputDevice.PlaybackStopped -= OnNextPlaybackStopped;
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
            }

            if (_audioFile != null)
            {
                _audioFile.Volume = _currentVolume;
            }

            _isCrossfading = false;
            _nextSong = null;

            Plugin.Log.Debug("Completed crossfade");

            if (completedSong != null)
            {
                SongChanged?.Invoke(this, completedSong);
            }
        }

        private void OnNextPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Plugin.Log.Error($"Next track playback stopped with error: {e.Exception.Message}");
                CleanupNextTrack();
            }
        }

        private void CleanupNextTrack()
        {
            if (_nextOutputDevice != null)
            {
                _nextOutputDevice.PlaybackStopped -= OnNextPlaybackStopped;
                _nextOutputDevice.Stop();
                _nextOutputDevice.Dispose();
                _nextOutputDevice = null;
            }
            if (_nextAudioFile != null)
            {
                _nextAudioFile.Dispose();
                _nextAudioFile = null;
            }
            _isCrossfading = false;
            _nextSong = null;
        }

        public void Dispose()
        {
            _pendingBgmUnmute = false;

            if (_weMutedGame)
            {
                try
                {
                    Plugin.GameConfig.Set(SystemConfigOption.SoundBgm, _originalBgmVolume);
                    _weMutedGame = false;
                    Plugin.Log.Information($"Restored game BGM to {_originalBgmVolume} on plugin unload");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Failed to restore game BGM on dispose: {ex.Message}");
                }
            }

            Stop();
        }

        public void ApplySettingsChange()
        {
            if (IsPlaying)
            {
                bool shouldMuteBgm = Plugin.Settings.MuteBgmWhenPlaying && Plugin.Settings.MusicChannel != 175;

                if (shouldMuteBgm && !_weMutedGame)
                {
                    MuteBgm(true);
                }
                else if (!shouldMuteBgm && _weMutedGame)
                {
                    MuteBgm(false);
                }
            }
        }
    }
}