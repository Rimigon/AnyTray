using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AnyTray.Infrastructure;
using AnyTray.Models;
using H.NotifyIcon;

namespace AnyTray.Services;

public interface ITrayService : IDisposable
{
    void Initialize(ObservableCollection<HiddenWindowInfo> hidden);
    void ShowBalloon(string title, string message);

    /// <summary>Поставщик списка открытых окон для подменю «Скрыть окно ▸» (вызывается при открытии меню).</summary>
    Func<IReadOnlyList<OpenWindowInfo>>? OpenWindowsProvider { get; set; }

    event EventHandler<HiddenWindowInfo>? RestoreRequested;
    event EventHandler? RestoreAllRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? ExitRequested;
    event EventHandler? HideForegroundRequested;
    /// <summary>Запрос скрыть конкретное окно (выбранное из списка открытых). hwnd окна.</summary>
    event EventHandler<nint>? HideWindowRequested;
}

/// <summary>
/// Tray-иконка (H.NotifyIcon) + динамическое контекстное меню со списком скрытых окон.
/// Иконка рисуется средствами WPF (без бинарного .ico). Всё работает на UI-потоке.
/// </summary>
public sealed class TrayService : ITrayService
{
    private TaskbarIcon? _icon;
    private ObservableCollection<HiddenWindowInfo>? _hidden;

    public Func<IReadOnlyList<OpenWindowInfo>>? OpenWindowsProvider { get; set; }

    public event EventHandler<HiddenWindowInfo>? RestoreRequested;
    public event EventHandler? RestoreAllRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? HideForegroundRequested;
    public event EventHandler<nint>? HideWindowRequested;

    public void Initialize(ObservableCollection<HiddenWindowInfo> hidden)
    {
        _hidden = hidden;
        _hidden.CollectionChanged += OnHiddenChanged;

        _icon = new TaskbarIcon
        {
            ToolTipText = "AnyTray",
            ContextMenu = BuildMenu()
        };

        // Фирменный логотип из встроенного ресурса; при сбое — генерируемый глиф.
        var logo = LoadLogoIcon();
        if (logo is not null)
            _icon.Icon = logo;
        else
            _icon.IconSource = CreateFallbackIcon();

        // Двойной клик по иконке открывает настройки.
        _icon.TrayMouseDoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _icon.ForceCreate();

        Logger.Info("TrayService инициализирован.");
    }

    private void OnHiddenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_icon is not null)
        {
            _icon.ContextMenu = BuildMenu();
            int count = _hidden?.Count ?? 0;
            _icon.ToolTipText = count == 0 ? "AnyTray" : $"AnyTray — скрыто окон: {count}";
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        menu.Items.Add(new MenuItem
        {
            Header = "Скрыть активное окно",
            Command = new RelayActionCommand(() => HideForegroundRequested?.Invoke(this, EventArgs.Empty))
        });

        // Подменю со списком ВСЕХ открытых окон — работает для любых приложений
        // (в т.ч. без overlay-кнопки: Telegram, VS Code, Word). Наполняется при открытии.
        var hideMenu = new MenuItem { Header = "Скрыть окно" };
        hideMenu.Items.Add(new MenuItem { Header = "(наведите для загрузки…)", IsEnabled = false });
        hideMenu.SubmenuOpened += (_, _) => PopulateHideSubmenu(hideMenu);
        menu.Items.Add(hideMenu);

        menu.Items.Add(new Separator());

        bool hasHidden = _hidden is { Count: > 0 };

        var header = new MenuItem { Header = "Скрытые окна", IsEnabled = false };
        menu.Items.Add(header);

        if (hasHidden)
        {
            foreach (var info in _hidden!)
            {
                var item = new MenuItem
                {
                    Header = info.DisplayName,
                    Command = new RelayActionCommand(() => RestoreRequested?.Invoke(this, info))
                };
                if (info.Icon is not null)
                    item.Icon = new Image { Source = info.Icon, Width = 16, Height = 16 };
                menu.Items.Add(item);
            }
        }
        else
        {
            menu.Items.Add(new MenuItem { Header = "(пусто)", IsEnabled = false });
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "Восстановить все",
            IsEnabled = hasHidden,
            Command = new RelayActionCommand(() => RestoreAllRequested?.Invoke(this, EventArgs.Empty))
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "Настройки…",
            Command = new RelayActionCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Выход",
            Command = new RelayActionCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty))
        });

        return menu;
    }

    /// <summary>Перестраивает подменю «Скрыть окно ▸» актуальным списком открытых окон.</summary>
    private void PopulateHideSubmenu(MenuItem hideMenu)
    {
        hideMenu.Items.Clear();

        var windows = OpenWindowsProvider?.Invoke() ?? Array.Empty<OpenWindowInfo>();
        if (windows.Count == 0)
        {
            hideMenu.Items.Add(new MenuItem { Header = "(нет доступных окон)", IsEnabled = false });
            return;
        }

        foreach (var win in windows)
        {
            var hwnd = win.Hwnd;
            var item = new MenuItem
            {
                Header = win.DisplayName,
                Command = new RelayActionCommand(() => HideWindowRequested?.Invoke(this, hwnd))
            };
            if (win.Icon is not null)
                item.Icon = new Image { Source = win.Icon, Width = 16, Height = 16 };
            hideMenu.Items.Add(item);
        }
    }

    public void ShowBalloon(string title, string message)
    {
        try { _icon?.ShowNotification(title, message); }
        catch (Exception ex) { Logger.Error("ShowBalloon ошибка.", ex); }
    }

    /// <summary>Грузит фирменный логотип из встроенного ресурса в System.Drawing.Icon (для трея).</summary>
    private static System.Drawing.Icon? LoadLogoIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/anytray.ico"));
            if (info?.Stream is null) return null;
            using var s = info.Stream;
            return new System.Drawing.Icon(s, new System.Drawing.Size(32, 32));
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось загрузить логотип трея — используется запасной глиф.", ex);
            return null;
        }
    }

    /// <summary>Запасная иконка трея (если ресурс недоступен): стрелка вверх в изумрудно-циановом градиенте.</summary>
    private static GeneratedIconSource CreateFallbackIcon() => new()
    {
        Text = "↑",
        Size = 64,
        FontFamily = new FontFamily("Segoe UI Symbol"),
        FontSize = 44,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Colors.White),
        Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(0x10, 0xB9, 0x81), 0),  // emerald
                new GradientStop(Color.FromRgb(0x06, 0xB4, 0xD4), 1),  // cyan
            },
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 1))
    };

    public void Dispose()
    {
        if (_hidden is not null) _hidden.CollectionChanged -= OnHiddenChanged;
        _icon?.Dispose();
        _icon = null;
    }
}

/// <summary>Лёгкая ICommand-обёртка для пунктов меню (без зависимости от ViewModel-команд).</summary>
internal sealed class RelayActionCommand : System.Windows.Input.ICommand
{
    private readonly Action _action;
    public RelayActionCommand(Action action) => _action = action;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
