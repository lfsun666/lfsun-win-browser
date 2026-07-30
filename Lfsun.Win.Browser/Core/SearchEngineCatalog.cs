namespace Lfsun.Win.Browser.Core;

public sealed record SearchEngineOption(string Name, string Template, string Keyword = "");

// 中文注释：用户自定义搜索引擎，Keyword 为地址栏前缀触发词（如 gh 关键词）。
public sealed class CustomSearchEngine
{
    public string Name { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
}

public static class SearchEngineCatalog
{
    private static readonly SearchEngineOption[] BuiltInOptions =
    [
        new("Bing", "https://www.bing.com/search?q={query}", "bing"),
        new("百度", "https://www.baidu.com/s?wd={query}", "bd"),
        new("Google", "https://www.google.com/search?q={query}", "g"),
        new("DuckDuckGo", "https://duckduckgo.com/?q={query}", "ddg")
    ];

    public static IReadOnlyList<SearchEngineOption> BuiltIns => BuiltInOptions;

    // 中文注释：内置 + 用户自定义（同名时自定义优先剔除内置）。
    public static IReadOnlyList<SearchEngineOption> GetAll(BrowserConfig config)
    {
        var customs = (config.CustomSearchEngines ?? [])
            .Where(engine => IsValidTemplate(engine.Template) && !string.IsNullOrWhiteSpace(engine.Name))
            .Select(engine => new SearchEngineOption(
                engine.Name.Trim(),
                engine.Template.Trim(),
                engine.Keyword?.Trim() ?? string.Empty))
            .ToList();
        var merged = BuiltInOptions
            .Where(builtIn => customs.All(custom =>
                !string.Equals(custom.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        merged.AddRange(customs);
        return merged;
    }

    public static SearchEngineOption? FindByKeyword(string keyword, BrowserConfig config)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        return GetAll(config).FirstOrDefault(option =>
            !string.IsNullOrWhiteSpace(option.Keyword) &&
            string.Equals(option.Keyword, keyword.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static SearchEngineOption? FindByName(string name)
    {
        return BuiltInOptions.FirstOrDefault(option =>
            string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static SearchEngineOption? FindByTemplate(string template)
    {
        return BuiltInOptions.FirstOrDefault(option =>
            string.Equals(option.Template, template, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidTemplate(string? template)
    {
        return !string.IsNullOrWhiteSpace(template) &&
               template.Contains("{query}", StringComparison.OrdinalIgnoreCase);
    }
}
