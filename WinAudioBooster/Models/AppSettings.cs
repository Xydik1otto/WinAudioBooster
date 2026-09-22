namespace WinAudioBooster.Models
{
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

        // Налаштування глобального хоткею (За замовчуванням: Ctrl + Alt + B)
        public uint HotkeyModifiers { get; set; } = 3; // ModControl (2) | ModAlt (1)
        public uint HotkeyVKey { get; set; } = 0x42; // 'B'
        public string HotkeyDisplayName { get; set; } = "Ctrl + Alt + B";
    }
}