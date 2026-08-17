using System.Windows.Input;
using AnyTray.Models;
using Xunit;

namespace AnyTray.Tests;

public class HotkeyDefinitionTests
{
    [Theory]
    [InlineData("Ctrl+Alt+H", ModifierKeys.Control | ModifierKeys.Alt, Key.H)]
    [InlineData("Ctrl+Shift+F12", ModifierKeys.Control | ModifierKeys.Shift, Key.F12)]
    [InlineData("Ctrl+Win+D", ModifierKeys.Control | ModifierKeys.Windows, Key.D)]
    [InlineData(" ctrl + alt + space ", ModifierKeys.Control | ModifierKeys.Alt, Key.Space)]
    public void TryParse_Parses_Modifiers_And_Key(string text, ModifierKeys mods, Key key)
    {
        Assert.True(HotkeyDefinition.TryParse(text, out var def));
        Assert.Equal(mods, def.Modifiers);
        Assert.Equal(key, def.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Ctrl+Alt")]              // нет клавиши
    [InlineData("H")]                      // нет модификаторов
    [InlineData("Ctrl+Alt+None")]          // Key.None — не клавиша
    [InlineData("Win+D")]                  // чистый Win+клавиша — перехватывает шорткаты ОС
    [InlineData("Win+E")]                  // зарезервировано ОС
    public void TryParse_Rejects_Invalid(string? text)
    {
        Assert.False(HotkeyDefinition.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_FallsBack_To_Default_On_Invalid()
    {
        // Контракт: при неудаче result должен быть Default (не null).
        Assert.False(HotkeyDefinition.TryParse("garbage", out var def));
        Assert.Equal(HotkeyDefinition.Default.Modifiers, def.Modifiers);
        Assert.Equal(HotkeyDefinition.Default.Key, def.Key);
    }

    [Fact]
    public void ToString_Roundtrips_Through_TryParse()
    {
        var def = new HotkeyDefinition(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.F5);
        var s = def.ToString();
        Assert.True(HotkeyDefinition.TryParse(s, out var parsed));
        Assert.Equal(def.Modifiers, parsed.Modifiers);
        Assert.Equal(def.Key, parsed.Key);
    }

    [Fact]
    public void Default_Is_Valid()
    {
        Assert.True(HotkeyDefinition.Default.IsValid);
    }

    [Fact]
    public void IsValid_False_Without_Modifiers()
    {
        var def = new HotkeyDefinition(ModifierKeys.None, Key.H);
        Assert.False(def.IsValid);
    }

    [Fact]
    public void IsValid_False_For_Win_Only()
    {
        // Чистый Win+клавиша перехватывает шорткаты ОС — не разрешаем.
        var def = new HotkeyDefinition(ModifierKeys.Windows, Key.D);
        Assert.False(def.IsValid);
    }

    [Fact]
    public void IsValid_True_For_Win_With_Ctrl()
    {
        // Win в сочетании с Ctrl/Alt/Shift — разрешён.
        var def = new HotkeyDefinition(ModifierKeys.Windows | ModifierKeys.Control, Key.D);
        Assert.True(def.IsValid);
    }

    [Fact]
    public void ToWin32_Always_Adds_NoRepeat()
    {
        var def = new HotkeyDefinition(ModifierKeys.Control, Key.H);
        def.ToWin32(out uint mods, out _);
        // MOD_NOREPEAT = 0x4000
        Assert.Equal(0x4000u, mods & 0x4000u);
        Assert.Equal(0x0002u, mods & 0x0002u); // MOD_CONTROL
    }
}