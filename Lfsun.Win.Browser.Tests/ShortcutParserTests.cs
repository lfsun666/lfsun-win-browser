using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public class ShortcutParserTests
{
    [Theory]
    [InlineData("Ctrl+D", Keys.Control | Keys.D)]
    [InlineData("ctrl+shift+t", Keys.Control | Keys.Shift | Keys.T)]
    [InlineData("Control+Alt+F4", Keys.Control | Keys.Alt | Keys.F4)]
    [InlineData("F5", Keys.F5)]
    public void TryParse_ValidShortcuts_ReturnsExpectedKeyData(string raw, Keys expected)
    {
        var ok = ShortcutParser.TryParse(raw, out var keyData);

        Assert.True(ok);
        Assert.Equal(expected, keyData);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+NotAKey")]
    public void TryParse_InvalidShortcuts_ReturnsFalse(string raw)
    {
        var ok = ShortcutParser.TryParse(raw, out var keyData);

        Assert.False(ok);
        Assert.Equal(Keys.None, keyData);
    }

    [Fact]
    public void ToDisplayString_OrdersModifiersCtrlShiftAlt()
    {
        var text = ShortcutParser.ToDisplayString(Keys.Alt | Keys.Shift | Keys.Control | Keys.K);

        Assert.Equal("Ctrl+Shift+Alt+K", text);
    }

    [Fact]
    public void Normalize_LowercaseInput_ProducesCanonicalForm()
    {
        Assert.Equal("Ctrl+D", ShortcutParser.Normalize("ctrl + d"));
    }

    [Fact]
    public void Normalize_UnparsableInput_ReturnsTrimmedOriginal()
    {
        Assert.Equal("not-a-shortcut", ShortcutParser.Normalize("  not-a-shortcut  "));
    }

    [Fact]
    public void TryParse_RoundTripsWithToDisplayString()
    {
        Assert.True(ShortcutParser.TryParse("Ctrl+Shift+N", out var keyData));
        var display = ShortcutParser.ToDisplayString(keyData);
        Assert.True(ShortcutParser.TryParse(display, out var reparsed));
        Assert.Equal(keyData, reparsed);
    }
}
