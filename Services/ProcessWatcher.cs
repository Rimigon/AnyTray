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
    private readonly Dictionary<int, int> _pidRefCount = new();       // pid -> count of watched windows
    private readonly Dictionary<int, Process> _processes = new();     // pid  -> Process (для Exited)
    private bool _disposed;

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
        if (_disposed) return;
        _watched[info.Hwnd] = info.ProcessId;
        _pidRefCount[info.ProcessId] = _pidRefCount.GetValueOrDefault(info.ProcessId) + 1;

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
        if (_disposed) return;
        if (!_watched.Remove(hwnd, out int pid)) return;

        // Уменьшаем счётчик ссылок на PID.
        if (_pidRefCount.TryGetValue(pid, out int count))
        {
            if (count <= 1)
                _pidRefCount.Remove(pid);
            else
                _pidRefCount[pid] = count - 1;
        }

        // Если для pid больше нет наблюдаемых окон — освобождаем Process.
        if (!_pidRefCount.ContainsKey(pid) && _processes.Remove(pid, out var p))
        {
            try { p.EnableRaisingEvents = false; } catch { }
            try { p.Dispose(); } catch { }
        }

        if (_watched.Count == 0) _timer.Stop();
    }

    private void Sweep()
    {
        if (_disposed || _watched.Count == 0) return;

        // Снимок мёртвых окон — обработчики WindowGone могут вызвать Unwatch (мутирует _watched).
        var dead = _watched.Keys.Where(hwnd => !IsWindow(hwnd)).ToArray();
        foreach (var hwnd in dead)
        {
            Logger.Info($"Скрытое окно (hwnd={hwnd}) закрыто — удаляем из списка.");
            WindowGone?.Invoke(this, hwnd);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        foreach (var p in _processes.Values)
        {
            try { p.EnableRaisingEvents = false; } catch { }
            try { p.Dispose(); } catch { }
        }
        _processes.Clear();
        _watched.Clear();
    }
}
