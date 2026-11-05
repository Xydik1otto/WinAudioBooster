using WinAudioBooster.Services.ViewModels;
using WinAudioBooster.Models;

namespace WinAudioBooster;

public partial class MainWindow
{
    private List<CustomTheme> _customThemes = new();

    // Пункти ComboBox тем, додані динамічно (кастомні гами) — для очищення при перебудові списку
    private readonly List<ComboBoxItem> _customThemeItems = new();

    private void InitializeTrayAndHotkeys()
    {
        // Авто-тема: стежимо за зміною світлої/темної теми Windows
        ThemeManager.InitializeSystemThemeWatcher();

        Loaded += (s, e) =>
        {
            _hotkeyManager.Register(this, HotkeyManager.ModControl | HotkeyManager.ModAlt, 0x42);

            // Фіксовані хоткеї: Ctrl+Alt+1..5 → пресети 1..5, Ctrl+Alt+D → bypass DSP
            RegisterFixedHotkeys();

            _hotkeyManager.HotkeyPressedById += (sender, id) =>
            {
                Dispatcher.Invoke(() => OnGlobalHotkey(id));
            };
        };

        _trayService = new TrayService(
            this, 
            () => { 
                // Стан бустера живе у ViewModel; бідинг сам оновить чекбокс
                _vm.IsBoosterEnabled = !_vm.IsBoosterEnabled;
                RestartAudioEngineIfEnabled();
                SaveCurrentSettings();
            }, 
            () => { 
                _trayService?.Dispose();
                Environment.Exit(0); 
            }
        );
    }

    // Реєструє фіксовані хоткеї: Ctrl+Alt+1..5 (пресети 1..5) та Ctrl+Alt+D (bypass DSP)
    private void RegisterFixedHotkeys()
    {
        uint mods = HotkeyManager.ModControl | HotkeyManager.ModAlt;

        // VK-коди '1'..'5' = 0x31..0x35
        for (int i = 0; i < 5; i++)
        {
            _hotkeyManager.Register(this, mods, (uint)(0x31 + i), HotkeyManager.PresetHotkeyBaseId + i);
        }

        // VK-код 'D' = 0x44
        _hotkeyManager.Register(this, mods, 0x44, HotkeyManager.BypassHotkeyId);
    }

    // Той самий механізм, що й у Ctrl+Alt+B: toggle бустера через ViewModel + перезапуск движка
    private void ToggleBoosterViaHotkey()
    {
        _vm.IsBoosterEnabled = !_vm.IsBoosterEnabled;
        RestartAudioEngineIfEnabled();
        SaveCurrentSettings();

        // OSD-фідбек: бустер вимкнено хоткеєм → bypass активний (і навпаки)
        ShowBypassOsd(_vm.IsBoosterEnabled);
    }

    // Маршрутизація глобальних хоткеїв за ідентифікатором
    private void OnGlobalHotkey(int hotkeyId)
    {
        if (hotkeyId == HotkeyManager.MainHotkeyId || hotkeyId == HotkeyManager.BypassHotkeyId)
        {
            ToggleBoosterViaHotkey();
            return;
        }

        if (hotkeyId >= HotkeyManager.PresetHotkeyBaseId && hotkeyId < HotkeyManager.PresetHotkeyBaseId + 5)
        {
            ApplyPresetByHotkeyIndex(hotkeyId - HotkeyManager.PresetHotkeyBaseId);
        }
    }

    // Застосувати N-й пресет зі списку _presetManager.LoadAllPresets() (порядок як у ComboBox)
    private void ApplyPresetByHotkeyIndex(int index)
    {
        var presets = _presetManager.LoadAllPresets();
        if (index < 0 || index >= presets.Count) return;

        ApplyPresetByName(presets[index].Name);

        // OSD-фідбек хоткея пресета
        ShowPresetOsd(presets[index].Name);
    }

