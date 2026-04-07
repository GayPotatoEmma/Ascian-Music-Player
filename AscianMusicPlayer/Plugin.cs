using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text.Unicode;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
using Dalamud.Interface.GameFonts;
using AscianMusicPlayer.Windows;
using AscianMusicPlayer.Audio;
using AscianMusicPlayer.Data;

namespace AscianMusicPlayer
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
        [PluginService] public static INotificationManager NotificationManager { get; private set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] public static IFlyTextGui FlyTextGui { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

        public static Settings Settings { get; private set; } = null!;

        public readonly WindowSystem WindowSystem = new("AscianMusicPlayer");
        public AudioController AudioController { get; private set; }
        public DatabaseService Database { get; private set; }
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

        internal const float LyricsFontBaseSize = 30.0f;

        private static readonly ushort[] LyricsGlyphRanges = new FluentGlyphRangeBuilder()
            .With(UnicodeRanges.BasicLatin)
            .With(UnicodeRanges.Latin1Supplement)
            .With(UnicodeRanges.LatinExtendedA)
            .With(UnicodeRanges.LatinExtendedB)
            .With(UnicodeRanges.LatinExtendedAdditional)
            .With(UnicodeRanges.Cyrillic)
            .With(UnicodeRanges.CyrillicSupplement)
            .With(UnicodeRanges.GreekandCoptic)
            .With(UnicodeRanges.GreekExtended)
            .With(UnicodeRanges.GeneralPunctuation)
            .With(UnicodeRanges.CjkUnifiedIdeographs)
            .With(UnicodeRanges.CjkSymbolsandPunctuation)
            .With(UnicodeRanges.CjkCompatibilityIdeographs)
            .With(UnicodeRanges.Hiragana)
            .With(UnicodeRanges.Katakana)
            .With(UnicodeRanges.KatakanaPhoneticExtensions)
            .With(UnicodeRanges.HangulSyllables)
            .With(UnicodeRanges.HangulJamo)
            .With(UnicodeRanges.HangulCompatibilityJamo)
            .With(UnicodeRanges.Thai)
            .With(UnicodeRanges.Arabic)
            .With(UnicodeRanges.Hebrew)
            .With(UnicodeRanges.Devanagari)
            .With(UnicodeRanges.HalfwidthandFullwidthForms)
            .With(UnicodeRanges.LetterlikeSymbols)
            .With(UnicodeRanges.MiscellaneousSymbols)
            .With(UnicodeRanges.SuperscriptsandSubscripts)
            .Build();

        private static List<(string DisplayName, string FilePath)>? _registryFontsCache;
        private static readonly Dictionary<string, (string? Path, int FaceIndex)> _fontPathCache = new(StringComparer.OrdinalIgnoreCase);

        private IFontAtlas? _lyricsFontAtlas;
        private IFontHandle? _lyricsFontHandle;

        private DateTime _lastVolumeCheck = DateTime.MinValue;
        private string? _lastDtrText = null;
        private string? _lastDtrTooltip = null;

        private int _lastLyricIndex = -1;
        private Song? _currentLyricSong = null;
        private bool _isFetchingLyrics = false;
        private TimeSpan _lastKnownPosition = TimeSpan.Zero;
        private DateTime _lipSyncStopTime = DateTime.MinValue;

        public Plugin()
        {
            Settings = PluginInterface.GetPluginConfig() as Settings ?? new Settings();

                        this.AudioController = new AudioController();
                        this.Database = new DatabaseService(PluginInterface.ConfigDirectory.FullName);
                        this.PlaylistManager = new PlaylistManager(Database);

                        if (Settings.Playlists.Count > 0)
                        {
                            Log.Information($"Migrating {Settings.Playlists.Count} playlists to database...");
                            PlaylistManager.MigrateFromConfig(Settings.Playlists);
                            Settings.Playlists.Clear();
                            Settings.Save();
                            Log.Information("Playlist migration complete.");
                        }

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
            PluginInterface.UiBuilder.DisableUserUiHide = true;

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

            if (_lipSyncStopTime != DateTime.MinValue && DateTime.UtcNow >= _lipSyncStopTime)
            {
                SetPlayerLipSync(0);
                _lipSyncStopTime = DateTime.MinValue;
            }
        }

        private void UpdateSyncedLyrics()
        {
            if (!AudioController.IsPlaying)
            {
                if (_lipSyncStopTime != DateTime.MinValue)
                {
                    SetPlayerLipSync(0);
                    _lipSyncStopTime = DateTime.MinValue;
                }
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

                if (_lipSyncStopTime != DateTime.MinValue)
                {
                    SetPlayerLipSync(0);
                    _lipSyncStopTime = DateTime.MinValue;
                }

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

            var currentTime = AudioController.CurrentTime + TimeSpan.FromMilliseconds(currentSong.LyricsOffsetMs);

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
                        if (Settings.LyricsDisplayMode == 3 || Settings.LyricsLipSync)
                        {
                            var lipSyncBase = (ushort)(626 + Math.Clamp(Settings.LipSyncStyle, 0, 2) * 3);
                            var talkId = lyricLine.Text.Length < 20 ? lipSyncBase
                                : lyricLine.Text.Length < 50 ? (ushort)(lipSyncBase + 1)
                                : (ushort)(lipSyncBase + 2);
                            SetPlayerLipSync(talkId);
                            var stopSecs = i + 1 < currentSong.SyncedLyrics.Count
                                ? Math.Clamp((currentSong.SyncedLyrics[i + 1].Time - currentTime).TotalSeconds, 0.5, 3.0)
                                : 1.5;
                            _lipSyncStopTime = DateTime.UtcNow.AddSeconds(stopSecs);
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
                    song.LrcLibLyricsAvailable = true;
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
                    song.LrcLibLyricsAvailable = true;
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
                switch (Settings.LyricsDisplayMode)
                {
                    case 2:
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
                        break;
                    case 3:
                        ShowChatBubble(lyric);
                        break;
                    default:
                        ChatGui.Print($"♪ {lyric}", "AMP", 56);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to print lyric: {ex.Message}");
            }
        }

        private unsafe void ShowChatBubble(string lyric)
        {
            try
            {
                var localPlayer = ObjectTable.LocalPlayer;
                if (localPlayer == null) return;

                var logModule = RaptureLogModule.Instance();
                if (logModule == null) return;

                Utf8String sender = default;
                Utf8String message = default;
                sender.Ctor();
                message.Ctor();
                try
                {
                    sender.SetString(localPlayer.Name.TextValue);
                    message.SetString($"♪ {lyric}");
                    var logKind = Settings.ChatBubbleStyles[
                        Math.Clamp(Settings.ChatBubbleStyleIndex, 0, Settings.ChatBubbleStyles.Length - 1)
                    ].LogKind;
                    logModule->ShowMiniTalkPlayer(logKind, &sender, &message, (ushort)localPlayer.HomeWorld.RowId, true);
                }
                finally
                {
                    sender.Dtor();
                    message.Dtor();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to show chat bubble: {ex.Message}");
            }
        }

        private unsafe void SetPlayerLipSync(ushort id)
        {
            try
            {
                var localPlayer = ObjectTable.LocalPlayer;
                if (localPlayer == null) return;
                var character = (Character*)localPlayer.Address;
                character->Timeline.SetLipsOverrideTimeline(id);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to set lip sync: {ex.Message}");
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
                var fontSize = LyricsFontBaseSize * maxScale;

                _lyricsFontAtlas = PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Async);

                var fontName = string.IsNullOrEmpty(Settings.LyricsSystemFontName) ? "Dalamud Default" : Settings.LyricsSystemFontName;

                string? resolvedFontPath = null;
                int resolvedFaceIndex = 0;
                if (fontName != "Dalamud Default" && !fontName.StartsWith("Game: "))
                    (resolvedFontPath, resolvedFaceIndex) = GetSystemFontPath(fontName);

                _lyricsFontHandle = _lyricsFontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                {
                    switch (fontName)
                    {
                        case "Dalamud Default":
                            tk.AddDalamudDefaultFont(fontSize, LyricsGlyphRanges);
                            break;
                        case "Game: Axis":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.Axis, fontSize), null, null);
                            break;
                        case "Game: Jupiter":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.Jupiter, fontSize), null, null);
                            break;
                        case "Game: Jupiter Numeric":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.JupiterNumeric, fontSize), null, null);
                            break;
                        case "Game: Meidinger":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.Meidinger, fontSize), null, null);
                            break;
                        case "Game: Meidinger Mid":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.MiedingerMid, fontSize), null, null);
                            break;
                        case "Game: Trump Gothic":
                            tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.TrumpGothic, fontSize), null, null);
                            break;
                        default:
                            if (resolvedFontPath != null)
                                tk.AddFontFromFile(resolvedFontPath, new SafeFontConfig { SizePx = fontSize, FontNo = resolvedFaceIndex, GlyphRanges = LyricsGlyphRanges });
                            else
                                tk.AddDalamudDefaultFont(fontSize, LyricsGlyphRanges);
                            break;
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to build lyrics font: {ex.Message}");
            }
        }

        private static (string? Path, int FaceIndex) GetSystemFontPath(string fontName)
        {
            if (_fontPathCache.TryGetValue(fontName, out var cached))
                return cached;

            var result = ResolveSystemFontPath(fontName);
            _fontPathCache[fontName] = result;
            return result;
        }

        private static (string? Path, int FaceIndex) ResolveSystemFontPath(string fontName)
        {
            try
            {
                var entries = GetRegistryFontEntries();

                foreach (var (displayName, filePath) in entries)
                    if (displayName.Equals(fontName, StringComparison.OrdinalIgnoreCase))
                        return (filePath, 0);

                foreach (var (displayName, filePath) in entries)
                    if (fontName.StartsWith(displayName, StringComparison.OrdinalIgnoreCase))
                        return (filePath, 0);

                var fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                var localFontsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Fonts");

                foreach (var folder in new[] { fontsFolder, localFontsFolder })
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (var ext in new[] { ".ttf", ".otf", ".ttc" })
                    {
                        foreach (var file in Directory.GetFiles(folder, $"*{ext}", SearchOption.TopDirectoryOnly))
                        {
                            var stem = Path.GetFileNameWithoutExtension(file);
                            if (stem.Equals(fontName, StringComparison.OrdinalIgnoreCase) ||
                                stem.Equals(fontName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                                stem.Replace("-", " ").Equals(fontName, StringComparison.OrdinalIgnoreCase) ||
                                stem.Replace("_", " ").Equals(fontName, StringComparison.OrdinalIgnoreCase))
                            {
                                return (file, 0);
                            }
                        }
                    }
                }

                return (null, 0);
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to find font path for '{fontName}': {ex.Message}");
                return (null, 0);
            }
        }

        internal static IReadOnlyList<(string DisplayName, string FilePath)> GetRegistryFontEntries()
        {
            if (_registryFontsCache != null)
                return _registryFontsCache;

            var entries = new List<(string DisplayName, string FilePath)>();
            var fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var localFontsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");

            void ScanRegistry(Microsoft.Win32.RegistryKey? hive, string subKey, string baseFolder)
            {
                using var key = hive?.OpenSubKey(subKey);
                if (key == null) return;
                foreach (var valueName in key.GetValueNames())
                {
                    var displayName = valueName
                        .Replace(" (TrueType)", "")
                        .Replace(" (OpenType)", "")
                        .Replace(" (All Res)", "")
                        .Trim();

                    var fileName = key.GetValue(valueName) as string;
                    if (string.IsNullOrEmpty(fileName)) continue;

                    if (!Path.IsPathRooted(fileName))
                        fileName = Path.Combine(baseFolder, fileName);

                    if (File.Exists(fileName))
                        entries.Add((displayName, fileName));
                }
            }

            ScanRegistry(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", fontsFolder);
            ScanRegistry(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Fonts", fontsFolder);
            ScanRegistry(Microsoft.Win32.Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", localFontsFolder);

            _registryFontsCache = entries;
            return _registryFontsCache;
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
            return LyricsFontBaseSize * maxScale;
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
            SetPlayerLipSync(0);
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
            this.Database.Dispose();
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/amp");
            CommandManager.RemoveHandler("/ampmini");
            CommandManager.RemoveHandler("/ampsettings");
            CommandManager.RemoveHandler("/ampplaylist");
            CommandManager.RemoveHandler("/amplyrics");
        }
    }
}