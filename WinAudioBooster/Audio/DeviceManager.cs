namespace WinAudioBooster.Audio;

public class AudioDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MMDevice Device { get; set; } = null!;

    public override string ToString() => Name;
}

public class DeviceManager
{
    public MMDevice? FindCableDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                             .FirstOrDefault(IsCableMatch);
        }
        catch { return null; }
    }

    public MMDevice? GetDefaultRenderDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch { return null; }
    }

    public List<AudioDeviceInfo> GetPhysicalOutputDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Where(d => !IsCableMatch(d))
                .Select(d => new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName, Device = d })
                .ToList();
        }
        catch { return new List<AudioDeviceInfo>(); }
    }

    public bool IsCableInstalled()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active).Any(IsCableMatch);
        }
        catch { return false; }
    }

    private static bool IsCableMatch(MMDevice device)
    {
        if (device == null) return false;
        string name = string.Empty;
        try { name = device.FriendlyName; } catch { return false; }
        if (string.IsNullOrEmpty(name)) return false;

        return name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Virtual Cable", StringComparison.OrdinalIgnoreCase);
    }
}