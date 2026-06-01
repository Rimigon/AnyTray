using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Infrastructure;

/// <summary>
/// Помощник для перевода между логическими DIP (WPF) и физическими пикселями (Win32),
/// с учётом per-monitor DPI. Позиционирование overlay делается в физ. пикселях
/// (через SetWindowPos), поэтому DPL-математика нужна только для размеров.
/// </summary>
public static class DpiHelper
{
    public const double DefaultDpi = 96.0;

    /// <summary>Эффективный масштаб монитора, на котором находится окно (1.0 = 100%).</summary>
    public static double GetScaleForWindow(nint hwnd)
    {
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi == 0)
            {
                // Fallback: через монитор.
                nint mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (mon != 0 && GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dx, out _) == S_OK && dx != 0)
                    dpi = dx;
            }
            return dpi == 0 ? 1.0 : dpi / DefaultDpi;
        }
        catch
        {
            return 1.0;
        }
    }

    /// <summary>Перевод размера в DIP → физические пиксели для данного окна.</summary>
    public static int DipToPixels(double dip, double scale) => (int)Math.Round(dip * scale);
}
