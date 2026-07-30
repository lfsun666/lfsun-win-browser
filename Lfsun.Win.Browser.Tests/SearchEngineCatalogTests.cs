using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public class SearchEngineCatalogTests
{
    [Fact]
    public void BuiltIns_ContainsFourEngines()
    {
        Assert.Equal(4, SearchEngineCatalog.BuiltIns.Count);
        Assert.All(SearchEngineCatalog.BuiltIns, option => Assert.Contains("{query}", option.Template));
    }

    [Theory]
    [InlineData("Bing")]
    [InlineData("bing")]
    [InlineData("BING")]
    public void FindByName_IgnoresCase(string name)
    {
        var option = SearchEngineCatalog.FindByName(name);

        Assert.NotNull(option);
        Assert.Equal("Bing", option!.Name);
    }

    [Fact]
    public void FindByName_Unknown_ReturnsNull()
    {
        Assert.Null(SearchEngineCatalog.FindByName("Yahoo"));
    }

    [Fact]
    public void FindByTemplate_MatchesKnownTemplate()
    {
        var option = SearchEngineCatalog.FindByTemplate("https://www.baidu.com/s?wd={query}");

        Assert.NotNull(option);
        Assert.Equal("百度", option!.Name);
    }

    [Fact]
    public void FindByTemplate_Unknown_ReturnsNull()
    {
        Assert.Null(SearchEngineCatalog.FindByTemplate("https://unknown.example.com/?q={query}"));
    }
}
