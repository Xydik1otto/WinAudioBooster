using CommunityToolkit.Mvvm.ComponentModel;

namespace WinAudioBooster.Services.ViewModels;

/// <summary>
/// ViewModel головного вікна: джерело правди для бідингу контролів та збереження стану.
/// Логіка аудіо лишається в code-behind (обробники подій), ViewModel лише
/// зберігає стан і віддає його у Snapshot()/Apply() для налаштувань.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // --- Головний бустер ---
    [ObservableProperty]
    private bool _isBoosterEnabled;

    // --- Гучність / компресор / лімітер (у % та дБ, як у AppSettings) ---
    [ObservableProperty]
    private double _gainPercent = 100;

    [ObservableProperty]
    private double _compressorThresholdDb = -20;

    [ObservableProperty]
    private double _compressorRatio = 3;

    [ObservableProperty]
    private double _limiterCeilingDb = -0.3;

    // --- Еквалайзер і швидкі DSP-тоггли ---
    [ObservableProperty]
    private bool _isEqEnabled = true;

    [ObservableProperty]
    private bool _isBassBoostEnabled;

    [ObservableProperty]
    private bool _isTrebleBoostEnabled;

    [ObservableProperty]
    private bool _isSubBassFilterEnabled;

    // --- Просторові DSP-тоггли ---
    [ObservableProperty]
    private bool _isLoudnessNormalizationEnabled;

    [ObservableProperty]
    private bool _isStereoWidenEnabled;

    [ObservableProperty]
    private bool _isCrossfeedEnabled;

    [ObservableProperty]
    private bool _isMonoEnabled;

    // Баланс L/R: -1 = повністю лівий, 0 = центр, +1 = повністю правий
    [ObservableProperty]
    private double _balance;

    // --- Пресет / тема / мова / системні опції ---
    [ObservableProperty]
    private string _selectedPresetName = "Light Boost";

    [ObservableProperty]
    private string _selectedTheme = "Razer Green";

    [ObservableProperty]
    private string _selectedLanguage = string.Empty;

    [ObservableProperty]
    private bool _isHearingProtectionEnabled = true;

    [ObservableProperty]
    private bool _isLoggingEnabled = true;

    // Таймер сну в хвилинах (0 = вимкнено; синхронізується з ComboBox у code-behind)
    [ObservableProperty]
    private int _sleepTimerMinutes;

    // Еквалайзер: 10 смуг для бідингу слайдерів
    public EqViewModel Eq { get; } = new();

    /// <summary>
    /// Знімок стану у AppSettings (лише поля, джерелом правди для яких є ViewModel;
    /// пристрій виводу, мова hotkey-параметрів і нічний режим додаються в code-behind).
    /// </summary>
    public AppSettings Snapshot()
    {
        return new AppSettings
        {
            IsBoosterEnabled = IsBoosterEnabled,
            GainDb = GainPercent,
            CompressorThresholdDb = CompressorThresholdDb,
            CompressorRatio = CompressorRatio,
            LimiterCeilingDb = LimiterCeilingDb,
            IsEqEnabled = IsEqEnabled,
            EqGainsDb = Eq.GetGains(),
            IsBassBoostEnabled = IsBassBoostEnabled,
            IsTrebleBoostEnabled = IsTrebleBoostEnabled,
            IsSubBassFilterEnabled = IsSubBassFilterEnabled,
            IsLoudnessNormalizationEnabled = IsLoudnessNormalizationEnabled,
            IsStereoWidenEnabled = IsStereoWidenEnabled,
            IsCrossfeedEnabled = IsCrossfeedEnabled,
            IsMonoEnabled = IsMonoEnabled,
            Balance = Balance,
            SelectedPresetName = SelectedPresetName,
            SelectedTheme = SelectedTheme,
            Language = SelectedLanguage,
            IsHearingProtectionEnabled = IsHearingProtectionEnabled,
            IsLoggingEnabled = IsLoggingEnabled
        };
    }

    /// <summary>
    /// Застосовує збережені налаштування до ViewModel. Бідинг сам оновить контроли,
    /// а обробники ValueChanged/Checked синхронізують DSP (гейтяться прапорцями ініціалізації).
    /// </summary>
    public void Apply(AppSettings settings)
    {
        IsBoosterEnabled = settings.IsBoosterEnabled;

        // Як і раніше: нульове/некоректне збережене підсилення → 100%
        GainPercent = settings.GainDb > 0 ? settings.GainDb : 100;

        CompressorThresholdDb = settings.CompressorThresholdDb;
        CompressorRatio = settings.CompressorRatio;
        LimiterCeilingDb = settings.LimiterCeilingDb;

        IsEqEnabled = settings.IsEqEnabled;
        Eq.SetGains(settings.EqGainsDb);

        IsBassBoostEnabled = settings.IsBassBoostEnabled;
        IsTrebleBoostEnabled = settings.IsTrebleBoostEnabled;
        IsSubBassFilterEnabled = settings.IsSubBassFilterEnabled;

        IsLoudnessNormalizationEnabled = settings.IsLoudnessNormalizationEnabled;
        IsStereoWidenEnabled = settings.IsStereoWidenEnabled;
        IsCrossfeedEnabled = settings.IsCrossfeedEnabled;
        IsMonoEnabled = settings.IsMonoEnabled;
        Balance = settings.Balance;

        IsHearingProtectionEnabled = settings.IsHearingProtectionEnabled;
        IsLoggingEnabled = settings.IsLoggingEnabled;

        SelectedPresetName = string.IsNullOrWhiteSpace(settings.SelectedPresetName)
            ? "Light Boost"
            : settings.SelectedPresetName;

        // Порожня тема у файлі → стандартна «Razer Green» (як і раніше в LoadSavedSettingsAndPresets)
        SelectedTheme = string.IsNullOrEmpty(settings.SelectedTheme)
            ? "Razer Green"
            : settings.SelectedTheme;

        SelectedLanguage = settings.Language;
    }
}
