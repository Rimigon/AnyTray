using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Infrastructure;

/// <summary>
/// Извлекает иконку окна как WPF <see cref="ImageSource"/> без System.Drawing.
/// Сначала неблокирующий <c>GetClassLongPtr</c> (не шлёт сообщений, безопасен для зависших
/// окон), затем fallback на <c>WM_GETICON</c> через <c>SendMessageTimeout</c> с
/// <c>SMTO_ABORTIFHUNG</c> — это покрывает Electron/VS Code/Telegram и прочие приложения,
/// которые выставляют иконку через WM_SETICON, а не в классе. Иконка опциональна.
/// </summary>
public static class IconHelper
{
    public static ImageSource? GetWindowIcon(nint hwnd)
    {
        try
        {
            nint hIcon = GetClassLongPtr(hwnd, GCLP_HICONSM);
            if (hIcon == 0) hIcon = GetClassLongPtr(hwnd, GCLP_HICON);
            if (hIcon == 0) hIcon = QueryIconViaTimeout(hwnd, ICON_SMALL2);
            if (hIcon == 0) hIcon = QueryIconViaTimeout(hwnd, ICON_SMALL);
            if (hIcon == 0) hIcon = QueryIconViaTimeout(hwnd, ICON_BIG);
            if (hIcon == 0) return null;

            // Создаём собственную копию — shared-иконку класса нельзя DestroyIcon.
            nint hCopy = CopyIcon(hIcon);
            if (hCopy == 0) return null;

            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    hCopy,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                DestroyIcon(hCopy);
            }
        }
        catch
        {
            Logger.Warn($"GetWindowIcon({hwnd}) не удалось.");
            return null;
        }
    }

    /// <summary>
    /// Запрашивает иконку через WM_GETICON с таймаутом — не блокирует UI при зависшем окне
    /// (SMTO_ABORTIFHUNG). Возвращает HICON или 0.
    /// </summary>
    private static nint QueryIconViaTimeout(nint hwnd, int iconType)
    {
        try
        {
            nint result = SendMessageTimeout(hwnd, WM_GETICON, iconType, 0,
                SMTO_ABORTIFHUNG, GetIconTimeoutMs, out _);
            return result;
        }
        catch
        {
            return 0;
        }
    }
}
