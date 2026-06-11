using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AnyTray.Infrastructure;
using AnyTray.Models;

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
/// Tray-иконка через проверенный System.Windows.Forms.NotifyIcon.
/// H.NotifyIcon.Wpf нестабилен на .NET 8 — нативный API работает надёжно.
/// </summary>
public sealed class TrayService : ITrayService
{
    private NotifyIcon? _notifyIcon;
    private ObservableCollection<HiddenWindowInfo>? _hidden;
    private DispatcherTimer? _menuRebuildTimer;
    private bool _disposed;

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

        _menuRebuildTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _menuRebuildTimer.Tick += (_, _) => { _menuRebuildTimer?.Stop(); RebuildMenu(); };

        _notifyIcon = new NotifyIcon
        {
            Text = "AnyTray",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        var logo = LoadLogoIcon();
        var fallback = CreateFallbackIcon();
        _notifyIcon.Icon = logo ?? fallback;
        Logger.Info($"Tray иконка: {(logo is not null ? "logo (AnyTray.ico)" : "fallback (generated)")}.");

        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                SettingsRequested?.Invoke(this, EventArgs.Empty);
        };

        Logger.Info("TrayService инициализирован.");

        // Тестовый balloon — если он появится, значит иконка создана (возможно, скрыта в overflow).
        _notifyIcon.ShowBalloonTip(5000, "AnyTray", "Иконка в трее активна. Если вы это видите — всё работает!", ToolTipIcon.Info);
    }

    private void OnHiddenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_menuRebuildTimer is not null)
        {
            _menuRebuildTimer.Stop();
            _menuRebuildTimer.Start();
        }
    }

    private void RebuildMenu()
    {
        if (_notifyIcon is not null && _hidden is not null)
        {
            _notifyIcon.ContextMenuStrip = BuildMenu();
            int count = _hidden.Count;
            _notifyIcon.Text = count == 0 ? "AnyTray" : $"AnyTray — скрыто окон: {count}";
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var hideActiveItem = new ToolStripMenuItem("Скрыть активное окно");
        hideActiveItem.Click += (_, _) => HideForegroundRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(hideActiveItem);

        var hideMenu = new ToolStripMenuItem("Скрыть окно");
        var loadingItem = new ToolStripMenuItem("(наведите для загрузки…)") { Enabled = false };
        hideMenu.DropDownItems.Add(loadingItem);
        hideMenu.DropDownOpening += (_, _) => PopulateHideSubmenu(hideMenu);
        menu.Items.Add(hideMenu);

        menu.Items.Add(new ToolStripSeparator());

        bool hasHidden = _hidden is { Count: > 0 };

        var header = new ToolStripMenuItem("Скрытые окна") { Enabled = false };
        menu.Items.Add(header);

        if (hasHidden)
        {
            foreach (var info in _hidden!)
            {
                var captured = info;
                var item = new ToolStripMenuItem(captured.DisplayName);
                item.Image = ToDrawingImage(captured.Icon);
                item.Click += (_, _) => RestoreRequested?.Invoke(this, captured);
                menu.Items.Add(item);
            }
        }
        else
        {
            menu.Items.Add(new ToolStripMenuItem("(пусто)") { Enabled = false });
        }

        menu.Items.Add(new ToolStripSeparator());
        var restoreAllItem = new ToolStripMenuItem("Восстановить все") { Enabled = hasHidden };
        restoreAllItem.Click += (_, _) => RestoreAllRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(restoreAllItem);
        menu.Items.Add(new ToolStripSeparator());
        var settingsItem = new ToolStripMenuItem("Настройки…");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void PopulateHideSubmenu(ToolStripMenuItem hideMenu)
    {
        hideMenu.DropDownItems.Clear();

        var windows = OpenWindowsProvider?.Invoke() ?? Array.Empty<OpenWindowInfo>();
        if (windows.Count == 0)
        {
            hideMenu.DropDownItems.Add(new ToolStripMenuItem("(нет доступных окон)") { Enabled = false });
            return;
        }

        foreach (var win in windows)
        {
            var capturedHwnd = win.Hwnd;
            var item = new ToolStripMenuItem(win.DisplayName);
            item.Image = ToDrawingImage(win.Icon);
            item.Click += (_, _) => HideWindowRequested?.Invoke(this, capturedHwnd);
            hideMenu.DropDownItems.Add(item);
        }
    }

    public void ShowBalloon(string title, string message)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Logger.Error("ShowBalloon ошибка.", ex);
        }
    }

    /// <summary>
    /// Грузит фирменный логотип из встроенного ресурса как нативную System.Drawing.Icon.
    /// Копирует stream в MemoryStream, чтобы Icon не зависел от жизни оригинального ресурса.
    /// </summary>
    private static System.Drawing.Icon? LoadLogoIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/anytray.ico"));
            if (info?.Stream is null) return null;
            using var src = info.Stream;
            using var ms = new MemoryStream();
            src.CopyTo(ms);
            ms.Position = 0;
            return new System.Drawing.Icon(ms);
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось загрузить логотип трея — используется запасная иконка.", ex);
            return null;
        }
    }

    /// <summary>Запасная иконка трея (если ресурс недоступен): сгенерированная программно 32×32.</summary>
    private static System.Drawing.Icon CreateFallbackIcon()
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(255, 8, 148, 178)); // teal
                using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 3);
                g.DrawRectangle(pen, 4, 4, 23, 23);
                // Внутренний крест/стрелка — символизирует «свернуть вниз»
                using var pen2 = new System.Drawing.Pen(System.Drawing.Color.White, 2);
                g.DrawLine(pen2, 16, 10, 16, 22);
                g.DrawLine(pen2, 12, 18, 16, 22);
                g.DrawLine(pen2, 20, 18, 16, 22);
            }
            nint hIcon = bmp.GetHicon();
            try
            {
                using var temp = System.Drawing.Icon.FromHandle(hIcon);
                return (System.Drawing.Icon)temp.Clone();
            }
            finally
            {
                Native.NativeMethods.DestroyIcon(hIcon);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сгенерировать fallback-иконку трея — используется SystemIcons.Application.", ex);
            return System.Drawing.SystemIcons.Application;
        }
    }

    /// <summary>Преобразует WPF ImageSource в System.Drawing.Image для WinForms меню.</summary>
    private static System.Drawing.Image? ToDrawingImage(ImageSource? source)
    {
        if (source == null) return null;
        try
        {
            if (source is BitmapSource bmp)
            {
                using var ms = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(ms);
                ms.Position = 0;
                return System.Drawing.Image.FromStream(ms);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Не удалось конвертировать иконку для меню трея: {ex.Message}");
        }
        return null;
    }

    public void PrepareShutdown() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hidden is not null) _hidden.CollectionChanged -= OnHiddenChanged;
        _menuRebuildTimer?.Stop();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
