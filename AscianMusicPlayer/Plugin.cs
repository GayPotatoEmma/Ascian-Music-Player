using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ManagedFontAtlas;
using AscianMusicPlayer.Windows;
using AscianMusicPlayer.Audio;

namespace AscianMusicPlayer
{
    public sealed class Plugin : IDalamudPlugin
    {

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
        [PluginService] public static INotificationManager NotificationManager { get; private set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] public static IFlyTextGui FlyTextGui { get; private set; } = null!;

        public static Settings Settings { get; private set; } = null!;

        public readonly WindowSystem WindowSystem = new("AscianMusicPlayer");
        public AudioController AudioController { get; private set; }
        public PlaylistManager PlaylistManager { get; private set; }
        public LyricsService LyricsService { get; private set; }
        public MainWindow MainWindow { get; private set; }
        public SettingsWindow SettingsWindow { get; private set; }
        public MiniPlayerWindow MiniPlayerWindow { get; private set; }
        public PlaylistWindow PlaylistWindow { get; private set; }
        public AboutWindow AboutWindow { get; private set; }
        public FirstLaunchWindow FirstLaunchWindow { get; private set; }
        public LyricsWindow LyricsWindow { get; private set; }
        public LyricsSettingsWindow LyricsSettingsWindow { get; private set; }
        private IDtrBarEntry? _dtrEntry;

        private IFontAtlas? _lyricsFontAtlas;
        private IFontHandle? _lyricsFontHandle;

        private DateTime _lastVolumeCheck = DateTime.MinValue;
        private string? _lastDtrText = null;
        private string? _lastDtrTooltip = null;

        private int _lastLyricIndex = -1;
        private Song? _currentLyricSong = null;
        private bool _isFetchingLyrics = false;
        private TimeSpan _lastKnownPosition = TimeSpan.Zero;

        public Plugin()
        {
            Settings = PluginInterface.GetPluginConfig() as Settings ?? new Settings();

            this.AudioController = new AudioController();
            this.PlaylistManager = new PlaylistManager(Settings.Playlists);
            this.LyricsService = new LyricsService();

            this.MainWindow = new MainWindow(this);
            this.SettingsWindow = new SettingsWindow(this);
            this.MiniPlayerWindow = new MiniPlayerWindow(this);
            this.PlaylistWindow = new PlaylistWindow(this);
            this.AboutWindow = new AboutWindow(this);
            this.FirstLaunchWindow = new FirstLaunchWindow(this);
            this.LyricsWindow = new LyricsWindow(this);
            this.LyricsSettingsWindow = new LyricsSettingsWindow(this);

            this.WindowSystem.AddWindow(this.MainWindow);
            this.WindowSystem.AddWindow(this.SettingsWindow);
            this.WindowSystem.AddWindow(this.MiniPlayerWindow);
            this.WindowSystem.AddWindow(this.PlaylistWindow);
            this.WindowSystem.AddWindow(this.AboutWindow);
            this.WindowSystem.AddWindow(this.FirstLaunchWindow);
            this.WindowSystem.AddWindow(this.LyricsWindow);
            this.WindowSystem.AddWindow(this.LyricsSettingsWindow);

            BuildLyricsFont();

            if (!Settings.HasCompletedFirstLaunch)
            {
                this.FirstLaunchWindow.IsOpen = true;
            }
            else if (!string.IsNullOrEmpty(Settings.MediaFolder))
            {
                try
                {
                    this.MainWindow.LoadSongs();
                    Log.Information($"Auto-loaded songs from: {Settings.MediaFolder}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to auto-load songs: {ex.Message}");
                }
            }

            CommandManager.AddHandler("/amp", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the main Ascian Music Player window.\n     Music controls: /amp [play|pause|next|previous|shuffle|repeat]"
            });

            CommandManager.AddHandler("/ampmini", new CommandInfo(OnCommandMini)
            {
                HelpMessage = "Opens the Ascian Mini Player."
            });

