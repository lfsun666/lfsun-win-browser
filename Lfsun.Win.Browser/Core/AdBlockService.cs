namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 中文注释：简版广告过滤服务。按域名后缀匹配内置广告/统计域名与用户自定义屏蔽域名，
/// 由 WebView2 的 WebResourceRequested 拦截命中的请求。
/// </summary>
public sealed class AdBlockService
{
    // 中文注释：内置常见广告与统计域名（纯域名后缀匹配，不做规则语法解析）。
    private static readonly HashSet<string> BuiltInDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        // 谷歌系广告与统计
        "doubleclick.net",
        "googlesyndication.com",
        "googleadservices.com",
        "google-analytics.com",
        "googletagmanager.com",
        "googletagservices.com",
        "adservice.google.com",
        "2mdn.net",
        "admob.com",
        // 主流广告交易与投放平台
        "adnxs.com",
        "adsrvr.org",
        "adsafeprotected.com",
        "advertising.com",
        "amazon-adsystem.com",
        "criteo.com",
        "criteo.net",
        "outbrain.com",
        "taboola.com",
        "moatads.com",
        "doubleverify.com",
        "rubiconproject.com",
        "pubmatic.com",
        "openx.net",
        "casalemedia.com",
        "smartadserver.com",
        "teads.tv",
        "yieldmo.com",
        "sharethrough.com",
        "media.net",
        "bidswitch.net",
        "mathtag.com",
        "rlcdn.com",
        "krxd.net",
        "bluekai.com",
        "demdex.net",
        "everesttech.net",
        "agkn.com",
        "exelator.com",
        "contextweb.com",
        "spotxchange.com",
        "innovid.com",
        "serving-sys.com",
        "sizmek.com",
        "flashtalking.com",
        "zedo.com",
        "adform.net",
        "adroll.com",
        "adcolony.com",
        "unityads.unity3d.com",
        "applovin.com",
        "chartboost.com",
        "inmobi.com",
        "mopub.com",
        "vungle.com",
        "ironsrc.com",
        "supersonicads.com",
        "adcash.com",
        "exoclick.com",
        "popads.net",
        "propellerads.com",
        "trafficjunky.net",
        "juicyads.com",
        "revcontent.com",
        "mgid.com",
        "adblade.com",
        "undertone.com",
        "sonobi.com",
        "gumgum.com",
        "triplelift.com",
        "indexexchange.com",
        "lijit.com",
        "sovrn.com",
        "33across.com",
        "yieldlab.net",
        "improvedigital.com",
        "adition.com",
        "smaato.net",
        "stroeer.de",
        // 行为统计与会话回放
        "scorecardresearch.com",
        "quantserve.com",
        "quantcount.com",
        "hotjar.com",
        "mixpanel.com",
        "segment.io",
        "segment.com",
        "amplitude.com",
        "mouseflow.com",
        "fullstory.com",
        "crazyegg.com",
        "chartbeat.com",
        "parsely.com",
        "clicktale.net",
        "luckyorange.com",
        "inspectlet.com",
        "kissmetrics.com",
        "heapanalytics.com",
        "matomo.cloud",
        "statcounter.com",
        "histats.com",
        "addthis.com",
        "sharethis.com",
        "branch.io",
        "appsflyer.com",
        "adjust.com",
        "kochava.com",
        "singular.net",
        "connect.facebook.net",
        "ads.linkedin.com",
        "ads-twitter.com",
        "static.ads-twitter.com",
        "analytics.tiktok.com",
        "ads.tiktok.com",
        "bat.bing.com",
        "clarity.ms",
        // 国内常见广告与统计
        "cnzz.com",
        "umeng.com",
        "umengcloud.com",
        "51.la",
        "hm.baidu.com",
        "cpro.baidu.com",
        "pos.baidu.com",
        "tanx.com",
        "alimama.com",
        "mmstat.com",
        "irs01.com",
        "miaozhen.com",
        "admaster.com.cn",
        "talkingdata.com",
        "talkingdata.net",
        "growingio.com",
        "sensorsdata.cn",
        "gridsumdissector.com",
        "mediav.com",
        "ipinyou.com"
    };

    // 中文注释：EasyList 域名规则中主机名的截断字符（^/$/|/*/ /?/:）。
    private static readonly char[] RuleTrimChars = { '^', '$', '|', '*', '/', '?', ':' };

    private readonly BrowserConfig _config;
    // 中文注释：自定义域名按配置列表引用做缓存，设置保存后引用变化时自动重建。
    private List<string>? _cachedCustomSource;
    private HashSet<string> _customDomains = new(StringComparer.OrdinalIgnoreCase);
    // 中文注释：EasyList 例外规则（@@||domain^）解析出的放行域名，优先级高于屏蔽规则。
    private HashSet<string> _customExceptionDomains = new(StringComparer.OrdinalIgnoreCase);

    public AdBlockService(BrowserConfig config)
    {
        _config = config;
    }

    public bool IsEnabled => _config.AdBlockEnabled;

    public bool ShouldBlock(Uri requestUri)
    {
        if (!_config.AdBlockEnabled)
        {
            return false;
        }

        var host = requestUri.Host;
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        RefreshCustomDomainsIfNeeded();

        // 中文注释：从完整主机名逐级去掉最左标签做后缀匹配（ads.example.com → example.com → com）。
        var candidate = host;
        while (true)
        {
            // 中文注释：例外域名（@@ 规则）优先放行——覆盖内置与自定义的任何屏蔽规则。
            if (_customExceptionDomains.Contains(candidate))
            {
                return false;
            }

            if (BuiltInDomains.Contains(candidate) || _customDomains.Contains(candidate))
            {
                return true;
            }

            var dotIndex = candidate.IndexOf('.');
            if (dotIndex < 0 || dotIndex == candidate.Length - 1)
            {
                return false;
            }

            candidate = candidate[(dotIndex + 1)..];
        }
    }

    private void RefreshCustomDomainsIfNeeded()
    {
        var source = _config.AdBlockCustomDomains;
        if (ReferenceEquals(source, _cachedCustomSource))
        {
            return;
        }

        _cachedCustomSource = source;
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in source ?? [])
        {
            var domain = ParseRuleHost(rawLine, out var isException);
            if (string.IsNullOrEmpty(domain))
            {
                continue;
            }

            if (isException)
            {
                exceptions.Add(domain);
            }
            else
            {
                blocked.Add(domain);
            }
        }

        _customDomains = blocked;
        _customExceptionDomains = exceptions;
    }

    /// <summary>
    /// 中文注释：解析一行自定义规则的主机名部分，兼容两类写法：
    /// 1) 纯域名（旧行为，向后兼容）：example.com；
    /// 2) EasyList 域名规则：屏蔽 `||example.com^` 与例外 `@@||example.com^`（`^/$/|` 等分隔符与
    ///    `$script`、`$third-party` 选项全部剥离；`!`/`#` 注释行忽略）。
    /// 仅提取主机锚定的域名规则（EasyList 中数量最大的一类）；不支持的形态（如完整 URL 正则规则）返回空串忽略。
    /// </summary>
    internal static string ParseRuleHost(string? rawLine, out bool isException)
    {
        isException = false;
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return string.Empty;
        }

        var line = rawLine.Trim();
        if (line.StartsWith('!') || line.StartsWith('#'))
        {
            return string.Empty;
        }

        if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            isException = true;
            line = line[2..].TrimStart();
        }

        if (line.StartsWith("||", StringComparison.Ordinal))
        {
            line = line[2..];
        }
        else if (line.StartsWith('|') || line.StartsWith("://", StringComparison.Ordinal))
        {
            // 中文注释：|http://... 形态的完整 URL 规则不做主机锚定解析，忽略。
            return string.Empty;
        }

        // 中文注释：剥离规则修饰符：^（分隔符）$（选项）|（锚）* / ? : （通配/路径/端口）。
        var cutIndex = line.IndexOfAny(RuleTrimChars);
        if (cutIndex >= 0)
        {
            line = line[..cutIndex];
        }

        // 中文注释：剩下部分须形如域名（仅字母数字点连字符、含点且不空段）。
        if (line.Length == 0 || !line.Contains('.') || line.StartsWith('.') || line.EndsWith('.'))
        {
            return string.Empty;
        }

        foreach (var ch in line)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('.' or '-'))
            {
                return string.Empty;
            }
        }

        return line.ToLowerInvariant();
    }
}
