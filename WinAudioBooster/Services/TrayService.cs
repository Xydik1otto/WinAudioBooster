using System.Drawing;
using System.Windows.Forms;

namespace WinAudioBooster.Services;

public class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly Window _mainWindow;
    private readonly Action _onToggleBooster;
    private readonly Action _onExit;

    public TrayService(Window mainWindow, Action onToggleBooster, Action onExit)
    {
        _mainWindow = mainWindow;
        _onToggleBooster = onToggleBooster;
        _onExit = onExit;
        InitializeTray();
    }

    private void InitializeTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "WinAudioBooster"
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Відкрити", null, (s, e) => ShowWindow());
        contextMenu.Items.Add("Увімкнути/Вимкнути бустер", null, (s, e) => _onToggleBooster());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Вихід", null, (s, e) => _onExit());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowWindow();

        _balloonThrottleTimer.Interval = BalloonThrottleMs;
        _balloonThrottleTimer.Tick += BalloonThrottleTimer_OnTick;
    }

    // OSD-фідбек хоткеїв: тротлінг 1 с — повідомлення, що приходять частіше,
    // відкладаються і показуються останнім текстом (без спаму балунами)
    private const int BalloonThrottleMs = 1000;
    private readonly System.Windows.Forms.Timer _balloonThrottleTimer = new();
    private string? _pendingBalloonTitle;
    private string? _pendingBalloonText;
    private DateTime _lastBalloonShownUtc = DateTime.MinValue;

    /// <summary>
    /// Не нав'язливий OSD-балун біля трей-іконки (фідбек хоткеїв: пресет/bypass/гучність).
    /// Якщо протягом останньої секунди балун уже показано — новий текст відкладається
    /// і показується одним повідомленням після закінчення тротлінг-вікна.
    /// </summary>
    public void ShowBalloonTip(string title, string text)
    {
        if (_notifyIcon == null) return;

        bool withinThrottle = (DateTime.UtcNow - _lastBalloonShownUtc).TotalMilliseconds < BalloonThrottleMs;
        if (!withinThrottle)
        {
            ShowBalloonCore(title, text);
            return;
        }

        // Оновлюємо відкладений текст: під час тротлінг-вікна показується лише найсвіжіший
        _pendingBalloonTitle = title;
        _pendingBalloonText = text;
        _balloonThrottleTimer.Start();
    }

    private void BalloonThrottleTimer_OnTick(object? sender, EventArgs e)
    {
        _balloonThrottleTimer.Stop();

        if (_pendingBalloonTitle == null || _pendingBalloonText == null) return;
        ShowBalloonCore(_pendingBalloonTitle, _pendingBalloonText);
        _pendingBalloonTitle = null;
        _pendingBalloonText = null;
    }

    private void ShowBalloonCore(string title, string text)
    {
        _lastBalloonShownUtc = DateTime.UtcNow;
        try
        {
            if (_notifyIcon == null) return;
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.ShowBalloonTip(1500);
        }
        catch (Exception ex)
        {
            // Балун може бути недоступний у деяких середовищах — не критично
            Debug.WriteLine($"Balloon tip skipped: {ex.Message}");
        }
    }

    public void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>
    /// Balloon-повідомлення в треї про доступну нову версію.
    /// Клік по balloon відкриває url у браузері (NotifyIcon підтримує BalloonTipClicked);
    /// підписник одноразовий — після першого кліку від'єднується.
    /// </summary>
    public void ShowUpdateAvailable(string latestVersion, string url)
    {
        if (_notifyIcon == null || string.IsNullOrEmpty(url)) return;

        // Одноразовий обробник кліку по balloon: відкрити сторінку релізу у браузері
        void OnBalloonTipClicked(object? sender, EventArgs e)
        {
            _notifyIcon!.BalloonTipClicked -= OnBalloonTipClicked;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { /* браузер не відкрився — не критично */ }
        }

        _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        _notifyIcon.BalloonTipTitle = "Доступне оновлення WinAudioBooster";
        _notifyIcon.BalloonTipText = $"Вийшла нова версія {latestVersion}.\nКлацніть тут, щоб відкрити сторінку завантаження.";
        _notifyIcon.ShowBalloonTip(10000);
    }

    public void Dispose()
    {
        _balloonThrottleTimer.Dispose();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}