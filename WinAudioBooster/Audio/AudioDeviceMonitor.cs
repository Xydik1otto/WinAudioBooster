namespace WinAudioBooster.Audio;

public class AudioDeviceMonitor : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;

    public event Action? DevicesOrStateChanged;

    public AudioDeviceMonitor()
    {
        _enumerator = new MMDeviceEnumerator();
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch { }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia)
        {
            DevicesOrStateChanged?.Invoke();
        }
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => DevicesOrStateChanged?.Invoke();
    public void OnDeviceAdded(string pwstrDeviceId) => DevicesOrStateChanged?.Invoke();
    public void OnDeviceRemoved(string deviceId) => DevicesOrStateChanged?.Invoke();
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    public void Dispose()
    {
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
            _enumerator.Dispose();
        }
        catch { }
    }
}