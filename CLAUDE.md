# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AnyTray is a C# WPF desktop utility for Windows 10/11 targeting .NET 8 (`net8.0-windows`). It hides any regular desktop window into the system tray (via `ShowWindow(SW_HIDE)`) and manages hidden windows through its own tray icon and context menu. AnyTray does **not** inject anything into other apps' title bars — it hides the foreign window and exposes it via its own tray menu.

## Build and Run

Requires .NET 8 SDK and Windows x64.

```powershell
# Build
dotnet build AnyTray.csproj -c Release -p:Platform=x64

# Run (starts without a main window; only the tray icon is visible)
dotnet run --project AnyTray.csproj -c Release

# Direct run after build
.\bin\x64\Release\net8.0-windows\AnyTray.exe
```

## Tests

```powershell
dotnet test AnyTray.Tests/AnyTray.Tests.csproj -c Release -p:Platform=x64
```

The `AnyTray.Tests` project (xUnit) covers pure logic: `HotkeyDefinition` parsing/round-trip and `AppSettings.Clone`/`GetHotkeyDefinition`. Win32/UI-dependent services are not unit-tested.

## Architecture

### Composition Root and DI

`App.xaml.cs` is the sole composition root. Services are manually instantiated and wired together — there is **no DI container**. The `App` class owns all service and ViewModel lifetimes, handles single-instance enforcement, unhandled exceptions, session ending, and guarantees `RestoreAllOnExit()` is called on shutdown. The `--autostart` flag is propagated to `MainViewModel.AutostartMode` to make crash-recovery silent on Windows startup.

### Service Layer

Every major subsystem exposes an interface (`IWindowManager`, `ITrayService`, `IHotkeyService`, `ISettingsService`, `IProcessWatcher`, `ISessionStateService`, `IMouseHookService`, `IAutostartService`). `MainViewModel` is the orchestrator — it owns `ObservableCollection<HiddenWindowInfo>` and coordinates hide/restore from all sources (tray menu, hotkey, middle-click on title bar).

### Tray Integration

`TrayService` uses `System.Windows.Forms.NotifyIcon` (not a WPF-specific tray library). The context menu is rebuilt on a debounced `DispatcherTimer` whenever the hidden-windows collection changes; the previous `ContextMenuStrip` is disposed to avoid GDI handle leaks. "Скрыть активное окно" uses the foreground captured at menu `Opened` (not `GetForegroundWindow()` at click time, which would be wrong because clicking the tray menu steals foreground). Menu item icons are produced from WPF `ImageSource` by copying into an independent `System.Drawing.Bitmap` (the source stream is disposed safely — `Image.FromStream` requires the stream to stay alive).

### Hiding and Restoring Windows

`WindowManager` is stateless. `IsManageableWindow` filters to real top-level app windows (visible, not `WS_CHILD`, not `WS_EX_TOOLWINDOW`, not DWM-cloaked, non-empty title, not own process). Owned windows are allowed unless their owner is disabled (a disabled owner means a modal dialog — hiding it would freeze the owner). `CanManageWindow` additionally checks UIPI reachability via `PostMessage(WM_NULL)` — used by the mouse hook to avoid eating middle-clicks on windows it cannot manage (elevated windows). Hide uses `ShowWindowAsync(SW_HIDE)` (non-blocking on hung windows); restore uses `SetWindowPlacement` (converting `SW_SHOWMINIMIZED → SW_SHOWNORMAL`) plus `SetForegroundWindow` with `BringWindowToTop`/`FlashWindow` fallback for UIPI.

### Global Input Hooks

- `HotkeyService` — registers a global hotkey (`RegisterHotKey`) on a message-only window (`HwndSource` with `HWND_MESSAGE` parent). The hook delegate is kept in a field.
- `MouseHookService` — installs a low-level mouse hook (`WH_MOUSE_LL`) in-process (no DLL injection). Intercepts middle-click on the title bar band (computed from `DWMWA_EXTENDED_FRAME_BOUNDS` + a 40 DIP band). The press is suppressed only for UIPI-reachable manageable windows; the release is always suppressed when the press was suppressed, so apps never receive an orphan `MBUTTONUP`.

### Process Watching

`ProcessWatcher` keeps hidden windows alive in the tray menu. Primary mechanism: a low-frequency `IsWindow` sweep on a `DispatcherTimer` (runs only while there are hidden windows). `Process.Exited` is used as a fast accelerator that triggers an out-of-band sweep (marshalled to the UI thread). Ref-counting by pid correctly handles multi-window processes and pid reuse.

### Native Interop

All Win32 P/Invoke is isolated under `Native/`:

- `NativeMethods.cs` — static `extern` methods;
- `NativeConstants.cs` — constants grouped by purpose;
- `NativeStructs.cs` — blittable structs (`RECT`, `POINT`, `WINDOWPLACEMENT`, `MSLLHOOKSTRUCT`, `MONITORINFO`);
- `Win32Window.cs` — lightweight readonly wrapper around `hwnd` that lazily queries window state (styles, title, cloaked, DWM bounds).

### Data and State

Runtime data in `%APPDATA%\AnyTray\`:

- `settings.json` — user settings;
- `hidden-session.json` — crash-recovery session state (persisted on every hide/restore, cleared on clean exit; **not** cleared after loading orphans so carried-over hidden windows remain recoverable);
- `logs\anytray.log` — rotating file log (~1 MB max, one `.1` backup).

### Elevation and UIPI

By default the app runs as `asInvoker` (no admin). Because of UIPI, it cannot reliably interact with elevated (admin) windows — hide via hotkey/menu will fail, and the middle-click hook will **not** suppress the click on such windows (checked via `PostMessage(WM_NULL)`). To support admin windows, change `requestedExecutionLevel` to `requireAdministrator` in `app.manifest` and rebuild.

### Shutdown Behavior

`App.xaml` sets `ShutdownMode="OnExplicitShutdown"`. On exit or session end, `MainViewModel.RestoreAllOnExit()` restores all hidden windows, and `TrayService.PrepareShutdown()` removes the tray icon before the process terminates.

### Crash Recovery

On startup, `MainViewModel.RecoverCrashedSession()` loads orphan windows from `hidden-session.json` and **non-modally** returns them to `HiddenWindows` (watched by `ProcessWatcher`), then shows a tray balloon. The user restores via the tray menu ("Восстановить все" or per-window click). There is no blocking dialog — neither in interactive startup nor in `--autostart` mode. The session file is kept in sync with the actually-hidden windows (persisted, not cleared) so a subsequent crash remains recoverable; windows are never lost.
