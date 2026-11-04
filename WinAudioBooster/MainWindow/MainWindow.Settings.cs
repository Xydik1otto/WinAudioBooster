namespace WinAudioBooster;

public partial class MainWindow
{
    private void InitializeTrayAndHotkeys()
    {
        Loaded += (s, e) =>
        {
            _hotkeyManager.Register(this, HotkeyManager.ModControl | HotkeyManager.ModAlt, 0x42);

            _hotkeyManager.HotkeyPressed += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    EnableBoosterCheckBox.IsChecked = !(EnableBoosterCheckBox.IsChecked == true);
                    RestartAudioEngineIfEnabled();
                    SaveCurrentSettings();
                });
            };
        };

        _trayService = new TrayService(
            this, 
            () => { 
                EnableBoosterCheckBox.IsChecked = !(EnableBoosterCheckBox.IsChecked == true);
                RestartAudioEngineIfEnabled();
                SaveCurrentSettings();
            }, 
            () => { 
                _trayService?.Dispose();
                Environment.Exit(0); 
            }
        );
    }

    private void LoadSavedSettingsAndPresets()
    {
        var settings = _settingsManager.LoadSettings();

        SelectCurrentLanguageInComboBox(string.IsNullOrEmpty(settings.Language) ? null : settings.Language);
        RefreshPresetsList(settings.SelectedPresetName);

        GainSlider.Value = settings.GainDb > 0 ? settings.GainDb : 100;
        CompThresholdSlider.Value = settings.CompressorThresholdDb;
        CompRatioSlider.Value = settings.CompressorRatio;
        LimiterCeilingSlider.Value = settings.LimiterCeilingDb;

        // Еквалайзер: слайдери, дБ-підписи та DSP
        ApplyEqToDspAndSettings(settings);

        if (OutputDeviceComboBox.ItemsSource is List<AudioDeviceInfo> physicalDevices && physicalDevices.Count > 0)
        {
            var savedDevice = physicalDevices.FirstOrDefault(d => 
                (!string.IsNullOrEmpty(settings.SelectedOutputDeviceId) && d.Id == settings.SelectedOutputDeviceId) ||
                (!string.IsNullOrEmpty(settings.SelectedOutputDeviceName) && d.Name == settings.SelectedOutputDeviceName));

            OutputDeviceComboBox.SelectedItem = savedDevice ?? physicalDevices[0];
        }

        if (!string.IsNullOrEmpty(settings.SelectedTheme))
        {
            SelectCurrentThemeInComboBox(settings.SelectedTheme);
            ThemeManager.ApplyTheme(settings.SelectedTheme);
        }
        else
        {
            ThemeManager.ApplyTheme("Razer Green");
        }

        if (settings.HotkeyModifiers != 0 && settings.HotkeyVKey != 0)
        {
            HotkeyDisplayText.Text = string.IsNullOrEmpty(settings.HotkeyDisplayName) ? "Ctrl + Alt + B" : settings.HotkeyDisplayName;
            Loaded += (s, e) => _hotkeyManager.Register(this, settings.HotkeyModifiers, settings.HotkeyVKey);
        }
        else
        {
            Loaded += (s, e) => _hotkeyManager.Register(this, HotkeyManager.ModControl | HotkeyManager.ModAlt, 0x42);
        }

        EnableBoosterCheckBox.IsChecked = settings.IsBoosterEnabled;
        UpdateAllUiTexts();
    }

    private void SaveCurrentSettings()
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        var settings = new AppSettings
        {
            IsBoosterEnabled = EnableBoosterCheckBox.IsChecked == true,
            Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "uk-UA",
            SelectedOutputDeviceId = selectedOutputInfo?.Id ?? string.Empty,
            SelectedOutputDeviceName = selectedOutputInfo?.Name ?? string.Empty,
            SelectedPresetName = (PresetsComboBox.SelectedItem as Preset)?.Name ?? "Light Boost",
            GainDb = GainSlider.Value,
            CompressorThresholdDb = CompThresholdSlider.Value,
            CompressorRatio = CompRatioSlider.Value,
            SelectedTheme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Razer Green",
            LimiterCeilingDb = LimiterCeilingSlider.Value,
            IsEqEnabled = EqEnabledCheckBox.IsChecked == true,
            EqGainsDb = GetEqGainsFromSliders()
        };

        _settingsManager.SaveSettings(settings);
    }

    private void SaveCurrentSettingsWithHotkey(uint modifiers, uint vKey, string displayName)
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        var settings = new AppSettings
        {
            IsBoosterEnabled = EnableBoosterCheckBox.IsChecked == true,
            Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "uk-UA",
            SelectedOutputDeviceId = selectedOutputInfo?.Id ?? string.Empty,
            SelectedOutputDeviceName = selectedOutputInfo?.Name ?? string.Empty,
            SelectedPresetName = (PresetsComboBox.SelectedItem as Preset)?.Name ?? "Light Boost",
            GainDb = GainSlider.Value,
            CompressorThresholdDb = CompThresholdSlider.Value,
            CompressorRatio = CompRatioSlider.Value,
            LimiterCeilingDb = LimiterCeilingSlider.Value,
            IsEqEnabled = EqEnabledCheckBox.IsChecked == true,
            EqGainsDb = GetEqGainsFromSliders(),
            HotkeyModifiers = modifiers,
            HotkeyVKey = vKey,
            HotkeyDisplayName = displayName
        };

        _settingsManager.SaveSettings(settings);
    }

    private void AutoStartCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        bool isChecked = AutoStartCheckBox.IsChecked == true;
        _autoStartService.SetAutoStart(isChecked);
    }

    private void ChangeHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isListeningForHotkey = true;
        ChangeHotkeyButton.Content = "Натисніть комбінацію...";
        ChangeHotkeyButton.Foreground = (SolidColorBrush)FindResource("RazerGreen");
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_isListeningForHotkey)
        {
            e.Handled = true;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            uint modifiers = 0;
            var kbModifiers = Keyboard.Modifiers;
            if (kbModifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyManager.ModControl;
            if (kbModifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyManager.ModAlt;
            if (kbModifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyManager.ModShift;

            int vKey = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

            bool success = _hotkeyManager.Register(this, modifiers, (uint)vKey);
            if (!success)
            {
                MessageBox.Show("Цю комбінацію вже зайнято іншою програмою або системою.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                _isListeningForHotkey = false;
                ChangeHotkeyButton.Content = "Змінити";
                ChangeHotkeyButton.ClearValue(ForegroundProperty);
                return;
            }

            string displayStr = "";
            if (kbModifiers.HasFlag(ModifierKeys.Control)) displayStr += "Ctrl + ";
            if (kbModifiers.HasFlag(ModifierKeys.Alt)) displayStr += "Alt + ";
            if (kbModifiers.HasFlag(ModifierKeys.Shift)) displayStr += "Shift + ";
            displayStr += e.Key.ToString();

            HotkeyDisplayText.Text = displayStr;
            ChangeHotkeyButton.Content = "Змінити";
            ChangeHotkeyButton.ClearValue(ForegroundProperty);
            _isListeningForHotkey = false;

            SaveCurrentSettingsWithHotkey(modifiers, (uint)vKey, displayStr);
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void SelectCurrentThemeInComboBox(string themeName)
    {
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (item.Content?.ToString() == themeName)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content?.ToString() is string themeName)
        {
            ThemeManager.ApplyTheme(themeName);
            SaveCurrentSettings();
        }
    }

    private void SelectCurrentLanguageInComboBox(string? savedLanguageCode = null)
    {
        string targetTag = savedLanguageCode ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uk" => "uk-UA",
            _ => "en-US"
        };

        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag is string tag && tag == targetTag)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void LanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string cultureCode)
        {
            App.SetCulture(new CultureInfo(cultureCode));
            UpdateBoosterStateUI();
            UpdateInstallButtonState();
            SaveCurrentSettings();
        }
    }

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Перевірка оновлень...";

        var (hasUpdate, version, url) = await _updateService.CheckForUpdatesAsync();

        if (hasUpdate)
        {
            var result = MessageBox.Show(
                $"Знайдено нову версію програми ({version})!\n\nБажаєте відкрити сторінку завантаження у браузері?",
                "Оновлення WinAudioBooster",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { }
            }
        }
        else
        {
            MessageBox.Show("У вас встановлена найновіша версія програми!", "Оновлення", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        CheckUpdatesButton.IsEnabled = true;
        CheckUpdatesButton.Content = "Перевірити оновлення";
    }

    private void UpdateAllUiTexts()
    {
        GainValueText.Text = $"{GainSlider.Value:F0}%";
        CompThresholdText.Text = $"{CompThresholdSlider.Value:F0} dB";
        CompRatioText.Text = $"{CompRatioSlider.Value:F1}:1";
        LimiterCeilingText.Text = $"{LimiterCeilingSlider.Value:F1} dB";
        UpdateEqTexts();
    }
}