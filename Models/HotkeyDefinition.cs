using System.Windows.Input;
using static AnyTray.Native.NativeConstants;

namespace AnyTray.Models;

/// <summary>
/// Описание глобальной горячей клавиши: модификаторы + клавиша.
/// Сериализуется в строку вида "Ctrl+Alt+H".
/// </summary>
public sealed class HotkeyDefinition
{
    public ModifierKeys Modifiers { get; init; }
    public Key Key { get; init; }

    public HotkeyDefinition() { }

    public HotkeyDefinition(ModifierKeys modifiers, Key key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public static HotkeyDefinition Default => new(ModifierKeys.Control | ModifierKeys.Alt, Key.H);

    /// <summary>Перевод в аргументы RegisterHotKey. Всегда добавляет MOD_NOREPEAT.</summary>
    public void ToWin32(out uint fsModifiers, out uint vk)
    {
        uint mods = MOD_NOREPEAT;
        if (Modifiers.HasFlag(ModifierKeys.Alt)) mods |= MOD_ALT;
        if (Modifiers.HasFlag(ModifierKeys.Control)) mods |= MOD_CONTROL;
        if (Modifiers.HasFlag(ModifierKeys.Shift)) mods |= MOD_SHIFT;
        if (Modifiers.HasFlag(ModifierKeys.Windows)) mods |= MOD_WIN;
        fsModifiers = mods;
        vk = (uint)KeyInterop.VirtualKeyFromKey(Key);
    }

    public bool IsValid => Key != Key.None && Modifiers != ModifierKeys.None;

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out HotkeyDefinition result)
    {
        result = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        var mods = ModifierKeys.None;
        Key key = Key.None;

        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win":
                case "windows": mods |= ModifierKeys.Windows; break;
                default:
                    if (Enum.TryParse<Key>(token, ignoreCase: true, out var k))
                        key = k;
                    break;
            }
        }

        if (key == Key.None) return false;
        result = new HotkeyDefinition(mods, key);
        return true;
    }
}
