namespace WinAudioBooster;

public partial class MainWindow
{
    // Захист слуху: попередження показується лише один раз за запуск додатку
    private bool _hearingProtectionShown = false;

    // Поріг підсилення (%), вище якого показуємо попередження про ризик для слуху
    private const double HearingProtectionLimitPercent = 150.0;

    private async void InitializeAudio()
    {
        // 1. Моніторинг підключення/відключення навушників та динаміків
        _deviceMonitor.DevicesOrStateChanged += () =>
        {
            Dispatcher.Invoke(() =>
            {
                LoadPhysicalOutputDevices();
                RestartAudioEngineIfEnabled();
            });
        };

        _audioEngine.ErrorOccurred += (s, err) =>
        {
            // Журнал: помилки аудіодвижка фіксуються у %APPDATA%\WinAudioBooster\log.txt
            LoggingService.WriteLog("ERROR", $"AudioEngine: {err}");

            Dispatcher.Invoke(() => MessageBox.Show(err, "WinAudioBooster Error", MessageBoxButton.OK, MessageBoxImage.Error));
        };

        // 2. Відстежуємо рух системного повзунка гучності Windows
        _volumeManager.VolumeChangedExternally += (s, winPercent) =>
        {
            Dispatcher.Invoke(() =>
            {
                bool suppressOsd = _isInitializing || _isApplyingPreset;

                if (GainSlider.Value <= 100.0)
                {
                    _isApplyingPreset = true;
                    GainSlider.Value = winPercent;
                    GainValueText.Text = $"{winPercent:F0}%";
                    _isApplyingPreset = false;
                    _miniMixerWindow?.SyncWithMainWindow();
                }

                // OSD-фідбек зміни гучності (як у Volume²): пропускаємо службові зміни
                // під час ініціалізації/застосування пресета, щоб не спамити при запуску
                if (!suppressOsd)
                {
                    ShowVolumeOsd(winPercent);
                }
            });
        };

        // 3. Перевірка та автоматична інсталяція VB-Cable, якщо він відсутній у системі
        try
        {
            await CheckAndEnsureCableInstalledAsync();
        }
        catch (Exception ex)
        {
            // Критична помилка старту (інсталяція кабелю) — у журнал
            LoggingService.WriteException("ERROR", ex);
        }

        // 4. Запуск аудіодвижка та авто-переключення виводу Windows
        try
        {
            await AutoSetupAudioPipelineAsync();
        }
        catch (Exception ex)
        {
            // Критична помилка старту аудіопайплайна — у журнал
            LoggingService.WriteException("ERROR", ex);
        }

        // 5. Фонова перевірка оновлень (раз на 12 годин; при новій версії — балун у треї)
        _ = RunPeriodicUpdateCheckAsync();

        UpdateInstallButtonState();
    }

