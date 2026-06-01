using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AnyTray.Infrastructure;
using AnyTray.Models;
using AnyTray.Native;
using AnyTray.Views;
using static AnyTray.Native.NativeConstants;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

public interface IOverlayButtonService : IDisposable
{
    void Start();
    void Stop();
    void ApplySettings(AppSettings settings);
    /// <summary>Целевое окно скрыто — убрать с него overlay.</summary>
    void OnWindowHidden(nint hwnd);
    /// <summary>Окно восстановлено — пересмотреть overlay (обычно сработает через foreground-событие).</summary>
    void OnWindowRestored(nint hwnd);
    /// <summary>Клик по overlay-кнопке: hwnd целевого окна, которое нужно скрыть. На UI-потоке.</summary>
    event EventHandler<nint>? HideRequested;
}

/// <summary>
/// Управляет overlay-кнопкой на заголовке чужого окна.
///
/// СТРАТЕГИЯ MVP: один переиспользуемый overlay показывается на ТЕКУЩЕМ окне в фокусе
/// (foreground). Это удовлетворяет требованию «кнопка на каждом обычном окне» (она появляется
/// на окне, с которым работает пользователь), но не плодит десятки невидимых overlay-окон.
/// Per-window словарь overlay — кандидат на v2.
///
/// Все WinEvent-хуки ставятся на UI-потоке (там работает message loop), поэтому колбэки приходят
/// на UI-поток без маршалинга. Делегат хука хранится в поле, чтобы его не собрал GC.
/// </summary>
public sealed class OverlayButtonService : IOverlayButtonService
{
    private const double OverlayWidthDip = 40.0;   // ширина нашей кнопки в DIP
    private const double OverlayHeightDip = 30.0;   // запасная высота (если нет caption bounds)
    private const int GapPx = 2;                    // зазор слева от системной «свернуть»

    private readonly Dispatcher _dispatcher;
    private readonly IWindowManager _windowManager;

    private readonly NativeMethods.WinEventProc _winEventProc; // держим живым для GC!
    private nint _hookSystem;
    private nint _hookObject;

    private OverlayButtonWindow? _overlay;
    private nint _overlayHwnd;
    private nint _targetHwnd;

    private readonly DispatcherTimer _repositionTimer;
    private readonly DispatcherTimer _hoverTimer;

    // Параметры caption-полосы текущей цели (физ. пиксели) — для hover-режима.
    private RECT _targetCaptionBand;

    private bool _running;
    private bool _enabled = true;
    private OverlayDisplayMode _mode = OverlayDisplayMode.AlwaysWhenForeground;
    private HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<nint>? HideRequested;

    public OverlayButtonService(Dispatcher dispatcher, IWindowManager windowManager)
    {
        _dispatcher = dispatcher;
        _windowManager = windowManager;
        _winEventProc = WinEventCallback;

        _repositionTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _repositionTimer.Tick += (_, _) => { _repositionTimer.Stop(); Reposition(); };

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _hoverTimer.Tick += (_, _) => UpdateHoverVisibility();
    }

