using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private CancellationTokenSource? _bgmUnmuteCts;

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
            // Restore game BGM when pausing music
            if (Plugin.Settings.MuteBgmWhenPlaying)
            {
                MuteBgm(false);
            }
        }

        public void Resume()
        {
            _outputDevice?.Play();
            // Mute game BGM when resuming music
            if (Plugin.Settings.MuteBgmWhenPlaying)
            {
                MuteBgm(true);
            }
        }

        public AudioController()
        {
            // No initialization needed for basic approach
        }

        public List<Song> LoadSongs(string folderPath)
        {
            var songs = new List<Song>();
            if (!Directory.Exists(folderPath)) return songs;

            var searchOption = Plugin.Settings.SearchSubfolders 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            // Use EnumerateFiles instead of GetFiles - lazy enumeration
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
            Stop();

            try
            {
                _audioFile = new AudioFileReader(song.FilePath);
                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(_audioFile);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
                _outputDevice.Play();

                // Sync to performance volume instead of BGM
                UpdateVolume();

                // Mute game BGM only if setting is enabled
                if (Plugin.Settings.MuteBgmWhenPlaying)
                {
                    MuteBgm(true);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error playing audio: {ex.Message}");
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // If playback stopped naturally (not by user), trigger song ended event
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

            // Restore game BGM
            MuteBgm(false);
        }

        public void UpdateVolume()
        {
            try
            {
                if (Plugin.GameConfig.TryGet((SystemConfigOption)Plugin.Settings.MusicChannel, out uint channelVol))
                {
                    float vol = channelVol / 100.0f;
                    
                    // Only update if volume actually changed
                    if (Math.Abs(_currentVolume - vol) > 0.001f)
                    {
                        _currentVolume = vol;
                        if (_audioFile != null)
                        {
                            _audioFile.Volume = _currentVolume;
                        }
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
                    // Cancel any pending unmute operation
                    _bgmUnmuteCts?.Cancel();
                    _bgmUnmuteCts?.Dispose();
                    _bgmUnmuteCts = null;

                    // Save current BGM volume and mute
                    if (Plugin.GameConfig.TryGet(SystemConfigOption.SoundBgm, out uint currentVolume))
                    {
                        _originalBgmVolume = currentVolume;
                        if (currentVolume > 0)
                        {
                            Plugin.GameConfig.Set(SystemConfigOption.SoundBgm, 0u);
                            _weMutedGame = true;
                            Plugin.Log.Information($"Muted game BGM (was {currentVolume})");
                        }
                    }
                }
                else
                {
                    // Restore BGM volume only if we muted it, with a delay to prevent audio blips between songs
                    if (_weMutedGame)
                    {
                        // Cancel any previous pending unmute
                        _bgmUnmuteCts?.Cancel();
                        _bgmUnmuteCts?.Dispose();

                        _bgmUnmuteCts = new CancellationTokenSource();
                        var token = _bgmUnmuteCts.Token;

                        Task.Delay(300, token).ContinueWith(t =>
                        {
                            if (!t.IsCanceled && _weMutedGame)
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
                        }, TaskScheduler.Default);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to mute game BGM: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _bgmUnmuteCts?.Cancel();
            _bgmUnmuteCts?.Dispose();
            _bgmUnmuteCts = null;
            Stop();
        }

        public void ApplySettingsChange()
        {
            // If music is currently playing, reapply BGM muting based on current settings
            if (IsPlaying)
            {
                // Check if BGM should be muted based on new settings
                bool shouldMuteBgm = Plugin.Settings.MuteBgmWhenPlaying && Plugin.Settings.MusicChannel != 175;

                if (shouldMuteBgm && !_weMutedGame)
                {
                    // Settings now require BGM to be muted, but it's not - mute it
                    MuteBgm(true);
                }
                else if (!shouldMuteBgm && _weMutedGame)
                {
                    // Settings no longer require BGM to be muted, but it is - restore it
                    MuteBgm(false);
                }
            }
        }
    }
}