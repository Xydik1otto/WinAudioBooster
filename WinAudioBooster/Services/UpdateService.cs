using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinAudioBooster.Services;

public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/Xydik1otto/WinAudioBooster/releases/latest";
    private const string AssetFileName = "WinAudioBooster.exe";
    private const int HttpClientTimeoutSeconds = 60;
    private const int WaitForExitTimeoutSeconds = 30;

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        // GitHub API повертає дайджест у форматі "sha256:<hex>" (для ассетів, завантажених після 2024)
        [JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;
    }

    /// <summary>Розширений результат перевірки оновлень (URL ассета та його SHA256).</summary>
    public record UpdateCheckResult
    {
        public bool HasUpdate { get; init; }
        public string LatestVersion { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string AssetDownloadUrl { get; init; } = string.Empty;
        public string AssetSha256 { get; init; } = string.Empty;
    }

    /// <summary>
    /// Останній повний результат перевірки оновлень.
    /// Існуючі виклики CheckForUpdatesAsync() деконструюють 3-елементний кортеж
    /// (MainWindow — заборонена зона редагування), тому додаткові дані (downloadUrl,
    /// SHA256) віддаються через цю властивість, щоб не ламати сумісність.
    /// </summary>
    public UpdateCheckResult? LastCheckResult { get; private set; }

    public async Task<(bool hasUpdate, string latestVersion, string url)> CheckForUpdatesAsync()
    {
        try
        {
            using var client = CreateHttpClient();

            var response = await client.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode) return (false, "", "");

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null || string.IsNullOrEmpty(release.TagName)) return (false, "", "");

            var asset = release.Assets?.FirstOrDefault(a =>
                string.Equals(a.Name, AssetFileName, StringComparison.OrdinalIgnoreCase));

            LastCheckResult = new UpdateCheckResult
            {
                HasUpdate = false,
                LatestVersion = release.TagName,
                Url = release.HtmlUrl,
                AssetDownloadUrl = asset?.BrowserDownloadUrl ?? string.Empty,
                AssetSha256 = ExtractSha256FromDigest(asset?.Digest)
            };

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            var latestVersionStr = release.TagName.TrimStart('v', 'V');

            if (Version.TryParse(latestVersionStr, out var latestVersion))
            {
                if (latestVersion > currentVersion)
                {
                    LastCheckResult = LastCheckResult with { HasUpdate = true };
                    return (true, release.TagName, release.HtmlUrl);
                }
            }
        }
        catch { }

        return (false, "", "");
    }

    /// <summary>
    /// Завантажує exe з release-ассета у %TEMP%\WinAudioBooster\update\, за потреби
    /// перевіряє SHA256, після чого запускає бат-скрипт: очікування закриття поточного
    /// процесу (до 30 с) → резервна копія .bak → заміна exe → перезапуск → очищення temp.
    /// </summary>
    /// <param name="assetDownloadUrl">Прямий URL ассета WinAudioBooster.exe (browser_download_url).</param>
    /// <param name="expectedSha256">Очікуваний SHA256 (з GitHub API Assets[].digest, "sha256:..." або чистий hex). Порожній/невідомий — перевірка пропускається з попередженням.</param>
    /// <param name="progress">Опціональний прогрес-репортер для UI.</param>
    /// <returns>true, якщо завантаження та запуск встановлення пройшли успішно.</returns>
    public async Task<bool> DownloadAndInstallUpdateAsync(string assetDownloadUrl, string? expectedSha256, IProgress<string>? progress = null)
    {
        void Report(string message) => progress?.Report(message);

        try
        {
            if (string.IsNullOrWhiteSpace(assetDownloadUrl))
            {
                Report("URL завантаження оновлення не вказано.");
                return false;
            }

            // %TEMP%\WinAudioBooster\update\ — тека для завантаженого exe
            var updateRoot = Path.Combine(Path.GetTempPath(), "WinAudioBooster");
            var updateDir = Path.Combine(updateRoot, "update");
            Directory.CreateDirectory(updateDir);

            // Очистити залишки попередніх спроб оновлення
            foreach (var stale in Directory.EnumerateFiles(updateRoot, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(stale); } catch { /* зайнятий файл — не критично */ }
            }

            var downloadedFile = Path.Combine(updateDir, AssetFileName);
            Report($"Завантаження оновлення: {assetDownloadUrl}");

            using (var client = CreateHttpClient())
            using (var response = await client.GetAsync(assetDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Report($"Помилка завантаження: HTTP {(int)response.StatusCode}.");
                    return false;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(downloadedFile);

                var buffer = new byte[81920];
                long totalRead = 0;
                int lastPercent = -1;
                int bytesRead;

                while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percent = (int)(totalRead * 100 / totalBytes);
                        if (percent != lastPercent && percent % 10 == 0)
                        {
                            lastPercent = percent;
                            Report($"Завантаження: {percent}%");
                        }
                    }
                }
            }

            Report($"Завантажено {new FileInfo(downloadedFile).Length:N0} байт.");

            // Перевірка цілісності за SHA256 — ОБОВ'ЯЗКОВА (fail-closed):
            // без digest з GitHub API оновлення НЕ встановлюється, бо запуск
            // неперевіреного exe — це шлях до remote code execution.
            var expectedHex = ExtractSha256FromDigest(expectedSha256);
            if (string.IsNullOrEmpty(expectedHex))
            {
                Report("Помилка: GitHub не надав SHA256 для оновлення — встановлення скасовано (безпечна політика).");
                try { File.Delete(downloadedFile); } catch { }
                return false;
            }

            Report("Перевірка SHA256...");
            await using var fileStream = File.OpenRead(downloadedFile);
            var actualHex = Convert.ToHexString(await SHA256.HashDataAsync(fileStream)).ToLowerInvariant();

            if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            {
                Report("Помилка: SHA256 завантаженого файлу не збігається з очікуваним. Оновлення скасовано.");
                try { File.Delete(downloadedFile); } catch { }
                return false;
            }

            Report("SHA256 збігається — файл цілий.");

            // Шлях до поточного виконуваного файлу
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
            {
                Report("Не вдалося визначити шлях до поточного виконуваного файлу.");
                return false;
            }

            // Бат-скрипт кладемо ОКРЕМО від теки update\, яку він видаляє,
            // щоб cmd не намагався читати команду з уже видаленого файлу
            var batPath = Path.Combine(updateRoot, "update.bat");
            await File.WriteAllTextAsync(batPath, BuildUpdateBatchScript(currentExe, downloadedFile, updateDir), new UTF8Encoding(false));

            Report("Запуск встановлення оновлення (додаток буде перезапущено)...");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Report($"Помилка оновлення: {ex.Message}");
            return false;
        }
    }

    /// <summary>Готує бат-скрипт встановлення: очікування закриття процесу → .bak → заміна → перезапуск → очищення temp.</summary>
    private static string BuildUpdateBatchScript(string currentExe, string downloadedExe, string updateDir)
    {
        // Жодних кириличних коментарів у бат-файлі — кодова сторінка cmd непередбачувана
        return $"""
        @echo off
        set "TARGET={currentExe}"
        set "SOURCE={downloadedExe}"
        set "UPDATEDIR={updateDir}"
        set "EXENAME={Path.GetFileName(currentExe)}"
        set /a WAITED=0
        :waitloop
        tasklist /FI "IMAGENAME eq %EXENAME%" 2>nul | find /I "%EXENAME%" >nul
        if errorlevel 1 goto :install
        if %WAITED% GEQ {WaitForExitTimeoutSeconds} goto :install
        ping -n 2 127.0.0.1 >nul
        set /a WAITED+=1
        goto :waitloop
        :install
        if exist "%TARGET%.bak" del /f /q "%TARGET%.bak"
        copy /y "%TARGET%" "%TARGET%.bak" >nul
        copy /y "%SOURCE%" "%TARGET%" >nul
        start "" "%TARGET%"
        rmdir /s /q "%UPDATEDIR%"
        del /f /q "%~f0" >nul 2>&1 & exit
        """;
    }

    /// <summary>Витягує hex-SHA256 з формату GitHub API "sha256:&lt;hex&gt;" (або приймає чистий hex-64).</summary>
    private static string ExtractSha256FromDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest)) return string.Empty;

        var value = digest.Trim();
        const string prefix = "sha256:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..].Trim();
        }
        else if (value.StartsWith("sha", StringComparison.OrdinalIgnoreCase))
        {
            // Інший алгоритм дайджеста (наприклад sha512) — перевірку виконати не можна
            return string.Empty;
        }

        // Повинен залишитися чистий hex рядок довжиною 64 символи
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : string.Empty;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "WinAudioBooster-Client");
        client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
        return client;
    }
}