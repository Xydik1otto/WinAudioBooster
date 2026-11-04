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
