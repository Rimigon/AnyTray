using System.IO;
using System.Text.Json;
using AnyTray.Infrastructure;
using AnyTray.Models;
using AnyTray.Native;
using static AnyTray.Native.NativeMethods;

namespace AnyTray.Services;

/// <summary>DTO для персиста скрытого окна (hwnd хранится как long для JSON).</summary>
public sealed class PersistedHiddenWindow
{
    public long Hwnd { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "unknown";
    public string Title { get; set; } = string.Empty;
    public WINDOWPLACEMENT Placement { get; set; }
}

public interface ISessionStateService
{
    /// <summary>Перезаписать файл сессии текущим набором скрытых окон.</summary>
    void Persist(IEnumerable<HiddenWindowInfo> hidden);
    /// <summary>Очистить файл сессии (вызывается при штатном выходе).</summary>
    void Clear();
    /// <summary>
    /// Прочитать «осиротевшие» окна от прошлой (аварийно завершившейся) сессии,
    /// оставив только те, что ещё существуют и принадлежат тому же pid.
    /// </summary>
    IReadOnlyList<PersistedHiddenWindow> LoadOrphans();
}

/// <summary>
/// Crash-recovery: на каждый hide/restore переписывает hidden-session.json.
/// При штатном выходе файл очищается. Непустой файл на старте = был краш.
/// </summary>
public sealed class SessionStateService : ISessionStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true // WINDOWPLACEMENT/RECT/POINT используют поля
    };

    private readonly string _file;

    public SessionStateService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnyTray");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "hidden-session.json");
    }

    public void Persist(IEnumerable<HiddenWindowInfo> hidden)
    {
        try
        {
            var list = hidden.Select(h => new PersistedHiddenWindow
            {
                Hwnd = h.Hwnd,
                ProcessId = h.ProcessId,
                ProcessName = h.ProcessName,
                Title = h.Title,
                Placement = h.OriginalPlacement
            }).ToList();

            if (list.Count == 0)
            {
                Clear();
                return;
            }

            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOptions));
            File.Move(tmp, _file, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сохранить hidden-session.json.", ex);
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_file)) File.Delete(_file);
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось очистить hidden-session.json.", ex);
        }
    }

    public IReadOnlyList<PersistedHiddenWindow> LoadOrphans()
    {
        try
        {
            if (!File.Exists(_file)) return Array.Empty<PersistedHiddenWindow>();

            var json = File.ReadAllText(_file);
            var list = JsonSerializer.Deserialize<List<PersistedHiddenWindow>>(json, JsonOptions)
                       ?? new List<PersistedHiddenWindow>();

            var alive = new List<PersistedHiddenWindow>();
            foreach (var item in list)
            {
                try
                {
                    nint hwnd = (nint)item.Hwnd;
                    if (!IsWindow(hwnd)) continue;
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if ((int)pid == item.ProcessId)
                        alive.Add(item);
                }
                catch
                {
                    // hwnd стал невалидным между IsWindow и GetWindowThreadProcessId — игнорируем
                }
            }
            return alive;
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось прочитать hidden-session.json.", ex);
            return Array.Empty<PersistedHiddenWindow>();
        }
    }
}
