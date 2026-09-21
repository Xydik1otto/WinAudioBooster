using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinAudioBooster.Audio;
using WinAudioBooster.Models;
using WinAudioBooster.Services;
using WinAudioBooster.Utils;

namespace WinAudioBooster
{
    public partial class MainWindow : Window
    {
        private bool _isInitializing = true;
        private bool _isApplyingPreset = false;

        private readonly AudioEngine _audioEngine = new();
        private readonly DeviceManager _deviceManager = new();
        private readonly VbCableInstaller _cableInstaller = new();
        private readonly PresetManager _presetManager = new();
        private readonly SettingsManager _settingsManager = new();
        private readonly DispatcherTimer _vuTimer = new();

        public MainWindow()
        {
            InitializeComponent();

            LoadPhysicalOutputDevices();
            LoadSavedSettingsAndPresets();

            InitializeAudio();
            InitializeVuTimer();

            _isInitializing = false;
        }

        private async void SetCableDefaultBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var cable = _deviceManager.FindCableDevice();
            if (cable == null)
            {
                MessageBox.Show("Віртуальний кабель CABLE Input не знайдено серед активних пристроїв.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cableName = cable.FriendlyName;

            bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(cableName);

            EnableBoosterCheckBox.IsChecked = true;
            RestartAudioEngineIfEnabled();

            if (ok)
            {
                MessageBox.Show("Звук Windows успішно переключено на VB-Cable!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Відкрито системні налаштування Звуку Windows. Оберіть 'CABLE Input' пристроєм за замовчуванням.", "Маршрутизація звуку", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void SetSpeakersDefaultBtn_OnClick(object sender, RoutedEventArgs e)
        {
            AudioDeviceInfo? targetDevice = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

            if (targetDevice == null)
            {
                var physicalDevices = _deviceManager.GetPhysicalOutputDevices();
                targetDevice = physicalDevices.FirstOrDefault();
            }

            if (targetDevice == null)
            {
                MessageBox.Show("Фізичні динаміки не знайдено в системі.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetName = targetDevice.Name;

            bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(targetName);

            EnableBoosterCheckBox.IsChecked = false;
            RestartAudioEngineIfEnabled();

            if (ok)
            {
                MessageBox.Show($"Звук Windows повернуто на {targetName}!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Відкрито системні налаштування Звуку Windows. Оберіть '{targetName}' пристроєм за замовчуванням.", "Маршрутизація звуку", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadPhysicalOutputDevices()
        {
            var physicalDevices = _deviceManager.GetPhysicalOutputDevices();
            OutputDeviceComboBox.ItemsSource = physicalDevices;

            if (physicalDevices.Count > 0)
            {
                OutputDeviceComboBox.SelectedIndex = 0;
            }
        }

        private void LoadSavedSettingsAndPresets()
        {
            var settings = _settingsManager.LoadSettings();

            SelectCurrentLanguageInComboBox(settings.Language);
            RefreshPresetsList(settings.SelectedPresetName);

            GainSlider.Value = settings.GainDb;
            CompThresholdSlider.Value = settings.CompressorThresholdDb;
            CompRatioSlider.Value = settings.CompressorRatio;
            LimiterCeilingSlider.Value = settings.LimiterCeilingDb;

            EnableBoosterCheckBox.IsChecked = settings.IsBoosterEnabled;

            UpdateAllUiTexts();
        }

        private void InitializeAudio()
        {
            _audioEngine.ErrorOccurred += (s, err) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(err, "WinAudioBooster Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };

            RestartAudioEngineIfEnabled();
            UpdateInstallButtonState();
        }

        private void RestartAudioEngineIfEnabled()
        {
            _audioEngine.Stop();

            if (EnableBoosterCheckBox.IsChecked == true)
            {
                var captureCableDevice = _deviceManager.FindCableDevice();
                var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

                if (captureCableDevice != null && selectedOutputInfo?.Device != null)
                {
                    _audioEngine.Start(captureCableDevice, selectedOutputInfo.Device);
                    SyncDspParameters();
                }
            }

            UpdateBoosterStateUI();
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

            if (_audioEngine.Dsp != null)
            {
                _audioEngine.Dsp.IsEnabled = isEnabled;
            }

            DspSettingsPanel.IsEnabled = isEnabled;

            if (isEnabled)
            {
                BoosterStateText.Text = (string)FindResource("BoosterState_On");
                BoosterStateText.Foreground = new SolidColorBrush(Colors.ForestGreen);
            }
            else
            {
                BoosterStateText.Text = (string)FindResource("BoosterState_Off");
                BoosterStateText.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void UpdateInstallButtonState()
        {
            bool isInstalled = _deviceManager.IsCableInstalled();

            if (isInstalled)
            {
                InstallCableButton.IsEnabled = false;
                InstallCableButton.Content = (string)FindResource("CableInstalled_Btn");
            }
            else
            {
                InstallCableButton.IsEnabled = true;
                InstallCableButton.Content = (string)FindResource("InstallCable_Btn");
            }
        }

        private async void InstallCableButton_OnClick(object sender, RoutedEventArgs e)
        {
            await InstallVbCableAsync();
        }

        private async Task InstallVbCableAsync()
        {
            InstallCableButton.IsEnabled = false;

            bool success = await _cableInstaller.DownloadAndInstallAsync(status =>
            {
                Dispatcher.Invoke(() => Title = $"WinAudioBooster – {status}");
            });

            Title = (string)FindResource("MainWindow_Title");

            await Task.Delay(1000);
            LoadPhysicalOutputDevices();
            InitializeAudio();

            if (_deviceManager.IsCableInstalled())
            {
                MessageBox.Show(
                    "Драйвер VB-Cable успішно активовано у системі!",
                    "Успіх",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (!success)
            {
                var result = MessageBox.Show(
                    "Не вдалося встановити драйвер.\n\nБажаєте відкрити офіційний сайт VB-Cable у браузері?",
                    "Встановлення VB-Cable",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    VbCableInstaller.OpenOfficialWebsite();
                }
            }
        }

        private void InitializeVuTimer()
        {
            _vuTimer.Interval = TimeSpan.FromMilliseconds(30);
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

        private void SyncDspParameters()
        {
            if (_audioEngine.Dsp == null) return;

            _audioEngine.Dsp.GainDb = (float)GainSlider.Value;
            _audioEngine.Dsp.CompressorThresholdDb = (float)CompThresholdSlider.Value;
            _audioEngine.Dsp.CompressorRatio = (float)CompRatioSlider.Value;
            _audioEngine.Dsp.LimiterCeilingDb = (float)LimiterCeilingSlider.Value;
        }

        private void RefreshPresetsList(string? selectPresetName = null)
        {
            _isApplyingPreset = true;

            var presets = _presetManager.LoadAllPresets();
            PresetsComboBox.ItemsSource = presets;

            if (!string.IsNullOrEmpty(selectPresetName))
            {
                var target = presets.FirstOrDefault(p => p.Name.Equals(selectPresetName, StringComparison.OrdinalIgnoreCase));
                if (target != null) PresetsComboBox.SelectedItem = target;
                else PresetsComboBox.SelectedIndex = 0;
            }
            else if (PresetsComboBox.SelectedIndex < 0 && presets.Count > 0)
            {
                PresetsComboBox.SelectedIndex = 0;
            }

            UpdatePresetButtonsState();
            _isApplyingPreset = false;
        }

        private void PresetsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingPreset || PresetsComboBox.SelectedItem is not Preset selectedPreset) return;

            _isApplyingPreset = true;

            GainSlider.Value = selectedPreset.GainDb;
            CompThresholdSlider.Value = selectedPreset.CompressorThresholdDb;
            CompRatioSlider.Value = selectedPreset.CompressorRatio;
            LimiterCeilingSlider.Value = selectedPreset.LimiterCeilingDb;

            SyncDspParameters();
            UpdatePresetButtonsState();

            _isApplyingPreset = false;
            SaveCurrentSettings();
        }

        private void UpdatePresetButtonsState()
        {
            if (PresetsComboBox.SelectedItem is Preset current)
            {
                DeletePresetButton.IsEnabled = !current.IsBuiltIn;
                UpdatePresetButton.IsEnabled = !current.IsBuiltIn;
            }
        }

        private void SaveAsPresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            string promptTitle = (string)FindResource("Preset_PromptName");
            string presetName = Microsoft.VisualBasic.Interaction.InputBox(promptTitle, "WinAudioBooster", "My Custom Preset");

            if (string.IsNullOrWhiteSpace(presetName)) return;

            var newPreset = new Preset
            {
                Name = presetName.Trim(),
                GainDb = GainSlider.Value,
                CompressorThresholdDb = CompThresholdSlider.Value,
                CompressorRatio = CompRatioSlider.Value,
                LimiterCeilingDb = LimiterCeilingSlider.Value
            };

            _presetManager.SaveCustomPreset(newPreset);
            RefreshPresetsList(newPreset.Name);
            SaveCurrentSettings();
        }

        private void UpdatePresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
            {
                current.GainDb = GainSlider.Value;
                current.CompressorThresholdDb = CompThresholdSlider.Value;
                current.CompressorRatio = CompRatioSlider.Value;
                current.LimiterCeilingDb = LimiterCeilingSlider.Value;

                _presetManager.SaveCustomPreset(current);
                SaveCurrentSettings();
            }
        }

        private void DeletePresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
            {
                _presetManager.DeleteCustomPreset(current.Name);
                RefreshPresetsList();
                SaveCurrentSettings();
            }
        }

        private void GainSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GainValueText != null) GainValueText.Text = $"+{e.NewValue:F1} dB";
            if (_audioEngine?.Dsp != null) _audioEngine.Dsp.GainDb = (float)e.NewValue;

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

        private void SaveCurrentSettings()
        {
            var settings = new AppSettings
            {
                IsBoosterEnabled = EnableBoosterCheckBox.IsChecked == true,
                Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "uk-UA",
                SelectedPresetName = (PresetsComboBox.SelectedItem as Preset)?.Name ?? "Light Boost",
                GainDb = GainSlider.Value,
                CompressorThresholdDb = CompThresholdSlider.Value,
                CompressorRatio = CompRatioSlider.Value,
                LimiterCeilingDb = LimiterCeilingSlider.Value
            };

            _settingsManager.SaveSettings(settings);
        }

        private void UpdateAllUiTexts()
        {
            GainValueText.Text = $"+{GainSlider.Value:F1} dB";
            CompThresholdText.Text = $"{CompThresholdSlider.Value:F0} dB";
            CompRatioText.Text = $"{CompRatioSlider.Value:F1}:1";
            LimiterCeilingText.Text = $"{LimiterCeilingSlider.Value:F1} dB";
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

        protected override void OnClosed(EventArgs e)
        {
            SaveCurrentSettings();
            _vuTimer.Stop();
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
    }
}