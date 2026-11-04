namespace WinAudioBooster;

public partial class MainWindow
{
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
            Dispatcher.Invoke(() => MessageBox.Show(err, "WinAudioBooster Error", MessageBoxButton.OK, MessageBoxImage.Error));
        };

        // 2. Відстежуємо рух системного повзунка гучності Windows
        _volumeManager.VolumeChangedExternally += (s, winPercent) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (GainSlider.Value <= 100.0)
                {
                    _isApplyingPreset = true;
                    GainSlider.Value = winPercent;
                    GainValueText.Text = $"{winPercent:F0}%";
                    _isApplyingPreset = false;
                    _miniMixerWindow?.SyncWithMainWindow();
                }
            });
        };

        // 3. Перевірка та автоматична інсталяція VB-Cable, якщо він відсутній у системі
        await CheckAndEnsureCableInstalledAsync();

        // 4. Запуск аудіодвижка та авто-переключення виводу Windows
        await AutoSetupAudioPipelineAsync();

        UpdateInstallButtonState();
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
        _vuTimer.Interval = TimeSpan.FromMilliseconds(33);
        _vuTimer.Tick += (s, e) =>
        {
            if (_audioEngine.Dsp != null && EnableBoosterCheckBox.IsChecked == true)
            {
                InputVuBar.Value = _audioEngine.Dsp.InputMeter.CurrentLevel;
                OutputVuBar.Value = _audioEngine.Dsp.OutputMeter.CurrentLevel;
            }
            else
            {
                InputVuBar.Value = 0;
                OutputVuBar.Value = 0;
            }
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