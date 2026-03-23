using Dalamud.Interface.Windowing;

namespace AscianMusicPlayer.Windows
{
    public abstract class PluginWindow : Window
    {
        protected PluginWindow(string name) : base(name)
        {
        }

        public override bool DrawConditions()
        {
            if (!Plugin.Settings.HideWithGameUi)
                return true;
            return !Plugin.GameGui.GameUiHidden;
        }
    }
}
