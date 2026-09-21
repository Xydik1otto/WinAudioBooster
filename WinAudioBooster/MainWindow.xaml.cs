using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinAudioBooster.Audio;
using WinAudioBooster.Services;

namespace WinAudioBooster
{
    public partial class MainWindow : Window
    {
        private bool _isInitializing = true;
        private readonly AudioEngine _audioEngine = new();
        private readonly DeviceManager _deviceManager = new();
        private readonly VbCableInstaller _cableInstaller = new();
        private readonly DispatcherTimer _vuTimer = new();

        public MainWindow()
        {
            InitializeComponent();
            SelectCurrentLanguageInComboBox();
            UpdateAllUiTexts();

            InitializeAudio();
            InitializeVuTimer();

            _isInitializing = false;
        }

        private async void InitializeAudio()
        {
            _audioEngine.ErrorOccurred += (s, err) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(err, "WinAudioBooster Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };

            var defaultCapture = _deviceManager.GetDefaultRenderDevice();
            var cableOutput = _deviceManager.FindCableDevice();

            // Якщо VB-Cable відсутній — пропонуємо завантажити й встановити його
            if (cableOutput == null)
            {
                var result = MessageBox.Show(
                    "Віртуальний аудіопристрій (VB-Cable) не знайдено.\n\n" +
                    "Бажаєте автоматично завантажити та встановити VB-Cable зараз?",
                    "Встановлення VB-Cable",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await InstallVbCableAsync();
                    cableOutput = _deviceManager.FindCableDevice();
                }
            }

            _audioEngine.Start(defaultCapture, cableOutput);
            SyncDspParameters();
        }

        private async System.Threading.Tasks.Task InstallVbCableAsync()
        {
            InstallCableButton.IsEnabled = false;

            bool success = await _cableInstaller.DownloadAndInstallAsync(status =>
            {
                Dispatcher.Invoke(() => Title = $"WinAudioBooster – {status}");
            });

            Title = (string)FindResource("MainWindow_Title");
            InstallCableButton.IsEnabled = true;

            if (success)
            {
                MessageBox.Show(
                    "Інсталяцію VB-Cable завершено!\nЯкщо пристрій не з'явився, перезапустіть програму.",
                    "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void InitializeVuTimer()
        {
            _vuTimer.Interval = TimeSpan.FromMilliseconds(30); // ~30 FPS
            _vuTimer.Tick += (s, e) =>
            {
                if (_audioEngine.Dsp != null)
                {
                    InputVuBar.Value = _audioEngine.Dsp.InputMeter.CurrentLevel;
                    OutputVuBar.Value = _audioEngine.Dsp.OutputMeter.CurrentLevel;
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

        private void GainSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GainValueText != null) GainValueText.Text = $"+{e.NewValue:F1} dB";
            if (_audioEngine?.Dsp != null) _audioEngine.Dsp.GainDb = (float)e.NewValue;
        }

        private void CompThresholdSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CompThresholdText != null) CompThresholdText.Text = $"{e.NewValue:F0} dB";
            if (_audioEngine?.Dsp != null) _audioEngine.Dsp.CompressorThresholdDb = (float)e.NewValue;
        }

        private void CompRatioSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CompRatioText != null) CompRatioText.Text = $"{e.NewValue:F1}:1";
            if (_audioEngine?.Dsp != null) _audioEngine.Dsp.CompressorRatio = (float)e.NewValue;
        }

        private void LimiterCeilingSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LimiterCeilingText != null) LimiterCeilingText.Text = $"{e.NewValue:F1} dB";
            if (_audioEngine?.Dsp != null) _audioEngine.Dsp.LimiterCeilingDb = (float)e.NewValue;
        }

        private async void InstallCableButton_OnClick(object sender, RoutedEventArgs e)
        {
            await InstallVbCableAsync();
        }

        private void UpdateAllUiTexts()
        {
            GainValueText.Text = $"+{GainSlider.Value:F1} dB";
            CompThresholdText.Text = $"{CompThresholdSlider.Value:F0} dB";
            CompRatioText.Text = $"{CompRatioSlider.Value:F1}:1";
            LimiterCeilingText.Text = $"{LimiterCeilingSlider.Value:F1} dB";
        }

        private void SelectCurrentLanguageInComboBox()
        {
            var currentCulture = CultureInfo.CurrentUICulture;
            string targetTag = currentCulture.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase) ? "uk-UA" : "en-US";

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
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _vuTimer.Stop();
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
    }
}