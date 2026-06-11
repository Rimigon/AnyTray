using System.Windows;
using System.Windows.Input;
using AnyTray.Models;
using AnyTray.ViewModels;

namespace AnyTray.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.CloseRequested += OnCloseRequested;
        Closing += (_, _) => { _vm.CloseRequested -= OnCloseRequested; };
    }

    private void OnCloseRequested(bool saved)
    {
        _vm.CloseRequested -= OnCloseRequested;
        Close();
    }

    /// <summary>Захват сочетания клавиш в поле горячей клавиши.</summary>
    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Игнорируем нажатие одних только модификаторов.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None) return; // требуем хотя бы один модификатор

        e.Handled = true;
        _vm.Hotkey = new HotkeyDefinition(mods, key);
    }
}
