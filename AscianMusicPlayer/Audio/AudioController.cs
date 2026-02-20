using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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

        public event EventHandler? SongEnded;

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

        public AudioController()
        {
        }

        public List<Song> LoadSongs(string folderPath)
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
                    songs.Add(new Song
                    {
                        FilePath = file,
                        Title = tfile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                        Artist = tfile.Tag.FirstPerformer ?? "Unknown",
                        Album = tfile.Tag.Album ?? "Unknown",
                        Duration = tfile.Properties.Duration
                    });
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
                if (Plugin.GameConfig.TryGet((SystemConfigOption)Plugin.Settings.MusicChannel, out uint channelVol))
                {
                    float vol = channelVol / 100.0f;
                    _currentVolume = vol;

                    if (_audioFile != null)
                    {
                        _audioFile.Volume = _currentVolume;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Failed to read music channel volume: {ex.Message}");
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