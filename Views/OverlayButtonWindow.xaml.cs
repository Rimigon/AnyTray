using System.Windows;
using System.Windows.Interop;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Views;

/// <summary>
/// Borderless / topmost / transparent окно-оверлей с единственной кнопкой «Hide to Tray».
/// Позиционируется и показывается из <see cref="Services.OverlayButtonService"/>.
/// </summary>
public partial class OverlayButtonWindow : Window
{
    /// <summary>Вызывается при клике по кнопке.</summary>
    public event EventHandler? HideClicked;

    public OverlayButtonWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;

        var hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        // TOOLWINDOW — вне Alt-Tab и панели задач; NOACTIVATE — клик не отбирает фокус у цели.
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
        => HideClicked?.Invoke(this, EventArgs.Empty);
}
