using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AscianMusicPlayer.Windows
{
    public class LyricsSettingsWindow : Window
    {
        private static readonly Vector4 SectionColor = new(0.2f, 0.8f, 1.0f, 1.0f);
        private readonly Plugin _plugin;

        public LyricsSettingsWindow(Plugin plugin) : base("Lyrics Settings###AscianMusicPlayerLyricsSettings")
        {
            _plugin = plugin;
            Size = new Vector2(275, 420);
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoResize;
        }

        public override void Draw()
        {
            using var tabBar = ImRaii.TabBar("LyricsSettingsTabBar");
            if (!tabBar) return;

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
        }

        private void DrawWindowTab()
        {
            using var itemWidth = ImRaii.ItemWidth(200);

            DrawSectionHeader("Window Size");

            ImGui.Text("Width");
            int width = Plugin.Settings.LyricsWindowWidth;
            if (ImGui.SliderInt("##LyricsWidth", ref width, 300, 1000))
            {
                Plugin.Settings.LyricsWindowWidth = width;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();

            ImGui.Text("Height");
            int height = Plugin.Settings.LyricsWindowHeight;
            if (ImGui.SliderInt("##LyricsHeight", ref height, 100, 500))
            {
                Plugin.Settings.LyricsWindowHeight = height;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            DrawSectionHeader("Window Appearance");

            ImGui.Text("Background Opacity");
            float bgAlpha = Plugin.Settings.LyricsWindowBgAlpha;
            if (ImGui.SliderFloat("##BgAlpha", ref bgAlpha, 0.0f, 1.0f, "%.2f"))
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

            DrawSectionHeader("Lines Shown");

            ImGui.Text("Next Lines:");
            int nextLineCount = Plugin.Settings.LyricsNextLineCount;
            if (ImGui.SliderInt("##NextLineCount", ref nextLineCount, 0, 5))
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
            if (ImGui.SliderFloat("##CurrentScale", ref currentScale, 0.5f, 3.0f, "%.1f"))
            {
                Plugin.Settings.LyricsCurrentLineScale = currentScale;
                _plugin.SaveSettings();
                _plugin.RebuildLyricsFont();
            }
            DrawTooltip("Text size multiplier for the current lyric line");

            ImGui.Spacing();

            ImGui.Text("Next Lines");
            float nextScale = Plugin.Settings.LyricsNextLineScale;
            if (ImGui.SliderFloat("##NextScale", ref nextScale, 0.5f, 2.0f, "%.1f"))
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
            if (ImGui.SliderFloat("##HorizAlign", ref horizAlign, 0.0f, 1.0f, "%.2f"))
            {
                Plugin.Settings.LyricsHorizontalAlignment = horizAlign;
                _plugin.SaveSettings();
            }
            DrawTooltip("0.0 = Left, 0.5 = Center, 1.0 = Right");

            ImGui.Spacing();

            ImGui.Text("Vertical");
            float vertAlign = Plugin.Settings.LyricsVerticalAlignment;
            if (ImGui.SliderFloat("##VertAlign", ref vertAlign, 0.0f, 1.0f, "%.2f"))
            {
                Plugin.Settings.LyricsVerticalAlignment = vertAlign;
                _plugin.SaveSettings();
            }
            DrawTooltip("0.0 = Top, 0.5 = Center, 1.0 = Bottom");
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
    }
}
