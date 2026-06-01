using System.Threading;

namespace AnyTray.Infrastructure;

/// <summary>
/// Гарантирует один экземпляр приложения на пользовательскую сессию через именованный Mutex.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // Per-session (Local\) — каждый пользователь / RDP-сессия получает свой экземпляр.
    private const string MutexName = @"Local\AnyTray_SingleInstance_{B3F1A6C2-7D54-4E8B-9C0A-2F1E6D7A5B43}";

    private Mutex? _mutex;

    public bool IsFirstInstance { get; private set; }

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsFirstInstance = createdNew;
        return createdNew;
    }

    public void Dispose()
    {
        try
        {
            if (IsFirstInstance && _mutex is not null)
                _mutex.ReleaseMutex();
        }
        catch { }
        finally
        {
            _mutex?.Dispose();
            _mutex = null;
        }
    }
}
