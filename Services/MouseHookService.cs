using System.Runtime.InteropServices;
using System.Windows.Threading;
using AnyTray.Infrastructure;
using AnyTray.Native;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

public interface IMouseHookService : IDisposable
{
    void SetEnabled(bool enabled);
    /// <summary>Средний клик по полосе заголовка управляемого окна. Приходит на UI-потоке. hwnd окна.</summary>
    event EventHandler<nint>? TitleBarMiddleClick;
}

/// <summary>
/// Глобальный низкоуровневый перехват мыши (WH_MOUSE_LL) — БЕЗ инжекта в чужие процессы
/// (хук работает в нашем процессе). Ловит средний клик по полосе заголовка любого окна
/// и подавляет это событие, чтобы оно не дошло до приложения (напр. не закрыло вкладку браузера).
///
/// Колбэк вызывается на каждое движение мыши, поэтому для не-средних кнопок мгновенно
/// уходит в CallNextHookEx без работы.
/// </summary>
public sealed class MouseHookService : IMouseHookService
{
    private const int TitleBandDip = 40; // высота «полосы заголовка», по которой ловим клик

    private readonly IWindowManager _windowManager;
    private readonly Dispatcher _dispatcher;
    private readonly LowLevelMouseProc _proc; // держим живым для GC
    private nint _hook;
    private bool _enabled;

    private nint _pendingHwnd; // окно, по заголовку которого нажали среднюю кнопку

    public event EventHandler<nint>? TitleBarMiddleClick;

    public MouseHookService(IWindowManager windowManager, Dispatcher dispatcher)
    {
        _windowManager = windowManager;
        _dispatcher = dispatcher;
        _proc = HookProc;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _enabled) return;
        _enabled = enabled;
        if (enabled) Install();
        else Uninstall();
    }

    private void Install()
    {
        if (_hook != 0) return;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == 0)
            Logger.Error($"Не удалось установить mouse-hook (код {Marshal.GetLastWin32Error()}).");
        else
            Logger.Info("MouseHookService: перехват среднего клика по заголовку включён.");
    }

    private void Uninstall()
    {
        if (_hook == 0) return;
        UnhookWindowsHookEx(_hook);
        _hook = 0;
        _pendingHwnd = 0;
        Logger.Info("MouseHookService: перехват выключен.");
    }

    private nint HookProc(int nCode, nint wParam, nint lParam)
    {
        // Быстрый выход для всех событий, кроме средней кнопки (колбэк зовётся на каждое движение!).
        if (nCode != HC_ACTION) return CallNextHookEx(_hook, nCode, wParam, lParam);
        int msg = (int)wParam;
        if (msg != WM_MBUTTONDOWN && msg != WM_MBUTTONUP)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        try
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            nint target = ResolveTitleBarWindow(data.pt);

            if (msg == WM_MBUTTONDOWN)
            {
                if (target != 0)
                {
                    _pendingHwnd = target;
                    return 1; // подавляем нажатие, чтобы приложение его не получило
                }
            }
            else // WM_MBUTTONUP
            {
                if (_pendingHwnd != 0)
                {
                    var hwnd = _pendingHwnd;
                    _pendingHwnd = 0;
                    // UI-действие выполняем асинхронно, чтобы не блокировать цепочку хуков.
                    _dispatcher.BeginInvoke(() => TitleBarMiddleClick?.Invoke(this, hwnd));
                    return 1; // подавляем отпускание
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка в mouse-hook.", ex);
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>Возвращает hwnd управляемого окна, если точка попала в его полосу заголовка; иначе 0.</summary>
    private nint ResolveTitleBarWindow(POINT pt)
    {
        nint child = WindowFromPoint(pt);
        if (child == 0) return 0;

        nint root = GetAncestor(child, GA_ROOT);
        if (root == 0 || !_windowManager.IsManageableWindow(root)) return 0;

        var w = new Win32Window(root);
        if (!w.TryGetExtendedFrameBounds(out RECT fr)) return 0;

        double scale = DpiHelper.GetScaleForWindow(root);
        int band = DpiHelper.DipToPixels(TitleBandDip, scale);

        bool inTitle = pt.X >= fr.Left && pt.X <= fr.Right &&
                       pt.Y >= fr.Top && pt.Y < fr.Top + band;
        return inTitle ? root : 0;
    }

    public void Dispose() => Uninstall();
}
