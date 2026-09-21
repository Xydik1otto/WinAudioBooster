using System;
using Microsoft.Win32;

namespace WinAudioBooster.Services
{
    public static class AutoStartService
    {
        private const string AppName = "WinAudioBooster";
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(AppName, $"\"{executablePath}\" --autostart");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName);
                    }
                }
            }
            catch { }
        }
    }
}