    public void ApplySettings(AppSettings settings)
    {
        _mode = settings.OverlayMode;
        _blacklist = new HashSet<string>(settings.OverlayBlacklist, StringComparer.OrdinalIgnoreCase);
        _enabled = settings.OverlayEnabled;

        if (_enabled)
        {
            Start();
            UpdateHoverTimerState();
            RetargetTo(GetForegroundWindow());
        }
        else
        {
            Stop();
        }
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        EnsureOverlay();

        // Хук 1: системные события (foreground / minimize start/end).
        _hookSystem = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND,
            0, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        // Хук 2: объектные события (show / hide / destroy / locationchange).
        _hookObject = SetWinEventHook(
            EVENT_OBJECT_DESTROY, EVENT_OBJECT_LOCATIONCHANGE,
            0, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        Logger.Info("OverlayButtonService запущен (WinEvent-хуки установлены).");
        RetargetTo(GetForegroundWindow());
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        if (_hookSystem != 0) { UnhookWinEvent(_hookSystem); _hookSystem = 0; }
        if (_hookObject != 0) { UnhookWinEvent(_hookObject); _hookObject = 0; }

        _repositionTimer.Stop();
        _hoverTimer.Stop();
        _targetHwnd = 0;
        HideOverlay();
        Logger.Info("OverlayButtonService остановлен.");
    }

    public void OnWindowHidden(nint hwnd)
    {
        if (hwnd == _targetHwnd)
        {
            _targetHwnd = 0;
            HideOverlay();
        }
    }

    public void OnWindowRestored(nint hwnd)
    {
        // После восстановления вызывается SetForegroundWindow → прилетит EVENT_SYSTEM_FOREGROUND,
        // который пере-нацелит overlay. На всякий случай пробуем сразу.
        if (_running && _windowManager.IsManageableWindow(hwnd))
            RetargetTo(hwnd);
    }

    // ---------------------------------------------------------------
    //  WinEvent callback (UI-поток)
    // ---------------------------------------------------------------
    private void WinEventCallback(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_running || !_enabled) return;
        if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
        if (hwnd == 0) return;

        switch (eventType)
        {
            case EVENT_SYSTEM_FOREGROUND:
                RetargetTo(hwnd);
                break;

            case EVENT_OBJECT_LOCATIONCHANGE:
                if (hwnd == _targetHwnd)
                    _repositionTimer.Start(); // дебаунс: перезапуск одноразового 16мс таймера
                break;

            case EVENT_SYSTEM_MINIMIZESTART:
            case EVENT_OBJECT_HIDE:
                if (hwnd == _targetHwnd) HideOverlay();
                break;

            case EVENT_SYSTEM_MINIMIZEEND:
            case EVENT_OBJECT_SHOW:
                if (hwnd == _targetHwnd) PositionAndShow();
                break;

            case EVENT_OBJECT_DESTROY:
                if (hwnd == _targetHwnd) { _targetHwnd = 0; HideOverlay(); }
                break;
        }
    }

    // ---------------------------------------------------------------
    //  Нацеливание / показ
    // ---------------------------------------------------------------
    private void RetargetTo(nint hwnd)
    {
        if (!_enabled || !_running) { HideOverlay(); return; }

        if (hwnd == 0 || !_windowManager.IsManageableWindow(hwnd) || IsBlacklisted(hwnd))
        {
            _targetHwnd = 0;
            HideOverlay();
            UpdateHoverTimerState();
            return;
        }

        _targetHwnd = hwnd;
        PositionAndShow();
        UpdateHoverTimerState();
    }

    private void PositionAndShow()
    {
        if (_targetHwnd == 0) { HideOverlay(); return; }

        var w = new Win32Window(_targetHwnd);
        if (!w.IsValid || !w.IsVisible || w.IsMinimized)
        {
            HideOverlay();
            return;
        }

        if (!TryComputeOverlayRect(_targetHwnd, out RECT rect, out _targetCaptionBand))
        {
            HideOverlay();
            return;
        }

        EnsureOverlay();
        if (_overlayHwnd == 0) return;

        // Позиционируем в ФИЗИЧЕСКИХ пикселях — это убирает DIP-математику и решает мульти-монитор.
        SetWindowPos(_overlayHwnd, HWND_TOPMOST,
            rect.Left, rect.Top, rect.Width, rect.Height,
            SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW);

        if (_mode == OverlayDisplayMode.AlwaysWhenForeground)
            _overlay!.Visibility = Visibility.Visible;
        else
            UpdateHoverVisibility(); // в hover-режиме видимость решает курсор
    }

    private void Reposition()
    {
        if (_targetHwnd != 0) PositionAndShow();
    }

    private void HideOverlay()
    {
        if (_overlay is not null) _overlay.Visibility = Visibility.Hidden;
    }

    // ---------------------------------------------------------------
    //  Hover-режим
    // ---------------------------------------------------------------
    private void UpdateHoverTimerState()
    {
        bool needHover = _running && _enabled && _mode == OverlayDisplayMode.OnHoverOnly && _targetHwnd != 0;
        if (needHover && !_hoverTimer.IsEnabled) _hoverTimer.Start();
        else if (!needHover && _hoverTimer.IsEnabled) _hoverTimer.Stop();
    }

    private void UpdateHoverVisibility()
    {
        if (_overlay is null || _targetHwnd == 0) return;
        if (_mode != OverlayDisplayMode.OnHoverOnly)
        {
            _overlay.Visibility = Visibility.Visible;
            return;
        }

        bool over = GetCursorPos(out POINT p) &&
                    p.X >= _targetCaptionBand.Left && p.X <= _targetCaptionBand.Right &&
                    p.Y >= _targetCaptionBand.Top && p.Y <= _targetCaptionBand.Bottom;

        _overlay.Visibility = over ? Visibility.Visible : Visibility.Hidden;
    }

