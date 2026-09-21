using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace WinAudioBooster.Services
{
    /// <summary>
    /// Сервіс для автоматичного завантаження та запуску інсталятора VB-Audio Virtual Cable.
    /// </summary>
    public class VbCableInstaller
    {
        private const string CableZipUrl = "https://download.vb-audio.com/Download_ZIP/VBCABLE_Driver_Pack43.zip";

        /// <summary>
        /// Завантажує, розпаковує та запускає інсталятор VB-Cable.
        /// </summary>
        public async Task<bool> DownloadAndInstallAsync(Action<string>? statusCallback = null)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "WinAudioBooster_VBCable");
                string zipPath = Path.Combine(tempPath, "VBCABLE_Driver_Pack.zip");
                string extractPath = Path.Combine(tempPath, "Extracted");

                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
                Directory.CreateDirectory(tempPath);

                statusCallback?.Invoke("Завантаження VB-Cable...");
                using (var client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(CableZipUrl);
                    await File.WriteAllBytesAsync(zipPath, bytes);
                }

                statusCallback?.Invoke("Розпакування інсталятора...");
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                string installerExecutable = Environment.Is64BitOperatingSystem
                    ? Path.Combine(extractPath, "VBCABLE_Setup_x64.exe")
                    : Path.Combine(extractPath, "VBCABLE_Setup.exe");

                if (!File.Exists(installerExecutable))
                {
                    throw new FileNotFoundException("Не знайдено файл інсталяції VB-Cable у завантаженому архіві.");
                }

                statusCallback?.Invoke("Запуск інсталятора з правами адміністратора...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerExecutable,
                    UseShellExecute = true,
                    Verb = "runas" // Запит прав адміністратора
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Помилка: {ex.Message}");
                return false;
            }
        }
    }
}