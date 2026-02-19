using System;
using System.IO;
using System.Text.Json;

namespace AscianMusicPlayer
{
    public class Settings
    {
        public string MediaFolder { get; set; } = string.Empty;
        public bool SearchSubfolders { get; set; } = false;
        public uint MusicChannel { get; set; } = 180;
        public bool MuteBgmWhenPlaying { get; set; } = true;

        public float TitleColumnWidth { get; set; } = 0;
        public float ArtistColumnWidth { get; set; } = 100;
        public float AlbumColumnWidth { get; set; } = 100;
        public float LengthColumnWidth { get; set; } = 85;

        public bool ShowArtistColumn { get; set; } = true;
        public bool ShowAlbumColumn { get; set; } = true;
        public bool ShowLengthColumn { get; set; } = true;

        public bool ShowInDtr { get; set; } = false;

        public const uint ChannelBgm = 175;
        public const uint ChannelSe = 176;
        public const uint ChannelVoice = 177;
        public const uint ChannelEnv = 178;
        public const uint ChannelSystem = 179;
        public const uint ChannelPerform = 180;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            "AscianMusicPlayer.json"
        );

        public static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<Settings>(json);
                    return loaded ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Failed to load settings: {ex.Message}");
            }

            return new Settings();
        }

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);

                Plugin.Log.Information("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
