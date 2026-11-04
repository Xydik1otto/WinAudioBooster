namespace WinAudioBooster;

public partial class MainWindow : Window
{
    private bool _isInitializing = true;
    private bool _isApplyingPreset = false;
    private bool _isListeningForHotkey = false;

    private readonly AudioDeviceMonitor _deviceMonitor = new();
    private readonly AudioEngine _audioEngine = new();
    private readonly DeviceManager _deviceManager = new();
    private readonly VbCableInstaller _cableInstaller = new();
    private readonly PresetManager _presetManager = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly DispatcherTimer _vuTimer = new();
    private readonly AppDetector _appDetector = new();
    private readonly VolumeManager _volumeManager = new();
    private readonly AppVolumeService _appVolumeService = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly HotkeyManager _hotkeyManager = new();
    private readonly UpdateService _updateService = new();

    private CancellationTokenSource? _sliderDebounceCts;
    private CancellationTokenSource? _appVolumeDebounceCts;
    private TrayService? _trayService;
    
    private MiniMixerWindow? _miniMixerWindow;
    public AudioEngine AudioEngineInstance => _audioEngine;

    // Раннє застосування теми (до InitializeComponent): читаємо збережений вибір і
    // застосовуємо скін/авто-режим, щоб перший рендер вікна вже мав правильні кольори
    private void ApplySavedThemeBeforeXamlLoad()
    {
        try
        {
            var settings = _settingsManager.LoadSettings();
            string theme = string.IsNullOrEmpty(settings.SelectedTheme) ? "Razer Green" : settings.SelectedTheme;
            ThemeManager.ApplyTheme(theme);
        }
        catch (Exception ex)
        {
            // Без збереженої теми — працюємо зі стандартними ресурсами App.xaml
            Debug.WriteLine($"Early theme apply skipped: {ex.Message}");
        }
    }

    public MainWindow()
    {
        // Застосовуємо збережену тему ДО парсингу XAML: StaticResource-кольори вікна
        // одразу захоплять правильні пензлі (скін + світлий overlay за потреби)
        ApplySavedThemeBeforeXamlLoad();

        InitializeComponent();

        InitializeTrayAndHotkeys();
        LoadPhysicalOutputDevices();
        LoadSavedSettingsAndPresets();
        InitializeDeviceProfiles();

        InitializeAudio();
        InitializeAppDetector();
        InitializeVuTimer();
        InitializeSleepTimer();

        _ = RefreshActiveAppSessionsAsync();
        AutoStartCheckBox.IsChecked = _autoStartService.IsAutoStartEnabled();

        _isInitializing = false;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _miniMixerWindow ??= new MiniMixerWindow(this);
            _miniMixerWindow.SyncWithMainWindow();
            _miniMixerWindow.Show();
        }
        base.OnStateChanged(e);
    }

    public void TriggerBoosterToggleFromMini()
    {
        RestartAudioEngineIfEnabled();
        SaveCurrentSettings();
    }

    protected override void OnClosed(EventArgs e)
    {
        _deviceMonitor.Dispose();
        SaveCurrentSettings();
        _vuTimer.Stop();
        _sleepTimer.Stop();
        _sleepFadeTimer.Stop();
        _sliderDebounceCts?.Cancel();
        _appVolumeDebounceCts?.Cancel();
        _hotkeyManager.Dispose();
        _trayService?.Dispose();
        _volumeManager.Dispose();
        _appDetector.Dispose();
        _audioEngine.Dispose();
        ThemeManager.Shutdown();
        base.OnClosed(e);
    }
}