    /// <summary>Разова перевірка оновлень при старті, потім кожні 12 годин.</summary>
    private async Task RunPeriodicUpdateCheckAsync()
    {
        // Даємо додатку спокійно стартувати — перша перевірка через 5 хвилин
        await Task.Delay(TimeSpan.FromMinutes(5));

        while (true)
        {
            try
            {
                var (hasUpdate, version, url) = await _updateService.CheckForUpdatesAsync();
                if (hasUpdate && !string.IsNullOrEmpty(version))
                {
                    Dispatcher.Invoke(() => _trayService?.ShowUpdateAvailable(version, url));
                }
            }
            catch (Exception ex)
            {
                // Мережа недоступна або GitHub API не відповів — у журнал і спроба наступного разу
                LoggingService.WriteLog("WARN", $"Перевірка оновлень не вдалася: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(12));
        }
    }

    private async Task CheckAndEnsureCableInstalledAsync()
    {
        if (!_deviceManager.IsCableInstalled())
        {
            var result = MessageBox.Show(
                "Віртуальний аудіокабель (VB-Audio CABLE) не знайдено в системі.\n\nБажаєте встановити його автоматично зараз?",
                "WinAudioBooster – Перший запуск",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                InstallCableButton.IsEnabled = false;

                bool success = await _cableInstaller.DownloadAndInstallAsync(status =>
                {
                    Dispatcher.Invoke(() => Title = $"WinAudioBooster – {status}");
                });

                Title = (string)FindResource("MainWindow_Title");

                if (success)
                {
                    MessageBox.Show("VB-Audio Cable успішно встановлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPhysicalOutputDevices();
                }
            }
        }
    }

    private async Task AutoSetupAudioPipelineAsync()
    {
        if (EnableBoosterCheckBox.IsChecked == true)
        {
            var captureCableDevice = _deviceManager.FindCableDevice();
            var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

            if (captureCableDevice != null)
            {
                // Переключаємо за точним ID кабелю
                _ = await AudioDeviceSwitcher.SetDefaultDeviceAsync(captureCableDevice.ID);

                if (selectedOutputInfo?.Device != null)
                {
                    _audioEngine.Start(captureCableDevice, selectedOutputInfo.Device);
                    await SyncDspParametersAsync();
                }
            }
        }

        UpdateBoosterStateUI();
    }

    private async void SetCableDefaultBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var cable = _deviceManager.FindCableDevice();
        if (cable == null)
        {
            MessageBox.Show("Віртуальний кабель CABLE Input не знайдено серед активних пристроїв.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(cable.ID);

        EnableBoosterCheckBox.IsChecked = true;
        RestartAudioEngineIfEnabled();

        if (ok) MessageBox.Show("Звук Windows успішно переключено на VB-Cable!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void SetSpeakersDefaultBtn_OnClick(object sender, RoutedEventArgs e)
    {
        AudioDeviceInfo? targetDevice = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo 
                                        ?? _deviceManager.GetPhysicalOutputDevices().FirstOrDefault();

        if (targetDevice == null) return;

        // Встановлюємо динаміки за замовчуванням
        bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(targetDevice.Id);

        EnableBoosterCheckBox.IsChecked = false;
        RestartAudioEngineIfEnabled();

        if (ok)
        {
            MessageBox.Show($"Звук Windows успішно повернуто на {targetDevice.Name}!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

   
    private async void RestartAudioEngineIfEnabled()
    {
        _audioEngine.Stop();

        if (EnableBoosterCheckBox.IsChecked == true)
        {
            await AutoSetupAudioPipelineAsync();
        }
        else
        {
            // Повертаємо системний звук Windows на динаміки/навушники за ID
            var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;
            if (selectedOutputInfo != null)
            {
                _ = await AudioDeviceSwitcher.SetDefaultDeviceAsync(selectedOutputInfo.Id);
            }
        }

        UpdateBoosterStateUI();
    }

    private void LoadPhysicalOutputDevices()
    {
        var physicalDevices = _deviceManager.GetPhysicalOutputDevices();
        OutputDeviceComboBox.ItemsSource = physicalDevices;
        if (physicalDevices.Count > 0 && OutputDeviceComboBox.SelectedIndex < 0)
        {
            OutputDeviceComboBox.SelectedIndex = 0;
        }
    }

    private void OutputDeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        RestartAudioEngineIfEnabled();
    }

    private void EnableBoosterCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        RestartAudioEngineIfEnabled();
        SaveCurrentSettings();
    }

    private void UpdateBoosterStateUI()
    {
        bool isEnabled = EnableBoosterCheckBox.IsChecked == true;
        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEnabled = isEnabled;

        DspSettingsPanel.IsEnabled = isEnabled;
        BoosterStateText.Text = (string)FindResource(isEnabled ? "BoosterState_On" : "BoosterState_Off");
        BoosterStateText.Foreground = new SolidColorBrush(isEnabled ? Colors.ForestGreen : Colors.Gray);
    }

    private void UpdateInstallButtonState()
    {
        bool isInstalled = _deviceManager.IsCableInstalled();
        InstallCableButton.IsEnabled = !isInstalled;
        InstallCableButton.Content = (string)FindResource(isInstalled ? "CableInstalled_Btn" : "InstallCable_Btn");
    }

    private async void InstallCableButton_OnClick(object sender, RoutedEventArgs e)
    {
        InstallCableButton.IsEnabled = false;

        bool success = await _cableInstaller.DownloadAndInstallAsync(status =>
        {
            Dispatcher.Invoke(() => Title = $"WinAudioBooster – {status}");
        });

        Title = (string)FindResource("MainWindow_Title");
        await Task.Delay(500);

        LoadPhysicalOutputDevices();
        InitializeAudio();
    }

    private void InitializeVuTimer()
    {
        // Спектр-візуалізатор малюється у тому ж кадрі _vuTimer (~30 fps)
        InitializeSpectrumVisualizer();

        _vuTimer.Interval = TimeSpan.FromMilliseconds(33);
        _vuTimer.Tick += (s, e) =>
        {
            bool boosterEnabled = EnableBoosterCheckBox.IsChecked == true;

            if (_audioEngine.Dsp != null && boosterEnabled)
            {
                InputVuBar.Value = _audioEngine.Dsp.InputMeter.CurrentLevel;
                OutputVuBar.Value = _audioEngine.Dsp.OutputMeter.CurrentLevel;
            }
            else
            {
                InputVuBar.Value = 0;
                OutputVuBar.Value = 0;
            }

            // Спектроаналізатор у тому ж кадрі (~30 fps): FFT останніх семплів
            // після лімітера → 32 лог-смуги; без даних бари плавно спадають
            UpdateSpectrumFrame(boosterEnabled);
        };
        _vuTimer.Start();
    }

    private async Task SyncDspParametersAsync()
    {
        var captureCableDevice = _deviceManager.FindCableDevice();
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        MMDevice? deviceToControl = EnableBoosterCheckBox.IsChecked == true 
            ? captureCableDevice 
            : selectedOutputInfo?.Device;

        _volumeManager.BindDevice(deviceToControl);

        float calculatedGainDb = await _volumeManager.SetVolumeAndCalculateGainDbAsync(
            GainSlider.Value, 
            deviceToControl, 
            selectedOutputInfo?.Device
        );

        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.GainDb = calculatedGainDb;
            _audioEngine.Dsp.CompressorThresholdDb = (float)CompThresholdSlider.Value;
            _audioEngine.Dsp.CompressorRatio = (float)CompRatioSlider.Value;
            _audioEngine.Dsp.LimiterCeilingDb = (float)LimiterCeilingSlider.Value;
        }
    }

    private void GainSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GainValueText != null) GainValueText.Text = $"{e.NewValue:F0}%";

        MaybeShowHearingProtectionWarning(e.NewValue);

        _sliderDebounceCts?.Cancel();
        _sliderDebounceCts = new CancellationTokenSource();
        var token = _sliderDebounceCts.Token;

        double val = e.NewValue;

        Task.Run(async () =>
        {
            await Task.Delay(15, token);
            if (token.IsCancellationRequested) return;

            var captureCableDevice = _deviceManager.FindCableDevice();
            var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

            MMDevice? deviceToControl = EnableBoosterCheckBox.IsChecked == true 
                ? captureCableDevice 
                : selectedOutputInfo?.Device;

            float calculatedGainDb = await _volumeManager.SetVolumeAndCalculateGainDbAsync(
                val, 
                deviceToControl, 
                selectedOutputInfo?.Device
            );

            if (_audioEngine?.Dsp != null)
            {
                _audioEngine.Dsp.GainDb = calculatedGainDb;
            }
        }, token);

        _miniMixerWindow?.SyncWithMainWindow();

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    /// <summary>
    /// Захист слуху: при підсиленні &gt;150% один раз на запуск показує попередження.
    /// Не спрацьовує під час ініціалізації та автоматичного застосування пресета/профіля.
    /// </summary>
    private void MaybeShowHearingProtectionWarning(double newValue)
    {
        if (_hearingProtectionShown) return;
        if (newValue <= HearingProtectionLimitPercent) return;
        if (_isInitializing || _isApplyingPreset) return;
        if (HearingProtectionCheckBox == null || HearingProtectionCheckBox.IsChecked != true) return;

        _hearingProtectionShown = true;

        string title = TryFindResource("HearingProtection_Title") as string ?? "Захист слуху";
        string message = TryFindResource("HearingProtection_Message") as string ??
            "Рівень підсилення більше 150% може пошкодити ваш слух.\n\n«Так» — продовжити,\n«Ні» — зменшити до 150%.";

        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
        {
            // Зменшуємо до 150%: повторний виклик ValueChanged не покаже попередження (150 <= поріг)
            GainSlider.Value = HearingProtectionLimitPercent;
        }
    }

    private void HearingProtectionCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
    }

    private void CompThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CompThresholdText != null) CompThresholdText.Text = $"{e.NewValue:F0} dB";
        if (_audioEngine?.Dsp != null) _audioEngine.Dsp.CompressorThresholdDb = (float)e.NewValue;

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    private void CompRatioSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CompRatioText != null) CompRatioText.Text = $"{e.NewValue:F1}:1";
        if (_audioEngine?.Dsp != null) _audioEngine.Dsp.CompressorRatio = (float)e.NewValue;

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    private void LimiterCeilingSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LimiterCeilingText != null) LimiterCeilingText.Text = $"{e.NewValue:F1} dB";
        if (_audioEngine?.Dsp != null) _audioEngine.Dsp.LimiterCeilingDb = (float)e.NewValue;

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }
}