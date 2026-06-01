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
    public bool IsVisible => IsWindowVisible(Handle);
    public bool IsMinimized => IsIconic(Handle);
    public bool IsMaximized => IsZoomed(Handle);

    public long Style => GetWindowLongPtr(Handle, GWL_STYLE).ToInt64();
    public long ExStyle => GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64();

    public bool HasStyle(long flag) => (Style & flag) == flag;
    public bool HasExStyle(long flag) => (ExStyle & flag) == flag;

    public int ProcessId
    {
        get
        {
            GetWindowThreadProcessId(Handle, out uint pid);
            return (int)pid;
        }
    }

    public string Title
    {
        get
        {
            int len = GetWindowTextLength(Handle);
            if (len <= 0) return string.Empty;
            var sb = new StringBuilder(len + 1);
            GetWindowText(Handle, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    public string ClassName
    {
        get
        {
            var sb = new StringBuilder(256);
            GetClassName(Handle, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    /// <summary>Окно скрыто DWM (UWP в фоне, скрыто на другом виртуальном рабочем столе и т.п.).</summary>
    public bool IsCloaked
    {
        get
        {
            int hr = DwmGetWindowAttribute(Handle, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            return hr == S_OK && cloaked != 0;
        }
    }

    /// <summary>true, если это корневое окно без владельца (а не дочерний popup/tool-окно).</summary>
    public bool IsRootOwner => GetAncestor(Handle, GA_ROOTOWNER) == Handle;

    public bool TryGetWindowRect(out RECT rect) => GetWindowRect(Handle, out rect);

    /// <summary>Реальные видимые границы окна с учётом DWM (без невидимой рамки ресайза).</summary>
    public bool TryGetExtendedFrameBounds(out RECT rect)
    {
        int hr = DwmGetWindowAttribute(Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>());
        return hr == S_OK;
    }

    /// <summary>Rect группы кнопок заголовка (Win11+) относительно origin окна (GetWindowRect).</summary>
    public bool TryGetCaptionButtonBounds(out RECT rect)
    {
        int hr = DwmGetWindowAttribute(Handle, DWMWA_CAPTION_BUTTON_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>());
        return hr == S_OK && rect.Width > 0 && rect.Height > 0;
    }
}
