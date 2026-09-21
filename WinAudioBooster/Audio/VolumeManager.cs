using System;
using NAudio.CoreAudioApi;

namespace WinAudioBooster.Audio
{
    public class VolumeManager : IDisposable
    {
        private MMDevice? _monitoredDevice;
        private bool _isInternalChange = false;

        public event EventHandler<double>? VolumeChangedExternally;

        /// <summary>
        /// Прив'язує відстеження гучності до CABLE Input (коли бустер увімкнено) або фізичних динаміків (коли вимкнено).
        /// </summary>
        public void BindDevice(MMDevice? device)
        {
            UnbindDevice();

            _monitoredDevice = device;
            if (_monitoredDevice?.AudioEndpointVolume != null)
            {
                _monitoredDevice.AudioEndpointVolume.OnVolumeNotification += OnWindowsVolumeChanged;
            }
        }

        private void OnWindowsVolumeChanged(AudioVolumeNotificationData data)
        {
            if (_isInternalChange) return;

            double percent = Math.Round(data.MasterVolume * 100.0);
            VolumeChangedExternally?.Invoke(this, percent);
        }

        /// <summary>
        /// Керує системним пристроєм у мікшері Windows (0..100%) та вираховує DSP Gain (дБ) для підсилення понад 100%.
        /// </summary>
        public float SetVolumeAndCalculateGainDb(double totalPercent, MMDevice? controlledDevice, MMDevice? physicalSpeakerDevice)
        {
            _isInternalChange = true;
            try
            {
                // Фізичні динаміки завжди тримаємо на 100% гучності для максимальної якості PCM-потоку
                if (physicalSpeakerDevice?.AudioEndpointVolume != null)
                {
                    physicalSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                }

                if (controlledDevice?.AudioEndpointVolume != null)
                {
                    if (totalPercent <= 100.0)
                    {
                        float scalar = (float)(totalPercent / 100.0);
                        controlledDevice.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(scalar, 0.0f, 1.0f);
                    }
                    else
                    {
                        // При значенні > 100% системний мікшер Windows фіксується на 100%
                        controlledDevice.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                    }
                }

                if (totalPercent <= 100.0)
                {
                    return 0.0f; // DSP Gain = 0 dB
                }

                // Переведення відсотків понад 100% у децибели (Gain dB)
                double ratio = totalPercent / 100.0;
                return (float)(20.0 * Math.Log10(ratio));
            }
            finally
            {
                _isInternalChange = false;
            }
        }

        public void UnbindDevice()
        {
            if (_monitoredDevice?.AudioEndpointVolume != null)
            {
                _monitoredDevice.AudioEndpointVolume.OnVolumeNotification -= OnWindowsVolumeChanged;
            }
            _monitoredDevice = null;
        }

        public void Dispose()
        {
            UnbindDevice();
        }
    }
}