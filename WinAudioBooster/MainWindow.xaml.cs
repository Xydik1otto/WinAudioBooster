using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
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
        private bool _isListeningForHotkey = false;

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

            LoadPhysicalOutputDevices();
            LoadSavedSettingsAndPresets();

            InitializeAudio();
            InitializeAppDetector();
            InitializeVuTimer();
            _ = RefreshActiveAppSessionsAsync();
            AutoStartCheckBox.IsChecked = _autoStartService.IsAutoStartEnabled();

            _isInitializing = false;
        }

        private void InitializeAppDetector()
        {
            _appDetector.PresetChangeRequested += (s, presetName) =>
            {
                Dispatcher.Invoke(() => RefreshPresetsList(presetName));
            };
        }

        private void AutoStartCheckBox_OnClick(object sender, RoutedEventArgs e)
        {
            bool isChecked = AutoStartCheckBox.IsChecked == true;
            _autoStartService.SetAutoStart(isChecked);
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

        private async void SetCableDefaultBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var cable = _deviceManager.FindCableDevice();
            if (cable == null)
            {
                MessageBox.Show("Віртуальний кабель CABLE Input не знайдено серед активних пристроїв.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(cable.FriendlyName);

            EnableBoosterCheckBox.IsChecked = true;
            RestartAudioEngineIfEnabled();

            if (ok) MessageBox.Show("Звук Windows успішно переключено на VB-Cable!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void SetSpeakersDefaultBtn_OnClick(object sender, RoutedEventArgs e)
        {
            AudioDeviceInfo? targetDevice = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo 
                ?? _deviceManager.GetPhysicalOutputDevices().FirstOrDefault();

            if (targetDevice == null) return;

            bool ok = await AudioDeviceSwitcher.SetDefaultDeviceAsync(targetDevice.Name);

            EnableBoosterCheckBox.IsChecked = false;
            RestartAudioEngineIfEnabled();

            if (ok) MessageBox.Show($"Звук Windows повернуто на {targetDevice.Name}!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadPhysicalOutputDevices()
        {
            var physicalDevices = _deviceManager.GetPhysicalOutputDevices();
            OutputDeviceComboBox.ItemsSource = physicalDevices;
            if (physicalDevices.Count > 0) OutputDeviceComboBox.SelectedIndex = 0;
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

            var physicalDevices = OutputDeviceComboBox.ItemsSource as System.Collections.Generic.List<AudioDeviceInfo>;
            if (physicalDevices != null && physicalDevices.Count > 0)
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

        private void ChangeHotkeyButton_OnClick(object sender, RoutedEventArgs e)
        {
            _isListeningForHotkey = true;
            ChangeHotkeyButton.Content = "Натисніть комбінацію...";
            ChangeHotkeyButton.Foreground = (SolidColorBrush)FindResource("RazerGreen");
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

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (_isListeningForHotkey)
            {
                e.Handled = true;

                if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl ||
                    e.Key == System.Windows.Input.Key.LeftAlt || e.Key == System.Windows.Input.Key.RightAlt ||
                    e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift ||
                    e.Key == System.Windows.Input.Key.LWin || e.Key == System.Windows.Input.Key.RWin)
                {
                    return;
                }

                uint modifiers = 0;
                var kbModifiers = System.Windows.Input.Keyboard.Modifiers;
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) modifiers |= HotkeyManager.ModControl;
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) modifiers |= HotkeyManager.ModAlt;
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) modifiers |= HotkeyManager.ModShift;

                int vKey = KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key);

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
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) displayStr += "Ctrl + ";
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) displayStr += "Alt + ";
                if (kbModifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) displayStr += "Shift + ";
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
                HotkeyModifiers = modifiers,
                HotkeyVKey = vKey,
                HotkeyDisplayName = displayName
            };

            _settingsManager.SaveSettings(settings);
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

            // Заповнюємо список пресетів
            var presets = _presetManager.LoadAllPresets();
            NewRulePresetCombo.ItemsSource = presets;
            NewRulePresetCombo.DisplayMemberPath = "Name";
            if (presets.Count > 0) NewRulePresetCombo.SelectedIndex = 0;

            // Заповнюємо випадаючий список запущених програм з вікнами
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

        private void InitializeAudio()
        {
            _audioEngine.ErrorOccurred += (s, err) =>
            {
                Dispatcher.Invoke(() => MessageBox.Show(err, "WinAudioBooster Error", MessageBoxButton.OK, MessageBoxImage.Error));
            };

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
                    _ = SyncDspParametersAsync();
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

        private void RefreshPresetsList(string? selectPresetName = null)
        {
            _isApplyingPreset = true;
            var presets = _presetManager.LoadAllPresets();
            PresetsComboBox.ItemsSource = presets;

            if (!string.IsNullOrEmpty(selectPresetName))
            {
                var target = presets.FirstOrDefault(p => p.Name.Equals(selectPresetName, StringComparison.OrdinalIgnoreCase));
                PresetsComboBox.SelectedItem = target ?? presets.FirstOrDefault();
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

            GainSlider.Value = selectedPreset.GainDb > 0 ? selectedPreset.GainDb : 100;
            CompThresholdSlider.Value = selectedPreset.CompressorThresholdDb;
            CompRatioSlider.Value = selectedPreset.CompressorRatio;
            LimiterCeilingSlider.Value = selectedPreset.LimiterCeilingDb;

            _ = SyncDspParametersAsync();
            UpdatePresetButtonsState();

            _isApplyingPreset = false;
            SaveCurrentSettings();
        }

        private void UpdatePresetButtonsState()
        {
            if (PresetsComboBox.SelectedItem is Preset current)
            {
                bool isCustom = !current.IsBuiltIn;
                DeletePresetButton.IsEnabled = isCustom;
                UpdatePresetButton.IsEnabled = isCustom;
                RenamePresetButton.IsEnabled = isCustom;
            }
        }

        private void SaveAsPresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            string promptTitle = TryFindResource("Preset_PromptName") as string ?? "Введіть назву пресету:";
            string presetName = Microsoft.VisualBasic.Interaction.InputBox(promptTitle, "WinAudioBooster", "My Custom Preset");

            if (string.IsNullOrWhiteSpace(presetName)) return;

            var newPreset = new Preset
            {
                Name = presetName.Trim(),
                GainDb = (float)GainSlider.Value,
                CompressorThresholdDb = (float)CompThresholdSlider.Value,
                CompressorRatio = (float)CompRatioSlider.Value,
                LimiterCeilingDb = (float)LimiterCeilingSlider.Value
            };

            _presetManager.SaveCustomPreset(newPreset);
            RefreshPresetsList(newPreset.Name);
            SaveCurrentSettings();
        }

        private void RenamePresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
            {
                string oldName = current.Name;
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву для пресету:", "WinAudioBooster", oldName);

                if (string.IsNullOrWhiteSpace(newName) || newName.Trim().Equals(oldName, StringComparison.OrdinalIgnoreCase)) return;

                _presetManager.RenameCustomPreset(oldName, newName.Trim());
                RefreshPresetsList(newName.Trim());
                SaveCurrentSettings();
            }
        }

        private void UpdatePresetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
            {
                current.GainDb = (float)GainSlider.Value;
                current.CompressorThresholdDb = (float)CompThresholdSlider.Value;
                current.CompressorRatio = (float)CompRatioSlider.Value;
                current.LimiterCeilingDb = (float)LimiterCeilingSlider.Value;

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
                LimiterCeilingDb = LimiterCeilingSlider.Value
            };

            _settingsManager.SaveSettings(settings);
        }

        private void UpdateAllUiTexts()
        {
            GainValueText.Text = $"{GainSlider.Value:F0}%";
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
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
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

        protected override void OnClosed(EventArgs e)
        {
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
}