    // ---------------------------------------------------------------
    //  Геометрия
    // ---------------------------------------------------------------
    /// <summary>
    /// Вычисляет прямоугольник overlay (физ. пиксели) слева от системной кнопки «свернуть»,
    /// а также caption-полосу окна (для hover).
    ///
    /// Используется ТОЛЬКО точный путь — границы системных кнопок (DWMWA_CAPTION_BUTTON_BOUNDS).
    /// Работает для стандартных окон и большинства Chromium-браузеров / Windows Terminal.
    /// Если окно полностью рисует свой заголовок без системных кнопок (например, VS Code),
    /// границы пустые → возвращаем false и overlay НЕ показываем (чтобы не перекрывать чужие кнопки).
    /// </summary>
    private static bool TryComputeOverlayRect(nint hwnd, out RECT overlay, out RECT captionBand)
    {
        overlay = default;
        captionBand = default;

        var w = new Win32Window(hwnd);
        if (!w.TryGetWindowRect(out RECT wr)) return false;

        // Пропускаем слишком маленькие окна (всплывашки/тосты).
        if (wr.Width < 200 || wr.Height < 80) return false;

        // Кадр DWM нужен и для проверки кастомного заголовка, и для maximized-клампа.
        if (!w.TryGetExtendedFrameBounds(out RECT fr)) return false;

        double scale = DpiHelper.GetScaleForWindow(hwnd);

        // ВАЖНО: показываем overlay ТОЛЬКО на окнах с настоящим СИСТЕМНЫМ заголовком.
        // Признак — наличие системной (нерабочей) caption-полосы: клиентская область
        // начинается заметно ниже верхней кромки кадра. Приложения с кастомным заголовком
        // (браузеры, Electron/Qt — VS Code, Telegram, WinUI/Terminal, UWP) рисуют всё в
        // клиентской области (полоса ≈ 0), их собственные кнопки наш overlay перекрывал бы —
        // поэтому для них кнопку НЕ показываем (скрытие доступно по hotkey и через меню трея).
        var clientOrigin = new POINT();
        ClientToScreen(hwnd, ref clientOrigin);
        int captionStrip = clientOrigin.Y - fr.Top;
        if (captionStrip < DpiHelper.DipToPixels(14, scale)) return false;

        // Точные границы системных кнопок (относительно origin окна).
        if (!w.TryGetCaptionButtonBounds(out RECT btn)) return false;

        int overlayW = DpiHelper.DipToPixels(OverlayWidthDip, scale);
        int gap = DpiHelper.DipToPixels(GapPx, scale);

        int buttonsScreenLeft = wr.Left + btn.Left;
        int top = wr.Top + btn.Top;
        int height = btn.Height;

        // Компенсация 8px-«вылета» рамки у развёрнутых (maximized) окон.
        if (top < fr.Top) top = fr.Top;

        overlay = new RECT
        {
            Left = buttonsScreenLeft - overlayW - gap,
            Top = top,
            Right = buttonsScreenLeft - gap,
            Bottom = top + height
        };
        captionBand = new RECT { Left = wr.Left, Top = top, Right = wr.Right, Bottom = top + height };
        return overlay.Width > 0 && overlay.Height > 0;
    }

    private bool IsBlacklisted(nint hwnd)
    {
        if (_blacklist.Count == 0) return false;
        try
        {
            var w = new Win32Window(hwnd);
            using var p = Process.GetProcessById(w.ProcessId);
            return _blacklist.Contains(p.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------
    private void EnsureOverlay()
    {
        if (_overlay is not null) return;

        _overlay = new OverlayButtonWindow();
        _overlay.HideClicked += OnOverlayClicked;
        _overlay.Show();                       // ShowActivated=False — фокус не уходит
        _overlay.Visibility = Visibility.Hidden;
        _overlayHwnd = new WindowInteropHelper(_overlay).Handle;
    }

    private void OnOverlayClicked(object? sender, EventArgs e)
    {
        if (_targetHwnd != 0)
            HideRequested?.Invoke(this, _targetHwnd);
    }

    public void Dispose()
    {
        Stop();
        if (_overlay is not null)
        {
            _overlay.HideClicked -= OnOverlayClicked;
            _overlay.Close();
            _overlay = null;
            _overlayHwnd = 0;
        }
    }
}
