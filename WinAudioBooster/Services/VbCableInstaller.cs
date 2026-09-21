using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinAudioBooster.Services
{
    public class VbCableInstaller
    {
        private const string CableWebsiteUrl = "https://vb-audio.com/Cable/";
        private const string FallbackZipUrl = "https://download.vb-audio.com/Download_ZIP/VBCABLE_Driver_Pack45.zip";

        public async Task<bool> DownloadAndInstallAsync(Action<string>? statusCallback = null)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempPath = Path.Combine(Path.GetTempPath(), "WinAudioBooster_VBCable");
                string extractPath = Path.Combine(tempPath, "Extracted");

                string? targetZipPath = FindValidZipArchiveByContent(appDir, tempPath);

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

        private string? FindValidZipArchiveByContent(string appDir, string tempPath)
        {
            var searchDirectories = new List<string>
            {
                appDir,
                Directory.GetParent(appDir)?.FullName ?? "",
                Directory.GetParent(appDir)?.Parent?.FullName ?? "",
                Directory.GetParent(appDir)?.Parent?.Parent?.FullName ?? "",
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
}