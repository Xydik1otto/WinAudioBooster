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

    // =====================================================================
    //  Користувацькі колірні гами (CustomTheme)
    // =====================================================================

    // Мапа «пензель застосунку → поле CustomTheme» (порядок неважливий)
    private static readonly (string BrushKey, string Field)[] CustomThemeBrushMap =
    {
        ("RazerBg", "Background"),
        ("RazerCardBg", "Card"),
        ("RazerCardHeaderBg", "CardHeader"),
        ("RazerBorder", "Border"),
        ("RazerBorderActive", "BorderActive"),
        ("RazerTextMain", "TextMain"),
        ("RazerTextMuted", "TextMuted"),
        ("RazerGreen", "Accent")
    };

    // Резервні значення на випадок, якщо пензель відсутній у поточних ресурсах
    private static readonly Dictionary<string, string> FallbackColors = new()
    {
        ["RazerBg"] = "#0D0E12",
        ["RazerCardBg"] = "#15171E",
        ["RazerCardHeaderBg"] = "#1E212B",
        ["RazerBorder"] = "#2A2E3D",
        ["RazerBorderActive"] = "#00FF66",
        ["RazerTextMain"] = "#F8FAFC",
        ["RazerTextMuted"] = "#94A3B8",
        ["RazerGreen"] = "#00FF66"
    };

    /// <summary>
    /// Читає поточні значення пензлів (після CopyBrushesIntoAppResources вони лежать
    /// у корені Application.Current.Resources) і формує CustomTheme із заданою назвою.
    /// </summary>
    public static WinAudioBooster.Models.CustomTheme CreateCustomThemeFromCurrent(string name)
    {
        var theme = new WinAudioBooster.Models.CustomTheme { Name = name };
        var appResources = Application.Current.Resources;

        foreach (var (brushKey, field) in CustomThemeBrushMap)
        {
            string hex = FallbackColors[brushKey];

            if (appResources[brushKey] is SolidColorBrush brush)
            {
                // #RRGGBB (альфа-канал опускаємо — гами непрозорі)
                hex = brush.Color.ToString().ToUpperInvariant();
                if (hex.Length == 9) hex = $"#{hex[3..]}";
            }

            typeof(WinAudioBooster.Models.CustomTheme)
                .GetProperty(field)!
                .SetValue(theme, hex);
        }

        return theme;
    }

    /// <summary>
    /// Застосовує користувацьку гаму: перевизначає ті самі пензлі в корені
    /// Application.Current.Resources (патерн CopyBrushesIntoAppResources —
    /// інлайн-значення App.xaml мають вищий пріоритет за MergedDictionaries).
    /// Невалідний hex або відсутній колір → пензель лишається від базового скіна.
    /// Викликати ПІСЛЯ базового скіна (як LightOverlay).
    /// </summary>
    public static void ApplyCustomTheme(WinAudioBooster.Models.CustomTheme theme)
    {
        if (theme == null || !theme.HasAnyColor) return;

        try
        {
            var appResources = Application.Current.Resources;

            foreach (var (brushKey, field) in CustomThemeBrushMap)
            {
                string? hex = (string?)typeof(WinAudioBooster.Models.CustomTheme)
                    .GetProperty(field)!
                    .GetValue(theme);

                if (string.IsNullOrWhiteSpace(hex)) continue;

                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex.Trim());
                    appResources[brushKey] = new SolidColorBrush(color);
                }
                catch (FormatException)
                {
                    // Невалідний hex — пензель лишається від базового скіна
                    Debug.WriteLine($"ThemeManager: некоректний колір «{hex}» для {brushKey} у темі «{theme.Name}»");
                }
            }

            Debug.WriteLine($"ThemeManager: застосовано користувацьку тему «{theme.Name}»");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ThemeManager: помилка застосування кастомної теми — {ex.Message}");
        }
    }

    /// <summary>Чи є така назва користувацькою темою зі списку (null/порожні — ні).</summary>
    public static WinAudioBooster.Models.CustomTheme? FindCustomTheme(
        IEnumerable<WinAudioBooster.Models.CustomTheme>? customThemes, string? name)
    {
        if (customThemes == null || string.IsNullOrWhiteSpace(name)) return null;

        return customThemes.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}