using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WinAudioBooster.Utils
{
    /// <summary>
    /// Автономний перемикач аудіопристроїв Windows без використання заблокованих COM-класів.
    /// </summary>
    public static class AudioDeviceSwitcher
    {
        /// <summary>
        /// Змінює системний пристрій відтворення за його назвою або відкриває налаштування Звуку.
        /// </summary>
        public static async Task<bool> SetDefaultDeviceAsync(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return false;

            // 1. Спроба змінити пристрій через PowerShell
            bool success = await Task.Run(() => TrySwitchViaPowerShell(deviceName));
            if (success) return true;

            // 2. Резервний варіант: відкриття системних налаштувань Звуку Windows
            OpenSoundSettings();
            return false;
        }

        private static bool TrySwitchViaPowerShell(string deviceName)
        {
            try
            {
                // Очищення назви від дужок для точного пошуку в системі
                string cleanName = deviceName.Split('(')[0].Trim();

                string psScript = $@"
                    $target = '{cleanName.Replace("'", "''")}';
                    if (Get-Command Set-AudioDevice -ErrorAction SilentlyContinue) {{
                        $dev = Get-AudioDevice -List | Where-Object {{ $_.Name -like ""*$target*"" }} | Select-Object -First 1;
                        if ($dev) {{ Set-AudioDevice -Index $dev.Index; exit 0 }}
                    }}
                    exit 1;
                ";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace(Environment.NewLine, " ")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(1200);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Відкриває системне вікно налаштувань Звуку Windows 11.
        /// </summary>
        public static void OpenSoundSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:sound",
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start("control.exe", "mmsys.cpl sounds");
                }
                catch { }
            }
        }
    }
}