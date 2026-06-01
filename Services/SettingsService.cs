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
        try
        {
            Directory.CreateDirectory(_dir);
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    Current = loaded;
                    return Current;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось прочитать settings.json — используются значения по умолчанию.", ex);
        }

        // Файл отсутствует/повреждён — defaults + перезапись.
        Current = new AppSettings();
        Save(Current);
        return Current;
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        try
        {
            Directory.CreateDirectory(_dir);
            var tmp = SettingsFilePath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsFilePath, overwrite: true);
            Logger.Info("Настройки сохранены.");
        }
        catch (Exception ex)
        {
            Logger.Error("Не удалось сохранить settings.json.", ex);
        }

        SettingsChanged?.Invoke(this, settings);
    }
}
