using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.UI;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class ReaderModeAndSplitViewTests
{
    [Fact]
    public void ReaderModeHelper_ScriptsShouldBeValidAndContainKeyMarkers()
    {
        Assert.False(string.IsNullOrWhiteSpace(ReaderModeHelper.ToggleReaderScript));
        Assert.Contains("__lfsun_reader_overlay__", ReaderModeHelper.ToggleReaderScript);
        Assert.Contains("article", ReaderModeHelper.ToggleReaderScript);
        Assert.Contains("estMinutes", ReaderModeHelper.ToggleReaderScript);

        Assert.False(string.IsNullOrWhiteSpace(ReaderModeHelper.ExitReaderScript));
        Assert.Contains("__lfsun_exit_reader", ReaderModeHelper.ExitReaderScript);

        Assert.False(string.IsNullOrWhiteSpace(ReaderModeHelper.PictureInPictureScript));
        Assert.Contains("requestPictureInPicture", ReaderModeHelper.PictureInPictureScript);
    }

    [Fact]
    public void ToolbarButtonIds_ShouldContainSplitViewAndReaderMode()
    {
        Assert.Equal("splitView", ToolbarButtonIds.SplitView);
        Assert.Equal("readerMode", ToolbarButtonIds.ReaderMode);

        var defaults = BrowserConfig.CreateDefaultToolbarButtons();
        Assert.Contains(defaults, b => b.Id == ToolbarButtonIds.SplitView);
        Assert.Contains(defaults, b => b.Id == ToolbarButtonIds.ReaderMode);
    }

    [Fact]
    public void NewTabPageBuilder_ShouldContainEngineSelectorAndQuotes()
    {
        var config = new BrowserConfig();
        var html = NewTabPageBuilder.Build(config, [], []);

        Assert.Contains("id=\"engineSelect\"", html);
        // 中文注释：引擎下拉选项改由真实引擎目录生成（内置 Bing/百度/Google/DuckDuckGo + 自定义引擎）。
        Assert.Contains("value=\"Bing\"", html);
        Assert.Contains("value=\"百度\"", html);
        Assert.Contains("value=\"Google\"", html);
        Assert.Contains("id=\"quote\"", html);
    }
}
