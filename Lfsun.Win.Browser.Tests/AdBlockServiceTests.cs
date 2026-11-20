using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

/// <summary>
/// 中文注释：广告过滤 EasyList 规则语法回归测试——自定义列表支持 ||domain^ 屏蔽规则、
/// @@||domain^ 例外规则（优先放行）、$options 修饰符剥离与 ! 注释行；纯域名行保持旧行为。
/// </summary>
public sealed class AdBlockServiceTests
{
    private static AdBlockService CreateService(IEnumerable<string> customRules, bool enabled = true)
    {
        var config = new BrowserConfig
        {
            AdBlockEnabled = enabled,
            AdBlockCustomDomains = customRules.ToList(),
        };
        return new AdBlockService(config);
    }

    [Fact]
    public void ShouldBlock_BuiltInDomain_StillBlocked()
    {
        var service = CreateService([]);
        Assert.True(service.ShouldBlock(new Uri("https://doubleclick.net/id")));
        Assert.True(service.ShouldBlock(new Uri("https://ad.example.cnzz.com/pixel")));
    }

    [Fact]
    public void ShouldBlock_Disabled_ReturnsFalseAlways()
    {
        var service = CreateService(["doubleclick.net"], enabled: false);
        Assert.False(service.ShouldBlock(new Uri("https://doubleclick.net/id")));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("||example.com^")]
    [InlineData("||example.com^$script")]
    [InlineData("||example.com^$third-party")]
    [InlineData("||example.com^$image,third-party")]
    [InlineData("||example.com")]
    public void ShouldBlock_PlainAndEasyListRules_BothBlock(string rule)
    {
        var service = CreateService([rule]);

        Assert.True(service.ShouldBlock(new Uri("https://example.com/ad.js")), $"规则应命中: {rule}");
        Assert.True(service.ShouldBlock(new Uri("https://cdn.example.com/ad.js")), $"规则应命中子域: {rule}");
    }

    [Fact]
    public void ShouldBlock_EasyListRule_DoesNotBlockParentOfSubdomain()
    {
        // 中文注释：||ads.example.com^ 只锚定 ads.example.com 及其子域，example.com 本体与兄弟域不受影响。
        var service = CreateService(["||ads.example.com^"]);

        Assert.True(service.ShouldBlock(new Uri("https://ads.example.com/x")));
        Assert.True(service.ShouldBlock(new Uri("https://cdn.ads.example.com/x")));
        Assert.False(service.ShouldBlock(new Uri("https://example.com/x")));
        Assert.False(service.ShouldBlock(new Uri("https://other.example.com/x")));
    }

    [Fact]
    public void ShouldBlock_ExceptionRule_OverridesCustomBlock()
    {
        var service = CreateService(["||example.com^", "@@||example.com^"]);

        Assert.False(service.ShouldBlock(new Uri("https://example.com/x")));
        Assert.False(service.ShouldBlock(new Uri("https://cdn.example.com/x")));
    }

    [Fact]
    public void ShouldBlock_ExceptionRule_OverridesBuiltInBlock()
    {
        var service = CreateService(["@@||clarity.ms^"]);

        Assert.False(service.ShouldBlock(new Uri("https://clarity.ms/hello")));
        Assert.True(service.ShouldBlock(new Uri("https://doubleclick.net/id")));
    }

    [Fact]
    public void ShouldBlock_CommentAndInvalidLines_Ignored()
    {
        var service = CreateService(new[]
        {
            "! 这是一条注释",
            "# 另一种注释",
            "",
            "   ",
            "|http://example.com/full-url-rule",
            "||valid-rule.com^",
            "not a domain!!"
        });

        Assert.True(service.ShouldBlock(new Uri("https://valid-rule.com/x")));
        Assert.False(service.ShouldBlock(new Uri("https://example.com/x")));
    }

    [Fact]
    public void ParseRuleHost_ExtractsDomainFromEasyListSyntax()
    {
        Assert.Equal("ads.example.com", AdBlockService.ParseRuleHost("||ads.example.com^", out var isBlock1));
        Assert.False(isBlock1);

        Assert.Equal("ads.example.com", AdBlockService.ParseRuleHost("@@||ads.example.com^$third-party", out var isException));
        Assert.True(isException);

        Assert.Equal("example.com", AdBlockService.ParseRuleHost("example.com", out _));
        Assert.Equal(string.Empty, AdBlockService.ParseRuleHost("! comment", out _));
        Assert.Equal(string.Empty, AdBlockService.ParseRuleHost("|http://example.com/x", out _));
        Assert.Equal(string.Empty, AdBlockService.ParseRuleHost("not a domain!!", out _));
    }
}
