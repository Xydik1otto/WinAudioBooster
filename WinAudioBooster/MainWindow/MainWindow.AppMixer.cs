namespace WinAudioBooster;

public partial class MainWindow
{
    private void InitializeAppDetector()
    {
        _appDetector.PresetChangeRequested += (s, presetName) =>
        {
            Dispatcher.Invoke(() => RefreshPresetsList(presetName));
        };
    }

    private async void ToggleAppMixer_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshActiveAppSessionsAsync();

        AppMixerOverlay.Visibility = Visibility.Visible;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        AppMixerOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    private async void RefreshAppSessions_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshActiveAppSessionsAsync();
    }

    private async Task RefreshActiveAppSessionsAsync()
    {
        var captureCableDevice = _deviceManager.FindCableDevice();
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        MMDevice? targetDevice = EnableBoosterCheckBox.IsChecked == true 
            ? captureCableDevice 
            : selectedOutputInfo?.Device;

        var activeSessions = await _appVolumeService.GetActiveAudioSessionsAsync(targetDevice);
        AppMixerItemsControl.ItemsSource = activeSessions;
    }

    private void AppVolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && slider.Tag is int pid)
        {
            _appVolumeDebounceCts?.Cancel();
            _appVolumeDebounceCts = new CancellationTokenSource();
            var token = _appVolumeDebounceCts.Token;

            double val = e.NewValue;

            Task.Run(async () =>
            {
                await Task.Delay(15, token);
                if (token.IsCancellationRequested) return;

                var captureCableDevice = _deviceManager.FindCableDevice();
                var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

                MMDevice? targetDevice = EnableBoosterCheckBox.IsChecked == true 
                    ? captureCableDevice 
                    : selectedOutputInfo?.Device;

                await _appVolumeService.SetAppVolumeAndBoostAsync(targetDevice, pid, val);
            }, token);
        }
    }

    private void CloseAppMixer_OnClick(object sender, RoutedEventArgs e)
    {
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        fadeOut.Completed += (s, ev) => AppMixerOverlay.Visibility = Visibility.Collapsed;
        AppMixerOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ToggleRulesEditor_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshRulesEditorUI();
        RulesEditorOverlay.Visibility = Visibility.Visible;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation { From = 0.0, To = 1.0, Duration = TimeSpan.FromMilliseconds(200) };
        RulesEditorOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void CloseRulesEditor_OnClick(object sender, RoutedEventArgs e)
    {
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation { From = 1.0, To = 0.0, Duration = TimeSpan.FromMilliseconds(150) };
        fadeOut.Completed += (s, ev) => RulesEditorOverlay.Visibility = Visibility.Collapsed;
        RulesEditorOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void RuleVolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RuleVolumeText != null)
        {
            RuleVolumeText.Text = $"{e.NewValue:F0}%";
        }
    }

    private void RefreshRulesEditorUI()
    {
        var settings = _settingsManager.LoadSettings();
        RulesItemsControl.ItemsSource = settings.CustomRules;

        var presets = _presetManager.LoadAllPresets();
        NewRulePresetCombo.ItemsSource = presets;
        NewRulePresetCombo.DisplayMemberPath = "Name";
        if (presets.Count > 0) NewRulePresetCombo.SelectedIndex = 0;

        var runningApps = AppDetector.GetRunningAppsWithWindows();
        ActiveProcessesComboBox.ItemsSource = runningApps;
        if (runningApps.Count > 0) ActiveProcessesComboBox.SelectedIndex = 0;
    }

    private void AddCustomRule_OnClick(object sender, RoutedEventArgs e)
    {
        string procName = ActiveProcessesComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(procName)) return;

        if (!procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            procName += ".exe";
        }

        string presetName = (NewRulePresetCombo.SelectedItem as Preset)?.Name ?? "Light Boost";
        double customVolume = RuleVolumeSlider.Value;

        var settings = _settingsManager.LoadSettings();
        settings.CustomRules.RemoveAll(r => r.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase));
    
        settings.CustomRules.Add(new ProcessRule 
        { 
            ProcessName = procName, 
            TargetPresetName = presetName, 
            VolumePercent = customVolume,
            Priority = 1 
        });

        _settingsManager.SaveSettings(settings);
        _appDetector.UpdateCustomRules(settings.CustomRules);

        RefreshRulesEditorUI();
    }

    private void DeleteRule_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string procName)
        {
            var settings = _settingsManager.LoadSettings();
            settings.CustomRules.RemoveAll(r => r.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase));
    
            _settingsManager.SaveSettings(settings);
            _appDetector.UpdateCustomRules(settings.CustomRules);

            RefreshRulesEditorUI();
        }
    }
}