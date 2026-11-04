namespace WinAudioBooster;

public partial class MainWindow
{
    private void InitializeTrayAndHotkeys()
    {
        // Авто-тема: стежимо за зміною світлої/темної теми Windows
        ThemeManager.InitializeSystemThemeWatcher();

        Loaded += (s, e) =>
        {
            _hotkeyManager.Register(this, HotkeyManager.ModControl | HotkeyManager.ModAlt, 0x42);

            // Фіксовані хоткеї: Ctrl+Alt+1..5 → пресети 1..5, Ctrl+Alt+D → bypass DSP
            RegisterFixedHotkeys();

            _hotkeyManager.HotkeyPressedById += (sender, id) =>
            {
                Dispatcher.Invoke(() => OnGlobalHotkey(id));
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

    // Реєструє фіксовані хоткеї: Ctrl+Alt+1..5 (пресети 1..5) та Ctrl+Alt+D (bypass DSP)
    private void RegisterFixedHotkeys()
    {
        uint mods = HotkeyManager.ModControl | HotkeyManager.ModAlt;

        // VK-коди '1'..'5' = 0x31..0x35
        for (int i = 0; i < 5; i++)
        {
            _hotkeyManager.Register(this, mods, (uint)(0x31 + i), HotkeyManager.PresetHotkeyBaseId + i);
        }

        // VK-код 'D' = 0x44
        _hotkeyManager.Register(this, mods, 0x44, HotkeyManager.BypassHotkeyId);
    }

    // Той самий механізм, що й у Ctrl+Alt+B: toggle чекбокса бустера + перезапуск движка
    private void ToggleBoosterViaHotkey()
    {
        EnableBoosterCheckBox.IsChecked = !(EnableBoosterCheckBox.IsChecked == true);
        RestartAudioEngineIfEnabled();
        SaveCurrentSettings();

        // OSD-фідбек: бустер вимкнено хоткеєм → bypass активний (і навпаки)
        ShowBypassOsd(EnableBoosterCheckBox.IsChecked == true);
    }

    // Маршрутизація глобальних хоткеїв за ідентифікатором
    private void OnGlobalHotkey(int hotkeyId)
    {
        if (hotkeyId == HotkeyManager.MainHotkeyId || hotkeyId == HotkeyManager.BypassHotkeyId)
        {
            ToggleBoosterViaHotkey();
            return;
        }

        if (hotkeyId >= HotkeyManager.PresetHotkeyBaseId && hotkeyId < HotkeyManager.PresetHotkeyBaseId + 5)
        {
            ApplyPresetByHotkeyIndex(hotkeyId - HotkeyManager.PresetHotkeyBaseId);
        }
    }

    // Застосувати N-й пресет зі списку _presetManager.LoadAllPresets() (порядок як у ComboBox)
    private void ApplyPresetByHotkeyIndex(int index)
    {
        var presets = _presetManager.LoadAllPresets();
        if (index < 0 || index >= presets.Count) return;

        ApplyPresetByName(presets[index].Name);

        // OSD-фідбек хоткея пресета
        ShowPresetOsd(presets[index].Name);
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

        // Швидкі DSP-тоггли: Bass Boost / Treble Boost / Cut sub-bass
        ApplyBoostTogglesToDsp(settings);

        // Просторові DSP-тоггли: Loudness / Stereo width / Crossfeed / Mono + Balance
        ApplySpatialTogglesToDsp(settings);

        // Журнал подій: чекбокс у налаштуваннях керує статичним LoggingService
        LoggingService.IsEnabled = settings.IsLoggingEnabled;
        LoggingEnabledCheckBox.IsChecked = settings.IsLoggingEnabled;

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
        HearingProtectionCheckBox.IsChecked = settings.IsHearingProtectionEnabled;
        SleepTimerComboBox.SelectedIndex = 0; // за замовчуванням "Вимк" (обробник пропускається під час ініціалізації)

        // Нічний режим за розкладом: UI + хвилинний таймер + перша перевірка
        InitializeQuietHours(settings);

        UpdateAllUiTexts();
    }

    private void SaveCurrentSettings()
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        // Списки, які керуються окремими редакторами (профілі пристроїв, правила гучності),
        // переносимо з поточного файлу налаштувань, щоб не втратити їх при збереженні
        var existing = _settingsManager.LoadSettings();

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
            SelectedTheme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string
                ?? (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Razer Green",
            LimiterCeilingDb = LimiterCeilingSlider.Value,
            IsEqEnabled = EqEnabledCheckBox.IsChecked == true,
            EqGainsDb = GetEqGainsFromSliders(),
            IsBassBoostEnabled = BassBoostCheckBox.IsChecked == true,
            IsTrebleBoostEnabled = TrebleBoostCheckBox.IsChecked == true,
            IsSubBassFilterEnabled = SubBassFilterCheckBox.IsChecked == true,
            IsLoudnessNormalizationEnabled = LoudnessCheckBox.IsChecked == true,
            IsStereoWidenEnabled = StereoWidthCheckBox.IsChecked == true,
            IsCrossfeedEnabled = CrossfeedCheckBox.IsChecked == true,
            IsMonoEnabled = MonoCheckBox.IsChecked == true,
            Balance = BalanceSlider.Value,
            IsLoggingEnabled = LoggingEnabledCheckBox.IsChecked == true,
            DevicePresetBindings = existing.DevicePresetBindings,
            AppVolumeRules = existing.AppVolumeRules,
            IsHearingProtectionEnabled = HearingProtectionCheckBox.IsChecked == true,

            // Нічний режим за розкладом
            QuietHoursEnabled = QuietHoursCheckBox.IsChecked == true,
            QuietHoursStart = Math.Max(0, QuietHoursStartComboBox.SelectedIndex),
            QuietHoursEnd = Math.Max(0, QuietHoursEndComboBox.SelectedIndex)
        };

        _settingsManager.SaveSettings(settings);
    }

    private void SaveCurrentSettingsWithHotkey(uint modifiers, uint vKey, string displayName)
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        // Списки, які керуються окремими редакторами, переносимо з поточного файлу налаштувань
        var existing = _settingsManager.LoadSettings();

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
            IsBassBoostEnabled = BassBoostCheckBox.IsChecked == true,
            IsTrebleBoostEnabled = TrebleBoostCheckBox.IsChecked == true,
            IsSubBassFilterEnabled = SubBassFilterCheckBox.IsChecked == true,
            IsLoudnessNormalizationEnabled = LoudnessCheckBox.IsChecked == true,
            IsStereoWidenEnabled = StereoWidthCheckBox.IsChecked == true,
            IsCrossfeedEnabled = CrossfeedCheckBox.IsChecked == true,
            IsMonoEnabled = MonoCheckBox.IsChecked == true,
            Balance = BalanceSlider.Value,
            IsLoggingEnabled = LoggingEnabledCheckBox.IsChecked == true,
            DevicePresetBindings = existing.DevicePresetBindings,
            AppVolumeRules = existing.AppVolumeRules,
            IsHearingProtectionEnabled = HearingProtectionCheckBox.IsChecked == true,

            // Нічний режим за розкладом
            QuietHoursEnabled = QuietHoursCheckBox.IsChecked == true,
            QuietHoursStart = Math.Max(0, QuietHoursStartComboBox.SelectedIndex),
            QuietHoursEnd = Math.Max(0, QuietHoursEndComboBox.SelectedIndex),

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

    // Увімкнення/вимкнення журналу подій (чекбокс у налаштуваннях)
    private void LoggingEnabledCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        LoggingService.IsEnabled = LoggingEnabledCheckBox.IsChecked == true;
        SaveCurrentSettings();
    }

    // «Відкрити лог»: показує %APPDATA%\WinAudioBooster\log.txt у системному текстовому редакторі
    private void OpenLogButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Якщо логу ще немає (щойно ввімкнули) — створюємо порожній файл
            if (!File.Exists(LoggingService.LogFilePath))
            {
                Directory.CreateDirectory(LoggingService.LogDirectory);
                File.WriteAllText(LoggingService.LogFilePath, string.Empty);
            }

            Process.Start(new ProcessStartInfo(LoggingService.LogFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggingService.WriteException("ERROR", ex);
            MessageBox.Show(
                $"Не вдалося відкрити файл журналу:\n{LoggingService.LogFilePath}\n\n{ex.Message}",
                "Журнал", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            // Порівнюємо спочатку Tag (стабільне значення для збереження), потім Content
            string itemValue = item.Tag as string ?? item.Content?.ToString() ?? string.Empty;
            if (itemValue == themeName)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            // Tag — стабільний ідентифікатор теми (незалежний від мови інтерфейсу)
            string themeName = selectedItem.Tag as string ?? selectedItem.Content?.ToString() ?? string.Empty;

            if (themeName == ThemeManager.AutoThemeName)
            {
                // Авто-режим: база — Windows 11 Fluent, світла тема Windows перекривається overlay
                ThemeManager.SetAutoMode("Windows 11");
            }
            else
            {
                ThemeManager.ApplyTheme(themeName);
            }

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
                $"Знайдено нову версію програми ({version})!\n\n«Так» — завантажити й встановити автоматично,\n«Ні» — відкрити сторінку завантаження у браузері.",
                "Оновлення WinAudioBooster",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                bool installed = await InstallPendingUpdateAsync();
                if (installed) return; // бат-скрипт перезапустить додаток після закриття процесу
            }
            else
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

    /// <summary>
    /// Завантажує й встановлює оновлення через UpdateService (з SHA256-перевіркою),
    /// після чого коректно завершує додаток — бат-скрипт замінить exe і перезапустить його.
    /// </summary>
    private async Task<bool> InstallPendingUpdateAsync()
    {
        var check = _updateService.LastCheckResult;
        if (check == null || string.IsNullOrEmpty(check.AssetDownloadUrl))
        {
            MessageBox.Show("Не вдалося знайти файл оновлення у релізі.", "Оновлення", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        CheckUpdatesButton.Content = "Завантаження оновлення...";

        // Прогрес завантаження у Title вікна (патерн VbCableInstaller) + у кнопці
        var progress = new Progress<string>(status =>
        {
            CheckUpdatesButton.Content = status;
            Title = $"WinAudioBooster – {status}";
        });

        bool installed = await _updateService.DownloadAndInstallUpdateAsync(
            check.AssetDownloadUrl, check.AssetSha256, progress);

        if (installed)
        {
            // Бат-скрипт чекає закриття процесу (до 30 с), потім замінює exe і перезапускає додаток
            SaveCurrentSettings();
            Application.Current.Shutdown();
            return true;
        }

        // Встановлення не відбулося — повертаємо заголовок вікна
        Title = (string)FindResource("MainWindow_Title");
        MessageBox.Show("Не вдалося встановити оновлення автоматично.\nСпробуйте завантажити його вручну зі сторінки релізів GitHub.",
            "Оновлення", MessageBoxButton.OK, MessageBoxImage.Warning);
        CheckUpdatesButton.IsEnabled = true;
        CheckUpdatesButton.Content = "Перевірити оновлення";
        return false;
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