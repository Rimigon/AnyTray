namespace AnyTray.Native;

/// <summary>
/// Все Win32-константы, сгруппированные по назначению. Без логики.
/// </summary>
public static class NativeConstants
{
    // ---- ShowWindow nCmdShow ----
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWNA = 8;
    public const int SW_RESTORE = 9;

    // ---- GetWindowLong indices ----
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // ---- Window styles (WS_*) ----
    public const long WS_CAPTION = 0x00C00000;
    public const long WS_SYSMENU = 0x00080000;
    public const long WS_THICKFRAME = 0x00040000;
    public const long WS_VISIBLE = 0x10000000;
    public const long WS_CHILD = 0x40000000;
    public const long WS_POPUP = 0x80000000;

    // ---- Extended window styles (WS_EX_*) ----
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;
    public const long WS_EX_LAYERED = 0x00080000;
    public const long WS_EX_TRANSPARENT = 0x00000020;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const long WS_EX_TOPMOST = 0x00000008;

    // ---- Hotkey modifiers ----
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;
    public const int WM_GETICON = 0x007F;

    // ---- Low-level mouse hook (WH_MOUSE_LL) ----
    public const int WH_MOUSE_LL = 14;
    public const int HC_ACTION = 0;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_MBUTTONDBLCLK = 0x0209;

    // WM_GETICON wParam
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // GetClassLongPtr indices
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // ---- DWM window attributes ----
    // ВАЖНО: реальные значения из enum DWMWINDOWATTRIBUTE.
    public const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;   // границы группы системных кнопок заголовка
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    // DwmGetWindowAttribute(DWMWA_CLOAKED) flags
    public const int DWM_CLOAKED_APP = 0x0000001;
    public const int DWM_CLOAKED_SHELL = 0x0000002;
    public const int DWM_CLOAKED_INHERITED = 0x0000004;

    public const int S_OK = 0;

    // ---- GetSystemMetrics ----
    public const int SM_CXSIZE = 30;
    public const int SM_CYSIZE = 31;
    public const int SM_CXFRAME = 32;
    public const int SM_CYFRAME = 33;
    public const int SM_CXPADDEDBORDER = 92;

    // ---- Monitor / DPI ----
    public const int MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;

    // ---- SetWindowPos ----
    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_NOTOPMOST = -2;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    // ---- WinEvent constants ----
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;

    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;

    // GetAncestor
    public const uint GA_PARENT = 1;
    public const uint GA_ROOT = 2;
    public const uint GA_ROOTOWNER = 3;

    // GetWindow
    public const uint GW_OWNER = 4;

    // Special parent for message-only windows
    public static readonly nint HWND_MESSAGE = -3;
}
