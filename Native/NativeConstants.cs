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
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;

    // ---- GetWindowLong indices ----
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // ---- Window styles (WS_*) ----
    public const long WS_CAPTION = 0x00C00000;
    public const long WS_SYSMENU = 0x00080000;
    public const long WS_CHILD = 0x40000000;

    // ---- Extended window styles (WS_EX_*) ----
    public const long WS_EX_TOOLWINDOW = 0x00000080;

    // ---- Hotkey modifiers ----
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;
    public const int WM_GETICON = 0x007F;
    public const int WM_NULL = 0x0000;

    // ---- Low-level mouse hook (WH_MOUSE_LL) ----
    public const int WH_MOUSE_LL = 14;
    public const int HC_ACTION = 0;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_MBUTTONDBLCLK = 0x0209;

    // MSLLHOOKSTRUCT.flags — признак синтетического (инжектированного) события.
    public const uint LLMHF_INJECTED = 0x0001;

    // WM_GETICON wParam
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // SendMessageTimeout flags
    public const uint SMTO_ABORTIFHUNG = 0x0008;
    public const uint GetIconTimeoutMs = 250; // безопасный таймаут для WM_GETICON зависшему окну

    // GetClassLongPtr indices
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // ---- DWM window attributes (значения из enum DWMWINDOWATTRIBUTE) ----
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    public const int S_OK = 0;

    // ---- Monitor / DPI ----
    public const int MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;

    // ---- SetWindowPos ----
    public static readonly nint HWND_TOPMOST = -1;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    // GetAncestor
    public const uint GA_ROOT = 2;
    public const uint GA_ROOTOWNER = 3;

    // Special parent for message-only windows
    public static readonly nint HWND_MESSAGE = -3;
}