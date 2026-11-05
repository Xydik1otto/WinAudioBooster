namespace WinAudioBooster;

public partial class MainWindow
{
    // Кількість смуг еквалайзера (31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Гц)
    private const int EqBandCount = 10;

    // Частоти смуг EQ (Гц) — цілі інтерполяції AutoEq-кривих
    private static readonly float[] EqBandFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    // Повертає слайдер відповідної смуги еквалайзера
    private Slider? EqSliderAt(int band) => band switch
    {
        0 => EqSlider0,
        1 => EqSlider1,
        2 => EqSlider2,
        3 => EqSlider3,
        4 => EqSlider4,
        5 => EqSlider5,
        6 => EqSlider6,
        7 => EqSlider7,
        8 => EqSlider8,
        9 => EqSlider9,
        _ => null
    };

    // Повертає підпис значення дБ відповідної смуги
    private TextBlock? EqDbTextAt(int band) => band switch
    {
        0 => EqDbText0,
        1 => EqDbText1,
        2 => EqDbText2,
        3 => EqDbText3,
        4 => EqDbText4,
        5 => EqDbText5,
        6 => EqDbText6,
        7 => EqDbText7,
        8 => EqDbText8,
        9 => EqDbText9,
        _ => null
    };

    // Оновлює текст дБ під слайдером смуги
    private void UpdateEqBandText(int band, double value)
    {
        if (EqDbTextAt(band) is { } text) text.Text = $"{value:F1} dB";
    }