            CommandManager.AddHandler("/ampsettings", new CommandInfo(OnCommandSettings)
            {
                HelpMessage = "Opens the Ascian Music Player settings."
            });

            CommandManager.AddHandler("/ampplaylist", new CommandInfo(OnCommandPlaylist)
            {
                HelpMessage = "Opens the Ascian Music Player playlist manager."
            });

            CommandManager.AddHandler("/amplyrics", new CommandInfo(OnCommandLyrics)
            {
                HelpMessage = "Opens the Ascian Music Player lyrics window."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Framework.Update += OnFrameworkUpdate;

            if (Settings.ShowInDtr)
            {
                InitializeDtr();
            }
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (Settings.BindToGameVolume)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastVolumeCheck).TotalMilliseconds >= 250)
                {
                    this.AudioController.UpdateVolume();
                    _lastVolumeCheck = now;
                }
            }

            this.AudioController.CheckBgmUnmute();
            this.AudioController.UpdateCrossfade();

            UpdateSyncedLyrics();
        }

        private void UpdateSyncedLyrics()
        {
            if (!AudioController.IsPlaying)
            {
                return;
            }

            var currentSong = MainWindow.GetCurrentSong();
            if (currentSong == null)
            {
                LyricsWindow.SetCurrentSong(null);
                return;
            }

            if (_currentLyricSong != currentSong)
            {
                _currentLyricSong = currentSong;
                _lastLyricIndex = -1;
                _isFetchingLyrics = false;
                _lastKnownPosition = TimeSpan.Zero;

                LyricsWindow.SetCurrentSong(currentSong);

                if (!currentSong.HasSyncedLyrics && Settings.FetchLyricsOnline && !_isFetchingLyrics)
                {
                    _isFetchingLyrics = true;
                    _ = FetchLyricsForCurrentSong(currentSong);
                }
            }

            if (!currentSong.HasSyncedLyrics)
            {
                return;
            }

            var currentTime = AudioController.CurrentTime;

            if (Math.Abs((currentTime - _lastKnownPosition).TotalSeconds) > 1.0)
            {
                _lastLyricIndex = -1;
                for (int i = 0; i < currentSong.SyncedLyrics.Count; i++)
                {
                    if (currentTime >= currentSong.SyncedLyrics[i].Time)
                    {
                        _lastLyricIndex = i;
                    }
                    else
                    {
                        break;
                    }
                }
                _lastKnownPosition = currentTime;
            }
            else
            {
                _lastKnownPosition = currentTime;
            }

            for (int i = _lastLyricIndex + 1; i < currentSong.SyncedLyrics.Count; i++)
            {
                var lyricLine = currentSong.SyncedLyrics[i];

                if (currentTime >= lyricLine.Time)
                {
                    bool shouldUpdateLyric = false;

                    if (i + 1 < currentSong.SyncedLyrics.Count)
                    {
                        var nextLine = currentSong.SyncedLyrics[i + 1];
                        if (currentTime < nextLine.Time)
                        {
                            shouldUpdateLyric = true;
                        }
                    }
                    else
                    {
                        shouldUpdateLyric = true;
                    }

                    if (shouldUpdateLyric)
                    {
                        if (Settings.LyricsDisplayMode > 0)
                        {
                            PrintLyricToChat(lyricLine.Text);
                        }
                        _lastLyricIndex = i;
                        LyricsWindow.UpdateCurrentLyricIndex(i);
                    }
                    break;
                }
            }
        }

        private async Task FetchLyricsForCurrentSong(Song song)
        {
            try
            {
                var lyrics = await LyricsService.FetchSyncedLyricsAsync(song);
                if (lyrics.Count > 0)
                {
                    song.SyncedLyrics = lyrics;
                    _lastLyricIndex = -1;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to fetch lyrics: {ex.Message}");
            }
        }

        private async Task FetchLyricsManually(Song song)
        {
            try
            {
                var lyrics = await LyricsService.FetchSyncedLyricsAsync(song);
                if (lyrics.Count > 0)
                {
                    song.SyncedLyrics = lyrics;
                    ChatGui.Print($"Fetched {lyrics.Count} synced lyric lines!", "AMP", 56);
                    Log.Information($"Manually fetched {lyrics.Count} synced lyric lines for: {song.Title}");
                }
                else
                {
                    ChatGui.Print("No synced lyrics found online.", "AMP", 56);
                    Log.Information($"No synced lyrics found online for: {song.Title}");
                }
            }
            catch (Exception ex)
            {
                ChatGui.Print($"Error fetching lyrics: {ex.Message}", "AMP", 56);
                Log.Error($"Failed to manually fetch lyrics: {ex.Message}");
            }
        }

        private void PrintLyricToChat(string lyric)
        {
            try
            {
                if (Settings.LyricsDisplayMode == 2)
                {
                    FlyTextGui.AddFlyText(
                        FlyTextKind.Named,
                        1,
                        0,
                        0,
                        new SeString(new List<Payload> { new TextPayload($"♪ {lyric}") }),
                        SeString.Empty,
                        Settings.FlyTextLyricColor,
                        0,
                        0
                    );
                }
                else
                {
                    ChatGui.Print($"♪ {lyric}", "AMP", 56);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to print lyric: {ex.Message}");
            }
        }

        private void OnCommand(string command, string args)
        {
            var argList = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (argList.Length == 0)
            {
                this.MainWindow.Toggle();
                return;
            }

            switch (argList[0])
            {
                case "play":
                    if (AudioController.IsPaused)
                    {
                        AudioController.Resume();
                        UpdateDtr();
                        Log.Information("Resuming playback");
                    }
                    else if (!AudioController.IsPlaying)
                    {
                        MainWindow.PlaySelectedSong();
                        Log.Information("Starting playback");
                    }
                    break;

                case "pause":
                    if (AudioController.IsPlaying)
                    {
                        AudioController.Pause();
                        UpdateDtr();
                        Log.Information("Paused playback");
                    }
                    break;

                case "next":
                    MainWindow.PlayNextPublic();
                    Log.Information("Playing next song");
                    break;

                case "previous":
                case "prev":
                    MainWindow.PlayPreviousPublic();
                    Log.Information("Playing previous song");
                    break;

                case "shuffle":
                    MainWindow.ToggleShufflePublic();
                    Log.Information($"Shuffle: {(MainWindow.IsShuffle ? "ON" : "OFF")}");
                    break;

                case "repeat":
                    MainWindow.ToggleRepeatPublic();
                    string[] repeatModes = { "OFF", "ALL", "ONE" };
                    Log.Information($"Repeat: {repeatModes[MainWindow.GetRepeatMode()]}");
                    break;

                case "fetchlyrics":
                    var song = MainWindow.GetCurrentSong();
                    if (song == null)
                    {
                        ChatGui.Print("No song is currently playing.", "AMP", 56);
                    }
                    else
                    {
                        ChatGui.Print($"Fetching lyrics for: {song.Title}...", "AMP", 56);
                        _ = FetchLyricsManually(song);
                    }
                    break;

                default:
                    Log.Warning($"Unknown command: /amp {argList[0]}");
                    Log.Information("Usage: /amp [play|pause|next|previous|shuffle|repeat|fetchlyrics]");
                    break;
            }
        }

        private void OnCommandMini(string command, string args)
        {
            this.MiniPlayerWindow.Toggle();
        }

        private void OnCommandSettings(string command, string args)
        {
            this.SettingsWindow.Toggle();
        }

        private void OnCommandPlaylist(string command, string args)
        {
            this.PlaylistWindow.Toggle();
        }

        private void OnCommandLyrics(string command, string args)
        {
            this.LyricsWindow.Toggle();
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        private void DrawMainUI()
        {
            this.MainWindow.IsOpen = true;
        }

        private void DrawConfigUI()
        {
            this.SettingsWindow.IsOpen = true;
        }

        public void SaveSettings()
        {
            Settings.Save();
        }

        private void BuildLyricsFont()
        {
            try
            {
                _lyricsFontHandle?.Dispose();
                _lyricsFontAtlas?.Dispose();

                var maxScale = Math.Max(Settings.LyricsCurrentLineScale, Settings.LyricsNextLineScale);
                var fontSize = 30.0f * maxScale;

                _lyricsFontAtlas = PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Async);
                _lyricsFontHandle = _lyricsFontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(fontSize)));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to build lyrics font: {ex.Message}");
            }
        }

        public void RebuildLyricsFont()
        {
            BuildLyricsFont();
        }

        public IFontHandle? GetLyricsFontHandle()
        {
            return _lyricsFontHandle;
        }

        public float GetLyricsFontBaseSize()
        {
            var maxScale = Math.Max(Settings.LyricsCurrentLineScale, Settings.LyricsNextLineScale);
            return 30.0f * maxScale;
        }

        private void InitializeDtr()
        {
            try
            {
                _dtrEntry = DtrBar.Get("Ascian Music Player");
                _dtrEntry.Shown = true;
                _dtrEntry.OnClick += OnDtrClick;
                UpdateDtr();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to initialize DTR: {ex.Message}");
            }
        }

        private void OnDtrClick(DtrInteractionEvent e)
        {
            try
            {
                if (e.ClickType == MouseClickType.Left)
                {
                    MainWindow.Toggle();
                }
                else if (e.ClickType == MouseClickType.Right)
                {
                    if (AudioController.IsPlaying)
                    {
                        AudioController.Pause();
                    }
                    else if (AudioController.IsPaused)
                    {
                        AudioController.Resume();
                    }
                    UpdateDtr();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to handle DTR click: {ex.Message}");
            }
        }

        public void UpdateDtr()
        {
            if (_dtrEntry == null || !Settings.ShowInDtr) return;

            try
            {
                string newText;
                string newTooltip;

                if (AudioController.IsPlaying && AudioController.HasAudio)
                {
                    var currentSong = MainWindow.GetCurrentSong();
                    if (currentSong != null)
                    {
                        newText = $"♪ {currentSong.Title}";
                        newTooltip = $"{currentSong.Title}\n{currentSong.Artist} - {currentSong.Album}";
                    }
                    else
                    {
                        newText = "♪ Playing...";
                        newTooltip = "Ascian Music Player";
                    }
                }
                else
                {
                    newText = "♪ --";
                    newTooltip = "Ascian Music Player";
                }

                if (newText != _lastDtrText)
                {
                    _dtrEntry.Text = newText;
                    _lastDtrText = newText;
                }

                if (newTooltip != _lastDtrTooltip)
                {
                    _dtrEntry.Tooltip = newTooltip;
                    _lastDtrTooltip = newTooltip;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to update DTR: {ex.Message}");
            }
        }

        public void PrintNowPlayingToChat(Song song)
        {
            try
            {
                var message = $"Now playing: {song.Title} - {song.Artist}";
                ChatGui.Print(message, "AMP", 56);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to print to chat: {ex.Message}");
            }
        }

        public void ToggleDtr(bool enabled)
        {
            if (enabled && _dtrEntry == null)
            {
                InitializeDtr();
            }
            else if (!enabled && _dtrEntry != null)
            {
                _dtrEntry.Remove();
                _dtrEntry = null;
            }
        }

        public void Dispose()
        {
            Framework.Update -= OnFrameworkUpdate;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            _dtrEntry?.Remove();
            _lyricsFontHandle?.Dispose();
            _lyricsFontAtlas?.Dispose();
            this.MainWindow.Cleanup();
            this.AudioController.Dispose();
            this.LyricsService.Dispose();
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/amp");
            CommandManager.RemoveHandler("/ampmini");
            CommandManager.RemoveHandler("/ampsettings");
            CommandManager.RemoveHandler("/ampplaylist");
            CommandManager.RemoveHandler("/amplyrics");
        }
    }
}