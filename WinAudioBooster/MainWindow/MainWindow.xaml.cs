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

    public MainWindow()
    {
        InitializeComponent();

        InitializeTrayAndHotkeys();
        LoadPhysicalOutputDevices();
        LoadSavedSettingsAndPresets();

        InitializeAudio();
        InitializeAppDetector();
        InitializeVuTimer();

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
        _sliderDebounceCts?.Cancel();
        _appVolumeDebounceCts?.Cancel();
        _hotkeyManager.Dispose();
        _trayService?.Dispose();
        _volumeManager.Dispose();
        _appDetector.Dispose();
        _audioEngine.Dispose();
        base.OnClosed(e);
    }
}