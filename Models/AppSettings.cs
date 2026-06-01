using System.Text.Json.Serialization;

namespace AnyTray.Models;

/// <summary>Настройки приложения. Сериализуются в %APPDATA%\AnyTray\settings.json.</summary>
public sealed class AppSettings
{
    public bool AutostartEnabled { get; set; } = false;

    /// <summary>Сериализованная горячая клавиша (см. <see cref="HotkeyDefinition"/>).</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+H";

    public bool OverlayEnabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayDisplayMode OverlayMode { get; set; } = OverlayDisplayMode.AlwaysWhenForeground;

    /// <summary>Имена процессов (без .exe, lower-case), для которых overlay не показывается.</summary>
    public List<string> OverlayBlacklist { get; set; } = new();

    /// <summary>Скрывать окно средним кликом по его заголовку (работает на всех приложениях).</summary>
    public bool TitleBarMiddleClickEnabled { get; set; } = true;

    /// <summary>true — средний клик прячет окно сразу; false — показывает мини-меню (по умолчанию).</summary>
    public bool TitleBarMiddleClickDirectHide { get; set; } = false;

    public int SchemaVersion { get; set; } = 1;

    public HotkeyDefinition GetHotkeyDefinition()
        => HotkeyDefinition.TryParse(Hotkey, out var def) ? def : HotkeyDefinition.Default;

    public AppSettings Clone() => new()
    {
        AutostartEnabled = AutostartEnabled,
        Hotkey = Hotkey,
        OverlayEnabled = OverlayEnabled,
        OverlayMode = OverlayMode,
        OverlayBlacklist = new List<string>(OverlayBlacklist),
        TitleBarMiddleClickEnabled = TitleBarMiddleClickEnabled,
        TitleBarMiddleClickDirectHide = TitleBarMiddleClickDirectHide,
        SchemaVersion = SchemaVersion
    };
}
