using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinAudioBooster.Services;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private readonly int _hotkeyId = 9999;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    public event EventHandler? HotkeyPressed;

    public bool Register(Window window, uint modifiers, uint virtualKey)
    {
        Unregister();
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        
        _source ??= HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        return RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmHotkey = 0x0312;
        if (msg == wmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            try { UnregisterHotKey(_windowHandle, _hotkeyId); } catch { }
            _source?.RemoveHook(HwndHook);
        }
    }

    public void Dispose() => Unregister();
}