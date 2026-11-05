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

    // Швидкі DSP-тоггли (чекбокси в секції Еквалайзер)
    public bool IsBassBoostEnabled { get; set; } = false;
    public bool IsTrebleBoostEnabled { get; set; } = false;
    public bool IsSubBassFilterEnabled { get; set; } = false;

    // Просторові DSP-тоггли (другий рядок чекбоксів у секції Еквалайзер)
    public bool IsLoudnessNormalizationEnabled { get; set; } = false;
    public bool IsStereoWidenEnabled { get; set; } = false;
    public bool IsCrossfeedEnabled { get; set; } = false;
    public bool IsMonoEnabled { get; set; } = false;

    // Баланс L/R: -1 = повністю лівий, 0 = центр, +1 = повністю правий
    public double Balance { get; set; } = 0;

    // Журнал подій (%APPDATA%\WinAudioBooster\log.txt)
    public bool IsLoggingEnabled { get; set; } = true;

    public uint HotkeyModifiers { get; set; } = 3;
    public uint HotkeyVKey { get; set; } = 0x42;
    public string HotkeyDisplayName { get; set; } = "Ctrl + Alt + B";
    public string SelectedTheme { get; set; } = "Razer Green";

    public List<ProcessRule> CustomRules { get; set; } = new();

    // Прив'язки «пристрій виводу → пресет» (авто-профілі за пристроєм)
    public List<DevicePresetBinding> DevicePresetBindings { get; set; } = new();

    // Запам'ятовані гучності процесів (per-app персистентність)
    public List<AppVolumeRule> AppVolumeRules { get; set; } = new();

    // Захист слуху: попередження при підсиленні >150% (один раз на запуск додатку)
    public bool IsHearingProtectionEnabled { get; set; } = true;

    // Нічний режим за розкладом (Quiet Hours): години початку/кінця (0–23), працює через північ
    public bool QuietHoursEnabled { get; set; } = false;
    public int QuietHoursStart { get; set; } = 23;
    public int QuietHoursEnd { get; set; } = 7;

    // Користувацькі колірні гами (custom color palettes), створені з поточної теми
    public List<CustomTheme> CustomThemes { get; set; } = new();
}