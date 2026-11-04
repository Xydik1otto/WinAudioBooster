using System.Text.Json;

namespace WinAudioBooster.Services;

public class SettingsManager
{
    private readonly string _settingsFilePath;
    private readonly string? _legacySettingsFilePath;

    public SettingsManager()
    {
        // Зберігаємо у %APPDATA%\WinAudioBooster, а не поруч із exe (BaseDirectory)
        _settingsFilePath = Path.Combine(GetDefaultAppDataDirectory(), "appsettings.json");
        _legacySettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        MigrateFromLegacyIfNeeded();
    }

    /// <summary>
    /// Конструктор для тестів: дозволяє підмінити APPDATA-папку та стару BaseDirectory-папку.
    /// </summary>
    public SettingsManager(string appDataDirectory, string? legacyBaseDirectory = null)
    {
        _settingsFilePath = Path.Combine(appDataDirectory, "appsettings.json");
        _legacySettingsFilePath = legacyBaseDirectory == null
            ? null
            : Path.Combine(legacyBaseDirectory, "appsettings.json");
        MigrateFromLegacyIfNeeded();
    }

    public static string GetDefaultAppDataDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(string.IsNullOrEmpty(appData)
            ? AppDomain.CurrentDomain.BaseDirectory
            : appData, "WinAudioBooster");
    }

    /// <summary>Якщо налаштування є у старій BaseDirectory, а в APPDATA немає — переносимо.</summary>
    private void MigrateFromLegacyIfNeeded()
    {
        try
        {
            if (_legacySettingsFilePath == null) return;
            if (!File.Exists(_legacySettingsFilePath)) return;
            if (File.Exists(_settingsFilePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            File.Copy(_legacySettingsFilePath, _settingsFilePath, overwrite: false);
        }
        catch
        {
            // Міграція не критична: за замовчуванням створяться нові налаштування
        }
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath)) return new AppSettings();

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { }
    }
}
