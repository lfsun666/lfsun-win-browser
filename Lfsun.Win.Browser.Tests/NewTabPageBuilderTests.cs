using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.History;
using Lfsun.Win.Browser.UI;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

/// <summary>
/// 中文注释：新标签页速拨面板的搜索引擎接线回归测试——
/// 下拉选项应来自真实引擎目录（含用户自定义引擎），默认引擎与地址栏口径一致，
/// 且焦点环颜色由强调色计算（不再硬编码蓝色）、每日名言按日期稳定。
/// </summary>
public sealed class NewTabPageBuilderTests
{
    private static BrowserConfig CreateConfig(Action<BrowserConfig>? mutate = null)
    {
        var config = new BrowserConfig();
        mutate?.Invoke(config);
        return config;
    }

    [Fact]
    public void Build_IncludesCustomSearchEngineOptions()
    {
        var config = CreateConfig(cfg =>
        {
            cfg.CustomSearchEngines = [new CustomSearchEngine
            {
                Name = "搜狗测试",
                Template = "https://test.example/s?q={query}",
                Keyword = "st",
            }];
        });

        var html = NewTabPageBuilder.Build(config, Array.Empty<HistoryEntry>(), Array.Empty<BookmarkNode>());

        Assert.Contains("<option value=\"搜狗测试\">搜狗测试</option>", html);
        // 中文注释：内置引擎也在目录里，应一并出现。
        Assert.Contains("<option value=\"Bing\">Bing</option>", html);
        Assert.Contains("https://test.example/s?q={query}", html);
    }

    [Fact]
    public void Build_DefaultEngineFollowsConfiguredTemplate()
    {
        var config = CreateConfig(cfg =>
        {
            cfg.DefaultSearchEngineUrlTemplate = "https://test.example/s?q={query}";
            cfg.DefaultSearchEngineName = "搜狗测试";
            cfg.CustomSearchEngines = [new CustomSearchEngine
            {
                Name = "搜狗测试",
                Template = "https://test.example/s?q={query}",
                Keyword = "st",
            }];
        });

        var html = NewTabPageBuilder.Build(config, Array.Empty<HistoryEntry>(), Array.Empty<BookmarkNode>());

        // 中文注释：默认引擎按模板精确匹配自定义引擎，注入的 JS 默认 id 应与其名称一致。
        Assert.Contains("const defaultEngineId = \"搜狗测试\";", html);
        Assert.Contains("const defaultEngineTemplate = \"https://test.example/s?q={query}\";", html);
    }

    [Fact]
    public void Build_AccentRingFollowsAppThemeAccentColor()
    {
        var html = NewTabPageBuilder.Build(CreateConfig(), Array.Empty<HistoryEntry>(), Array.Empty<BookmarkNode>());

        // 中文注释：焦点环颜色由 AppTheme.Accent 计算（rgba 注入），不再硬编码蓝色 rgb(33,115,243)。
        Assert.Contains("--accent-ring: rgba(", html);
        Assert.DoesNotContain("rgba(33, 115, 243", html);
        Assert.DoesNotContain("rgba(33,115,243", html);
    }

    [Fact]
    public void Build_QuoteIsDateStableNotRandom()
    {
        var html = NewTabPageBuilder.Build(CreateConfig(), Array.Empty<HistoryEntry>(), Array.Empty<BookmarkNode>());

        // 中文注释：每日名言按日期取模（当天多次打开稳定同一句），不再每次随机。
        Assert.DoesNotContain("Math.random()", html);
        Assert.Contains("dayHash", html);
    }
}
