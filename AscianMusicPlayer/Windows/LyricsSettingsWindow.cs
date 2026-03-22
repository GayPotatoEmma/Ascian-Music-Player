using System.Numerics;
using System.Drawing.Text;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class LyricsSettingsWindow : PluginWindow
    {
        private static readonly Vector4 SectionColor = new(0.2f, 0.8f, 1.0f, 1.0f);
        private readonly Plugin _plugin;

        private string[] _allFontNames = [];

        public LyricsSettingsWindow(Plugin plugin) : base("Lyrics Settings###AscianMusicPlayerLyricsSettings")
        {
            _plugin = plugin;
            Size = new Vector2(275, 350);
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoResize;
            LoadFonts();
        }

        private void LoadFonts()
        {
            try
            {
                var gameFonts = new[] { "Game: Axis", "Game: Jupiter", "Game: Jupiter Numeric", "Game: Meidinger", "Game: Meidinger Mid", "Game: Trump Gothic" };

                using var installedFonts = new InstalledFontCollection();
                var systemFonts = installedFonts.Families
                    .Select(f => f.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n);

                _allFontNames = new[] { "Dalamud Default" }
                    .Concat(gameFonts)
                    .Concat(systemFonts)
                    .ToArray();
            }
            catch
            {
                _allFontNames = ["Dalamud Default"];
            }
        }

        public override void Draw()
        {
            using var tabBar = ImRaii.TabBar("LyricsSettingsTabBar");
            if (!tabBar) return;

            using (var generalTab = ImRaii.TabItem("General"))
            {
                if (generalTab)
                    DrawGeneralTab();
            }

            using (var windowTab = ImRaii.TabItem("Window"))
            {
                if (windowTab)
                    DrawWindowTab();
            }

            using (var textTab = ImRaii.TabItem("Text"))
            {
                if (textTab)
                    DrawTextTab();
            }

            using (var syncTab = ImRaii.TabItem("Sync"))
            {
                if (syncTab)
                    DrawSyncTab();
            }
        }

        private void DrawGeneralTab()
        {
            using var itemWidth = ImRaii.ItemWidth(180);

            DrawSectionHeader("Display Mode");

            ImGui.Text("Lyrics display mode");
            string[] displayModes = ["None", "Chat", "Flytext"];
            int currentMode = Plugin.Settings.LyricsDisplayMode;
            if (ImGui.Combo("##LyricsDisplayMode", ref currentMode, displayModes, displayModes.Length))
            {
                Plugin.Settings.LyricsDisplayMode = currentMode;
                _plugin.SaveSettings();
            }
            DrawTooltip("How to display synced lyrics\nNone: Disabled\nChat: Print to chat\nFlytext: Show as flytext");

            if (Plugin.Settings.LyricsDisplayMode == 2)
            {
                ImGui.Spacing();
                using var indent = ImRaii.PushIndent();
                var color = Plugin.Settings.FlyTextLyricColor;
                var r = (color >> 0) & 0xFF;
                var g = (color >> 8) & 0xFF;
                var b = (color >> 16) & 0xFF;
                var a = (color >> 24) & 0xFF;
                var colorVec = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);

                if (ImGui.ColorEdit4("Flytext color", ref colorVec, ImGuiColorEditFlags.NoInputs))
                {
                    r = (uint)(colorVec.X * 255);
                    g = (uint)(colorVec.Y * 255);
                    b = (uint)(colorVec.Z * 255);
                    a = (uint)(colorVec.W * 255);
                    Plugin.Settings.FlyTextLyricColor = r | (g << 8) | (b << 16) | (a << 24);
                    _plugin.SaveSettings();
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Online Lyrics");

            if (ImGui.Checkbox("Fetch lyrics from LRCLIB.net", ref Plugin.Settings.FetchLyricsOnline))
            {
                _plugin.SaveSettings();
            }
            DrawTooltip("Automatically download synced lyrics if no .lrc file exists");
        }

        private void DrawWindowTab()
        {
            using var itemWidth = ImRaii.ItemWidth(200);

            DrawSectionHeader("Window Size");

            ImGui.Text("Width");
            int width = Plugin.Settings.LyricsWindowWidth;
            if (ImGui.SliderInt("##LyricsWidth", ref width, 300, 1000, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsWindowWidth = width;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();

            ImGui.Text("Height");
            int height = Plugin.Settings.LyricsWindowHeight;
            if (ImGui.SliderInt("##LyricsHeight", ref height, 100, 500, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsWindowHeight = height;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Window Appearance");

            ImGui.Text("Background Opacity");
            float bgAlpha = Plugin.Settings.LyricsWindowBgAlpha;
            if (ImGui.SliderFloat("##BgAlpha", ref bgAlpha, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsWindowBgAlpha = bgAlpha;
                _plugin.SaveSettings();
            }
            DrawTooltip("0.0 = Transparent, 1.0 = Opaque");

            ImGui.Spacing();

            bool clickthrough = Plugin.Settings.LyricsWindowClickthrough;
            if (ImGui.Checkbox("Clickthrough", ref clickthrough))
            {
                Plugin.Settings.LyricsWindowClickthrough = clickthrough;
                _plugin.SaveSettings();
            }
            DrawTooltip("Allow mouse clicks to pass through the lyrics window");
        }

        private void DrawTextTab()
        {
            using var itemWidth = ImRaii.ItemWidth(200);

            DrawSectionHeader("Font");

            ImGui.Text("Font");

            var currentFontName = Plugin.Settings.LyricsSystemFontName;
            if (string.IsNullOrEmpty(currentFontName))
            {
                currentFontName = "Dalamud Default";
            }

            using (var combo = ImRaii.Combo("##LyricsFont", currentFontName))
            {
                if (combo)
                {
                    foreach (var fontName in _allFontNames)
                    {
                        bool isSelected = fontName == currentFontName;
                        if (ImGui.Selectable(fontName, isSelected))
                        {
                            Plugin.Settings.LyricsSystemFontName = fontName;
                            _plugin.SaveSettings();
                            _plugin.RebuildLyricsFont();
                        }
                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Font Colors");

            var currentColor = UintToVec4(Plugin.Settings.LyricsCurrentLineColor);
            if (ImGui.ColorEdit4("Current Line", ref currentColor, ImGuiColorEditFlags.NoInputs))
            {
                Plugin.Settings.LyricsCurrentLineColor = Vec4ToUint(currentColor);
                _plugin.SaveSettings();
            }

            var nextColor = UintToVec4(Plugin.Settings.LyricsNextLineColor);
            if (ImGui.ColorEdit4("Next Lines", ref nextColor, ImGuiColorEditFlags.NoInputs))
            {
                Plugin.Settings.LyricsNextLineColor = Vec4ToUint(nextColor);
                _plugin.SaveSettings();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Lines Shown");

            ImGui.Text("Next Lines:");
            int nextLineCount = Plugin.Settings.LyricsNextLineCount;
            if (ImGui.SliderInt("##NextLineCount", ref nextLineCount, 0, 5, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsNextLineCount = nextLineCount;
                _plugin.SaveSettings();
            }
            DrawTooltip("Number of upcoming lyric lines to display");

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Text Scale");

            ImGui.Text("Current Line");
            float currentScale = Plugin.Settings.LyricsCurrentLineScale;
            if (ImGui.SliderFloat("##CurrentScale", ref currentScale, 0.5f, 3.0f, "%.1f", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsCurrentLineScale = currentScale;
                _plugin.SaveSettings();
                _plugin.RebuildLyricsFont();
            }
            DrawTooltip("Text size multiplier for the current lyric line");

            ImGui.Spacing();

            ImGui.Text("Next Lines");
            float nextScale = Plugin.Settings.LyricsNextLineScale;
            if (ImGui.SliderFloat("##NextScale", ref nextScale, 0.5f, 2.0f, "%.1f", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsNextLineScale = nextScale;
                _plugin.SaveSettings();
                _plugin.RebuildLyricsFont();
            }
            DrawTooltip("Text size multiplier for upcoming lyric lines");

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Alignment");

            ImGui.Text("Horizontal");
            float horizAlign = Plugin.Settings.LyricsHorizontalAlignment;
            if (ImGui.SliderFloat("##HorizAlign", ref horizAlign, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsHorizontalAlignment = horizAlign;
                _plugin.SaveSettings();
            }
            DrawTooltip("0.0 = Left, 0.5 = Center, 1.0 = Right");

            ImGui.Spacing();

            ImGui.Text("Vertical");
            float vertAlign = Plugin.Settings.LyricsVerticalAlignment;
            if (ImGui.SliderFloat("##VertAlign", ref vertAlign, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                Plugin.Settings.LyricsVerticalAlignment = vertAlign;
                _plugin.SaveSettings();
            }
            DrawTooltip("0.0 = Top, 0.5 = Center, 1.0 = Bottom");
        }

        private void DrawSyncTab()
        {
            var currentSong = _plugin.MainWindow.GetCurrentSong();

            if (currentSong == null)
            {
                ImGui.TextDisabled("No song playing");
                ImGui.TextDisabled("Play a song to adjust its lyrics offset");
                return;
            }

            DrawSectionHeader("Current Song");

            ImGui.TextWrapped(currentSong.Title);
            ImGui.TextDisabled(currentSong.Artist);

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Lyrics Offset");

            using (ImRaii.ItemWidth(150))
            {
                int offsetMs = currentSong.LyricsOffsetMs;
                if (ImGui.InputInt("##LyricsOffset", ref offsetMs, 100, 500))
                {
                    currentSong.LyricsOffsetMs = offsetMs;
                    _plugin.Database.SetLyricsOffset(currentSong.FilePath, offsetMs);
                }
            }
            ImGui.SameLine();
            ImGui.Text("ms");
            DrawTooltip("Positive = lyrics appear earlier\nNegative = lyrics appear later");

            ImGui.Spacing();

            if (ImGui.Button("Reset to 0"))
            {
                currentSong.LyricsOffsetMs = 0;
                _plugin.Database.SetLyricsOffset(currentSong.FilePath, 0);
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextDisabled("Tip: Use +/- 100ms increments");
            ImGui.TextDisabled("to fine-tune the sync timing");
        }

        private static void DrawSectionHeader(string title)
        {
            ImGui.TextColored(SectionColor, title);
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static void DrawTooltip(string text)
        {
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(text);
        }

        private static Vector4 UintToVec4(uint color)
        {
            var r = (color >> 0) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = (color >> 16) & 0xFF;
            var a = (color >> 24) & 0xFF;
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static uint Vec4ToUint(Vector4 color)
        {
            var r = (uint)(color.X * 255);
            var g = (uint)(color.Y * 255);
            var b = (uint)(color.Z * 255);
            var a = (uint)(color.W * 255);
            return r | (g << 8) | (b << 16) | (a << 24);
        }
    }
}
