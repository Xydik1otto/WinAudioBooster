using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace WinAudioBooster.Audio
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public MMDevice Device { get; set; } = null!;

        public override string ToString() => Name;
    }

    public class DeviceManager
    {
        public bool IsCableInstalled()
        {
            try
            {
                using var mediaClassKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}");
                if (mediaClassKey != null)
                {
                    foreach (var subkeyName in mediaClassKey.GetSubKeyNames())
                    {
                        using var subkey = mediaClassKey.OpenSubKey(subkeyName);
                        if (subkey != null)
                        {
                            string driverDesc = subkey.GetValue("DriverDesc")?.ToString() ?? "";
                            string providerName = subkey.GetValue("ProviderName")?.ToString() ?? "";

                            if (driverDesc.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                                driverDesc.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
                                providerName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All);
                return allDevices.Any(IsCableMatch);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Пошук віртуального кабелю CABLE Input (звідки програма захоплює системний звук).
        /// </summary>
        public MMDevice? FindCableDevice()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var outputDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                return outputDevices.FirstOrDefault(IsCableMatch);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Отримує поточний системний пристрій відтворення за замовчуванням у Windows.
        /// </summary>
        public MMDevice? GetDefaultRenderDevice()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Отримує список тільки реальних ФІЗИЧНИХ пристроїв відтворення (динаміки, навушники, HDMI).
        /// </summary>
        public List<AudioDeviceInfo> GetPhysicalOutputDevices()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var outputDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                return outputDevices
                    .Where(d => !IsCableMatch(d))
                    .Select(d => new AudioDeviceInfo
                    {
                        Id = d.ID,
                        Name = d.FriendlyName,
                        Device = d
                    })
                    .ToList();
            }
            catch
            {
                return new List<AudioDeviceInfo>();
            }
        }

        private static bool IsCableMatch(MMDevice device)
        {
            if (device == null || string.IsNullOrEmpty(device.FriendlyName)) return false;
            string name = device.FriendlyName;
            return name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Virtual Cable", StringComparison.OrdinalIgnoreCase);
        }
    }
}