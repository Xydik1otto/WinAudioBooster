namespace WinAudioBooster;

/// <summary>
/// Частковий клас MainWindow: таймер сну (15/30/60 хв).
/// По закінченні — плавний fade-out гучності за 10 с (через існуючий механізм
/// GainSlider → GainDb), потім вимкнення бустера і повернення гучності.
/// </summary>
public partial class MainWindow
{
    // Таймер зворотного відліку до початку fade-out
    private readonly DispatcherTimer _sleepTimer = new();

    // Таймер плавного зниження гучності (10 с)
    private readonly DispatcherTimer _sleepFadeTimer = new();

    // Гучність користувача до початку fade-out (для повернення після вимкнення)
    private double _sleepPreFadeVolume;

    // Кількість кроків fade-out, що залишились
    private int _sleepFadeStepsLeft;

    // Чи виконується зараз fade-out
    private bool _isSleepFadeActive;

    // Прапорець для програмної зміни вибору таймера (щоб не перезапускати відлік)
    private bool _isSleepTimerUpdating;

    // 20 кроків × 500 мс = 10 секунд fade-out
    private const int SleepFadeTotalSteps = 20;
    private const double SleepFadeStepMs = 500;

    private void InitializeSleepTimer()
    {
        _sleepTimer.Tick += (s, e) =>
        {
            _sleepTimer.Stop();
            StartSleepFadeOut();
        };

        _sleepFadeTimer.Interval = TimeSpan.FromMilliseconds(SleepFadeStepMs);
        _sleepFadeTimer.Tick += SleepFadeTimer_OnTick;
    }

    // ComboBox "Таймер сну": Tag = "0" (Вимк) | "15" | "30" | "60" (хвилин)
    private void SleepTimerComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _isSleepTimerUpdating) return;

        int minutes = ParseSleepTimerMinutes();

        // Якщо fade-out уже ішов — скасовуємо і повертаємо гучність
        CancelSleepFade(restoreVolume: true);
        _sleepTimer.Stop();

        if (minutes <= 0) return;

        _sleepTimer.Interval = TimeSpan.FromMinutes(minutes);
        _sleepTimer.Start();
    }

    private int ParseSleepTimerMinutes()
    {
        if (SleepTimerComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            int.TryParse(tag, out int minutes))
        {
            return minutes;
        }
        return 0;
    }

    // Початок плавного fade-out гучності до нуля за 10 секунд
    private void StartSleepFadeOut()
    {
        if (_isSleepFadeActive) return;

        _isSleepFadeActive = true;
        _sleepPreFadeVolume = GainSlider.Value;
        _sleepFadeStepsLeft = SleepFadeTotalSteps;

        // _isApplyingPreset = true: fade не має тригерити збереження налаштувань
        // та попередження захисту слуху на кожному кроці
        _isApplyingPreset = true;
        _sleepFadeTimer.Start();
    }

    private void SleepFadeTimer_OnTick(object? sender, EventArgs e)
    {
        _sleepFadeStepsLeft--;

        if (_sleepFadeStepsLeft <= 0)
        {
            GainSlider.Value = 0;
            _sleepFadeTimer.Stop();
            FinishSleepFade();
            return;
        }

        // Плавне зниження гейна через слайдер — далі спрацьовує існуючий
        // механізм GainSlider_OnValueChanged → VolumeManager → Dsp.GainDb
        GainSlider.Value = _sleepPreFadeVolume * _sleepFadeStepsLeft / SleepFadeTotalSteps;
    }

    // Fade-out завершено: вимкнути бустер (той самий шлях, що чекбокс бустера) і повернути гучність
    private void FinishSleepFade()
    {
        _isSleepFadeActive = false;
        _isApplyingPreset = false;

        EnableBoosterCheckBox.IsChecked = false;
        RestartAudioEngineIfEnabled();

        // Повертаємо гучність, яку мав користувач до fade-out
        _isApplyingPreset = true;
        GainSlider.Value = _sleepPreFadeVolume;
        _isApplyingPreset = false;

        SaveCurrentSettings();

        // Скидаємо вибір таймера на "Вимк" (без перезапуску відліку)
        _isSleepTimerUpdating = true;
        SleepTimerComboBox.SelectedIndex = 0;
        _isSleepTimerUpdating = false;
    }

    // Скасування fade-out (наприклад, користувач змінив таймер під час фейду)
    private void CancelSleepFade(bool restoreVolume)
    {
        if (!_isSleepFadeActive) return;

        _sleepFadeTimer.Stop();
        _isSleepFadeActive = false;

        if (restoreVolume)
        {
            _isApplyingPreset = true;
            GainSlider.Value = _sleepPreFadeVolume;
            _isApplyingPreset = false;
        }
    }
}
