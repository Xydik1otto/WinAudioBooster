using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace WinAudioBooster.Audio
{
    /// <summary>
    /// Опис аудіопристрою для відображення в UI.
    /// </summary>
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public MMDevice Device { get; set; } = null!;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Менеджер для пошуку та вибору аудіопристроїв Windows.
    /// </summary>
    public class DeviceManager
    {
        private readonly MMDeviceEnumerator _enumerator = new();

        /// <summary>
        /// Отримати список усіх активних пристроїв відтворення.
        /// </summary>
        public List<AudioDeviceInfo> GetOutputDevices()
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return devices.Select(d => new AudioDeviceInfo
            {
                Id = d.ID,
                Name = d.FriendlyName,
                Device = d
            }).ToList();
        }

        /// <summary>
        /// Отримати пристрій відтворення за замовчуванням (Loopback source).
        /// </summary>
        public MMDevice? GetDefaultRenderDevice()
        {
            try
            {
                return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Автоматичний пошук віртуального кабелю (будь-якого пристрою зі словом "CABLE" у назві).
        /// </summary>
        public MMDevice? FindCableDevice()
        {
            var outputDevices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return outputDevices.FirstOrDefault(d => 
                d.FriendlyName.ToUpperInvariant().Contains("CABLE"));
        }
    }
}