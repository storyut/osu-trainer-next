using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace osu_trainer_avalonia.Services
{
    public sealed class UpdateInfo
    {
        public string Version { get; init; } = "";
        public string HtmlUrl { get; init; } = "";
    }

    /// <summary>
    /// Checks GitHub Releases for a version newer than this assembly's. A convenience
    /// feature, not a critical path: any failure (offline, rate-limited, no releases
    /// published yet, unparsable tag) is swallowed here and reported as "no update found" —
    /// deliberately, not silently ignored elsewhere (ERR-1).
    /// </summary>
    public static class UpdateChecker
    {
        private const string ReleasesUrl = "https://api.github.com/repos/storyut/osu-trainer-next/releases/latest";

        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("osu-trainer-next");
                using var response = await http.GetAsync(ReleasesUrl);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;
                if (!root.TryGetProperty("tag_name", out var tagProp) || !root.TryGetProperty("html_url", out var urlProp))
                    return null;

                var rawTag = tagProp.GetString();
                if (string.IsNullOrEmpty(rawTag))
                    return null;

                var versionText = rawTag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? rawTag[1..] : rawTag;
                if (!Version.TryParse(versionText, out var latest))
                    return null;

                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                if (latest <= current)
                    return null;

                return new UpdateInfo { Version = rawTag, HtmlUrl = urlProp.GetString() ?? "" };
            }
            catch
            {
                return null;
            }
        }
    }
}
