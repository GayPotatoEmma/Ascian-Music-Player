using System;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using AscianMusicPlayer.Windows;
using AscianMusicPlayer.Audio;

namespace AscianMusicPlayer
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Ascian Music Player";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;

        public static Settings Settings { get; private set; } = null!;

        public WindowSystem WindowSystem = new("AscianMusicPlayer");
        public AudioController AudioController { get; private set; }
        public MainWindow MainWindow { get; private set; }
        public SettingsWindow SettingsWindow { get; private set; }
        public MiniPlayerWindow MiniPlayerWindow { get; private set; }
        private IDtrBarEntry? _dtrEntry;

        private DateTime _lastVolumeCheck = DateTime.MinValue;
        private string? _lastDtrText = null;
        private string? _lastDtrTooltip = null;

        public Plugin()
        {
            Settings = PluginInterface.GetPluginConfig() as Settings ?? new Settings();

            this.AudioController = new AudioController();

            this.MainWindow = new MainWindow(this);
            this.SettingsWindow = new SettingsWindow(this);
            this.MiniPlayerWindow = new MiniPlayerWindow(this);

            this.WindowSystem.AddWindow(this.MainWindow);
            this.WindowSystem.AddWindow(this.SettingsWindow);
            this.WindowSystem.AddWindow(this.MiniPlayerWindow);

            if (!string.IsNullOrEmpty(Settings.MediaFolder))
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
            var now = DateTime.UtcNow;
            if ((now - _lastVolumeCheck).TotalMilliseconds >= 250)
            {
                this.AudioController.UpdateVolume();
                _lastVolumeCheck = now;
            }
        }

        private void OnCommand(string command, string args)
        {
            var argList = args.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

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

                default:
                    Log.Warning($"Unknown command: /amp {argList[0]}");
                    Log.Information("Usage: /amp [play|pause|next|previous|shuffle|repeat]");
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
            _dtrEntry?.Remove();
            this.MainWindow.Cleanup();
            this.AudioController.Dispose();
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/amp");
            CommandManager.RemoveHandler("/ampmini");
            CommandManager.RemoveHandler("/ampsettings");
        }
    }
}