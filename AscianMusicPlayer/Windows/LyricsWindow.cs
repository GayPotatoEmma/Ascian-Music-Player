using System;
using System.Numerics;
using AscianMusicPlayer.Audio;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

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
            this.Size = new Vector2(500, 80);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar;
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

        public override void Draw()
        {
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
                DrawCenteredLyric(currentLine.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), 1.3f);
            }
            else
            {
                DrawCenteredLyric("", new Vector4(1.0f, 1.0f, 1.0f, 1.0f), 1.3f);
            }

            ImGui.Spacing();

            if (_currentLyricIndex + 1 >= 0 && _currentLyricIndex + 1 < lyrics.Count)
            {
                var nextLine = lyrics[_currentLyricIndex + 1];
                DrawCenteredLyric(nextLine.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f), 0.9f);
            }
            else
            {
                DrawCenteredLyric("", new Vector4(0.6f, 0.6f, 0.6f, 1.0f), 0.9f);
            }

            ImGui.Spacing();

            if (_currentLyricIndex + 2 >= 0 && _currentLyricIndex + 2 < lyrics.Count)
            {
                var lineAfterNext = lyrics[_currentLyricIndex + 2];
                DrawCenteredLyric(lineAfterNext.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f), 0.9f);
            }
            else
            {
                DrawCenteredLyric("", new Vector4(0.6f, 0.6f, 0.6f, 1.0f), 0.9f);
            }
        }

        private void DrawCenteredLyric(string text, Vector4 color, float scale)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);

            var textSize = ImGui.CalcTextSize(text);
            var scaledTextWidth = textSize.X * scale;
            var windowWidth = ImGui.GetWindowSize().X;
            var contentRegionMin = ImGui.GetWindowContentRegionMin();
            var contentRegionMax = ImGui.GetWindowContentRegionMax();
            var contentWidth = contentRegionMax.X - contentRegionMin.X;

            ImGui.SetCursorPosX(contentRegionMin.X + (contentWidth - scaledTextWidth) / 2f);

            ImGui.SetWindowFontScale(scale);
            ImGui.Text(text);
            ImGui.SetWindowFontScale(1.0f);

            ImGui.PopStyleColor();
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
