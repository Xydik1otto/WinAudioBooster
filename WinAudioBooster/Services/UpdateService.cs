using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinAudioBooster.Services
{
    public class UpdateService
    {
        // Замініть "bigpi/WinAudioBooster" на ваш реальний репозиторій на GitHub, якщо він відрізняється
        private const string GitHubApiUrl = "https://api.github.com/repos/bigpi/WinAudioBooster/releases/latest";

        public class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;
        }

        public async Task<(bool hasUpdate, string latestVersion, string url)> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                // GitHub API вимагає User-Agent заголовк
                client.DefaultRequestHeaders.Add("User-Agent", "WinAudioBooster-Client");

                var response = await client.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode) return (false, "", "");

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release == null || string.IsNullOrEmpty(release.TagName)) return (false, "", "");

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
                var latestVersionStr = release.TagName.TrimStart('v', 'V');

                if (Version.TryParse(latestVersionStr, out var latestVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        return (true, release.TagName, release.HtmlUrl);
                    }
                }
            }
            catch { }

            return (false, "", "");
        }
    }
}