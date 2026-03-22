using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;

namespace AscianMusicPlayer.Windows
{
    public class FirstLaunchWindow : PluginWindow
    {
        private readonly Plugin _plugin;
        private readonly FileDialogManager _fileDialogManager;
        private string _mediaFolderInput = string.Empty;
        private bool _searchSubfoldersInput = false;
        private bool _muteBgmInput = true;
        private bool _bindToGameVolumeInput = true;
        private bool _showInDtrInput = false;
        private int _selectedChannel = 4;

        private readonly string[] _channelNames =
        {
            "Sound Effects",
            "Voice",
            "Ambient Sounds",
            "System Sounds",
            "Performance"
        };

        private readonly uint[] _channelIds = 
        {
            Settings.ChannelSe,
            Settings.ChannelVoice,
            Settings.ChannelEnv,
            Settings.ChannelSystem,
            Settings.ChannelPerform
        };

        public FirstLaunchWindow(Plugin plugin) : base("Welcome to Ascian Music Player!###AscianMusicPlayerFirstLaunch")
        {
            _plugin = plugin;
            var height = Util.IsWine() ? 485 : 450;
            this.Size = new Vector2(500, height);
            this.SizeCondition = ImGuiCond.Appearing;
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

            _fileDialogManager = new FileDialogManager();
            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _searchSubfoldersInput = Plugin.Settings.SearchSubfolders;
            _muteBgmInput = Plugin.Settings.MuteBgmWhenPlaying;
            _bindToGameVolumeInput = Plugin.Settings.BindToGameVolume;
            _showInDtrInput = Plugin.Settings.ShowInDtr;

            _selectedChannel = System.Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;
        }

        public override void Draw()
        {
            _fileDialogManager.Draw();

            ImGui.TextWrapped("Thank you for installing Ascian Music Player! Let's get you set up.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Media Folder");
            ImGui.TextWrapped("Select the folder containing your music files (.mp3, .wav, .flac).");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(-100);
            ImGui.InputText("##MediaFolder", ref _mediaFolderInput, 500);
            ImGui.SameLine();
            if (ImGui.Button("Browse...", new Vector2(90, 0)))
            {
                _fileDialogManager.OpenFolderDialog("Select Music Folder", (success, path) =>
                {
                    if (success && !string.IsNullOrEmpty(path))
                    {
                        _mediaFolderInput = path;
                    }
                }, _mediaFolderInput);
            }

            if (!string.IsNullOrEmpty(_mediaFolderInput) && !Directory.Exists(_mediaFolderInput))
            {
                ImGui.Spacing();
                using var color = ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped("Invalid folder path. Please select a valid media folder to continue.");
            }

            if (Util.IsWine())
            {
                ImGui.Spacing();
                using var color = ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
                ImGui.TextWrapped("You appear to be running the game under Wine. This may cause playback issues with 24-bit audio files.");
            }

            ImGui.Spacing();

            ImGui.Checkbox("Search subfolders for music files", ref _searchSubfoldersInput);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, the plugin will search all subfolders within your media folder for music files.");
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Audio Settings");
            ImGui.Spacing();

            ImGui.Checkbox("Mute game background music when playing", ref _muteBgmInput);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Automatically mutes FFXIV's background music while your music is playing.");
            }

            ImGui.Spacing();

            ImGui.Checkbox("Bind volume to game music channel", ref _bindToGameVolumeInput);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Player volume will follow a selected game music channel's volume setting.");
            }

            if (_bindToGameVolumeInput)
            {
                using var indent = ImRaii.PushIndent();
                ImGui.Text("Music Channel:");
                ImGui.SetNextItemWidth(200);
                ImGui.Combo("##MusicChannel", ref _selectedChannel, _channelNames, _channelNames.Length);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select which game audio channel to bind the player volume to.");
                }
            }

            ImGui.Spacing();

            ImGui.Checkbox("Show current song in Server Info Bar", ref _showInDtrInput);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Displays the currently playing song in the game's Server Info Bar at the top of the screen.");
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var availableWidth = ImGui.GetContentRegionAvail().X;
            var buttonWidth = 120f;
            var spacing = 10f;
            var totalButtonWidth = (buttonWidth * 2) + spacing;
            var startX = (availableWidth - totalButtonWidth) / 2f;

            if (startX > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);
            }

            bool canFinish = !string.IsNullOrEmpty(_mediaFolderInput) && Directory.Exists(_mediaFolderInput);

            using (ImRaii.Disabled(!canFinish))
            {
                if (ImGui.Button("Finish Setup", new Vector2(buttonWidth, 30)))
                {
                    Plugin.Settings.MediaFolder = _mediaFolderInput;
                    Plugin.Settings.SearchSubfolders = _searchSubfoldersInput;
                    Plugin.Settings.MuteBgmWhenPlaying = _muteBgmInput;
                    Plugin.Settings.BindToGameVolume = _bindToGameVolumeInput;
                    Plugin.Settings.MusicChannel = _channelIds[_selectedChannel];
                    Plugin.Settings.ShowInDtr = _showInDtrInput;
                    Plugin.Settings.HasCompletedFirstLaunch = true;
                    Plugin.Settings.Save();

                    if (_showInDtrInput)
                    {
                        _plugin.ToggleDtr(true);
                    }

                    _plugin.MainWindow.LoadSongs();
                    _plugin.MainWindow.IsOpen = true;

                    this.IsOpen = false;
                }
            }

            if (!canFinish && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Please select a valid media folder to continue.");
            }

            ImGui.SameLine(0, spacing);

            if (ImGui.Button("Skip for Now", new Vector2(buttonWidth, 30)))
            {
                Plugin.Settings.HasCompletedFirstLaunch = true;
                Plugin.Settings.Save();
                this.IsOpen = false;
            }

            ImGui.Spacing();

            using (var color = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f)))
            {
                var disclaimerText = "More settings and options are available in the Settings window.";
                var disclaimerSize = ImGui.CalcTextSize(disclaimerText);
                ImGui.SetCursorPosX((availableWidth - disclaimerSize.X) / 2f);
                ImGui.Text(disclaimerText);
            }
        }
    }
}
