namespace WinAudioBooster;

public partial class MainWindow
{
    // Кількість смуг еквалайзера (31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Гц)
    private const int EqBandCount = 10;

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

    // Увімкнення/вимкнення еквалайзера (checkbox у секції EQ)
    private void EqToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        bool isEnabled = EqEnabledCheckBox.IsChecked == true;
        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEqEnabled = isEnabled;

        _miniMixerWindow?.SyncWithMainWindow();

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Один обробник для трьох швидких DSP-тогглів (Tag = Bass | Treble | SubBass)
    private void DspBoostToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;
        bool isChecked = checkBox.IsChecked == true;

        if (_audioEngine.Dsp != null)
        {
            switch (checkBox.Tag as string)
            {
                case "Bass":
                    _audioEngine.Dsp.IsBassBoostEnabled = isChecked;
                    break;
                case "Treble":
                    _audioEngine.Dsp.IsTrebleBoostEnabled = isChecked;
                    break;
                case "SubBass":
                    _audioEngine.Dsp.IsSubBassFilterEnabled = isChecked;
                    break;
            }
        }

        if (!_isInitializing && !_isApplyingPreset) SaveCurrentSettings();
    }

    // Другий рядок швидких DSP-тогглів: Loudness / Stereo width / Crossfeed / Mono (за Tag)
    private void DspSpatialToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;
        bool isChecked = checkBox.IsChecked == true;

        if (_audioEngine.Dsp != null)
        {
            switch (checkBox.Tag as string)
            {
                case "Loudness":
                    _audioEngine.Dsp.IsLoudnessNormalizationEnabled = isChecked;
                    break;
                case "StereoWidth":
                    _audioEngine.Dsp.IsStereoWidenEnabled = isChecked;
                    break;
                case "Crossfeed":
                    _audioEngine.Dsp.IsCrossfeedEnabled = isChecked;
                    break;
                case "Mono":
                    _audioEngine.Dsp.IsMonoEnabled = isChecked;
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

    // Застосовує збережені DSP-тоггли до чекбоксів та DSP (викликається зі LoadSavedSettingsAndPresets)
    private void ApplyBoostTogglesToDsp(AppSettings settings)
    {
        BassBoostCheckBox.IsChecked = settings.IsBassBoostEnabled;
        TrebleBoostCheckBox.IsChecked = settings.IsTrebleBoostEnabled;
        SubBassFilterCheckBox.IsChecked = settings.IsSubBassFilterEnabled;

        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.IsBassBoostEnabled = settings.IsBassBoostEnabled;
            _audioEngine.Dsp.IsTrebleBoostEnabled = settings.IsTrebleBoostEnabled;
            _audioEngine.Dsp.IsSubBassFilterEnabled = settings.IsSubBassFilterEnabled;
        }
    }

    // Застосовує просторові DSP-тоггли та Balance (викликається зі LoadSavedSettingsAndPresets)
    private void ApplySpatialTogglesToDsp(AppSettings settings)
    {
        LoudnessCheckBox.IsChecked = settings.IsLoudnessNormalizationEnabled;
        StereoWidthCheckBox.IsChecked = settings.IsStereoWidenEnabled;
        CrossfeedCheckBox.IsChecked = settings.IsCrossfeedEnabled;
        MonoCheckBox.IsChecked = settings.IsMonoEnabled;
        BalanceSlider.Value = settings.Balance;

        if (_audioEngine.Dsp != null)
        {
            _audioEngine.Dsp.IsLoudnessNormalizationEnabled = settings.IsLoudnessNormalizationEnabled;
            _audioEngine.Dsp.IsStereoWidenEnabled = settings.IsStereoWidenEnabled;
            _audioEngine.Dsp.IsCrossfeedEnabled = settings.IsCrossfeedEnabled;
            _audioEngine.Dsp.IsMonoEnabled = settings.IsMonoEnabled;
            _audioEngine.Dsp.Balance = (float)settings.Balance;
        }
    }

    // Кнопка "Скинути": ResetEq у DSP + нулі в усі слайдери
    private void ResetEqButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isApplyingPreset = true;
        try
        {
            _audioEngine.Dsp?.ResetEq();

            for (int i = 0; i < EqBandCount; i++)
            {
                if (EqSliderAt(i) is { } slider) slider.Value = 0;
            }
        }
        finally
        {
            _isApplyingPreset = false;
        }

        SaveCurrentSettings();
    }

    // Застосовує збережені налаштування EQ до слайдерів та DSP (викликається зі LoadSavedSettingsAndPresets)
    private void ApplyEqToDspAndSettings(AppSettings settings)
    {
        EqEnabledCheckBox.IsChecked = settings.IsEqEnabled;

        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEqEnabled = settings.IsEqEnabled;

        if (settings.EqGainsDb is { Length: EqBandCount } gains)
        {
            for (int i = 0; i < EqBandCount; i++)
            {
                if (EqSliderAt(i) is { } slider) slider.Value = gains[i];
            }
        }

        UpdateEqTexts();
    }

    // Читає значення всіх смуг зі слайдерів (для збереження в налаштування/пресет)
    internal double[] GetEqGainsFromSliders()
    {
        var gains = new double[EqBandCount];
        for (int i = 0; i < EqBandCount; i++)
        {
            gains[i] = EqSliderAt(i)?.Value ?? 0;
        }
        return gains;
    }

    // Застосовує значення смуг з пресета: слайдери самі синхронізують DSP через ValueChanged
    internal void ApplyEqGainsToSliders(double[]? gains)
    {
        if (gains == null || gains.Length != EqBandCount) return;

        for (int i = 0; i < EqBandCount; i++)
        {
            if (EqSliderAt(i) is { } slider) slider.Value = gains[i];
        }
    }

    // Оновлює всі дБ-підписи еквалайзера за поточними значеннями слайдерів
    internal void UpdateEqTexts()
    {
        for (int i = 0; i < EqBandCount; i++)
        {
            if (EqSliderAt(i) is { } slider) UpdateEqBandText(i, slider.Value);
        }
    }

    // Викликається з міні-мікшера: перемикання EQ синхронізовано з головним вікном
    public void TriggerEqToggleFromMini()
    {
        if (_audioEngine.Dsp != null) _audioEngine.Dsp.IsEqEnabled = EqEnabledCheckBox.IsChecked == true;
        SaveCurrentSettings();
    }
}
