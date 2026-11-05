using Microsoft.Win32;

namespace WinAudioBooster.Services;

/// <summary>
/// Менеджер тем. Крім звичайних скінів підтримує режим "Auto (Windows)":
/// читає системну тему (HKCU\...\Themes\Personalize\AppsUseLightTheme) і підписується
/// на SystemEvents.UserPreferenceChanged, щоб перемикатися в реальному часі.
/// У світлій системній темі поверх вибраного скіна застосовується LightOverlay.xaml.
/// </summary>
public static class ThemeManager
{
    // Назва авто-режиму (зберігається в AppSettings.SelectedTheme і в ComboBox теми)
    public const string AutoThemeName = "Auto (Windows)";

    private static bool _isAutoMode;
    private static string _autoBaseSkin = "Windows 11";
    private static bool _isSystemEventsSubscribed;

    public static void ApplyTheme(string themeName)
    {
        if (themeName == AutoThemeName)
        {
            _isAutoMode = true;
            ApplyAutoTheme();
            return;
        }

        _isAutoMode = false;
        ApplySkin(themeName);
    }

    /// <summary>
    /// Увімкнути авто-режим із заданою базою (скіном). Викликається з ComboBox теми.
    /// </summary>
    public static void SetAutoMode(string baseSkin)
    {
        _autoBaseSkin = string.IsNullOrWhiteSpace(baseSkin) ? "Windows 11" : baseSkin;
        ApplyTheme(AutoThemeName);
    }

    /// <summary>
    /// Підписка на зміну системної теми Windows (викликається один раз із головного вікна).
    /// </summary>
    public static void InitializeSystemThemeWatcher()
    {
        if (_isSystemEventsSubscribed) return;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _isSystemEventsSubscribed = true;
    }

    /// <summary>
    /// Відписка від системних подій (викликається при закритті головного вікна).
    /// </summary>
    public static void Shutdown()
    {
        if (!_isSystemEventsSubscribed) return;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _isSystemEventsSubscribed = false;
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // General — категорія, у якій приходить зміна світлої/темної теми
        if (e.Category == UserPreferenceCategory.General && _isAutoMode)
        {
            ApplyAutoTheme();
        }
    }

    /// <summary>
    /// Стандарт EarTrumpet: читає AppsUseLightTheme з HKCU (1 = світла тема, 0 = темна).
    /// Якщо ключ відсутній або недоступний — вважаємо тему темною.
    /// </summary>
    public static bool IsWindowsLightTheme()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                defaultValue: 0);

            return value is int lightTheme && lightTheme != 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ThemeManager: не вдалося прочитати системну тему — {ex.Message}");
            return false;
        }
    }

    // Авто-режим: темна тема Windows → скін як є; світла → той самий скін + світлий overlay
    private static void ApplyAutoTheme()
    {
        bool isLight = IsWindowsLightTheme();
        ApplySkin(_autoBaseSkin, applyLightOverlay: isLight);
        Debug.WriteLine($"ThemeManager: auto → {(isLight ? "light" : "dark")} (база: {_autoBaseSkin})");
    }

    private static void ApplySkin(string themeName, bool applyLightOverlay = false)
    {
        string sourcePath = themeName switch
        {
            "Arch Linux" => "Themes/Skins/ArchLinux.xaml",
            "Windows XP" => "Themes/Skins/WindowsXP.xaml",
            "Windows 11" => "Themes/Skins/Win11Fluent.xaml",
            "Minimalism" => "Themes/Skins/Minimalism.xaml",
            "Medieval RPG" => "Themes/Skins/Medieval.xaml",
            _ => "Themes/Skins/StudioRack.xaml"
        };

        try
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            var existingSkin = mergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Skins/"));
            if (existingSkin != null)
            {
                mergedDictionaries.Remove(existingSkin);
            }

            var dict = new ResourceDictionary
            {
                Source = new Uri(sourcePath, UriKind.Relative)
            };

            mergedDictionaries.Add(dict);

            // Кольорові пензлі скіна копіюємо в корінь ресурсів додатку:
            // App.xaml визначає Razer* пензлі інлайн, а інлайн має вищий пріоритет
            // за MergedDictionaries — без копіювання скінові кольори не застосуються.
            CopyBrushesIntoAppResources(dict);

            // Світлий overlay: перевизначає фонові/текстові кольори поверх скіна
            if (applyLightOverlay)
            {
                var overlay = new ResourceDictionary
                {
                    Source = new Uri("Themes/Skins/LightOverlay.xaml", UriKind.Relative)
                };
                mergedDictionaries.Add(overlay);
                CopyBrushesIntoAppResources(overlay);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Theme error: {ex.Message}");
        }
    }

    // Копіює SolidColorBrush-и з джерела в корінь Application.Current.Resources (перекриває інлайн App.xaml)
    private static void CopyBrushesIntoAppResources(ResourceDictionary source)
    {
        var appResources = Application.Current.Resources;
        foreach (object key in source.Keys)
        {
            if (key is string keyName && keyName.StartsWith("Razer", StringComparison.Ordinal) && source[key] is Brush)
            {
                appResources[keyName] = source[key];
            }
        }
    }
}