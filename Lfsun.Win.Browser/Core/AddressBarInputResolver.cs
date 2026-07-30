using System.Text.RegularExpressions;

namespace Lfsun.Win.Browser.Core;

public static class AddressBarInputResolver
{
    private static readonly Regex Ipv4Pattern = new(@"^\d{1,3}(\.\d{1,3}){3}$", RegexOptions.Compiled);
    private static readonly Regex Ipv4WithPortPattern = new(@"^\d{1,3}(\.\d{1,3}){3}:\d{1,5}$", RegexOptions.Compiled);

    public static bool TryResolve(string rawInput, BrowserConfig config, out Uri targetUri, out bool treatedAsSearch)
    {
        treatedAsSearch = false;
        targetUri = new Uri("about:blank");
        var value = rawInput.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // 中文注释：关键字前缀触发指定搜索引擎（如 “gh 关键词”），命中后剩余文本作为搜索词。
        var spaceIndex = value.IndexOf(' ');
        if (spaceIndex > 0 && spaceIndex < value.Length - 1)
        {
            var keyword = value[..spaceIndex];
            var remainder = value[(spaceIndex + 1)..].Trim();
            if (remainder.Length > 0 &&
                SearchEngineCatalog.FindByKeyword(keyword, config) is { } keywordEngine &&
                TryBuildSearchUri(remainder, keywordEngine.Template, out var keywordSearchUri))
            {
                treatedAsSearch = true;
                targetUri = keywordSearchUri;
                return true;
            }
        }

        // Pure numeric input should be treated as a search keyword.
        if (value.All(char.IsDigit))
        {
            if (TryBuildSearchUri(value, config.DefaultSearchEngineUrlTemplate, out var numericSearchUri))
            {
                treatedAsSearch = true;
                targetUri = numericSearchUri;
                return true;
            }
        }

        if (LooksLikeAbsoluteUrl(value) && Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute is not null)
        {
            targetUri = absolute;
            return true;
        }

        if (LooksLikeHostOrPath(value) && Uri.TryCreate($"https://{value}", UriKind.Absolute, out var httpsUri) && httpsUri is not null)
        {
            targetUri = httpsUri;
            return true;
        }

        if (TryBuildSearchUri(value, config.DefaultSearchEngineUrlTemplate, out var searchUri))
        {
            treatedAsSearch = true;
            targetUri = searchUri;
            return true;
        }

        return false;
    }

    private static bool LooksLikeAbsoluteUrl(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri is null)
        {
            return false;
        }

        return uri.Scheme is "http" or "https" or "about" or "file";
    }

    private static bool LooksLikeHostOrPath(string input)
    {
        if (input.Contains(' '))
        {
            return false;
        }

        if (input.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Ipv4Pattern.IsMatch(input))
        {
            return true;
        }

        if (Ipv4WithPortPattern.IsMatch(input))
        {
            return true;
        }

        if (input.Contains('/') || input.Contains('\\'))
        {
            return true;
        }

        // Pure number (e.g. 163) should go search, not URL.
        if (input.All(char.IsDigit))
        {
            return false;
        }

        if (input.Contains('.') && input.Any(char.IsLetter))
        {
            return true;
        }

        if (input.Contains(':') && input.Contains('.') && !input.All(char.IsDigit))
        {
            return true;
        }

        return false;
    }

    private static bool TryBuildSearchUri(string query, string template, out Uri uri)
    {
        uri = new Uri("about:blank");
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var normalizedTemplate = template.Trim();
        if (!normalizedTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encodedQuery = Uri.EscapeDataString(query);
        var target = normalizedTemplate.Replace("{query}", encodedQuery, StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(target, UriKind.Absolute, out var parsedUri) || parsedUri is null)
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }
}

