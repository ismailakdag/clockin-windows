using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clockin.Windows;

public static class HotKeyManager
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002;
    private static HwndSource? _source;
    private static ClockStore? _store;
    private static Action? _show;
    public static void Start(Window window, ClockStore store, Action show)
    {
        _store = store; _show = show;
        _source = (HwndSource)PresentationSource.FromVisual(window)!;
        _source.AddHook(WndProc);
        RegisterHotKey(_source.Handle, 1, ModControl | ModAlt, (uint)KeyI);
        RegisterHotKey(_source.Handle, 2, ModControl | ModAlt, (uint)KeyP);
        RegisterHotKey(_source.Handle, 3, ModControl | ModAlt, (uint)KeyO);
        RegisterHotKey(_source.Handle, 4, ModControl | ModAlt, (uint)KeyE);
    }
    public static void Stop()
    {
        if (_source is null) return;
        for (var i = 1; i <= 4; i++) UnregisterHotKey(_source.Handle, i);
        _source.RemoveHook(WndProc); _source = null;
    }
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && _store is not null)
        {
            switch (wParam.ToInt32())
            {
                case 1: if (_store.Running is null) _store.ClockIn(); else if (_store.Running.IsPaused) _store.Resume(); break;
                case 2: if (_store.Running?.IsPaused == true) _store.Resume(); else _store.Pause(); break;
                case 3: _store.ClockOut(); break;
                case 4: _show?.Invoke(); break;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int KeyI = 0x49, KeyP = 0x50, KeyO = 0x4F, KeyE = 0x45;
}
