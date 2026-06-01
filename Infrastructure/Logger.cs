using System.IO;

namespace AnyTray.Infrastructure;

/// <summary>
/// Минимальный потокобезопасный файловый логгер.
/// Пишет в %APPDATA%\AnyTray\logs\anytray.log. Без внешних зависимостей.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static string _logFile = string.Empty;
    private const long MaxSizeBytes = 1_000_000; // ~1 МБ, затем простая ротация

    public static void Initialize()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnyTray", "logs");
            Directory.CreateDirectory(dir);
            _logFile = Path.Combine(dir, "anytray.log");
            Info("==== AnyTray запущен ====");
        }
        catch
        {
            // Логирование не должно ронять приложение.
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        if (string.IsNullOrEmpty(_logFile)) return;
        try
        {
            lock (_lock)
            {
                Rotate();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFile, line);
            }
        }
        catch
        {
            // Глотаем — логгер не должен бросать.
        }
    }

    private static void Rotate()
    {
        try
        {
            var fi = new FileInfo(_logFile);
            if (fi.Exists && fi.Length > MaxSizeBytes)
            {
                var bak = _logFile + ".1";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(_logFile, bak);
            }
        }
        catch { }
    }
}
