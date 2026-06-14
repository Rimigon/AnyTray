using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using AnyTray.Infrastructure;
using AnyTray.Services;
using AnyTray.ViewModels;
using AnyTray.Views;

namespace AnyTray;

/// <summary>
/// Composition root: single-instance, сборка сервисов, жизненный цикл,
/// гарантированное восстановление всех окон при выходе.
/// </summary>
public partial class App : System.Windows.Application
{
    private SingleInstance? _single;

    private SettingsService? _settingsService;
    private WindowManager? _windowManager;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private AutostartService? _autostartService;
    private ProcessWatcher? _processWatcher;
    private SessionStateService? _sessionService;
    private MouseHookService? _mouseHookService;
    private MainViewModel? _mainVm;

    private SettingsWindow? _settingsWindow;
    private bool _shuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Initialize();

        bool autostart = e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        if (autostart) Logger.Info("Запущено в режиме автозапуска.");

        // Один экземпляр на сессию.
        _single = new SingleInstance();
        if (!_single.TryAcquire())
        {
            Logger.Info("AnyTray уже запущен — завершаемся.");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        SystemEvents.SessionEnding += OnSessionEnding;

        // --- Сборка сервисов ---
        var dispatcher = Dispatcher.CurrentDispatcher;

        _settingsService = new SettingsService();
        _settingsService.Load();

        _windowManager = new WindowManager();
        _trayService = new TrayService();
        _hotkeyService = new HotkeyService();
        _autostartService = new AutostartService();
        _processWatcher = new ProcessWatcher(dispatcher);
        _sessionService = new SessionStateService();
        _mouseHookService = new MouseHookService(_windowManager, dispatcher);

        _mainVm = new MainViewModel(
            _windowManager, _trayService, _hotkeyService,
            _settingsService, _autostartService, _processWatcher, _sessionService, _mouseHookService);

        _mainVm.SettingsRequested += (_, _) => OpenSettings();
        _mainVm.ExitRequested += (_, _) => ExitApp();
        _mainVm.HideMenuRequested += (_, hwnd) => ShowHideMenu(hwnd);

        _mainVm.Initialize();

        Logger.Info("AnyTray готов к работе (в трее).");
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = new SettingsViewModel(_settingsService!);
        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Мини-меню «Свернуть в трей» у курсора (по среднему клику на заголовке).
    /// Это настоящее активируемое окно — надёжно закрывается по клику в любое другое место.
    /// </summary>
    private void ShowHideMenu(nint hwnd)
    {
        if (_windowManager is null || _mainVm is null) return;
        if (!_windowManager.IsManageableWindow(hwnd)) return;

        var title = new Native.Win32Window(hwnd).Title;
        if (string.IsNullOrWhiteSpace(title)) title = "это окно";
        if (title.Length > 48) title = title[..45] + "…";

        var menu = new HideMenuWindow(title, () => _mainVm!.HideWindow(hwnd));
        menu.Show();
    }

    private void ExitApp()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;

        Logger.Info("Завершение работы по запросу пользователя...");
        try { _mainVm?.RestoreAllOnExit(); }
        catch (Exception ex) { Logger.Error("Ошибка восстановления окон при выходе.", ex); }
        finally
        {
            _trayService?.PrepareShutdown(); // убираем иконку из трея ДО shutdown
            _trayService = null;             // предотвращаем двойной Dispose в OnExit
            Shutdown();
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        try { _mainVm?.RestoreAllOnExit(); }
        catch (Exception ex) { Logger.Error("Ошибка восстановления окон при завершении сессии.", ex); }
        finally
        {
            _trayService?.PrepareShutdown();
            _trayService = null;
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Фатальные исключения нельзя глотать — приложение в неопределённом состоянии.
        if (e.Exception is OutOfMemoryException or ThreadAbortException)
        {
            Logger.Error("Фатальное исключение в UI-потоке.", e.Exception);
            e.Handled = false;
            return;
        }

        Logger.Error("Необработанное исключение в UI-потоке.", e.Exception);
        // Tray-приложение должно пережить нефатальную ошибку; состояние скрытых окон
        // персистится в hidden-session.json, поэтому при необходимости сработает crash-recovery.
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            // Если ExitApp/OnSessionEnding уже вызывали — избегаем двойного restore.
            if (!_shuttingDown)
            {
                _mainVm?.RestoreAllOnExit();
                _trayService?.PrepareShutdown();
            }
            _mainVm?.Dispose();

            _hotkeyService?.Dispose();
            _processWatcher?.Dispose();
            _mouseHookService?.Dispose();
            _trayService?.Dispose();          // явно убираем иконку и отписываемся
            _single?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка при завершении.", ex);
        }
        finally
        {
            Logger.Info("==== AnyTray завершён ====");
            base.OnExit(e);
        }
    }
}
