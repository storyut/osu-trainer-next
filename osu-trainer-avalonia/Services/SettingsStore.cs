using System;
using System.IO;
using System.Text.Json;

namespace osu_trainer_avalonia.Services
{
    /// <summary>
    /// Reads/writes <see cref="AppSettings"/> as JSON. Mirrors
    /// <c>BeatmapEditor.SaveProfilesToDisk</c>/<c>LoadProfilesFromDisk</c>'s error-handling
    /// shape: a missing file is a normal first run (silent defaults), a corrupt file or a
    /// write failure is reported via <paramref name="onError"/> and falls back to defaults.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string path;
        private readonly Action<string> onError;

        public SettingsStore(Action<string> onError) : this(DefaultPath(), onError)
        {
        }

        public SettingsStore(string path, Action<string> onError)
        {
            this.path = path;
            this.onError = onError;
        }

        private static string DefaultPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "osu-trainer-next", "settings.json");

        public AppSettings Load()
        {
            if (!File.Exists(path))
                return new AppSettings();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
            catch (Exception e)
            {
                onError($"Could not read settings ({e.Message}); using defaults.");
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
            }
            catch (Exception e)
            {
                onError($"Unable to save settings.{Environment.NewLine}{Environment.NewLine}{e.Message}");
            }
        }
    }
}
