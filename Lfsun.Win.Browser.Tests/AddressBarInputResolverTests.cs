using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public class AddressBarInputResolverTests
{
    private static BrowserConfig CreateConfig(string template = "https://www.bing.com/search?q={query}")
    {
        return new BrowserConfig { DefaultSearchEngineUrlTemplate = template };
    }

    [Fact]
    public void TryResolve_EmptyInput_ReturnsFalse()
    {
        var ok = AddressBarInputResolver.TryResolve("   ", CreateConfig(), out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryResolve_PureNumber_TreatedAsSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("163", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Contains("q=163", uri.ToString());
    }

    [Fact]
    public void TryResolve_AbsoluteHttpsUrl_ReturnsSameUrl()
    {
        var ok = AddressBarInputResolver.TryResolve("https://example.com/path", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.False(treatedAsSearch);
        Assert.Equal("https://example.com/path", uri.ToString());
    }

    [Fact]
    public void TryResolve_BareHost_GetsHttpsPrefix()
    {
        var ok = AddressBarInputResolver.TryResolve("example.com", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.False(treatedAsSearch);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("example.com", uri.Host);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:8080")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.1.1:5000")]
    public void TryResolve_LocalHostAndIpv4_TreatedAsUrl(string input)
    {
        var ok = AddressBarInputResolver.TryResolve(input, CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.False(treatedAsSearch);
        Assert.Equal("https", uri.Scheme);
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    [InlineData("[::1]")]
    [InlineData("[2001:db8::1]:443")]
    public void TryResolve_Ipv6Literal_TreatedAsUrlNotSearch(string input)
    {
        var ok = AddressBarInputResolver.TryResolve(input, CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok, $"IPv6 字面量应按 URL 处理: {input}");
        Assert.False(treatedAsSearch, $"IPv6 字面量不应掉进搜索分支: {input}");
        Assert.Equal("https", uri.Scheme);
        // 中文注释：.NET Uri.Host 对 IPv6 主机会去掉外层方括号，主机内必含冒号。
        Assert.Contains(":", uri.Host);
    }

    [Fact]
    public void TryResolve_Ipv6WithPort_PreservesPort()
    {
        var ok = AddressBarInputResolver.TryResolve("[::1]:8080", CreateConfig(), out var uri, out _);

        Assert.True(ok);
        Assert.Equal(8080, uri.Port);
    }

    [Fact]
    public void TryResolve_AmbiguousIpv6WithTrailingSegment_StaysIpv6Address()
    {
        // 中文注释：2001:db8::1:8080 是合法 IPv6 地址（末段即 8080），按地址整体处理，与浏览器惯例一致。
        var ok = AddressBarInputResolver.TryResolve("2001:db8::1:8080", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.False(treatedAsSearch);
        Assert.Equal("https", uri.Scheme);
    }

    [Fact]
    public void TryResolve_NonIpv6ColonText_TreatedAsSearch()
    {
        // 中文注释：含冒号但非 IPv6 字面量的文本（如 “hello:world”）应保持原有搜索行为；
        // 注：纯十六进制形态（如 abc::def）是合法 IPv6 压缩地址，按 URL 处理符合浏览器惯例。
        var ok = AddressBarInputResolver.TryResolve("hello:world", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
    }

    [Fact]
    public void TryResolve_PlainKeywordWithSpace_TreatedAsSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("hello world", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Contains("hello%20world", uri.AbsoluteUri);
    }

    [Fact]
    public void TryResolve_SearchTemplateWithoutPlaceholder_ReturnsFalseForKeyword()
    {
        var ok = AddressBarInputResolver.TryResolve("hello world", CreateConfig("https://bad.example.com/search"), out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolve_AboutBlank_TreatedAsUrl()
    {
        var ok = AddressBarInputResolver.TryResolve("about:blank", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.False(treatedAsSearch);
        Assert.Equal("about", uri.Scheme);
    }

    [Fact]
    public void TryResolve_CustomEngineKeywordPrefix_UsesCustomTemplate()
    {
        var config = CreateConfig();
        config.CustomSearchEngines.Add(new CustomSearchEngine
        {
            Name = "GitHub",
            Keyword = "gh",
            Template = "https://github.com/search?q={query}"
        });

        var ok = AddressBarInputResolver.TryResolve("gh lfsun browser", config, out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("github.com", uri.Host);
        Assert.Contains("q=lfsun%20browser", uri.AbsoluteUri);
    }

    [Fact]
    public void TryResolve_BuiltInKeywordPrefix_UsesBuiltInTemplate()
    {
        var ok = AddressBarInputResolver.TryResolve("ddg privacy", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("duckduckgo.com", uri.Host);
    }

    [Fact]
    public void TryResolve_QuestionMarkPrefix_ForcesDefaultSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("? example.com", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("www.bing.com", uri.Host);
        Assert.Contains("q=example.com", uri.AbsoluteUri);
    }

    [Fact]
    public void TryResolve_ChineseQuestionMarkPrefix_ForcesDefaultSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("？ 百度云盘", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("www.bing.com", uri.Host);
        Assert.Contains("q=%E7%99%BE%E5%BA%A6%E4%BA%91%E7%9B%98", uri.AbsoluteUri);
    }

    [Fact]
    public void TryResolve_UnknownKeywordPrefix_FallsBackToDefaultSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("zz something", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("www.bing.com", uri.Host);
    }
}
