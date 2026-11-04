namespace WinAudioBooster.Audio;

public class VolumeManager : IDisposable
{
    private MMDevice? _monitoredDevice;
    private bool _isInternalChange = false;

    public event EventHandler<double>? VolumeChangedExternally;

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

    public Task<float> SetVolumeAndCalculateGainDbAsync(double totalPercent, MMDevice? controlledDevice, MMDevice? physicalSpeakerDevice)
    {
        return Task.Run(() =>
        {
            _isInternalChange = true;
            try
            {
                if (physicalSpeakerDevice?.AudioEndpointVolume != null)
                {
                    if (Math.Abs(physicalSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar - 1.0f) > 0.01f)
                    {
                        physicalSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                    }
                }

                if (controlledDevice?.AudioEndpointVolume != null)
                {
                    float targetScalar = totalPercent <= 100.0 ? Math.Clamp((float)(totalPercent / 100.0), 0.0f, 1.0f) : 1.0f;
                    
                    if (Math.Abs(controlledDevice.AudioEndpointVolume.MasterVolumeLevelScalar - targetScalar) > 0.005f)
                    {
                        controlledDevice.AudioEndpointVolume.MasterVolumeLevelScalar = targetScalar;
                    }
                }

                if (totalPercent <= 100.0) return 0.0f;

                double ratio = totalPercent / 100.0;
                return (float)(20.0 * Math.Log10(ratio));
            }
            catch
            {
                return 0.0f;
            }
            finally
            {
                _isInternalChange = false;
            }
        });
    }

    public void UnbindDevice()
    {
        if (_monitoredDevice?.AudioEndpointVolume != null)
        {
            _monitoredDevice.AudioEndpointVolume.OnVolumeNotification -= OnWindowsVolumeChanged;
        }
        _monitoredDevice = null;
    }

    public void Dispose() => UnbindDevice();
}