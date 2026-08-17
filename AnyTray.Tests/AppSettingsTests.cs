using AnyTray.Models;
using Xunit;

namespace AnyTray.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Default_Hotkey_String_Parses_To_Default_Definition()
    {
        var s = new AppSettings();
        Assert.Equal("Ctrl+Alt+H", s.Hotkey);
        var def = s.GetHotkeyDefinition();
        Assert.True(def.IsValid);
        Assert.Equal(HotkeyDefinition.Default.Key, def.Key);
    }

    [Fact]
    public void Clone_Preserves_All_Fields()
    {
        var s = new AppSettings
        {
            AutostartEnabled = true,
            Hotkey = "Ctrl+Shift+F9",
            TitleBarMiddleClickEnabled = false,
            TitleBarMiddleClickDirectHide = true,
            SchemaVersion = 7
        };

        var c = s.Clone();

        Assert.NotSame(s, c);
        Assert.Equal(s.AutostartEnabled, c.AutostartEnabled);
        Assert.Equal(s.Hotkey, c.Hotkey);
        Assert.Equal(s.TitleBarMiddleClickEnabled, c.TitleBarMiddleClickEnabled);
        Assert.Equal(s.TitleBarMiddleClickDirectHide, c.TitleBarMiddleClickDirectHide);
        Assert.Equal(s.SchemaVersion, c.SchemaVersion);
    }

    [Fact]
    public void Clone_Is_Independent_Mutation()
    {
        var s = new AppSettings { Hotkey = "Ctrl+Alt+H" };
        var c = s.Clone();
        c.Hotkey = "Win+D";

        Assert.Equal("Ctrl+Alt+H", s.Hotkey);   // оригинал не изменился
        Assert.Equal("Win+D", c.Hotkey);
    }

    [Fact]
    public void GetHotkeyDefinition_Falls_Back_On_Invalid()
    {
        var s = new AppSettings { Hotkey = "???" };
        var def = s.GetHotkeyDefinition();
        Assert.Equal(HotkeyDefinition.Default.Key, def.Key);
        Assert.Equal(HotkeyDefinition.Default.Modifiers, def.Modifiers);
    }
}