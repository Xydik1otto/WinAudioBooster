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
    }

    public void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}