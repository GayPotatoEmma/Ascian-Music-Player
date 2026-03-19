using System;
using System.Numerics;
using AscianMusicPlayer.Audio;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Components;
using Dalamud.Interface;

namespace AscianMusicPlayer.Windows
{
    public class LyricsWindow : Window
    {
        private readonly Plugin _plugin;
        private Song? _currentSong;
        private int _currentLyricIndex = -1;

        public LyricsWindow(Plugin plugin) : base("Lyrics###AscianMusicPlayerLyrics")
        {
            _plugin = plugin;
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        }

        public void UpdateCurrentLyricIndex(int index)
        {
            _currentLyricIndex = index;
        }

        public void SetCurrentSong(Song? song)
        {
            if (_currentSong != song)
            {
                _currentSong = song;
                _currentLyricIndex = -1;
            }
        }

        public override void PreDraw()
        {
            this.Size = new Vector2(Plugin.Settings.LyricsWindowWidth, Plugin.Settings.LyricsWindowHeight);
            this.SizeCondition = ImGuiCond.Always;
        }

        public override void Draw()
        {
            DrawSettingsButton();

            ImGui.Spacing();

            if (_currentSong == null)
            {
                DrawCenteredText("No song playing", new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                return;
            }

            if (!_currentSong.HasSyncedLyrics)
            {
                DrawCenteredText($"'{_currentSong.Title}' has no synced lyrics", new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                return;
            }

            var lyrics = _currentSong.SyncedLyrics;

            if (_currentLyricIndex >= 0 && _currentLyricIndex < lyrics.Count)
            {
                var currentLine = lyrics[_currentLyricIndex];
                DrawCenteredLyric(currentLine.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.Settings.LyricsCurrentLineScale);
            }
            else
            {
                DrawCenteredLyric("", new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.Settings.LyricsCurrentLineScale);
            }

            ImGui.Spacing();

            for (int i = 1; i <= Plugin.Settings.LyricsNextLineCount; i++)
            {
                int lineIndex = _currentLyricIndex + i;
                if (lineIndex >= 0 && lineIndex < lyrics.Count)
                {
                    var nextLine = lyrics[lineIndex];
                    DrawCenteredLyric(nextLine.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f), Plugin.Settings.LyricsNextLineScale);
                }
                else
                {
                    DrawCenteredLyric("", new Vector4(0.6f, 0.6f, 0.6f, 1.0f), Plugin.Settings.LyricsNextLineScale);
                }

                if (i < Plugin.Settings.LyricsNextLineCount)
                {
                    ImGui.Spacing();
                }
            }
        }

        private void DrawSettingsButton()
        {
            var windowWidth = ImGui.GetWindowWidth();
            var buttonSize = ImGui.GetFrameHeight();

            ImGui.SameLine(windowWidth - buttonSize - ImGui.GetStyle().WindowPadding.X);

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            {
                _plugin.LyricsSettingsWindow.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Lyrics Settings");
            }
        }

        private void DrawCenteredLyric(string text, Vector4 color, float scale)
        {
            using var colorStyle = ImRaii.PushColor(ImGuiCol.Text, color);

            var textSize = ImGui.CalcTextSize(text);
            var scaledTextWidth = textSize.X * scale;
            var contentRegionMin = ImGui.GetWindowContentRegionMin();
            var contentRegionMax = ImGui.GetWindowContentRegionMax();
            var contentWidth = contentRegionMax.X - contentRegionMin.X;

            ImGui.SetCursorPosX(contentRegionMin.X + (contentWidth - scaledTextWidth) / 2f);

            ImGui.SetWindowFontScale(scale);
            ImGui.Text(text);
            ImGui.SetWindowFontScale(1.0f);
        }

        private void DrawCenteredText(string text, Vector4 color)
        {
            var textSize = ImGui.CalcTextSize(text);
            var contentWidth = ImGui.GetContentRegionAvail().X;
            var availableHeight = ImGui.GetContentRegionAvail().Y;

            ImGui.SetCursorPosX((contentWidth - textSize.X) / 2f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (availableHeight - ImGui.GetTextLineHeight()) / 2f);

            ImGui.TextColored(color, text);
        }
    }
}
