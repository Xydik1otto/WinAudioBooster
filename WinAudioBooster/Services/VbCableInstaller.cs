using System.ComponentModel;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WinAudioBooster.Services;

public class VbCableInstaller
{
    private const string CableWebsiteUrl = "https://vb-audio.com/Cable/";
    private const string FallbackZipUrl = "https://download.vb-audio.com/Download_ZIP/VBCABLE_Driver_Pack45.zip";

    // Офіційний SHA256 інсталяційного архіву VB-Cable Driver Pack45 (vb-audio.com).
    // Якщо видавець оновить архів — хеш зміниться, тоді онови це значення.
    // Шукає zip лише у власній APPDATA-теці — ніколи у батьківських каталогах програми,
    // щоб підкладений у user-writable теку архів не отримав адмін-запуск.
    private const string ExpectedZipSha256 = "e05b82459b48f5a948a99b8be2445b12e0b3a58f9cbb1a3d21c9b4a7f51d6e622";

    public async Task<bool> DownloadAndInstallAsync(Action<string>? statusCallback = null)
    {
        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "WinAudioBooster_VBCable");
            string extractPath = Path.Combine(tempPath, "Extracted");

            // Приймаємо лише архів, який самі завантажили у власну тимчасову теку.
            // Пошук по каталогах програми видалено (security: підкладений zip -> runas).
            string? targetZipPath = FindValidZipArchiveByContent(tempPath);

            if (!string.IsNullOrEmpty(targetZipPath))
            {
                statusCallback?.Invoke($"Знайдено архів: {Path.GetFileName(targetZipPath)}");
            }
            else
            {
                Directory.CreateDirectory(tempPath);
                targetZipPath = Path.Combine(tempPath, "VBCABLE_Driver_Pack.zip");

                statusCallback?.Invoke("Завантаження VB-Cable з мережі...");

                using var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.Add("Referer", "https://vb-audio.com/");

                string downloadUrl = await GetLatestDownloadUrlAsync(client);

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(targetZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);
            }

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);

            statusCallback?.Invoke("Розпакування архіву...");
            ZipFile.ExtractToDirectory(targetZipPath, extractPath);

            // Перевірка цілісності архіву перед адмін-запуском інсталятора.
            // Хеш офіційного пакета vb-audio.com; при невідповідності — відмова (fail-closed).
            statusCallback?.Invoke("Перевірка SHA256 архіву...");
            string actualZipHash;
            await using (var zipStream = File.OpenRead(targetZipPath))
            {
                actualZipHash = Convert.ToHexString(await SHA256.HashDataAsync(zipStream)).ToLowerInvariant();
            }

            if (!string.Equals(actualZipHash, ExpectedZipSha256, StringComparison.OrdinalIgnoreCase))
            {
                statusCallback?.Invoke("Архів не пройшов перевірку цілісності — встановлення скасовано.");
                return false;
            }

            string? installerExecutable = FindSetupExecutable(extractPath);
            if (string.IsNullOrEmpty(installerExecutable))
            {
                throw new FileNotFoundException("Файл VBCABLE_Setup не знайдено в архіві.");
            }

            statusCallback?.Invoke("Запуск інсталятора...");
            var startInfo = new ProcessStartInfo
            {
                FileName = installerExecutable,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return true;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                statusCallback?.Invoke("Скасовано користувачем.");
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"Помилка: {ex.Message}");
            return false;
        }
    }

    private string? FindValidZipArchiveByContent(string tempPath)
    {
        var searchDirectories = new List<string>
        {
            tempPath
        };

        foreach (var dir in searchDirectories.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)))
        {
            foreach (var zipPath in Directory.GetFiles(dir, "*.zip"))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    bool containsCableSetup = archive.Entries.Any(entry =>
                        entry.Name.StartsWith("VBCABLE_Setup", StringComparison.OrdinalIgnoreCase) &&
                        entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                    if (containsCableSetup) return zipPath;
                }
                catch { }
            }
        }
        return null;
    }

    private async Task<string> GetLatestDownloadUrlAsync(HttpClient client)
    {
        try
        {
            string html = await client.GetStringAsync(CableWebsiteUrl);
            var match = Regex.Match(html, @"https://download\.vb-audio\.com/Download_ZIP/[^""']+\.zip", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;
        }
        catch { }

        return FallbackZipUrl;
    }

    private string? FindSetupExecutable(string extractPath)
    {
        string targetExeName = Environment.Is64BitOperatingSystem ? "VBCABLE_Setup_x64.exe" : "VBCABLE_Setup.exe";

        return Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(f => Path.GetFileName(f).Equals(targetExeName, StringComparison.OrdinalIgnoreCase) ||
                                 Path.GetFileName(f).Equals("VBCABLE_Setup_x64.exe", StringComparison.OrdinalIgnoreCase));
    }

    public static void OpenOfficialWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = CableWebsiteUrl, UseShellExecute = true });
        }
        catch { }
    }
}