using System.Text;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Native;

/// <summary>
/// Тонкая read-only обёртка над hwnd. Все значения вычисляются по запросу
/// (без кэширования) — стили и заголовок чужого окна могут меняться в любой момент.
/// </summary>
public readonly struct Win32Window
{
    public nint Handle { get; }

    public Win32Window(nint handle) => Handle = handle;

    public bool IsValid => Handle != 0 && IsWindow(Handle);
    public bool IsVisible { get { try { return IsWindowVisible(Handle); } catch { return false; } } }
    public bool IsMinimized { get { try { return IsIconic(Handle); } catch { return false; } } }
    public bool IsMaximized { get { try { return IsZoomed(Handle); } catch { return false; } } }

    public long Style { get { try { return GetWindowLongPtr(Handle, GWL_STYLE).ToInt64(); } catch { return 0; } } }
    public long ExStyle { get { try { return GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64(); } catch { return 0; } } }

    public bool HasStyle(long flag) { try { return (Style & flag) == flag; } catch { return false; } }
    public bool HasExStyle(long flag) { try { return (ExStyle & flag) == flag; } catch { return false; } }

    public int ProcessId
    {
        get
        {
            try
            {
                GetWindowThreadProcessId(Handle, out uint pid);
                return (int)pid;
            }
            catch
            {
                return 0;
            }
        }
    }

    public string Title
    {
        get
        {
            try
            {
                int len = GetWindowTextLength(Handle);
                if (len <= 0) return string.Empty;
                if (len > 4096) len = 4096;
                var sb = new StringBuilder(len + 1);
                GetWindowText(Handle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public string ClassName
    {
        get
        {
            try
            {
                var sb = new StringBuilder(256);
                GetClassName(Handle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>Окно скрыто DWM (UWP в фоне, скрыто на другом виртуальном рабочем столе и т.п.).</summary>
    public bool IsCloaked
    {
        get
        {
            try
            {
                int hr = DwmGetWindowAttribute(Handle, DWMWA_CLOAKED, out int cloaked, sizeof(int));
                return hr == S_OK && cloaked != 0;
            }
            catch { return false; }
        }
    }

    /// <summary>true, если это корневое окно без владельца (а не дочерний popup/tool-окно).</summary>
    public bool IsRootOwner { get { try { return GetAncestor(Handle, GA_ROOTOWNER) == Handle; } catch { return false; } } }

    public bool TryGetWindowRect(out RECT rect) { try { return GetWindowRect(Handle, out rect); } catch { rect = default; return false; } }

    /// <summary>Реальные видимые границы окна с учётом DWM (без невидимой рамки ресайза).</summary>
    public bool TryGetExtendedFrameBounds(out RECT rect)
    {
        try
        {
            int hr = DwmGetWindowAttribute(Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>());
            return hr == S_OK;
        }
        catch
        {
            rect = default;
            return false;
        }
    }
}
