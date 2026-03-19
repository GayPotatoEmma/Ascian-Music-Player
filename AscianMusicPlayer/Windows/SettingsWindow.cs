using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class SettingsWindow : Window
    {
        private readonly Plugin _plugin;
        private string _mediaFolderInput = string.Empty;
        private int _selectedChannel = 4;
        private readonly FileDialogManager _fileDialogManager;

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

        public SettingsWindow(Plugin plugin) : base("Settings###AscianMusicPlayerSettings")
        {
            _plugin = plugin;
            this.Size = new Vector2(275, 300);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoResize;

            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _selectedChannel = Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;

            _fileDialogManager = new FileDialogManager();
        }

        public override void OnOpen()
        {
            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _selectedChannel = Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;
        }

        public override void Draw()
        {
            _fileDialogManager.Draw();

            using var tabBar = ImRaii.TabBar("SettingsTabs");
            if (!tabBar) return;

            using (var audioTab = ImRaii.TabItem("Audio"))
            {
                if (audioTab)
                    DrawAudioTab();
            }

            using (var displayTab = ImRaii.TabItem("Display"))
            {
                if (displayTab)
                    DrawDisplayTab();
            }

            using (var libraryTab = ImRaii.TabItem("Library"))
            {
                if (libraryTab)
                    DrawLibraryTab();
            }
        }

        private void DrawAudioTab()
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Volume Settings");
            ImGui.Separator();

            if (ImGui.Checkbox("Bind to Game Volume", ref Plugin.Settings.BindToGameVolume))
            {
                _plugin.SaveSettings();
                _plugin.AudioController.UpdateVolume();
            }

            if (!Plugin.Settings.BindToGameVolume)
            {
                ImGui.SetNextItemWidth(180 * ImGui.GetIO().FontGlobalScale);
                if (ImGui.SliderFloat("Volume", ref Plugin.Settings.MusicVolume, 0f, 100f, "%.0f%%"))
                {
                    Plugin.Settings.MusicVolume = Math.Clamp(Plugin.Settings.MusicVolume, 0f, 100f);
                    _plugin.SaveSettings();
                    _plugin.AudioController.UpdateVolume();
                }
            }
            else
            {
                ImGui.Text("Music Channel");
                ImGui.SetNextItemWidth(180 * ImGui.GetIO().FontGlobalScale);
                if (ImGui.Combo("##MusicChannel", ref _selectedChannel, _channelNames, _channelNames.Length))
                {
                    Plugin.Settings.MusicChannel = _channelIds[_selectedChannel];
                    _plugin.SaveSettings();
                    _plugin.AudioController.ApplySettingsChange();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Playback Settings");
            ImGui.Separator();

            if (ImGui.Checkbox("Mute BGM when playing music", ref Plugin.Settings.MuteBgmWhenPlaying))
            {
                _plugin.SaveSettings();
                _plugin.AudioController.ApplySettingsChange();
            }

            ImGui.Spacing();

            ImGui.Text("Crossfade Duration");
            ImGui.SetNextItemWidth(180 * ImGui.GetIO().FontGlobalScale);
            if (ImGui.SliderFloat("##Crossfade", ref Plugin.Settings.CrossfadeDuration, 0f, 10f, "%.0f seconds"))
            {
                Plugin.Settings.CrossfadeDuration = MathF.Round(Plugin.Settings.CrossfadeDuration);
                Plugin.Settings.CrossfadeDuration = Math.Clamp(Plugin.Settings.CrossfadeDuration, 0f, 10f);
                _plugin.SaveSettings();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Fade between songs (0 = disabled)");
            }
        }

        private void DrawDisplayTab()
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Display Settings");
            ImGui.Separator();

            if (ImGui.Checkbox("Show current song in Server Info Bar", ref Plugin.Settings.ShowInDtr))
            {
                _plugin.SaveSettings();
                _plugin.ToggleDtr(Plugin.Settings.ShowInDtr);
            }

            ImGui.Spacing();

            if (ImGui.Checkbox("Print current song to chat", ref Plugin.Settings.PrintSongToChat))
            {
                _plugin.SaveSettings();
            }
        }

        private void DrawLibraryTab()
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Media Library");
            ImGui.Separator();

            ImGui.Text("Media Folder");
            ImGui.SetNextItemWidth(180 * ImGui.GetIO().FontGlobalScale);
            ImGui.InputText("##MediaFolder", ref _mediaFolderInput, 260);

            ImGui.SameLine();
            if (ImGui.Button("Browse..."))
            {
                _fileDialogManager.OpenFolderDialog("Select Music Folder", (success, path) =>
                {
                    if (success && !string.IsNullOrEmpty(path))
                    {
                        _mediaFolderInput = path;
                    }
                }, Plugin.Settings.MediaFolder);
            }

            if (Util.IsWine())
            {
                using var color = ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
                ImGui.TextWrapped("24-bit audio formats may have trouble playing under Wine.");
            }

            ImGui.Spacing();

            if (ImGui.Checkbox("Scan subfolders", ref Plugin.Settings.SearchSubfolders))
            {
                _plugin.SaveSettings();
            }

            ImGui.Spacing();

            var scale = ImGui.GetIO().FontGlobalScale;
            if (ImGui.Button("Scan", new Vector2(100 * scale, 0)))
            {
                if (!string.IsNullOrEmpty(_mediaFolderInput))
                {
                    Plugin.Settings.MediaFolder = _mediaFolderInput;
                    _plugin.SaveSettings();
                    _plugin.MainWindow.LoadSongs();
                    Plugin.Log.Information($"Scanning folder: {_mediaFolderInput}");
                }
            }

            if (!string.IsNullOrEmpty(_mediaFolderInput) && _mediaFolderInput != Plugin.Settings.MediaFolder)
            {
                ImGui.SameLine();
                if (ImGui.Button("Apply Folder", new Vector2(100 * scale, 0)))
                {
                    Plugin.Settings.MediaFolder = _mediaFolderInput;
                    _plugin.SaveSettings();
                    _plugin.MainWindow.LoadSongs();
                }
            }
        }
    }
}
