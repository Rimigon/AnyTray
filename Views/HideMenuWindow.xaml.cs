using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AnyTray.Infrastructure;
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
    private bool _closing;

    public HideMenuWindow(string title, Action onHide)
    {
        InitializeComponent();
        _onHide = onHide;
        TitleText.Text = title;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (GetCursorPos(out POINT p))
        {
            // Учитываем DPI и размеры окна, чтобы не вылезти за пределы экрана.
            double scale = DpiHelper.GetScaleForWindow(hwnd);
            int w = DpiHelper.DipToPixels(ActualWidth, scale);
            int h = DpiHelper.DipToPixels(ActualHeight, scale);

            nint mon = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
            var mi = MONITORINFO.Create();
            if (mon != 0 && GetMonitorInfo(mon, ref mi))
            {
                int left = Math.Max(mi.rcWork.Left, Math.Min(p.X, mi.rcWork.Right - w));
                int top = Math.Max(mi.rcWork.Top, Math.Min(p.Y, mi.rcWork.Bottom - h));
                SetWindowPos(hwnd, HWND_TOPMOST, left, top, 0, 0,
                    SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
            else
            {
                SetWindowPos(hwnd, HWND_TOPMOST, p.X, p.Y, 0, 0,
                    SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
        }
        Opacity = 1;       // показываем уже на нужном месте (без «прыжка»)

        // Активацию и подписку на Deactivated откладываем в следующий кадр Dispatcher,
        // иначе возможна гонка: событие активации/деактивации при загрузке вызовет Close()
        // внутри обработчика Loaded, что приводит к InvalidOperationException.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_closing) return;
            Activate();        // чтобы сработал Deactivated при клике мимо
            Deactivated += (_, _) => { if (!_closing) Close(); };
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
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
