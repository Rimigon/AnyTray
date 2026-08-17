using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnyTray.Infrastructure;
using AnyTray.Models;

namespace AnyTray.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsFilePath { get; }
    AppSettings Load();
    void Save(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}

/// <summary>
/// Загрузка/сохранение настроек в %APPDATA%\AnyTray\settings.json.
/// Запись атомарная (через .tmp + File.Move), при повреждённом файле — defaults.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dir;

    public string SettingsFilePath { get; }
    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnyTray");
        SettingsFilePath = Path.Combine(_dir, "settings.json");
    }

    public AppSettings Load()
    {
        bool exists = false;
        try
        {
            Directory.CreateDirectory(_dir);
            exists = File.Exists(SettingsFilePath);
            if (exists)
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Migrate(); // привести к актуальной схеме (точка расширения на будущее)
                    Current = loaded;
                    return Current;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось прочитать settings.json — используются значения по умолчанию.", ex);
            // Сохраним повреждённый файл, чтобы пользователь/разработчик могли его разобрать —
            // иначе следующий Save из UI молча сотрёт его.
            BackupCorruptSettings();
        }

        Current = new AppSettings();
        if (!exists)
        {
            // Файла не было — создаём дефолтный. Если файл был, но повреждён — не перезаписываем
            // автоматически, чтобы не стереть данные пользователя; перезапишется при Save из UI.
            Save(Current);
        }
        return Current;
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        bool success = false;
        try
        {
            Directory.CreateDirectory(_dir);
            var tmp = SettingsFilePath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsFilePath, overwrite: true);
            Logger.Info("Настройки сохранены.");
            success = true;
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сохранить settings.json.", ex);
        }

        if (success)
            SettingsChanged?.Invoke(this, settings);
    }

    /// <summary>
    /// Копирует повреждённый settings.json в settings.json.corrupt-<timestamp>, чтобы данные
    /// пользователя не потерялись безследно при следующем Save.
    /// </summary>
    private void BackupCorruptSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return;
            var bak = Path.Combine(_dir, $"settings.json.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(SettingsFilePath, bak, overwrite: false);
            Logger.Warn($"Повреждённый settings.json сохранён как: {bak}");
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сохранить резервную копию повреждённого settings.json.", ex);
        }
    }
}
