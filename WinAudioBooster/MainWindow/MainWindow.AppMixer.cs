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

        // Персистентність гучності per-app: застосовуємо збережені відсотки до відповідних процесів
        try
        {
            var settings = _settingsManager.LoadSettings();
            if (targetDevice != null && settings.AppVolumeRules.Count > 0)
            {
                var applyTasks = new List<Task>();
                foreach (var session in activeSessions)
                {
                    string procName = ResolveProcessName(session.ProcessId, session.ProcessName);
                    var rule = settings.AppVolumeRules.FirstOrDefault(r =>
                        r.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase));
                    if (rule == null) continue;

                    // Оновлюємо модель, щоб повзунок у UI одразу показав збережене значення
                    session.VolumePercent = rule.VolumePercent;
                    applyTasks.Add(_appVolumeService.SetAppVolumeAndBoostAsync(targetDevice, session.ProcessId, rule.VolumePercent));
                }

                if (applyTasks.Count > 0) await Task.WhenAll(applyTasks);
            }
        }
        catch
        {
            // Автозастосування не критичне: показуємо сесії як є
        }

        AppMixerItemsControl.ItemsSource = activeSessions;
    }

    private void AppVolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && slider.Tag is int pid)
        {
            // Персистентність per-app: запам'ятовуємо останнє значення для цього процесу (синхронно)
            string procName = ResolveProcessName(pid, fallback: null);
            SaveAppVolumeRule(procName, e.NewValue);

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

    /// <summary>Повертає ім'я процесу (нормалізоване, з .exe) для ключа правил гучності.</summary>
    private static string ResolveProcessName(int processId, string? fallback)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            string name = proc.ProcessName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : name + ".exe";
            }
        }
        catch
        {
            // Процес міг завершитися — використовуємо fallback
        }

        return fallback ?? string.Empty;
    }

    /// <summary>Синхронне збереження правила гучності процесу (load-modify-save, як для інших налаштувань).</summary>
    private void SaveAppVolumeRule(string? processName, double volumePercent)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;

        try
        {
            var settings = _settingsManager.LoadSettings();
            var rule = settings.AppVolumeRules.FirstOrDefault(r =>
                r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

            if (rule != null)
            {
                rule.VolumePercent = volumePercent;
            }
            else
            {
                settings.AppVolumeRules.Add(new AppVolumeRule
                {
                    ProcessName = processName,
                    VolumePercent = volumePercent
                });
            }

            _settingsManager.SaveSettings(settings);
        }
        catch
        {
            // Збереження не критичне: гучність все одно застосована до сесії
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