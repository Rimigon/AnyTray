using System.Diagnostics;
using AnyTray.Infrastructure;
using AnyTray.Models;
using AnyTray.Native;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

public interface IWindowManager
{
    int OwnProcessId { get; }
    bool IsManageableWindow(nint hwnd);
    IReadOnlyList<Win32Window> EnumerateManageableTopLevelWindows();
    bool TryHide(nint hwnd, out HiddenWindowInfo? info);
    bool TryRestore(HiddenWindowInfo info);
    bool IsAlive(nint hwnd);
    bool IsOwnWindow(nint hwnd);
}

/// <summary>
/// Поиск, валидация, скрытие и восстановление чужих окон. Состояния не хранит —
/// набор скрытых окон живёт в <see cref="ViewModels.MainViewModel"/>.
/// </summary>
public sealed class WindowManager : IWindowManager
{
    public int OwnProcessId { get; } = Environment.ProcessId;

    public bool IsOwnWindow(nint hwnd)
    {
        var w = new Win32Window(hwnd);
        return w.ProcessId == OwnProcessId;
    }

    public bool IsAlive(nint hwnd) => hwnd != 0 && IsWindow(hwnd);

    /// <summary>
    /// Окно пригодно к скрытию, если это нормальное верхнеуровневое окно приложения:
    /// валидно и видимо; не дочернее (нет WS_CHILD); нет WS_EX_TOOLWINDOW; корневое окно
    /// (GA_ROOTOWNER == self); не cloaked (DWM); есть непустой заголовок; не наш процесс.
    ///
    /// Стили заголовка (WS_CAPTION/WS_SYSMENU) НЕ требуются намеренно: многие приложения с
    /// кастомным/безрамочным заголовком (Telegram, VS Code, Electron/Qt) их не выставляют,
    /// но скрывать их по горячей клавише нужно. Возможность ПОКАЗАТЬ overlay-кнопку
    /// определяется отдельно — по реальным границам системных кнопок заголовка.
    /// </summary>
    public bool IsManageableWindow(nint hwnd)
    {
        try
        {
            var w = new Win32Window(hwnd);
            if (!w.IsValid || !w.IsVisible) return false;
            if (IsOwnWindow(hwnd)) return false;
            if (w.HasStyle(WS_CHILD)) return false;
            if (w.HasExStyle(WS_EX_TOOLWINDOW)) return false;
            if (!w.IsRootOwner) return false;
            if (w.IsCloaked) return false;
            if (string.IsNullOrWhiteSpace(w.Title)) return false;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"IsManageableWindow({hwnd}) ошибка.", ex);
            return false;
        }
    }

    public IReadOnlyList<Win32Window> EnumerateManageableTopLevelWindows()
    {
        var result = new List<Win32Window>();
        EnumWindows((hwnd, _) =>
        {
            if (IsManageableWindow(hwnd))
                result.Add(new Win32Window(hwnd));
            return true; // продолжать перебор
        }, 0);
        return result;
    }

    public bool TryHide(nint hwnd, out HiddenWindowInfo? info)
    {
        info = null;
        try
        {
            if (!IsAlive(hwnd)) return false;

            var w = new Win32Window(hwnd);

            // Захватываем состояние ПЕРЕД скрытием.
            var placement = WINDOWPLACEMENT.CreateEmpty();
            GetWindowPlacement(hwnd, ref placement);

            int pid = w.ProcessId;
            string procName = TryGetProcessName(pid);
            string title = w.Title;
            var icon = IconHelper.GetWindowIcon(hwnd);

            if (!ShowWindow(hwnd, SW_HIDE))
            {
                // ShowWindow вернул false, если окно уже было скрыто — проверяем фактическое состояние.
                if (new Win32Window(hwnd).IsVisible)
                {
                    Logger.Warn($"Не удалось скрыть окно '{title}' (hwnd={hwnd}).");
                    return false;
                }
            }

            info = new HiddenWindowInfo
            {
                Hwnd = hwnd,
                ProcessId = pid,
                ProcessName = procName,
                Title = title,
                OriginalPlacement = placement,
                HiddenAtUtc = DateTime.UtcNow,
                Icon = icon
            };
            Logger.Info($"Скрыто окно '{title}' ({procName}, hwnd={hwnd}).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"TryHide({hwnd}) ошибка.", ex);
            return false;
        }
    }

    public bool TryRestore(HiddenWindowInfo info)
    {
        try
        {
            var hwnd = info.Hwnd;
            if (!IsAlive(hwnd))
            {
                Logger.Warn($"Окно '{info.Title}' уже не существует — восстановление пропущено.");
                return false;
            }

            // Снова делаем окно видимым.
            ShowWindow(hwnd, SW_SHOW);

            // Восстанавливаем точное положение/состояние. Если оригинал был свёрнут —
            // показываем нормально, чтобы пользователь увидел окно.
            var placement = info.OriginalPlacement;
            if (placement.length == 0) placement.length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>();

            if (placement.showCmd == SW_SHOWMINIMIZED)
                placement.showCmd = SW_SHOWNORMAL;

            SetWindowPlacement(hwnd, ref placement);

            // Если по какой-то причине placement не сработал — гарантируем видимость.
            if (new Win32Window(hwnd).IsMinimized)
                ShowWindow(hwnd, SW_RESTORE);

            SetForegroundWindow(hwnd);
            Logger.Info($"Восстановлено окно '{info.Title}' (hwnd={hwnd}).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"TryRestore({info.Hwnd}) ошибка.", ex);
            return false;
        }
    }

    private static string TryGetProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}
