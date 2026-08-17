# AnyTray

> Свернуть **любое** окно Windows в системный трей — даже если приложение само этого не умеет.

AnyTray — фоновая утилита для Windows 10/11. Она скрывает обычные desktop-окна через `ShowWindow(SW_HIDE)` и управляет ими через **собственную** иконку в системном трее (AnyTray не встраивает чужую иконку в трей — он прячет чужое окно и показывает своё меню). Скрывать можно окна **любых** приложений: Telegram, VS Code, Word, браузеры и т.д.

## Возможности

- **Горячая клавиша** — скрывает активное окно (по умолчанию `Ctrl+Alt+H`, настраивается). Чистая `Win+клавиша` не допускается (перехватывала бы зарезервированные шорткаты ОС); `Win` в сочетании с `Ctrl`/`Alt`/`Shift` разрешён.
- **Средний клик (колёсиком) по заголовку окна** — скрывает окно или показывает мини-меню у курсора (по настройке). Работает на всех приложениях, включая приложения с кастомным заголовком (Telegram, VS Code, Electron/Qt). Клик по заголовку подавляется, чтобы он не дошёл до приложения (например, не закрыл вкладку браузера).
- **Меню трея**:
  - «Скрыть активное окно»;
  - «Скрыть окно ▸» — список всех открытых окон с иконками, чтобы скрыть любое из них;
  - список скрытых окон с иконками — клик восстанавливает;
  - «Восстановить все», «Настройки…», «Выход».
- **Двойной клик левой по иконке трея** — открывает настройки.
- **Восстановление при выходе** — при штатном выходе все скрытые окна автоматически восстанавливаются.
- **Crash-recovery** — если процесс завершён аварийно (краш/убийство) со скрытыми окнами, при следующем запуске AnyTray тихо вернёт их в список скрытых и покажет balloon. Восстановить можно через меню трея («Восстановить все» или кликом по окну). Модальных диалогов при старте нет — в т.ч. в режиме автозапуска (`--autostart`).
- **Автозапуск с Windows** — через `HKCU\…\Run`, без прав администратора.
- **Один экземпляр** на пользовательскую сессию (named mutex).

## Ограничения

- **Elevated-окна (UIPI).** По умолчанию AnyTray запущен как `asInvoker` и не может скрыть окно процесса, запущенного от администратора. Средний клик по заголовку такого окна **не подавляется** (AnyTray заранее проверяет доступность окна и не «ест» клик, который всё равно не сможет обработать). Чтобы управлять админскими окнами, пересоберите с `requestedExecutionLevel="requireAdministrator"` в `app.manifest`.
- **Окна с владельцем** — модальные диалоги (owner отключён) не скрываются: это заморозило бы окно-владелец, т.к. модальный цикл не завершается. Немодальные owned-окна (панели инструментов, окна «Найти…» без модальности) скрывать можно.
- **Иконки в меню** берутся сначала из класса окна (`GetClassLongPtr`), затем через `WM_GETICON` с таймаутом. Для некоторых приложений иконка может отсутствовать.

## Сборка и запуск

Требуется .NET 8 SDK и Windows x64.

```powershell
# Сборка
dotnet build AnyTray.csproj -c Release -p:Platform=x64

# Запуск (главного окна нет — только иконка в трее)
dotnet run --project AnyTray.csproj -c Release

# Готовый exe
.\bin\x64\Release\net8.0-windows\AnyTray.exe
```

## Тесты

```powershell
dotnet test AnyTray.Tests/AnyTray.Tests.csproj -c Release -p:Platform=x64
```

Покрыты чистые алгоритмические части: парсинг горячей клавиши (`HotkeyDefinition`), настройки (`AppSettings.Clone`/`GetHotkeyDefinition`).

## Архитектура

### Корень композиции

`App.xaml.cs` — единственный composition root. Сервисы создаются вручную (без DI-контейнера). Класс `App` владеет временем жизни всех сервисов и ViewModel, обеспечивает single-instance, обрабатывает необработанные исключения и завершение сессии, гарантирует `RestoreAllOnExit()` при выходе.

`App.xaml` задаёт `ShutdownMode="OnExplicitShutdown"` — главного окна нет, приложение живёт в трее.

### Сервисный слой

| Сервис | Назначение |
|---|---|
| `WindowManager` | Поиск/валидация/скрытие/восстановление чужих окон. Состояния не хранит — список скрытых живёт в `MainViewModel`. |
| `TrayService` | Иконка трея и контекстное меню через `System.Windows.Forms.NotifyIcon`. Меню перестраивается по дебаунс-таймеру при изменении списка скрытых окон (старое меню диспозится — без утечек GDI). |
| `HotkeyService` | Глобальная горячая клавиша через message-only окно (`HwndSource` + `RegisterHotKey`). |
| `MouseHookService` | Низкоуровневый перехват `WH_MOUSE_LL` — средний клик по заголовку. Подавление клика происходит только для окон, доступных по UIPI. |
| `ProcessWatcher` | Следит, что скрытые окна ещё живы: низкочастотный sweep `IsWindow` на `DispatcherTimer` + `Process.Exited` как ускоритель. Ref-counting по pid для многооконных процессов. |
| `SessionStateService` | Crash-recovery: персист `hidden-session.json` на каждое hide/restore, очистка при штатном выходе. |
| `SettingsService` | `settings.json` (атомарная запись через `.tmp` + `File.Move`). |
| `AutostartService` | `HKCU\…\Run`. |

`MainViewModel` — оркестратор: владеет `ObservableCollection<HiddenWindowInfo>` и сводит вместе все источники команд (горячая клавиша, средний клик, меню трея).

### Native interop

Весь Win32 P/Invoke изолирован в `Native/`:

- `NativeMethods.cs` — `extern`-методы;
- `NativeConstants.cs` — константы по группам;
- `NativeStructs.cs` — blittable-структуры (`RECT`, `POINT`, `WINDOWPLACEMENT`, `MSLLHOOKSTRUCT`, `MONITORINFO`);
- `Win32Window.cs` — тонкая read-only обёртка над `hwnd` (стили, заголовок, cloaked, границы DWM и т.п. — вычисляются по запросу, без кэширования).

### Данные

 Runtime-данные в `%APPDATA%\AnyTray\`:

- `settings.json` — настройки;
- `hidden-session.json` — состояние скрытых окон для crash-recovery (персистится на hide/restore, очищается при штатном выходе);
- `logs\anytray.log` — ротирующийся лог (~1 МБ, один бэкап `anytray.log.1`).

### Завершение

При выходе или завершении сессии `MainViewModel.RestoreAllOnExit()` восстанавливает все скрытые окна, а `TrayService.PrepareShutdown()` убирает иконку трея до остановки процесса.

## Структура проекта

```text
App.xaml(.cs)        Composition root, жизненный цикл, single-instance
Models/              HiddenWindowInfo, OpenWindowInfo, AppSettings, HotkeyDefinition
Native/              NativeMethods, NativeConstants, NativeStructs, Win32Window
Services/            WindowManager, TrayService, HotkeyService, MouseHookService,
                     ProcessWatcher, SessionStateService, SettingsService, AutostartService
Infrastructure/      Logger, SingleInstance, IconHelper, DpiHelper
ViewModels/          MainViewModel, SettingsViewModel
Views/               SettingsWindow, HideMenuWindow
AnyTray.Tests/       xUnit-тесты чистой логики
Assets/              anytray.ico / .png / .svg — иконки
tools/               Скрипты сборки и генерации иконок
```

## Лицензия

См. репозиторий.
