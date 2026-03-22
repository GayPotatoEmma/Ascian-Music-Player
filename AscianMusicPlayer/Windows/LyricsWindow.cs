using System.Collections.Generic;
using System.Numerics;
using AscianMusicPlayer.Audio;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class LyricsWindow : PluginWindow
    {
        private readonly Plugin _plugin;
        private Song? _currentSong;
        private int _currentLyricIndex = -1;

        public LyricsWindow(Plugin plugin) : base("Lyrics###AscianMusicPlayerLyrics")
        {
            _plugin = plugin;
            this.SizeCondition = ImGuiCond.FirstUseEver;
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
            this.BgAlpha = Plugin.Settings.LyricsWindowBgAlpha;

            var baseFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar;
            this.Flags = Plugin.Settings.LyricsWindowClickthrough
                ? baseFlags | ImGuiWindowFlags.NoInputs
                : baseFlags;
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
            var availableHeight = ImGui.GetContentRegionAvail().Y;
            var spacing = ImGui.GetStyle().ItemSpacing.Y;

            ImGui.SetWindowFontScale(Plugin.Settings.LyricsCurrentLineScale);
            var currentLineHeight = ImGui.CalcTextSize("M").Y;
            ImGui.SetWindowFontScale(1.0f);

            ImGui.SetWindowFontScale(Plugin.Settings.LyricsNextLineScale);
            var nextLineHeight = ImGui.CalcTextSize("M").Y;
            ImGui.SetWindowFontScale(1.0f);

            var actualNextLineCount = 0;
            for (int i = 1; i <= Plugin.Settings.LyricsNextLineCount; i++)
            {
                int lineIndex = _currentLyricIndex + i;
                if (lineIndex >= 0 && lineIndex < lyrics.Count)
                {
                    actualNextLineCount++;
                }
            }

            var totalHeight = currentLineHeight;
            if (actualNextLineCount > 0)
            {
                totalHeight += spacing;
                totalHeight += actualNextLineCount * nextLineHeight;
                totalHeight += (actualNextLineCount - 1) * spacing;
            }

            var verticalOffset = (availableHeight - totalHeight) * Plugin.Settings.LyricsVerticalAlignment;
            if (verticalOffset > 0)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + verticalOffset);
            }

            if (_currentLyricIndex >= 0 && _currentLyricIndex < lyrics.Count)
            {
                var currentLine = lyrics[_currentLyricIndex];
                DrawCenteredLyric(currentLine.Text, UintToVec4(Plugin.Settings.LyricsCurrentLineColor), Plugin.Settings.LyricsCurrentLineScale);
            }
            else
            {
                ImGui.Dummy(new Vector2(0, currentLineHeight));
            }

            ImGui.Spacing();

            for (int i = 1; i <= Plugin.Settings.LyricsNextLineCount; i++)
            {
                int lineIndex = _currentLyricIndex + i;
                if (lineIndex >= 0 && lineIndex < lyrics.Count)
                {
                    var nextLine = lyrics[lineIndex];
                    DrawCenteredLyric(nextLine.Text, UintToVec4(Plugin.Settings.LyricsNextLineColor), Plugin.Settings.LyricsNextLineScale);
                }
                else
                {
                    ImGui.Dummy(new Vector2(0, nextLineHeight));
                }

                if (i < Plugin.Settings.LyricsNextLineCount)
                {
                    ImGui.Spacing();
                }
            }
        }

        private void DrawCenteredLyric(string text, Vector4 color, float scale)
        {
            using var colorStyle = ImRaii.PushColor(ImGuiCol.Text, color);

            var fontHandle = _plugin.GetLyricsFontHandle();
            ImRaii.Font? fontPush = null;

            if (fontHandle != null)
            {
                try
                {
                    var font = fontHandle.Lock();
                    if (font != null)
                    {
                        fontPush = ImRaii.PushFont(font.ImFont);
                        var baseFontSize = _plugin.GetLyricsFontBaseSize();
                        var targetSize = Plugin.LyricsFontBaseSize * scale;
                        var fontScale = targetSize / baseFontSize;
                        ImGui.SetWindowFontScale(fontScale);
                    }
                }
                catch
                {
                    ImGui.SetWindowFontScale(scale);
                }
            }
            else
            {
                ImGui.SetWindowFontScale(scale);
            }

            var contentRegionMin = ImGui.GetWindowContentRegionMin();
            var contentRegionMax = ImGui.GetWindowContentRegionMax();
            var contentWidth = contentRegionMax.X - contentRegionMin.X;

            var lines = WrapText(text, contentWidth);
            foreach (var line in lines)
            {
                var lineSize = ImGui.CalcTextSize(line);
                ImGui.SetCursorPosX(contentRegionMin.X + (contentWidth - lineSize.X) * Plugin.Settings.LyricsHorizontalAlignment);
                ImGui.Text(line);
            }

            ImGui.SetWindowFontScale(1.0f);

            fontPush?.Dispose();
        }

        private static List<string> WrapText(string text, float maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var testSize = ImGui.CalcTextSize(testLine);

                if (testSize.X <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                    }

                    if (ImGui.CalcTextSize(word).X > maxWidth)
                    {
                        lines.Add(word);
                        currentLine = "";
                    }
                    else
                    {
                        currentLine = word;
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
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

        private static Vector4 UintToVec4(uint color)
        {
            var r = (color >> 0) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = (color >> 16) & 0xFF;
            var a = (color >> 24) & 0xFF;
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }
}
