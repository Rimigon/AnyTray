using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnyTray.Infrastructure;
using AnyTray.Models;
using AnyTray.Native;
using AnyTray.Services;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.ViewModels;

/// <summary>
/// «Мозг» приложения: владеет коллекцией скрытых окон и сценариями hide/restore,
/// к которым сходятся все источники команд (hotkey, средний клик по заголовку, tray-меню).
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IWindowManager _windowManager;
    private readonly ITrayService _tray;
    private readonly IHotkeyService _hotkey;
    private readonly ISettingsService _settings;
    private readonly IAutostartService _autostart;
    private readonly IProcessWatcher _watcher;
    private readonly ISessionStateService _session;
    private readonly IMouseHookService _mouseHook;
    private bool _disposed;
    private HotkeyDefinition _currentHotkey = HotkeyDefinition.Default;

    // Сохранённые делегаты для корректной отписки (lambda каждый раз создаёт новый instance).
    private EventHandler? _trayHideForegroundHandler;
    private EventHandler<nint>? _trayHideWindowHandler;
    private EventHandler<HiddenWindowInfo>? _trayRestoreHandler;
    private EventHandler? _trayRestoreAllHandler;
    private EventHandler? _traySettingsHandler;
    private EventHandler? _trayExitHandler;
    private EventHandler<nint>? _watcherWindowGoneHandler;
    private EventHandler<nint>? _mouseHookMiddleClickHandler;
    private EventHandler<AppSettings>? _settingsChangedHandler;

    public ObservableCollection<HiddenWindowInfo> HiddenWindows { get; } = new();

    public IRelayCommand HideActiveWindowCommand { get; }
    public IRelayCommand<HiddenWindowInfo> RestoreCommand { get; }
    public IRelayCommand RestoreAllCommand { get; }
    public IRelayCommand ExitCommand { get; }

    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    /// <summary>Запрос показать мини-меню скрытия у курсора (средний клик по заголовку). hwnd окна.</summary>
    public event EventHandler<nint>? HideMenuRequested;

    public MainViewModel(
        IWindowManager windowManager, ITrayService tray, IHotkeyService hotkey,
        ISettingsService settings, IAutostartService autostart,
        IProcessWatcher watcher, ISessionStateService session, IMouseHookService mouseHook)
    {
        _windowManager = windowManager;
        _tray = tray;
        _hotkey = hotkey;
        _settings = settings;
        _autostart = autostart;
        _watcher = watcher;
        _session = session;
        _mouseHook = mouseHook;

        HideActiveWindowCommand = new RelayCommand(HideActiveWindow);
        RestoreCommand = new RelayCommand<HiddenWindowInfo>(info => { if (info is not null) Restore(info); });
        RestoreAllCommand = new RelayCommand(RestoreAll);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public void Initialize()
    {
        // Tray
        _tray.Initialize(HiddenWindows);
        _tray.OpenWindowsProvider = GetHideableWindows;

        _trayHideForegroundHandler = (_, _) => HideActiveWindow();
        _tray.HideForegroundRequested += _trayHideForegroundHandler;

        _trayHideWindowHandler = (_, hwnd) => HideWindow(hwnd);
        _tray.HideWindowRequested += _trayHideWindowHandler;

        _trayRestoreHandler = (_, info) => Restore(info);
        _tray.RestoreRequested += _trayRestoreHandler;

        _trayRestoreAllHandler = (_, _) => RestoreAll();
        _tray.RestoreAllRequested += _trayRestoreAllHandler;

        _traySettingsHandler = (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _tray.SettingsRequested += _traySettingsHandler;

        _trayExitHandler = (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _tray.ExitRequested += _trayExitHandler;

        // Hotkey
        _hotkey.Initialize();

        // ProcessWatcher
        _watcherWindowGoneHandler = (_, hwnd) => OnWindowGone(hwnd);
        _watcher.WindowGone += _watcherWindowGoneHandler;

        // Средний клик по заголовку
        _mouseHookMiddleClickHandler = (_, hwnd) => OnTitleBarMiddleClick(hwnd);
        _mouseHook.TitleBarMiddleClick += _mouseHookMiddleClickHandler;

        // Settings
        _settingsChangedHandler = (_, s) => ApplySettings(s);
        _settings.SettingsChanged += _settingsChangedHandler;

        ApplySettings(_settings.Current);
        RecoverCrashedSession();
    }

    private void ApplySettings(AppSettings s)
    {
        // Горячая клавиша
        var def = s.GetHotkeyDefinition();
        if (!def.IsValid)
        {
            Logger.Warn($"Пропускаем невалидную горячую клавишу '{s.Hotkey}'.");
        }
        else if (!_hotkey.Register(def, HideActiveWindow))
        {
            _tray.ShowBalloon("AnyTray", $"Горячая клавиша {def} занята другим приложением.");
            // Пытаемся восстановить предыдущую валидную горячую клавишу, если она отличалась.
            if (_currentHotkey.IsValid && _currentHotkey.ToString() != def.ToString())
            {
                if (!_hotkey.Register(_currentHotkey, HideActiveWindow))
                    Logger.Warn($"Не удалось восстановить предыдущую горячую клавишу {_currentHotkey}.");
            }
        }
        else
        {
            _currentHotkey = def;
        }

        // Средний клик по заголовку
        _mouseHook.SetEnabled(s.TitleBarMiddleClickEnabled);

        // Автозапуск
        _autostart.SetEnabled(s.AutostartEnabled);
    }

    /// <summary>Средний клик по заголовку окна: сразу скрыть или показать мини-меню (по настройке).</summary>
    private void OnTitleBarMiddleClick(nint hwnd)
    {
        if (_settings.Current.TitleBarMiddleClickDirectHide)
            HideWindow(hwnd);
        else
            HideMenuRequested?.Invoke(this, hwnd);
    }

    // ---------------------------------------------------------------
    //  Hide
    // ---------------------------------------------------------------
    public void HideActiveWindow() => HideWindow(GetForegroundWindow());

    public void HideWindow(nint hwnd)
    {
        if (hwnd == 0) return;
        if (_windowManager.IsOwnWindow(hwnd)) return;
        if (HiddenWindows.Any(h => h.Hwnd == hwnd)) return;

        if (!_windowManager.IsManageableWindow(hwnd))
        {
            Logger.Warn($"Окно (hwnd={hwnd}) нельзя скрыть (не обычное desktop-окно).");
            _tray.ShowBalloon("AnyTray", "Это окно нельзя скрыть в трей.");
            return;
        }

        if (_windowManager.TryHide(hwnd, out var info) && info is not null)
        {
            HiddenWindows.Add(info);
            _watcher.Watch(info);
            _session.Persist(HiddenWindows);
        }
    }

    /// <summary>
    /// Список всех открытых окон, доступных для скрытия (для подменю «Скрыть окно ▸» в трее).
    /// Работает для ЛЮБЫХ приложений (Telegram, VS Code, Word). Уже скрытые окна исключаются.
    /// </summary>
    private IReadOnlyList<OpenWindowInfo> GetHideableWindows()
    {
        var hidden = HiddenWindows.Select(h => h.Hwnd).ToHashSet();
        var result = new List<OpenWindowInfo>();

        foreach (var w in _windowManager.EnumerateManageableTopLevelWindows())
        {
            if (hidden.Contains(w.Handle)) continue;

            string proc = "?";
            try { using var p = Process.GetProcessById(w.ProcessId); proc = p.ProcessName; }
            catch { }

            var title = w.Title;
            if (string.IsNullOrWhiteSpace(title)) title = "(без заголовка)";
            if (title.Length > 60) title = title[..57] + "…";

            try
            {
                result.Add(new OpenWindowInfo
                {
                    Hwnd = w.Handle,
                    DisplayName = $"{title}  —  {proc}",
                    Icon = IconHelper.GetWindowIcon(w.Handle)
                });
            }
            catch (Exception ex)
            {
                Logger.Warn($"Не удалось получить иконку окна '{title}' (hwnd={w.Handle}): {ex.Message}");
            }
        }

        return result
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ---------------------------------------------------------------
    //  Restore
    // ---------------------------------------------------------------
    public void Restore(HiddenWindowInfo info)
    {
        _windowManager.TryRestore(info); // даже при неудаче убираем из списка
        RemoveFromList(info.Hwnd);
    }

    public void RestoreAll()
    {
        foreach (var info in HiddenWindows.ToArray())
        {
            _windowManager.TryRestore(info);
            _watcher.Unwatch(info.Hwnd);
        }
        HiddenWindows.Clear();
        _session.Clear();
    }

    private void OnWindowGone(nint hwnd) => RemoveFromList(hwnd);

    private void RemoveFromList(nint hwnd)
    {
        var existing = HiddenWindows.FirstOrDefault(h => h.Hwnd == hwnd);
        if (existing is not null)
        {
            HiddenWindows.Remove(existing);
            _watcher.Unwatch(hwnd);
            _session.Persist(HiddenWindows);
        }
    }

    // ---------------------------------------------------------------
    //  Crash recovery
    // ---------------------------------------------------------------
    private void RecoverCrashedSession()
    {
        var orphans = _session.LoadOrphans();
        if (orphans.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"AnyTray обнаружил {orphans.Count} окно(окон), скрытых в прошлой сессии " +
            "(возможно, после аварийного завершения). Восстановить их сейчас?",
            "AnyTray — восстановление окон",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var o in orphans)
            {
                try
                {
                    var info = new HiddenWindowInfo
                    {
                        Hwnd = (nint)o.Hwnd,
                        ProcessId = o.ProcessId,
                        ProcessName = o.ProcessName,
                        Title = o.Title,
                        OriginalPlacement = o.Placement,
                        HiddenAtUtc = DateTime.UtcNow
                    };
                    _windowManager.TryRestore(info);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка восстановления осиротевшего окна '{o.Title}'.", ex);
                }
            }
        }
        else
        {
            // Пользователь отказался от автовосстановления — окна остаются скрытыми,
            // но мы добавляем их в список, чтобы их можно было восстановить вручную через трей.
            foreach (var o in orphans)
            {
                try
                {
                    var info = new HiddenWindowInfo
                    {
                        Hwnd = (nint)o.Hwnd,
                        ProcessId = o.ProcessId,
                        ProcessName = o.ProcessName,
                        Title = o.Title,
                        OriginalPlacement = o.Placement,
                        HiddenAtUtc = DateTime.UtcNow
                    };
                    HiddenWindows.Add(info);
                    _watcher.Watch(info);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка добавления осиротевшего окна '{o.Title}' в список.", ex);
                }
            }
            _session.Persist(HiddenWindows);
        }

        if (orphans.Count > 0 && result != MessageBoxResult.Yes)
        {
            _tray.ShowBalloon("AnyTray", $"{orphans.Count} окно(окон) осталось скрытым. Нажмите на иконку трея, чтобы восстановить.");
        }

        _session.Clear();
    }

    /// <summary>Безопасное завершение: восстановить ВСЕ скрытые окна (вызывается при выходе).</summary>
    public void RestoreAllOnExit()
    {
        try
        {
            foreach (var info in HiddenWindows.ToArray())
            {
                try
                {
                    _windowManager.TryRestore(info);
                    _watcher.Unwatch(info.Hwnd);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка восстановления окна {info.Title} (hwnd={info.Hwnd}) при выходе.", ex);
                }
            }
            HiddenWindows.Clear();
            _session.Clear();
            Logger.Info("Все скрытые окна восстановлены при выходе.");
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка восстановления окон при выходе.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_trayHideForegroundHandler is not null)
            _tray.HideForegroundRequested -= _trayHideForegroundHandler;
        if (_trayHideWindowHandler is not null)
            _tray.HideWindowRequested -= _trayHideWindowHandler;
        if (_trayRestoreHandler is not null)
            _tray.RestoreRequested -= _trayRestoreHandler;
        if (_trayRestoreAllHandler is not null)
            _tray.RestoreAllRequested -= _trayRestoreAllHandler;
        if (_traySettingsHandler is not null)
            _tray.SettingsRequested -= _traySettingsHandler;
        if (_trayExitHandler is not null)
            _tray.ExitRequested -= _trayExitHandler;

        if (_watcherWindowGoneHandler is not null)
            _watcher.WindowGone -= _watcherWindowGoneHandler;
        if (_mouseHookMiddleClickHandler is not null)
            _mouseHook.TitleBarMiddleClick -= _mouseHookMiddleClickHandler;
        if (_settingsChangedHandler is not null)
            _settings.SettingsChanged -= _settingsChangedHandler;
    }
}
