using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Infrastructure;

/// <summary>
/// Извлекает иконку окна как WPF <see cref="ImageSource"/> без System.Drawing.
/// Используется только неблокирующий GetClassLongPtr (не шлёт сообщений чужому окну),
/// поэтому безопасно даже для зависших приложений. Иконка опциональна — при ошибке null.
/// </summary>
public static class IconHelper
{
    public static ImageSource? GetWindowIcon(nint hwnd)
    {
        try
        {
            nint hIcon = GetClassLongPtr(hwnd, GCLP_HICONSM);
            if (hIcon == 0) hIcon = GetClassLongPtr(hwnd, GCLP_HICON);
            if (hIcon == 0) return null;

            var src = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }
}