    private void LoadSavedSettingsAndPresets()
    {
        var settings = _settingsManager.LoadSettings();

        // ViewModel — джерело правди для збереженого стану: Apply() виставляє всі
        // [ObservableProperty], а бідинг сам оновлює слайдери/чекбокси/комбобокс мови
        _vm.Apply(settings);

        // Користувацькі гами: читаємо зі збережених налаштувань і додаємо у ComboBox
        _customThemes = settings.CustomThemes ?? new List<CustomTheme>();
        PopulateCustomThemesInComboBox();

        SelectCurrentLanguageInComboBox(string.IsNullOrEmpty(settings.Language) ? null : settings.Language);
        RefreshPresetsList(settings.SelectedPresetName);

        // Еквалайзер: слайдери вже синхронізовані бідингом — переносимо значення в DSP
        ApplyEqToDspAndSettings(settings);

        // Швидкі DSP-тоггли: Bass Boost / Treble Boost / Cut sub-bass
        ApplyBoostTogglesToDsp(settings);

        // Просторові DSP-тоггли: Loudness / Stereo width / Crossfeed / Mono + Balance
        ApplySpatialTogglesToDsp(settings);

        // Журнал подій: чекбокс прив'язаний до ViewModel, статичний LoggingService синхронізуємо тут
        LoggingService.IsEnabled = settings.IsLoggingEnabled;

        if (OutputDeviceComboBox.ItemsSource is List<AudioDeviceInfo> physicalDevices && physicalDevices.Count > 0)
        {
            var savedDevice = physicalDevices.FirstOrDefault(d => 
                (!string.IsNullOrEmpty(settings.SelectedOutputDeviceId) && d.Id == settings.SelectedOutputDeviceId) ||
                (!string.IsNullOrEmpty(settings.SelectedOutputDeviceName) && d.Name == settings.SelectedOutputDeviceName));

            OutputDeviceComboBox.SelectedItem = savedDevice ?? physicalDevices[0];
        }

        if (!string.IsNullOrEmpty(settings.SelectedTheme))
        {
            var savedCustom = ThemeManager.FindCustomTheme(_customThemes, settings.SelectedTheme);
            if (savedCustom != null)
            {
                // Кастомна гама: ApplyTheme для невідомого ім'я застосовує базовий скін
                // (fallback StudioRack), гаму накладаємо ПІСЛЯ нього — як LightOverlay
                ThemeManager.ApplyTheme(settings.SelectedTheme);
                ThemeManager.ApplyCustomTheme(savedCustom);
                SelectCustomThemeInComboBox(savedCustom.Name);
            }
            else
            {
                SelectCurrentThemeInComboBox(settings.SelectedTheme);
                ThemeManager.ApplyTheme(settings.SelectedTheme);
            }

            // Тема не прив'язана бідингом (у ComboBox немає пункту «Razer Green») — синхронізуємо стан у VM
            _vm.SelectedTheme = settings.SelectedTheme;
        }
        else
        {
            _vm.SelectedTheme = "Razer Green";
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

        SleepTimerComboBox.SelectedIndex = 0; // за замовчуванням "Вимк" (обробник пропускається під час ініціалізації)
        _vm.SleepTimerMinutes = 0;

        // Нічний режим за розкладом: UI + хвилинний таймер + перша перевірка
        InitializeQuietHours(settings);

        UpdateAllUiTexts();
    }

    private void SaveCurrentSettings()
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        // ViewModel — джерело правди: основні налаштування беремо зі Snapshot()
        var settings = _vm.Snapshot();

        // Списки, які керуються окремими редакторами (профілі пристроїв, правила гучності),
        // переносимо з поточного файлу налаштувань, щоб не втратити їх при збереженні
        var existing = _settingsManager.LoadSettings();

        // Мова: читаємо з ComboBox з fallback «uk-UA» — як і в оригіналі (виправляє невалідні збережені мови)
        settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "uk-UA";

        settings.SelectedOutputDeviceId = selectedOutputInfo?.Id ?? string.Empty;
        settings.SelectedOutputDeviceName = selectedOutputInfo?.Name ?? string.Empty;
        settings.DevicePresetBindings = existing.DevicePresetBindings;
        settings.AppVolumeRules = existing.AppVolumeRules;

        // Користувацькі гами: список живе у MainWindow (редагується кнопками тем)
        settings.CustomThemes = _customThemes;

        // Нічний режим за розкладом (стан лишається в контролах, не в ViewModel)
        settings.QuietHoursEnabled = QuietHoursCheckBox.IsChecked == true;
        settings.QuietHoursStart = Math.Max(0, QuietHoursStartComboBox.SelectedIndex);
        settings.QuietHoursEnd = Math.Max(0, QuietHoursEndComboBox.SelectedIndex);

        _settingsManager.SaveSettings(settings);
    }

