namespace AnyTray.Models;

/// <summary>Режим показа overlay-кнопки на заголовке чужого окна.</summary>
public enum OverlayDisplayMode
{
    /// <summary>Показывать всегда, пока целевое окно видимо и в фокусе.</summary>
    AlwaysWhenForeground = 0,

    /// <summary>Показывать только при наведении курсора на область заголовка.</summary>
    OnHoverOnly = 1
}
