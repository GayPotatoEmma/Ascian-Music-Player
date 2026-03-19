using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AscianMusicPlayer.Windows
{
    public class LyricsSettingsWindow : Window
    {
        private readonly Plugin _plugin;

        public LyricsSettingsWindow(Plugin plugin) : base("Lyrics Settings###AscianMusicPlayerLyricsSettings")
        {
            _plugin = plugin;
            this.Size = new Vector2(300, 300);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.Flags = ImGuiWindowFlags.NoResize;
        }

        public override void Draw()
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Window Settings");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Window Size:");
            ImGui.SetNextItemWidth(200);
            int width = Plugin.Settings.LyricsWindowWidth;
            if (ImGui.SliderInt("Width##LyricsWidth", ref width, 300, 1000))
            {
                Plugin.Settings.LyricsWindowWidth = width;
                _plugin.SaveSettings();
            }

            ImGui.SetNextItemWidth(200);
            int height = Plugin.Settings.LyricsWindowHeight;
            if (ImGui.SliderInt("Height##LyricsHeight", ref height, 100, 500))
            {
                Plugin.Settings.LyricsWindowHeight = height;
                _plugin.SaveSettings();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Display Settings");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Next Lines Shown:");
            ImGui.SetNextItemWidth(200);
            int nextLineCount = Plugin.Settings.LyricsNextLineCount;
            if (ImGui.SliderInt("##NextLineCount", ref nextLineCount, 0, 5))
            {
                Plugin.Settings.LyricsNextLineCount = nextLineCount;
                _plugin.SaveSettings();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of upcoming lyric lines to display");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "Text Scale");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Current Line:");
            ImGui.SetNextItemWidth(200);
            float currentScale = Plugin.Settings.LyricsCurrentLineScale;
            if (ImGui.SliderFloat("##CurrentScale", ref currentScale, 0.5f, 3.0f, "%.1f"))
            {
                Plugin.Settings.LyricsCurrentLineScale = currentScale;
                _plugin.SaveSettings();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Text size multiplier for the current lyric line");
            }

            ImGui.Spacing();

            ImGui.Text("Next Lines:");
            ImGui.SetNextItemWidth(200);
            float nextScale = Plugin.Settings.LyricsNextLineScale;
            if (ImGui.SliderFloat("##NextScale", ref nextScale, 0.5f, 2.0f, "%.1f"))
            {
                Plugin.Settings.LyricsNextLineScale = nextScale;
                _plugin.SaveSettings();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Text size multiplier for upcoming lyric lines");
            }
        }
    }
}
