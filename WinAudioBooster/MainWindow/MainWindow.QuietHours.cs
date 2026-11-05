namespace WinAudioBooster;

/// <summary>
/// Частковий клас MainWindow: нічний режим за розкладом (Quiet Hours, ідея Volume²).
/// DispatcherTimer раз на хвилину: якщо поточна година у діапазоні (працює через північ,
/// наприклад 23 → 7) і бустер активний — застосовується пресет «Night Mode».
/// При виході з діапазону повертається попередній пресет (запам'ятовується при вході).
/// </summary>
public partial class MainWindow
{
    private const string NightModePresetName = "Night Mode";

    private readonly DispatcherTimer _quietHoursTimer = new();

    // Чи перебуваємо зараз у нічному режимі
    private bool _isQuietModeActive;

    // Пресет, який був активний перед входом у нічний режим
    private string? _presetBeforeQuietMode;

    // Прапорець програмного вибору годин (щоб SelectionChanged не перезапускав логіку)
    private bool _isQuietHoursUiUpdating;

    /// <summary>
    /// Викликається з LoadSavedSettingsAndPresets: заповнює ComboBox годин (0–23),
    /// виставляє збережені значення, запускає хвилинний таймер і робить першу перевірку.
    /// </summary>
    private void InitializeQuietHours(AppSettings settings)
    {
        FillQuietHoursComboBox(QuietHoursStartComboBox);
        FillQuietHoursComboBox(QuietHoursEndComboBox);

        _isQuietHoursUiUpdating = true;
        QuietHoursCheckBox.IsChecked = settings.QuietHoursEnabled;
        QuietHoursStartComboBox.SelectedIndex = Math.Clamp(settings.QuietHoursStart, 0, 23);
        QuietHoursEndComboBox.SelectedIndex = Math.Clamp(settings.QuietHoursEnd, 0, 23);
        _isQuietHoursUiUpdating = false;

        _quietHoursTimer.Interval = TimeSpan.FromMinutes(1);
        _quietHoursTimer.Tick += (s, e) => CheckQuietHours();
        _quietHoursTimer.Start();

        // Перевірка одразу після запуску (можливо, вже нічний час)
        CheckQuietHours();
    }

    // 24 пункти «00:00» … «23:00»; година = індекс елемента
    private static void FillQuietHoursComboBox(ComboBox comboBox)
    {
        for (int hour = 0; hour < 24; hour++)
        {
            comboBox.Items.Add(new ComboBoxItem { Content = $"{hour:00}:00", Tag = hour });
        }
    }

    private void CheckQuietHours()
    {
        bool enabled = QuietHoursCheckBox.IsChecked == true;
        int startHour = Math.Max(0, QuietHoursStartComboBox.SelectedIndex);
        int endHour = Math.Max(0, QuietHoursEndComboBox.SelectedIndex);
        int currentHour = DateTime.Now.Hour;

        bool inRange = enabled && IsHourInQuietRange(currentHour, startHour, endHour);
        bool boosterActive = EnableBoosterCheckBox.IsChecked == true;

        // Вхід у нічний режим: діапазон + активний бустер + ще не в нічному режимі
        if (inRange && boosterActive && !_isQuietModeActive)
        {
            _isQuietModeActive = true;
            _presetBeforeQuietMode = (PresetsComboBox.SelectedItem as Preset)?.Name;
            ApplyPresetByName(NightModePresetName);
            return;
        }

        // Вихід з нічного режиму: вийшли з діапазону (або бустер вимкнули) → повертаємо пресет
        if (_isQuietModeActive && (!inRange || !boosterActive))
        {
            _isQuietModeActive = false;
            string? restore = _presetBeforeQuietMode;
            _presetBeforeQuietMode = null;

            if (!string.IsNullOrWhiteSpace(restore) &&
                !restore.Equals(NightModePresetName, StringComparison.OrdinalIgnoreCase))
            {
                ApplyPresetByName(restore);
            }
        }
    }

    // Діапазон може переходити через північ (23 → 7)
    private static bool IsHourInQuietRange(int hour, int start, int end)
    {
        if (start == end) return false; // нульовий діапазон — нічого не робимо
        if (start < end) return hour >= start && hour < end;
        return hour >= start || hour < end;
    }

    private void QuietHoursCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings();
        CheckQuietHours();
    }

    private void QuietHoursComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isQuietHoursUiUpdating) return;

        SaveCurrentSettings();
        CheckQuietHours();
    }
}
