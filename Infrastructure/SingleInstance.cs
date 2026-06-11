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
    private bool _ownsMutex;

    public bool IsFirstInstance { get; private set; }

    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            if (createdNew)
            {
                IsFirstInstance = true;
                _ownsMutex = true;
                return true;
            }

            // Захватываем существующий mutex — если он abandoned (предыдущий краш),
            // считаем себя первым экземпляром.
            try
            {
                _ownsMutex = _mutex.WaitOne(0);
                IsFirstInstance = _ownsMutex;
            }
            catch (AbandonedMutexException)
            {
                IsFirstInstance = true;
                _ownsMutex = true;
            }
            catch
            {
                IsFirstInstance = false;
                _ownsMutex = false;
            }
            return IsFirstInstance;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_ownsMutex && _mutex is not null)
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
