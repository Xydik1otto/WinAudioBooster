using WinAudioBooster.Audio;

namespace WinAudioBooster.Models;

public class AppSettings
{
    public bool IsBoosterEnabled { get; set; } = false;
    public string Language { get; set; } = string.Empty;
    public string SelectedOutputDeviceId { get; set; } = string.Empty;
    public string SelectedOutputDeviceName { get; set; } = string.Empty;
    public string SelectedPresetName { get; set; } = "Light Boost";

    public double GainDb { get; set; } = 100.0;
    public double CompressorThresholdDb { get; set; } = -20.0;
    public double CompressorRatio { get; set; } = 3.0;
    public double LimiterCeilingDb { get; set; } = -0.3;

    // 10-смуговий еквалайзер
    public bool IsEqEnabled { get; set; } = true;
    public double[] EqGainsDb { get; set; } = new double[DspProcessor.BandCount];

    public uint HotkeyModifiers { get; set; } = 3;
    public uint HotkeyVKey { get; set; } = 0x42;
    public string HotkeyDisplayName { get; set; } = "Ctrl + Alt + B";
    public string SelectedTheme { get; set; } = "Razer Green";

    public List<ProcessRule> CustomRules { get; set; } = new();
}