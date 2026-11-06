using Lfsun.Win.Browser.UI;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class QrCodeGeneratorTests
{
    [Fact]
    public void Generate_ShouldReturnValidBitmap_ForUrl()
    {
        const string url = "https://github.com/lfsun666/lfsun-win-browser";
        using var bitmap = QrCodeGenerator.Generate(url, pixelSize: 4, margin: 2);

        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
        Assert.Equal(bitmap.Width, bitmap.Height);
    }

    [Fact]
    public void Generate_ShouldHandleShortAndLongContent()
    {
        using var shortBmp = QrCodeGenerator.Generate("hi", pixelSize: 2, margin: 1);
        Assert.NotNull(shortBmp);

        var longUrl = "https://example.com/search?q=" + new string('a', 80);
        using var longBmp = QrCodeGenerator.Generate(longUrl, pixelSize: 2, margin: 1);
        Assert.NotNull(longBmp);
        Assert.True(longBmp.Width > shortBmp.Width);
    }
}