    // Рух повзунка смуги: оновлюємо дБ-підпис, DSP та зберігаємо налаштування
    private void EqSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider { Tag: string tag } || !int.TryParse(tag, out int band)) return;

        UpdateEqBandText(band, e.NewValue);

        if (_audioEngine.Dsp != null) _audioEngine.Dsp.SetEqBandGain(band, (float)e.NewValue);

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Увімкнення/вимкнення еквалайзера (checkbox прив'язаний до ViewModel)
    private void EqToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        bool isEnabled = _vm.IsEqEnabled;
        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEqEnabled = isEnabled;

        _miniMixerWindow?.SyncWithMainWindow();

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Один обробник для трьох швидких DSP-тогглів (Tag = Bass | Treble | SubBass); стан — у ViewModel
    private void DspBoostToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        if (_audioEngine.Dsp != null)
        {
            switch (checkBox.Tag as string)
            {
                case "Bass":
                    _audioEngine.Dsp.IsBassBoostEnabled = _vm.IsBassBoostEnabled;
                    break;
                case "Treble":
                    _audioEngine.Dsp.IsTrebleBoostEnabled = _vm.IsTrebleBoostEnabled;
                    break;
                case "SubBass":
                    _audioEngine.Dsp.IsSubBassFilterEnabled = _vm.IsSubBassFilterEnabled;
                    break;
            }
        }

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Другий рядок швидких DSP-тогглів: Loudness / Stereo width / Crossfeed / Mono (за Tag); стан — у ViewModel
    private void DspSpatialToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        if (_audioEngine.Dsp != null)
        {
            switch (checkBox.Tag as string)
            {
                case "Loudness":
                    _audioEngine.Dsp.IsLoudnessNormalizationEnabled = _vm.IsLoudnessNormalizationEnabled;
                    break;
                case "StereoWidth":
                    _audioEngine.Dsp.IsStereoWidenEnabled = _vm.IsStereoWidenEnabled;
                    break;
                case "Crossfeed":
                    _audioEngine.Dsp.IsCrossfeedEnabled = _vm.IsCrossfeedEnabled;
                    break;
                case "Mono":
                    _audioEngine.Dsp.IsMonoEnabled = _vm.IsMonoEnabled;
                    break;
            }
        }

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Слайдер Balance (-1..1): від'ємні — вліво (L), додатні — вправо (R), 0 — центр
    private void BalanceSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BalanceValueText != null) BalanceValueText.Text = FormatBalance(e.NewValue);
        if (_audioEngine?.Dsp != null) _audioEngine.Dsp.Balance = (float)e.NewValue;

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Текстове значення балансу: «0» по центру, інакше «L 40%» / «R 40%»
    private static string FormatBalance(double value)
    {
        return Math.Abs(value) < 0.05
            ? "0"
            : $"{(value < 0 ? "L" : "R")} {Math.Abs(value) * 100:F0}%";
    }

    // Застосовує збережені DSP-тоггли до DSP (чекбокси вже оновлені бідингом із ViewModel)
    private void ApplyBoostTogglesToDsp(AppSettings settings)
    {
        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.IsBassBoostEnabled = settings.IsBassBoostEnabled;
            _audioEngine.Dsp.IsTrebleBoostEnabled = settings.IsTrebleBoostEnabled;
            _audioEngine.Dsp.IsSubBassFilterEnabled = settings.IsSubBassFilterEnabled;
        }
    }

    // Застосовує просторові DSP-тоггли та Balance до DSP (контроли вже оновлені бідингом)
    private void ApplySpatialTogglesToDsp(AppSettings settings)
    {
        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.IsLoudnessNormalizationEnabled = settings.IsLoudnessNormalizationEnabled;
            _audioEngine.Dsp.IsStereoWidenEnabled = settings.IsStereoWidenEnabled;
            _audioEngine.Dsp.IsCrossfeedEnabled = settings.IsCrossfeedEnabled;
            _audioEngine.Dsp.IsMonoEnabled = settings.IsMonoEnabled;
            _audioEngine.Dsp.Balance = (float)settings.Balance;
        }
    }

    // Кнопка «Імпорт AutoEq»: читає CSV-криву, інтерполює на 10 смуг і застосовує
    private void ImportAutoEqButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = TryFindResource("AutoEq_OpenDialog_Title") as string ?? "Імпорт AutoEq-кривої",
            Filter = "AutoEq (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;

        string content;
        try
        {
            content = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowAutoEqError(ex.Message);
            return;
        }

        try
        {
            double[] gains = AutoEqImporter.ParseAndInterpolate(content, EqBandFrequencies);

            // Застосування через звичайний патерн гейтингу пресетів:
            // пишемо в ViewModel — бідинг оновить слайдери, а ValueChanged синхронізує DSP
            _isApplyingPreset = true;
            try
            {
                for (int i = 0; i < EqBandCount; i++)
                {
                    double clamped = Math.Clamp(gains[i], -12.0, 12.0);

                    _vm.Eq.Bands[i].GainDb = clamped;

                    // Пряме застосування в DSP (дулює з ValueChanged, але надійно)
                    if (_audioEngine.Dsp != null) _audioEngine.Dsp.SetEqBandGain(i, (float)clamped);
                }

                UpdateEqTexts();
            }
            finally
            {
                _isApplyingPreset = false;
            }

            SaveCurrentSettings();
        }
        catch (FormatException ex)
        {
            ShowAutoEqError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowAutoEqError(ex.Message);
        }
    }

    // MessageBox з описом помилки парсингу/застосування AutoEq-кривої
    private void ShowAutoEqError(string details)
    {
        string title = TryFindResource("AutoEq_Error_Title") as string ?? "Помилка імпорту AutoEq";
        string template = TryFindResource("AutoEq_Error_Message") as string ??
            "Не вдалося імпортувати AutoEq-криву.\n\n{0}";

        MessageBox.Show(string.Format(template, details), title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // Кнопка "Скинути": ResetEq у DSP + нулі в усі смуги EQ (ViewModel; слайдери оновить бідинг)
    private void ResetEqButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isApplyingPreset = true;
        try
        {
            _audioEngine.Dsp?.ResetEq();

            for (int i = 0; i < EqBandCount; i++)
            {
                _vm.Eq.Bands[i].GainDb = 0;
            }
        }
        finally
        {
            _isApplyingPreset = false;
        }

        SaveCurrentSettings();
    }

    // Застосовує збережені налаштування EQ до DSP (слайдери вже синхронізовані бідингом із ViewModel)
    private void ApplyEqToDspAndSettings(AppSettings settings)
    {
        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.IsEqEnabled = settings.IsEqEnabled;

            if (settings.EqGainsDb is { Length: EqBandCount } gains)
            {
                for (int i = 0; i < EqBandCount; i++)
                {
                    _audioEngine.Dsp.SetEqBandGain(i, (float)gains[i]);
                }
            }
        }

        UpdateEqTexts();
    }

    // Читає значення всіх смуг з ViewModel (для збереження в налаштування/пресет)
    internal double[] GetEqGainsFromSliders()
    {
        return _vm.Eq.GetGains();
    }

    // Застосовує значення смуг з пресета: пишемо в ViewModel — бідинг оновить слайдери,
    // а ValueChanged синхронізує DSP (як і раніше)
    internal void ApplyEqGainsToSliders(double[]? gains)
    {
        _vm.Eq.SetGains(gains);
    }

    // Оновлює всі дБ-підписи еквалайзера за поточними значеннями слайдерів
    internal void UpdateEqTexts()
    {
        for (int i = 0; i < EqBandCount; i++)
        {
            if (EqSliderAt(i) is { } slider) UpdateEqBandText(i, slider.Value);
        }
    }

    // Викликається з міні-мікшера: перемикання EQ синхронізовано з головним вікном (стан — у ViewModel)
    public void TriggerEqToggleFromMini()
    {
        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEqEnabled = _vm.IsEqEnabled;
        SaveCurrentSettings();
    }
}