    private void SaveCurrentSettingsWithHotkey(uint modifiers, uint vKey, string displayName)
    {
        var selectedOutputInfo = OutputDeviceComboBox.SelectedItem as AudioDeviceInfo;

        // ViewModel — джерело правди: основні налаштування беремо зі Snapshot()
        var settings = _vm.Snapshot();

        // Списки, які керуються окремими редакторами, переносимо з поточного файлу налаштувань
        var existing = _settingsManager.LoadSettings();

        // Мова: читаємо з ComboBox з fallback «uk-UA» — як і в оригіналі (виправляє невалідні збережені мови)
        settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "uk-UA";

        settings.SelectedOutputDeviceId = selectedOutputInfo?.Id ?? string.Empty;
        settings.SelectedOutputDeviceName = selectedOutputInfo?.Name ?? string.Empty;
        settings.DevicePresetBindings = existing.DevicePresetBindings;
        settings.AppVolumeRules = existing.AppVolumeRules;

        // Користувацькі гами: список живе у MainWindow (редагується кнопками тем)
        settings.CustomThemes = _customThemes;

        // Нічний режим за розкладом (стан лишається в контролах, не в ViewModel)
        settings.QuietHoursEnabled = QuietHoursCheckBox.IsChecked == true;
        settings.QuietHoursStart = Math.Max(0, QuietHoursStartComboBox.SelectedIndex);
        settings.QuietHoursEnd = Math.Max(0, QuietHoursEndComboBox.SelectedIndex);

        settings.HotkeyModifiers = modifiers;
        settings.HotkeyVKey = vKey;
        settings.HotkeyDisplayName = displayName;

        _settingsManager.SaveSettings(settings);
    }

