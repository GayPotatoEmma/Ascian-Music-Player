using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace AscianMusicPlayer.Windows
{
    public class SettingsWindow : Window
    {
        private readonly Plugin _plugin;
        private string _mediaFolderInput = string.Empty;
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

        public SettingsWindow(Plugin plugin) : base("Ascian Music Player Settings###AscianMusicPlayerSettings")
        {
            _plugin = plugin;
            this.Size = new Vector2(275, 365);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoResize;

            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _selectedChannel = System.Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;
        }

        public override void Draw()
        {
            ImGui.Text("Music Playback Settings");
            ImGui.Separator();

            ImGui.Text("Music Channel:");
            ImGui.SetNextItemWidth(250);
            if (ImGui.Combo("##MusicChannel", ref _selectedChannel, _channelNames, _channelNames.Length))
            {
                Plugin.Settings.MusicChannel = _channelIds[_selectedChannel];
                _plugin.SaveSettings();
                _plugin.AudioController.ApplySettingsChange();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool muteBgm = Plugin.Settings.MuteBgmWhenPlaying;

            if (ImGui.Checkbox("Mute BGM when playing music", ref muteBgm))
            {
                Plugin.Settings.MuteBgmWhenPlaying = muteBgm;
                _plugin.SaveSettings();
                _plugin.AudioController.ApplySettingsChange();
            }

            ImGui.Spacing();

            bool showInDtr = Plugin.Settings.ShowInDtr;

            if (ImGui.Checkbox("Show current song in Server Info Bar", ref showInDtr))
            {
                Plugin.Settings.ShowInDtr = showInDtr;
                _plugin.SaveSettings();
                _plugin.ToggleDtr(showInDtr);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Media Folder:");
            ImGui.SetNextItemWidth(250);
            ImGui.InputText("##MediaFolder", ref _mediaFolderInput, 260);

            if (Util.IsWine())
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
                ImGui.TextWrapped("24-bit audio formats may have trouble playing under Wine.");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();

            bool searchSubfolders = Plugin.Settings.SearchSubfolders;
            if (ImGui.Checkbox("Scan subfolders", ref searchSubfolders))
            {
                Plugin.Settings.SearchSubfolders = searchSubfolders;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();

            if (ImGui.Button("Scan", new Vector2(100, 0)))
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
                if (ImGui.Button("Apply Folder", new Vector2(100, 0)))
                {
                    Plugin.Settings.MediaFolder = _mediaFolderInput;
                    _plugin.SaveSettings();
                    _plugin.MainWindow.LoadSongs();
                }
            }
        }
    }
}
