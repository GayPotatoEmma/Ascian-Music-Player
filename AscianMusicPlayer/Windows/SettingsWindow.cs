using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Dalamud.Interface.ImGuiFileDialog;

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
            var height = Util.IsWine() ? 505 : 470;
            this.Size = new Vector2(275, height);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoResize;

            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _selectedChannel = System.Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;

            _fileDialogManager = new FileDialogManager();
        }

        public override void OnOpen()
        {
            _mediaFolderInput = Plugin.Settings.MediaFolder;
            _selectedChannel = System.Array.IndexOf(_channelIds, Plugin.Settings.MusicChannel);
            if (_selectedChannel < 0) _selectedChannel = 4;
        }

        public override void Draw()
        {
            _fileDialogManager.Draw();

            ImGui.Text("Volume Settings");
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
                ImGui.Text("Music Channel:");
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

            ImGui.Text("Music Playback Settings");
            ImGui.Separator();

            if (ImGui.Checkbox("Mute BGM when playing music", ref Plugin.Settings.MuteBgmWhenPlaying))
            {
                _plugin.SaveSettings();
                _plugin.AudioController.ApplySettingsChange();
            }

            ImGui.Spacing();

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

            ImGui.Spacing();

            ImGui.Text("Crossfade Duration:");
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

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Lyrics Settings");
            ImGui.Separator();

            if (ImGui.Checkbox("Print synced lyrics to chat", ref Plugin.Settings.PrintSyncedLyrics))
            {
                _plugin.SaveSettings();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Display synced lyrics to Echo chat if available in song metadata");
            }

            if (Plugin.Settings.PrintSyncedLyrics)
            {
                ImGui.Indent();
                if (ImGui.Checkbox("Fetch lyrics from LRCLIB.net", ref Plugin.Settings.FetchLyricsOnline))
                {
                    _plugin.SaveSettings();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Automatically download synced lyrics if not in metadata");
                }
                ImGui.Unindent();

                ImGui.Text("Lyrics display mode:");
                ImGui.SetNextItemWidth(180 * ImGui.GetIO().FontGlobalScale);
                string[] displayModes = { "Chat", "Flytext" };
                int currentMode = Plugin.Settings.UseFlyTextForLyrics ? 1 : 0;
                if (ImGui.Combo("##LyricsDisplayMode", ref currentMode, displayModes, displayModes.Length))
                {
                    Plugin.Settings.UseFlyTextForLyrics = currentMode == 1;
                    _plugin.SaveSettings();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Media Folder:");
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
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
                ImGui.TextWrapped("24-bit audio formats may have trouble playing under Wine.");
                ImGui.PopStyleColor();
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
