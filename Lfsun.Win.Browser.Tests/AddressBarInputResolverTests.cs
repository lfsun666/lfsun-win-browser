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
    public void TryResolve_UnknownKeywordPrefix_FallsBackToDefaultSearch()
    {
        var ok = AddressBarInputResolver.TryResolve("zz something", CreateConfig(), out var uri, out var treatedAsSearch);

        Assert.True(ok);
        Assert.True(treatedAsSearch);
        Assert.Equal("www.bing.com", uri.Host);
    }
}
