using System.Diagnostics;
using System.Windows.Threading;
using AnyTray.Infrastructure;
using AnyTray.Models;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

public interface IProcessWatcher : IDisposable
{
    void Watch(HiddenWindowInfo info);
    void Unwatch(nint hwnd);
    /// <summary>hwnd скрытого окна, которое перестало существовать. Приходит на UI-потоке.</summary>
    event EventHandler<nint>? WindowGone;
}

/// <summary>
/// Следит за тем, что скрытые окна ещё живы. Основной механизм — низкочастотный
/// свип IsWindow на <see cref="DispatcherTimer"/> (работает только пока есть скрытые окна),
/// что корректно ловит закрытие отдельного окна у многооконного процесса и переиспользование pid.
/// <see cref="Process.Exited"/> используется как быстрый ускоритель (триггерит внеочередной свип).
/// </summary>
public sealed class ProcessWatcher : IProcessWatcher
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<nint, int> _watched = new();          // hwnd -> pid
    private readonly Dictionary<int, Process> _processes = new();     // pid  -> Process (для Exited)

    public event EventHandler<nint>? WindowGone;

    public ProcessWatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _timer.Tick += (_, _) => Sweep();
    }

    public void Watch(HiddenWindowInfo info)
    {
        _watched[info.Hwnd] = info.ProcessId;

        if (!_processes.ContainsKey(info.ProcessId))
        {
            try
            {
                var p = Process.GetProcessById(info.ProcessId);
                p.EnableRaisingEvents = true;
                // Exited прилетает на пуле потоков — внеочередной свип маршалим на UI.
                p.Exited += (_, _) => _dispatcher.BeginInvoke(Sweep);
                _processes[info.ProcessId] = p;
            }
            catch
            {
                // Процесс защищён/недоступен — положимся на таймер.
            }
        }

        if (!_timer.IsEnabled) _timer.Start();
    }

    public void Unwatch(nint hwnd)
    {
        if (!_watched.Remove(hwnd, out int pid)) return;

        // Если для pid больше нет наблюдаемых окон — освобождаем Process.
        if (!_watched.ContainsValue(pid) && _processes.Remove(pid, out var p))
        {
            try { p.Dispose(); } catch { }
        }

        if (_watched.Count == 0) _timer.Stop();
    }

    private void Sweep()
    {
        if (_watched.Count == 0) return;

        // Снимок ключей, чтобы не мутировать словарь во время перебора.
        foreach (var hwnd in _watched.Keys.ToArray())
        {
            if (!IsWindow(hwnd))
            {
                Logger.Info($"Скрытое окно (hwnd={hwnd}) закрыто — удаляем из списка.");
                WindowGone?.Invoke(this, hwnd);
            }
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        foreach (var p in _processes.Values)
        {
            try { p.Dispose(); } catch { }
        }
        _processes.Clear();
        _watched.Clear();
    }
}
