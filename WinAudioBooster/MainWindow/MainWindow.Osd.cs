namespace WinAudioBooster;

/// <summary>
/// Частковий клас MainWindow: OSD-фідбек глобальних хоткеїв (ідея Volume²).
/// Показує не нав'язливий toast/balloon через TrayService з тротлінгом 1 с:
/// «Пресет: Headphones», «Bypass: On/Off», «Гучність: 250%».
/// </summary>
public partial class MainWindow
{
    // Пауза OSD на старті: програмне налаштування гучності при запуску не має виглядати як дія користувача
    private static readonly DateTime AppStartTime = DateTime.Now;
    private static readonly TimeSpan OsdStartupQuietPeriod = TimeSpan.FromSeconds(5);

    // Локалізований рядок OSD з безпечним фолбеком (якщо ключ відсутній у ресурсах)
    private string GetOsdString(string resourceKey, string fallback)
    {
        return TryFindResource(resourceKey) as string ?? fallback;
    }

    private void ShowOsd(string message)
    {
        string title = TryFindResource("AppTitle") as string ?? "WinAudioBooster";
        _trayService?.ShowBalloonTip(title, message);
    }

    // OSD після застосування пресета хоткеєм Ctrl+Alt+1..5
    private void ShowPresetOsd(string presetName)
    {
        string template = GetOsdString("OSD_Preset", "Пресет: {0}");
        ShowOsd(string.Format(CultureInfo.CurrentCulture, template, presetName));
    }

    // OSD після toggle бустера/bypass хоткеєм (Ctrl+Alt+B / Ctrl+Alt+D)
    private void ShowBypassOsd(bool boosterEnabled)
    {
        // Бустер вимкнено → bypass активний
        string key = boosterEnabled ? "OSD_BypassOff" : "OSD_BypassOn";
        string fallback = boosterEnabled ? "Bypass: Off" : "Bypass: On";
        ShowOsd(GetOsdString(key, fallback));
    }

    // OSD при зміні системної гучності Windows (глобальні медіа-хоткеї/повзунок)
    private void ShowVolumeOsd(double volumePercent)
    {
        // Не показуємо OSD протягом перших секунд: це службові зміни при запуску,
        // а не дії користувача
        if (DateTime.Now - AppStartTime < OsdStartupQuietPeriod) return;

        string template = GetOsdString("OSD_Volume", "Гучність: {0:F0}%");
        ShowOsd(string.Format(CultureInfo.CurrentCulture, template, volumePercent));
    }
}
