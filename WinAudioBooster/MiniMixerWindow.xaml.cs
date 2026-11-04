namespace WinAudioBooster;

public partial class MiniMixerWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly DispatcherTimer _vuTimer = new();
    private readonly OutputPeakHistoryVisualizer _spectrumVisualizer;

    public MiniMixerWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;

        // Візуалізатор історії піків виходу (32 бари, оновлюється у _vuTimer)
        _spectrumVisualizer = new OutputPeakHistoryVisualizer(MiniSpectrumCanvas);

        var desktopWorkingArea = SystemParameters.WorkArea;
        Left = desktopWorkingArea.Right - Width - 10;
        Top = desktopWorkingArea.Bottom - Height - 10;

        SyncWithMainWindow();
        InitializeVuTimer();
    }

    public void SyncWithMainWindow()
    {
        MiniGainSlider.Value = _mainWindow.GainSlider.Value;
        MiniGainText.Text = $"{MiniGainSlider.Value:F0}%";
        MiniBoosterToggle.IsChecked = _mainWindow.EnableBoosterCheckBox.IsChecked;
        MiniEqToggle.IsChecked = _mainWindow.EqEnabledCheckBox.IsChecked;
    }

    private void InitializeVuTimer()
    {
        _vuTimer.Interval = TimeSpan.FromMilliseconds(30);
        _vuTimer.Tick += (s, e) =>
        {
            bool boosterEnabled = _mainWindow.EnableBoosterCheckBox.IsChecked == true;

            if (_mainWindow.AudioEngineInstance.Dsp != null && boosterEnabled)
            {
                MiniVuBar.Value = _mainWindow.AudioEngineInstance.Dsp.OutputMeter.CurrentLevel;
            }
            else
            {
                MiniVuBar.Value = 0;
            }

            // Історія піків у тому ж кадрі; при вимкненому DSP пишуться нулі
            _spectrumVisualizer.Push(_mainWindow.AudioEngineInstance.Dsp?.OutputMeter.CurrentLevel ?? 0, boosterEnabled);
        };
        _vuTimer.Start();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MiniGainSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MiniGainText != null) MiniGainText.Text = $"{e.NewValue:F0}%";
        if (_mainWindow?.GainSlider != null)
        {
            _mainWindow.GainSlider.Value = e.NewValue;
        }
    }

    private void MiniBoosterToggle_OnClick(object sender, RoutedEventArgs e)
    {
        _mainWindow.EnableBoosterCheckBox.IsChecked = MiniBoosterToggle.IsChecked;
        _mainWindow.TriggerBoosterToggleFromMini();
    }

    private void MiniEqToggle_OnClick(object sender, RoutedEventArgs e)
    {
        _mainWindow.EqEnabledCheckBox.IsChecked = MiniEqToggle.IsChecked;
        _mainWindow.TriggerEqToggleFromMini();
    }

    private void ExpandButton_OnClick(object sender, RoutedEventArgs e)
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        Hide();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _vuTimer.Stop();
        base.OnClosed(e);
    }
}