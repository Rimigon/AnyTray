using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AnyTray.Native;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Views;

/// <summary>
/// Маленькое окно-меню «Свернуть в трей» у курсора (по среднему клику на заголовке).
/// Это НАСТОЯЩЕЕ активируемое окно — поэтому надёжно закрывается по клику в любое
/// другое место (Deactivated) и по Esc, в отличие от «отвязанного» ContextMenu.
/// </summary>
public partial class HideMenuWindow : Window
{
    private readonly Action _onHide;

    public HideMenuWindow(string title, Action onHide)
    {
        InitializeComponent();
        _onHide = onHide;
        TitleText.Text = title;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Позиционируем верх-лево у курсора (в физических пикселях, без DIP-математики).
        var hwnd = new WindowInteropHelper(this).Handle;
        if (GetCursorPos(out POINT p))
        {
            SetWindowPos(hwnd, HWND_TOPMOST, p.X, p.Y, 0, 0,
                SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }
        Opacity = 1;       // показываем уже на нужном месте (без «прыжка»)
        Activate();        // чтобы сработал Deactivated при клике мимо

        // Подписываемся на Deactivated ТОЛЬКО после активации — иначе возможное
        // дребезжание фокуса при показе закрыло бы меню сразу.
        Deactivated += (_, _) => Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        _onHide();
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
