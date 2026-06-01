using System.Windows.Media;

namespace AnyTray.Models;

/// <summary>
/// Лёгкое описание открытого (ещё не скрытого) окна — для списка «Скрыть окно ▸» в меню трея.
/// </summary>
public sealed class OpenWindowInfo
{
    public nint Hwnd { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public ImageSource? Icon { get; init; }
}
