using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace WinAudioBooster.Services
{
    public class AutoStartService
    {
        private const string AppName = "WinAudioBooster";

        public bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                object? value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        public void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoStart error: {ex.Message}");
            }
        }
    }
}