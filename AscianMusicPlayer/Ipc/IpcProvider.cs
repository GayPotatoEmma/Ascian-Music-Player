using System;
using Dalamud.Plugin.Ipc;

namespace AscianMusicPlayer.Ipc
{
    /// <summary>
    /// Exposes AscianMusicPlayer state and controls via Dalamud IPC.
    /// Consumer prefix: "AscianMusicPlayer."
    ///
    /// Read gates (Func):
    ///   GetTitle        → string    Current song title, empty if nothing loaded
    ///   GetArtist       → string    Current song artist
    ///   GetAlbum        → string    Current song album
    ///   GetDuration     → float     Total duration in seconds
    ///   GetPosition     → float     Current playback position in seconds
    ///   GetPlaybackState→ int       0=stopped, 1=playing, 2=paused
    ///   GetVolume       → float     Current volume 0-100
    ///   GetShuffle      → bool      Shuffle enabled
    ///   GetRepeatMode   → int       0=off, 1=all, 2=one
    ///
    /// Control gates (Action):
    ///   Play            ()          Resume or start
    ///   Pause           ()          Pause playback
    ///   Next            ()          Skip to next song
    ///   Previous        ()          Skip to previous song
    ///   ToggleShuffle   ()          Toggle shuffle
    ///   ToggleRepeat    ()          Cycle repeat mode
    ///   SetVolume       (float)     Set volume 0-100
    ///   SetPosition     (float)     Seek to position in seconds
    /// </summary>
    public sealed class IpcProvider : IDisposable
    {
        private readonly ICallGateProvider<string> _getTitle;
        private readonly ICallGateProvider<string> _getArtist;
        private readonly ICallGateProvider<string> _getAlbum;
        private readonly ICallGateProvider<float> _getDuration;
        private readonly ICallGateProvider<float> _getPosition;
        private readonly ICallGateProvider<int> _getPlaybackState;
        private readonly ICallGateProvider<float> _getVolume;
        private readonly ICallGateProvider<bool> _getShuffle;
        private readonly ICallGateProvider<int> _getRepeatMode;

        private readonly ICallGateProvider<object> _play;
        private readonly ICallGateProvider<object> _pause;
        private readonly ICallGateProvider<object> _next;
        private readonly ICallGateProvider<object> _previous;
        private readonly ICallGateProvider<object> _toggleShuffle;
        private readonly ICallGateProvider<object> _toggleRepeat;
        private readonly ICallGateProvider<float, object> _setVolume;
        private readonly ICallGateProvider<float, object> _setPosition;

        public IpcProvider(Plugin plugin)
        {
            var pi = Plugin.PluginInterface;

            _getTitle = pi.GetIpcProvider<string>("AscianMusicPlayer.GetTitle");
            _getTitle.RegisterFunc(() => plugin.MainWindow.GetCurrentSong()?.Title ?? string.Empty);

            _getArtist = pi.GetIpcProvider<string>("AscianMusicPlayer.GetArtist");
            _getArtist.RegisterFunc(() => plugin.MainWindow.GetCurrentSong()?.Artist ?? string.Empty);

            _getAlbum = pi.GetIpcProvider<string>("AscianMusicPlayer.GetAlbum");
            _getAlbum.RegisterFunc(() => plugin.MainWindow.GetCurrentSong()?.Album ?? string.Empty);

            _getDuration = pi.GetIpcProvider<float>("AscianMusicPlayer.GetDuration");
            _getDuration.RegisterFunc(() => (float)(plugin.MainWindow.GetCurrentSong()?.Duration.TotalSeconds ?? 0.0));

            _getPosition = pi.GetIpcProvider<float>("AscianMusicPlayer.GetPosition");
            _getPosition.RegisterFunc(() => (float)plugin.AudioController.CurrentTime.TotalSeconds);

            _getPlaybackState = pi.GetIpcProvider<int>("AscianMusicPlayer.GetPlaybackState");
            _getPlaybackState.RegisterFunc(() =>
                plugin.AudioController.IsPlaying ? 1 :
                plugin.AudioController.IsPaused  ? 2 : 0);

            _getVolume = pi.GetIpcProvider<float>("AscianMusicPlayer.GetVolume");
            _getVolume.RegisterFunc(() => Plugin.Settings.MusicVolume);

            _getShuffle = pi.GetIpcProvider<bool>("AscianMusicPlayer.GetShuffle");
            _getShuffle.RegisterFunc(() => plugin.MainWindow.IsShuffle);

            _getRepeatMode = pi.GetIpcProvider<int>("AscianMusicPlayer.GetRepeatMode");
            _getRepeatMode.RegisterFunc(() => plugin.MainWindow.GetRepeatMode());

            _play = pi.GetIpcProvider<object>("AscianMusicPlayer.Play");
            _play.RegisterAction(() =>
            {
                if (plugin.AudioController.IsPaused)
                    plugin.AudioController.Resume();
                else if (!plugin.AudioController.IsPlaying)
                    plugin.MainWindow.PlaySelectedSong();
            });

            _pause = pi.GetIpcProvider<object>("AscianMusicPlayer.Pause");
            _pause.RegisterAction(() =>
            {
                if (plugin.AudioController.IsPlaying)
                    plugin.AudioController.Pause();
            });

            _next = pi.GetIpcProvider<object>("AscianMusicPlayer.Next");
            _next.RegisterAction(() => plugin.MainWindow.PlayNextPublic());

            _previous = pi.GetIpcProvider<object>("AscianMusicPlayer.Previous");
            _previous.RegisterAction(() => plugin.MainWindow.PlayPreviousPublic());

            _toggleShuffle = pi.GetIpcProvider<object>("AscianMusicPlayer.ToggleShuffle");
            _toggleShuffle.RegisterAction(() => plugin.MainWindow.ToggleShufflePublic());

            _toggleRepeat = pi.GetIpcProvider<object>("AscianMusicPlayer.ToggleRepeat");
            _toggleRepeat.RegisterAction(() => plugin.MainWindow.ToggleRepeatPublic());

            _setVolume = pi.GetIpcProvider<float, object>("AscianMusicPlayer.SetVolume");
            _setVolume.RegisterAction(vol =>
            {
                Plugin.Settings.MusicVolume = Math.Clamp(vol, 0f, 100f);
                plugin.AudioController.UpdateVolume();
                plugin.SaveSettings();
            });

            _setPosition = pi.GetIpcProvider<float, object>("AscianMusicPlayer.SetPosition");
            _setPosition.RegisterAction(secs =>
                plugin.AudioController.SetPosition(TimeSpan.FromSeconds(secs)));
        }

        public void Dispose()
        {
            _getTitle.UnregisterFunc();
            _getArtist.UnregisterFunc();
            _getAlbum.UnregisterFunc();
            _getDuration.UnregisterFunc();
            _getPosition.UnregisterFunc();
            _getPlaybackState.UnregisterFunc();
            _getVolume.UnregisterFunc();
            _getShuffle.UnregisterFunc();
            _getRepeatMode.UnregisterFunc();

            _play.UnregisterAction();
            _pause.UnregisterAction();
            _next.UnregisterAction();
            _previous.UnregisterAction();
            _toggleShuffle.UnregisterAction();
            _toggleRepeat.UnregisterAction();
            _setVolume.UnregisterAction();
            _setPosition.UnregisterAction();
        }
    }
}
