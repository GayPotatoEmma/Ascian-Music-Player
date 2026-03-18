using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace AscianMusicPlayer.Windows
{
    public class AboutWindow : Window
    {
        private ISharedImmediateTexture? _logoTexture;
        private bool _textureLoadAttempted = false;
        private PropertyInfo? _textureHandleProperty;
        private string? _versionText;
        private float _windowPadding;
        private float _contentWidth;

        private const string Slogan = "It really whips the Chocobo's ass";
        private const string PluginName = "Ascian Music Player";
        private const string AuthorText = "By gaypotatoemma";
        private const string KofiLine1 = "If you like my work,";
        private const string KofiLine2 = "consider donating to my Ko-Fi!";
        private const string ButtonText = "Support on Ko-Fi";
        private const string KofiUrl = "https://ko-fi.com/gaypotatoemma";

        private const float SloganScale = 0.85f;
        private const float KofiTextScale = 0.9f;
        private const float ButtonScale = 0.9f;

        private static readonly Vector4 SloganColor = new(0.6f, 0.6f, 0.6f, 1.0f);
        private static readonly Vector4 PluginNameColor = new(0.2f, 0.8f, 1.0f, 1.0f);
        private static readonly Vector4 KofiButtonColor = new(0.15f, 0.55f, 0.65f, 1.0f);
        private static readonly Vector4 KofiButtonHovered = new(0.20f, 0.65f, 0.75f, 1.0f);
        private static readonly Vector4 KofiButtonActive = new(0.10f, 0.45f, 0.55f, 1.0f);

        public AboutWindow(Plugin plugin) : base("About###AscianMusicPlayerAbout")
        {
            this.Size = new Vector2(220, 350);
            this.SizeCondition = ImGuiCond.Always;
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public override void OnOpen()
        {
            if (!_textureLoadAttempted)
            {
                LoadLogo();
                _textureLoadAttempted = true;
            }

            if (_versionText == null)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                _versionText = $"Version {version?.Major}.{version?.Minor}.{version?.Build}.{version?.Revision}";
            }

            _windowPadding = ImGui.GetStyle().WindowPadding.X;
            _contentWidth = 220 - (_windowPadding * 2);
        }

        private void LoadLogo()
        {
            try
            {
                var logoPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? "", "Images", "logo.png");
                if (File.Exists(logoPath))
                {
                    _logoTexture = Plugin.TextureProvider.GetFromFile(logoPath);
                }
                else
                {
                    Plugin.Log.Warning($"Logo file not found at: {logoPath}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to load logo: {ex.Message}");
            }
        }

        public override void Draw()
        {
            ImGui.Spacing();
            ImGui.Spacing();

            if (_logoTexture != null)
            {
                if (_logoTexture.TryGetWrap(out var textureWrap, out _) && textureWrap != null)
                {
                    var logoSize = new Vector2(128, 128);
                    SetCursorCentered(logoSize.X);

                    _textureHandleProperty ??= textureWrap.GetType().GetProperty("Handle");
                    if (_textureHandleProperty?.GetValue(textureWrap) is ImTextureID textureId)
                    {
                        ImGui.Image(textureId, logoSize);
                    }
                    ImGui.Spacing();
                }
            }

            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.7f))
            {
                ImGui.SetWindowFontScale(SloganScale);
                SetCursorCentered(ImGui.CalcTextSize(Slogan).X);
                ImGui.TextColored(SloganColor, Slogan);
                ImGui.SetWindowFontScale(1.0f);
            }

            ImGui.Spacing();

            SetCursorCentered(ImGui.CalcTextSize(PluginName).X);
            ImGui.TextColored(PluginNameColor, PluginName);

            ImGui.Spacing();

            SetCursorCentered(ImGui.CalcTextSize(_versionText).X);
            ImGui.Text(_versionText);

            ImGui.Spacing();

            SetCursorCentered(ImGui.CalcTextSize(AuthorText).X);
            ImGui.Text(AuthorText);

            ImGui.Spacing();

            ImGui.Separator();

            ImGui.Spacing();

            ImGui.SetWindowFontScale(KofiTextScale);

            SetCursorCentered(ImGui.CalcTextSize(KofiLine1).X);
            ImGui.Text(KofiLine1);
            SetCursorCentered(ImGui.CalcTextSize(KofiLine2).X);
            ImGui.Text(KofiLine2);

            ImGui.SetWindowFontScale(1.0f);

            ImGui.Spacing();

            ImGui.SetWindowFontScale(ButtonScale);
            var buttonWidth = ImGui.CalcTextSize(ButtonText).X + 16;
            const float buttonHeight = 25;
            SetCursorCentered(buttonWidth);

            using (ImRaii.PushColor(ImGuiCol.Button, KofiButtonColor))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, KofiButtonHovered))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, KofiButtonActive))
            {
                if (ImGui.Button(ButtonText, new Vector2(buttonWidth, buttonHeight)))
                {
                    Util.OpenLink(KofiUrl);
                }
            }

            ImGui.SetWindowFontScale(1.0f);

            ImGui.Spacing();
        }

        private void SetCursorCentered(float itemWidth)
        {
            ImGui.SetCursorPosX(_windowPadding + (_contentWidth - itemWidth) / 2f);
        }
    }
}
