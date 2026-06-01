using System.Text.Json.Serialization;
using System.Windows.Media;
using AnyTray.Native;

namespace AnyTray.Models;

/// <summary>
/// Запись о скрытом окне. Ключ идентификации — <see cref="Hwnd"/> (а не pid),
/// чтобы корректно поддерживать несколько окон одного процесса.
/// </summary>
public sealed class HiddenWindowInfo
{
    public nint Hwnd { get; init; }
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "unknown";
    public string Title { get; init; } = string.Empty;

    /// <summary>Состояние окна, захваченное ПЕРЕД скрытием — для точного восстановления.</summary>
    public WINDOWPLACEMENT OriginalPlacement { get; init; }

    public DateTime HiddenAtUtc { get; init; }

    /// <summary>Иконка приложения для подменю трея (может отсутствовать). Не сериализуется.</summary>
    [JsonIgnore]
    public ImageSource? Icon { get; set; }

    /// <summary>Человекочитаемая подпись пункта меню.</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Title) ? "(без заголовка)" : Title;
            if (title.Length > 60) title = title[..57] + "…";
            return $"{title}  —  {ProcessName}";
        }
    }
}
