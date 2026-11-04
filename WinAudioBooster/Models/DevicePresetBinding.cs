namespace WinAudioBooster.Models;

/// <summary>Прив'язка «пристрій виводу → пресет» для авто-застосування при зміні дефолтного пристрою.</summary>
public class DevicePresetBinding
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
}
