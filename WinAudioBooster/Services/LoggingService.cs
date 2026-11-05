namespace WinAudioBooster.Services;

/// <summary>
/// Простий файловий лог додатку: %APPDATA%\WinAudioBooster\log.txt.
/// При перевищенні 2 МБ поточний log.txt перейменовується у log.old.txt
/// (одна генерація ротації), а новий log.txt починається з чистого аркуша.
/// Усі помилки самого логера ковтаються — логування ніколи не ламає додаток.
/// </summary>
public static class LoggingService
{
    private const long MaxLogBytes = 2 * 1024 * 1024; // 2 МБ

    private static readonly object _writeLock = new();

    /// <summary>Увімкнення журналу налаштуванням IsLoggingEnabled (чекбокс у налаштуваннях).</summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>Тека логів: %APPDATA%\WinAudioBooster\</summary>
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinAudioBooster");

    /// <summary>Повний шлях до поточного log.txt.</summary>
    public static string LogFilePath => Path.Combine(LogDirectory, "log.txt");

    /// <summary>Дописує рядок у лог: "[2026-11-05 12:34:56] [LEVEL] message".</summary>
    public static void WriteLog(string level, string message)
    {
        if (!IsEnabled) return;

        try
        {
            lock (_writeLock)
            {
                Directory.CreateDirectory(LogDirectory);

                // Ротація: log.txt > 2 МБ → log.old.txt (попередній .old перезаписується)
                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MaxLogBytes)
                {
                    var oldPath = Path.Combine(LogDirectory, "log.old.txt");
                    if (File.Exists(oldPath))
                    {
                        File.Delete(oldPath);
                    }
                    File.Move(LogFilePath, oldPath);
                }

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Логування не має ніколи впливати на роботу додатку
        }
    }

    /// <summary>Записує короткий дамп виключення (тип + повідомлення + стек).</summary>
    public static void WriteException(string level, Exception ex)
    {
        WriteLog(level, $"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
    }
}
