using System.Diagnostics;
using Microsoft.Win32;
using AnyTray.Infrastructure;

namespace AnyTray.Services;

public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>
/// Автозапуск через HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Не требует прав администратора. Путь берётся из реального exe текущего процесса.
/// </summary>
public sealed class AutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AnyTray";

    private static string ExePath
        => Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка чтения автозапуска.", ex);
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exe = ExePath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(ValueName, $"\"{exe}\" --autostart");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            Logger.Info($"Автозапуск: {(enabled ? "включён" : "выключен")}.");
        }
        catch (Exception ex)
        {
            Logger.Error("Ошибка записи автозапуска.", ex);
        }
    }
}
