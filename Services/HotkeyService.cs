using System.Windows.Interop;
using AnyTray.Infrastructure;
using AnyTray.Models;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

public interface IHotkeyService : IDisposable
{
    void Initialize();
    /// <summary>Перерегистрирует горячую клавишу. false — комбинация занята другим приложением.</summary>
    bool Register(HotkeyDefinition def, Action onPressed);
    void Unregister();
}

/// <summary>
/// Глобальные горячие клавиши через скрытое message-only окно (HwndSource).
/// Должен инициализироваться на UI-потоке WPF — там работает message loop, в который
/// приходит WM_HOTKEY.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int HotkeyId = 0x9000;

    private HwndSource? _source;
    private nint _hwnd;
    private Action? _onPressed;
    private bool _registered;

    public void Initialize()
    {
        if (_source is not null) return;

        var parameters = new HwndSourceParameters("AnyTrayHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = HWND_MESSAGE, // message-only окно: невидимое, без накладных расходов
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _hwnd = _source.Handle;
        Logger.Info("HotkeyService инициализирован (message-only окно).");
    }

    public bool Register(HotkeyDefinition def, Action onPressed)
    {
        if (_source is null) Initialize();
        Unregister();

        _onPressed = onPressed;
        def.ToWin32(out uint mods, out uint vk);

        _registered = RegisterHotKey(_hwnd, HotkeyId, mods, vk);
        if (_registered)
            Logger.Info($"Горячая клавиша зарегистрирована: {def}");
        else
            Logger.Warn($"Не удалось зарегистрировать горячую клавишу {def} (вероятно, занята).");

        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != 0)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            try { _onPressed?.Invoke(); }
            catch (Exception ex) { Logger.Error("Ошибка обработчика горячей клавиши.", ex); }
        }
        return 0;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
        _hwnd = 0;
    }
}
