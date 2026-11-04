using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinAudioBooster.Services;

/// <summary>
/// Менеджер глобальних хоткеїв. Підтримує кілька одночасно зареєстрованих комбінацій:
/// основний хоткей бустера (налаштовуваний) + фіксовані Ctrl+Alt+1..5 (пресети) і Ctrl+Alt+D (bypass DSP).
/// </summary>
public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Ідентифікатори хоткеїв (унікальні в межах вікна)
    public const int MainHotkeyId = 9999;          // основний хоткей бустера (налаштовуваний)
    public const int PresetHotkeyBaseId = 10001;   // Ctrl+Alt+1..5 → пресети 1..5 (ідентифікатори 10001..10005)
    public const int BypassHotkeyId = 10010;       // Ctrl+Alt+D → bypass DSP (toggle бустера)

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _hookAttached;
    private readonly HashSet<int> _registeredIds = new();

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    // Основний хоткей (сумісність зі старим API)
    public event EventHandler? HotkeyPressed;

    // Усі хоткеї: аргумент — ідентифікатор (MainHotkeyId / PresetHotkeyBaseId+n / BypassHotkeyId)
    public event EventHandler<int>? HotkeyPressedById;

    public bool Register(Window window, uint modifiers, uint virtualKey)
    {
        return Register(window, modifiers, virtualKey, MainHotkeyId);
    }

    /// <summary>
    /// Реєструє хоткей із заданим ідентифікатором. Повторна реєстрація того самого id
    /// спершу знімає стару комбінацію (так змінюється основний хоткей без втрати інших).
    /// </summary>
    public bool Register(Window window, uint modifiers, uint virtualKey, int id)
    {
        EnsureHook(window);

        if (_registeredIds.Contains(id))
        {
            UnregisterId(id);
        }

        bool success = RegisterHotKey(_windowHandle, id, modifiers, virtualKey);
        if (success)
        {
            _registeredIds.Add(id);
        }
        else
        {
            Debug.WriteLine($"HotkeyManager: не вдалося зареєструвати хоткей id={id} (зайнято іншою програмою?)");
        }

        return success;
    }

    // Приєднує HwndHook один раз; повторні виклики безпечні
    private void EnsureHook(Window window)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
        }

        if (!_hookAttached && _source != null)
        {
            _source.AddHook(HwndHook);
            _hookAttached = true;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmHotkey = 0x0312;
        if (msg == wmHotkey)
        {
            int id = wParam.ToInt32();
            if (_registeredIds.Contains(id))
            {
                handled = true;

                // Основний хоткей — старий event для сумісності
                if (id == MainHotkeyId)
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }

                HotkeyPressedById?.Invoke(this, id);
            }
        }
        return IntPtr.Zero;
    }

    // Знімає лише основний хоткей (сумісність: використовується під час зміни комбінації)
    public void Unregister()
    {
        UnregisterId(MainHotkeyId);
    }

    // Знімає всі зареєстровані хоткеї
    public void UnregisterAll()
    {
        foreach (int id in _registeredIds.ToList())
        {
            UnregisterId(id);
        }
    }

    private void UnregisterId(int id)
    {
        if (!_registeredIds.Remove(id)) return;

        try { UnregisterHotKey(_windowHandle, id); } catch { }

        // Хук від'єднуємо лише коли не залишилось жодного хоткея
        if (_registeredIds.Count == 0 && _hookAttached)
        {
            _source?.RemoveHook(HwndHook);
            _hookAttached = false;
        }
    }

    public void Dispose() => UnregisterAll();
}