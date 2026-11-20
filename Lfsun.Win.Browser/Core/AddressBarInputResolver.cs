using System.Net;
using System.Net.Sockets;
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

        // 中文注释：问号前缀强制触发默认搜索引擎检索（如 “? 关键词” 或 “？ 关键词”）。
        if (value.StartsWith("? ") || value.StartsWith("？ "))
        {
            var query = value[2..].Trim();
            if (query.Length > 0 && TryBuildSearchUri(query, config.DefaultSearchEngineUrlTemplate, out var explicitSearchUri))
            {
                treatedAsSearch = true;
                targetUri = explicitSearchUri;
                return true;
            }
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

        // 中文注释：IPv6 字面量（::1 / 2001:db8::1 / [::1]:8080）——裸 IPv6 不是合法 URL 形态，
        // 需补中括号后再拼 https，否则会掉进搜索分支。
        if (TryNormalizeIpv6Literal(value, out var ipv6HostPart))
        {
            if (Uri.TryCreate($"https://{ipv6HostPart}/", UriKind.Absolute, out var ipv6Uri) && ipv6Uri is not null)
            {
                targetUri = ipv6Uri;
                return true;
            }
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

    /// <summary>
    /// 中文注释：识别地址栏里的 IPv6 字面量并归一为 URL 主机形态。
    /// 支持三种写法：裸地址（2001:db8::1）、带端口的中括号形式（[::1]:8080）、
    /// 纯中括号包裹（[::1]）；返回 “[地址]” 或 “[地址]:端口” 形态供拼接 scheme。
    /// 端口与地址末段歧义（2001:db8::1:8080）按浏览器惯例整体视作 IPv6 地址。
    /// </summary>
    private static bool TryNormalizeIpv6Literal(string input, out string hostPart)
    {
        hostPart = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || !input.Contains(':'))
        {
            return false;
        }

        var candidate = input.Trim();
        if (candidate.StartsWith('['))
        {
            var closeIndex = candidate.IndexOf(']');
            // 中文注释：closeIndex <= 1 表示未闭合或空括号；']' 恰在末位（如 [::1]）是合法无端口形态。
            if (closeIndex <= 1)
            {
                return false;
            }

            var portSuffix = candidate[(closeIndex + 1)..];
            if (portSuffix.Length > 0)
            {
                if (portSuffix[0] != ':' || portSuffix.Length < 2 ||
                    !int.TryParse(portSuffix[1..], out var port) || port is < 0 or > 65535)
                {
                    // 中文注释：括号后只能跟合法端口，否则按带路径等复杂形态交给后续分支。
                    return false;
                }
            }

            var bracketed = candidate[1..closeIndex];
            if (!TryParseIpv6Address(bracketed, out var bare))
            {
                return false;
            }

            hostPart = portSuffix.Length > 0 ? $"[{bare}]{portSuffix}" : $"[{bare}]";
            return true;
        }

        // 中文注释：裸地址形态（含末段端口歧义），直接按 IPv6 地址解析。
        if (!TryParseIpv6Address(candidate, out var bareAddress))
        {
            return false;
        }

        hostPart = $"[{bareAddress}]";
        return true;
    }

    // 中文注释：仅接受 IPv6 家族；IPv4 交给既有 Ipv4Pattern 分支处理。
    private static bool TryParseIpv6Address(string text, out string normalized)
    {
        normalized = string.Empty;
        if (IPAddress.TryParse(text, out var address) &&
            address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            normalized = address.ToString();
            return true;
        }

        return false;
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