    private void AutoStartCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        bool isChecked = AutoStartCheckBox.IsChecked == true;
        _autoStartService.SetAutoStart(isChecked);
    }

    // Увімкнення/вимкнення журналу подій (чекбокс прив'язаний до ViewModel)
    private void LoggingEnabledCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        LoggingService.IsEnabled = _vm.IsLoggingEnabled;
        SaveCurrentSettings();
    }

    // «Відкрити лог»: показує %APPDATA%\WinAudioBooster\log.txt у системному текстовому редакторі
    private void OpenLogButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Якщо логу ще немає (щойно ввімкнули) — створюємо порожній файл
            if (!File.Exists(LoggingService.LogFilePath))
            {
                Directory.CreateDirectory(LoggingService.LogDirectory);
                File.WriteAllText(LoggingService.LogFilePath, string.Empty);
            }

            Process.Start(new ProcessStartInfo(LoggingService.LogFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggingService.WriteException("ERROR", ex);
            MessageBox.Show(
                $"Не вдалося відкрити файл журналу:\n{LoggingService.LogFilePath}\n\n{ex.Message}",
                "Журнал", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ChangeHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isListeningForHotkey = true;
        ChangeHotkeyButton.Content = "Натисніть комбінацію...";
        ChangeHotkeyButton.Foreground = (SolidColorBrush)FindResource("RazerGreen");
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_isListeningForHotkey)
        {
            e.Handled = true;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            uint modifiers = 0;
            var kbModifiers = Keyboard.Modifiers;
            if (kbModifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyManager.ModControl;
            if (kbModifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyManager.ModAlt;
            if (kbModifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyManager.ModShift;

            int vKey = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

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
            if (kbModifiers.HasFlag(ModifierKeys.Control)) displayStr += "Ctrl + ";
            if (kbModifiers.HasFlag(ModifierKeys.Alt)) displayStr += "Alt + ";
            if (kbModifiers.HasFlag(ModifierKeys.Shift)) displayStr += "Shift + ";
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

    private void SelectCurrentThemeInComboBox(string themeName)
    {
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            // Порівнюємо спочатку Tag (стабільне значення для збереження), потім Content
            string itemValue = item.Tag as string ?? item.Content?.ToString() ?? string.Empty;
            if (itemValue == themeName)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Кнопка «Видалити» активна лише для користувацької гами (навіть під час ініціалізації)
        UpdateDeleteThemeButtonState();

        if (_isInitializing) return;

        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            // Tag — стабільний ідентифікатор теми (незалежний від мови інтерфейсу)
            string themeName = selectedItem.Tag as string ?? selectedItem.Content?.ToString() ?? string.Empty;

            // Синхронізуємо вибір теми у ViewModel (джерело правди для збереження)
            _vm.SelectedTheme = themeName;

            var customTheme = ThemeManager.FindCustomTheme(_customThemes, themeName);
            if (customTheme != null)
            {
                // Користувацька гама: оверврайт пензлів поверх базового скіна
                ThemeManager.ApplyCustomTheme(customTheme);
            }
            else if (themeName == ThemeManager.AutoThemeName)
            {
                // Авто-режим: база — Windows 11 Fluent, світла тема Windows перекривається overlay
                ThemeManager.SetAutoMode("Windows 11");
            }
            else
            {
                ThemeManager.ApplyTheme(themeName);
            }

            SaveCurrentSettings();
        }
    }

    // =====================================================================
    //  Користувацькі колірні гами (CustomTheme): UI-логіка
    // =====================================================================

    // Перебудовує хвіст ComboBox тем: прибирає старі динамічні пункти, додає кастомні гами
    private void PopulateCustomThemesInComboBox()
    {
        foreach (var item in _customThemeItems)
        {
            ThemeComboBox.Items.Remove(item);
        }
        _customThemeItems.Clear();

        // Вбудовані назви тем (Tag у XAML) — колізії не додаємо
        var builtinNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (item.Tag is string tag) builtinNames.Add(tag);
        }

        foreach (var theme in _customThemes)
        {
            if (string.IsNullOrWhiteSpace(theme.Name) || builtinNames.Contains(theme.Name)) continue;

            var comboItem = new ComboBoxItem { Content = theme.Name, Tag = theme.Name };
            _customThemeItems.Add(comboItem);
            ThemeComboBox.Items.Add(comboItem);
        }

        UpdateDeleteThemeButtonState();
    }

    // Виділяє кастомну гаму в ComboBox за назвою (пункт динамічний, тому окремий хелпер)
    private void SelectCustomThemeInComboBox(string themeName)
    {
        var item = _customThemeItems.FirstOrDefault(i => i.Tag as string == themeName);
        if (item != null) ThemeComboBox.SelectedItem = item;
    }

    // «Видалити» активне лише коли виділено користувацьку гаму (вбудовані видаляти не можна)
    private void UpdateDeleteThemeButtonState()
    {
        bool isCustomSelected = ThemeComboBox.SelectedItem is ComboBoxItem item &&
                                _customThemeItems.Contains(item);
        DeleteThemeButton.IsEnabled = isCustomSelected;
    }

    // «Створити з поточної...»: зчитує поточні пензлі, питає назву, зберігає гаму і вибирає її
    private void CreateThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomThemeNameWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        string name = dialog.ThemeName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            MessageBox.Show(GetString("CustomTheme_EmptyName"), GetString("CustomTheme_NameTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Унікальність: серед кастомних і серед вбудованих тем
        bool duplicate = ThemeManager.FindCustomTheme(_customThemes, name) != null ||
                         ThemeComboBox.Items.OfType<ComboBoxItem>()
                             .Any(i => string.Equals(i.Tag as string, name, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            MessageBox.Show(GetString("CustomTheme_DuplicateName"), GetString("CustomTheme_NameTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Зчитуємо поточні пензлі (останній застосований скін/overlay/гама) → нова гама
        var theme = ThemeManager.CreateCustomThemeFromCurrent(name);
        _customThemes.Add(theme);

        // Персистентність одразу: гама має пережити рестарт навіть без наступних збережень
        SaveCurrentSettings();

        // Перебудовуємо список і вибираємо нову тему (SelectionChanged сам застосує гаму)
        PopulateCustomThemesInComboBox();
        SelectCustomThemeInComboBox(name);
    }

    // «Видалити»: прибирає виділену користувацьку гаму; вбудовані теми захищені
    private void DeleteThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

        string themeName = selectedItem.Tag as string ?? selectedItem.Content?.ToString() ?? string.Empty;
        var theme = ThemeManager.FindCustomTheme(_customThemes, themeName);

        if (theme == null)
        {
            // Вбудовані теми видаляти не можна
            MessageBox.Show(GetString("CustomTheme_DeleteBuiltin"), GetString("DeleteTheme_Btn"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            // Шаблон «...«{0}»?» із ресурсів — підставляємо назву теми
            GetString("CustomTheme_DeleteConfirm").Replace("{0}", theme.Name),
            GetString("DeleteTheme_Btn"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        bool wasSelected = string.Equals(_vm.SelectedTheme, theme.Name, StringComparison.OrdinalIgnoreCase);
        _customThemes.Remove(theme);
        PopulateCustomThemesInComboBox();

        if (wasSelected)
        {
            // Видалена тема була активною — повертаємось до нейтрального базового скіна
            var fallback = ThemeComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => (i.Tag as string) == "Windows 11");
            if (fallback != null) ThemeComboBox.SelectedItem = fallback; // обробник сам застосує і збереже
            else _vm.SelectedTheme = "Windows 11";
        }
        else
        {
            SaveCurrentSettings();
        }
    }

    // Ресурсний рядок за ключем (з fallback на ключ, якщо ресурс відсутній)
    private static string GetString(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    /// <summary>
    /// Мінімальний діалог вводу назви користувацької теми (замість Microsoft.VisualBasic.InputBox —
    /// без додаткової залежності і з локалізованими кнопками).
    /// </summary>
    private sealed class CustomThemeNameWindow : Window
    {
        private readonly TextBox _nameBox;

        public string ThemeName => _nameBox.Text;

        public CustomThemeNameWindow()
        {
            Title = GetStringStatic("CustomTheme_NameTitle");
            Width = 340;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var label = new TextBlock
            {
                Margin = new Thickness(12, 12, 12, 6),
                Text = GetStringStatic("CustomTheme_NameLabel")
            };

            _nameBox = new TextBox { Margin = new Thickness(12, 0, 12, 12), Height = 26, VerticalContentAlignment = VerticalAlignment.Center };

            var okButton = new Button
            {
                Content = GetStringStatic("CustomTheme_Ok"),
                Width = 80, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 8, 0)
            };
            okButton.Click += (_, _) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = GetStringStatic("CustomTheme_Cancel"),
                Width = 80, Height = 26, IsCancel = true
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 12, 12)
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var root = new StackPanel();
            root.Children.Add(label);
            root.Children.Add(_nameBox);
            root.Children.Add(buttons);

            Content = root;
            Loaded += (_, _) => { _nameBox.Focus(); _nameBox.SelectAll(); };
        }

        // Статичний доступ до ресурсів (діалог ще не має вікна-власника)
        private static string GetStringStatic(string key) =>
            Application.Current.TryFindResource(key) as string ?? key;
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
            // Синхронізуємо мову у ViewModel (SelectedValue також прив'язаний бідингом)
            _vm.SelectedLanguage = cultureCode;

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
                $"Знайдено нову версію програми ({version})!\n\n«Так» — завантажити й встановити автоматично,\n«Ні» — відкрити сторінку завантаження у браузері.",
                "Оновлення WinAudioBooster",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                bool installed = await InstallPendingUpdateAsync();
                if (installed) return; // бат-скрипт перезапустить додаток після закриття процесу
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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

    /// <summary>
    /// Завантажує й встановлює оновлення через UpdateService (з SHA256-перевіркою),
    /// після чого коректно завершує додаток — бат-скрипт замінить exe і перезапустить його.
    /// </summary>
    private async Task<bool> InstallPendingUpdateAsync()
    {
        var check = _updateService.LastCheckResult;
        if (check == null || string.IsNullOrEmpty(check.AssetDownloadUrl))
        {
            MessageBox.Show("Не вдалося знайти файл оновлення у релізі.", "Оновлення", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        CheckUpdatesButton.Content = "Завантаження оновлення...";

        // Прогрес завантаження у Title вікна (патерн VbCableInstaller) + у кнопці
        var progress = new Progress<string>(status =>
        {
            CheckUpdatesButton.Content = status;
            Title = $"WinAudioBooster – {status}";
        });

        bool installed = await _updateService.DownloadAndInstallUpdateAsync(
            check.AssetDownloadUrl, check.AssetSha256, progress);

        if (installed)
        {
            // Бат-скрипт чекає закриття процесу (до 30 с), потім замінює exe і перезапускає додаток
            SaveCurrentSettings();
            Application.Current.Shutdown();
            return true;
        }

        // Встановлення не відбулося — повертаємо заголовок вікна
        Title = (string)FindResource("MainWindow_Title");
        MessageBox.Show("Не вдалося встановити оновлення автоматично.\nСпробуйте завантажити його вручну зі сторінки релізів GitHub.",
            "Оновлення", MessageBoxButton.OK, MessageBoxImage.Warning);
        CheckUpdatesButton.IsEnabled = true;
        CheckUpdatesButton.Content = "Перевірити оновлення";
        return false;
    }

    private void UpdateAllUiTexts()
    {
        // GainValueText тепер прив'язаний бідингом до GainPercent — текст оновлюється автоматично
        CompThresholdText.Text = $"{CompThresholdSlider.Value:F0} dB";
        CompRatioText.Text = $"{CompRatioSlider.Value:F1}:1";
        LimiterCeilingText.Text = $"{LimiterCeilingSlider.Value:F1} dB";
        UpdateEqTexts();
    }
}