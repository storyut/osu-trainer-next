using System.Text.Json.Serialization;

namespace osu_trainer_avalonia.Services
{
    /// <summary>Source-generated serializer metadata for <see cref="AppSettings"/>, avoiding the
    /// reflection warm-up <see cref="System.Text.Json.JsonSerializer"/> otherwise pays on the
    /// startup path (<c>SettingsStore.Load()</c> runs pre-paint) and enabling trimming.</summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    internal partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
}
