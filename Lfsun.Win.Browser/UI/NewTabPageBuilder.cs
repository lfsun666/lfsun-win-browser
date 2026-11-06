using System.Net;
using System.Text;
using System.Text.Json;
using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.History;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 新标签页速拨面板生成器。
/// 输出完整本地 HTML（NavigateToString 渲染）：问候语+数字时钟、默认引擎搜索框、快捷功能芯片、
/// 常访站点卡片（历史访问频次 Top8，不足时用书签补齐），配色无缝跟随 AppTheme。
/// </summary>
public static class NewTabPageBuilder
{
    private const int MaxTopSites = 8;

    public static string Build(
        BrowserConfig config,
        IReadOnlyList<HistoryEntry> historyEntries,
        IReadOnlyList<BookmarkNode> bookmarkItems)
    {
        var topSites = CollectTopSites(historyEntries, bookmarkItems);
        var engineName = string.IsNullOrWhiteSpace(config.DefaultSearchEngineName)
            ? "必应"
            : config.DefaultSearchEngineName.Trim();
        var searchTemplateJs = JsonSerializer.Serialize(config.DefaultSearchEngineUrlTemplate);

        var cardsHtml = new StringBuilder();
        foreach (var site in topSites)
        {
            var encodedUrl = WebUtility.HtmlEncode(site.Url);
            var encodedTitle = WebUtility.HtmlEncode(site.Title);
            var encodedHost = WebUtility.HtmlEncode(site.Host);
            var badge = WebUtility.HtmlEncode(ResolveBadgeText(site.Title, site.Host));
            cardsHtml.Append($"""
                <a class="card" href="{encodedUrl}" title="{encodedTitle}&#10;{encodedUrl}">
                  <span class="badge">{badge}</span>
                  <span class="card-title">{encodedTitle}</span>
                  <span class="card-host">{encodedHost}</span>
                </a>
                """);
        }

        var emptyHtml = topSites.Count == 0
            ? """<div class="empty">开始浏览后，这里会自动聚合你的高频访问站点</div>"""
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>新标签页</title>
            <style>
              :root {
                --bg: {{ToCss(AppTheme.WindowBackground)}};
                --surface: {{ToCss(AppTheme.Surface)}};
                --surface-alt: {{ToCss(AppTheme.SurfaceAlt)}};
                --border: {{ToCss(AppTheme.PanelBorder)}};
                --text: {{ToCss(AppTheme.TextPrimary)}};
                --text-2: {{ToCss(AppTheme.TextSecondary)}};
                --accent: {{ToCss(AppTheme.Accent)}};
                --input-bg: {{ToCss(AppTheme.InputBackground)}};
              }
              * { margin: 0; padding: 0; box-sizing: border-box; }
              body {
                min-height: 100vh; display: flex; flex-direction: column; align-items: center;
                background: var(--bg); color: var(--text);
                font-family: -apple-system, "Microsoft YaHei UI", "Segoe UI", sans-serif;
                padding: 7vh 24px 36px;
                user-select: none;
                transition: background-color .2s ease;
              }
              .clock {
                font-size: 52px; font-weight: 700; letter-spacing: 2px;
                background: linear-gradient(135deg, var(--text) 30%, var(--accent));
                -webkit-background-clip: text; -webkit-text-fill-color: transparent;
              }
              .greeting { margin-top: 6px; font-size: 15px; color: var(--text-2); font-weight: 500; }
              .search-container {
                margin-top: 32px; width: min(660px, 92vw); display: flex; flex-direction: column; align-items: center; gap: 14px;
              }
              .search-box {
                width: 100%; display: flex;
                background: var(--input-bg); border: 1.5px solid var(--border); border-radius: 26px;
                overflow: hidden; transition: all .2s cubic-bezier(0.16, 1, 0.3, 1);
                box-shadow: 0 4px 16px rgba(0,0,0,.04);
              }
              .search-box:focus-within {
                border-color: var(--accent);
                box-shadow: 0 6px 26px rgba(0,0,0,.14), 0 0 0 3px rgba(33, 115, 243, .18);
              }
              .search-box input {
                flex: 1; border: 0; outline: 0; background: transparent; color: var(--text);
                font-size: 15px; padding: 14px 22px;
              }
              .search-box button {
                border: 0; cursor: pointer; background: var(--accent); color: #fff;
                font-size: 14px; font-weight: 600; padding: 0 28px;
                transition: opacity .15s ease, filter .15s ease;
              }
              .search-box button:hover { opacity: .92; filter: brightness(1.05); }
              .quick-nav {
                display: flex; flex-wrap: wrap; justify-content: center; gap: 8px;
              }
              .quick-chip {
                display: inline-flex; align-items: center; gap: 5px;
                padding: 6px 12px; border-radius: 16px;
                font-size: 12px; color: var(--text-2); text-decoration: none;
                background: var(--surface-alt); border: 1px solid var(--border);
                transition: all .16s ease;
              }
              .quick-chip:hover {
                color: var(--accent); border-color: var(--accent); transform: translateY(-1.5px);
              }
              .section-title {
                margin: 40px 0 16px; font-size: 13px; color: var(--text-2);
                align-self: center; font-weight: 600; letter-spacing: 1px; text-transform: uppercase;
              }
              .grid {
                display: grid; grid-template-columns: repeat(4, 136px); gap: 16px;
              }
              @media (max-width: 640px) { .grid { grid-template-columns: repeat(2, 136px); } }
              .card {
                display: flex; flex-direction: column; align-items: center; gap: 8px;
                background: var(--surface); border: 1px solid var(--border); border-radius: 16px;
                padding: 18px 12px 14px; text-decoration: none; color: var(--text);
                transition: transform .18s cubic-bezier(0.16, 1, 0.3, 1), box-shadow .18s ease, border-color .18s ease;
                box-shadow: 0 2px 8px rgba(0,0,0,.03);
              }
              .card:hover {
                transform: translateY(-4px); border-color: var(--accent);
                box-shadow: 0 10px 24px rgba(0,0,0,.12);
              }
              .badge {
                width: 46px; height: 46px; border-radius: 13px; display: flex;
                align-items: center; justify-content: center; font-size: 19px; font-weight: 700;
                color: #fff; background: var(--accent);
                box-shadow: 0 4px 12px rgba(0,0,0,.15);
              }
              .card-title {
                max-width: 116px; font-size: 12.5px; font-weight: 600; white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis;
              }
              .card-host {
                max-width: 116px; font-size: 11px; color: var(--text-2); white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis;
              }
              .empty { margin-top: 12px; font-size: 13px; color: var(--text-2); }
              .footer-version {
                margin-top: auto; padding-top: 48px; font-size: 12px; color: var(--text-2);
                opacity: .85; letter-spacing: .3px;
              }
            </style>
            </head>
            <body>
              <div class="clock" id="clock">--:--</div>
              <div class="greeting" id="greeting"></div>
              <div class="search-container">
                <form class="search-box" id="searchForm">
                  <input id="searchInput" type="text" autofocus autocomplete="off"
                         placeholder="使用 {{WebUtility.HtmlEncode(engineName)}} 搜索，或输入网址后回车">
                  <button type="submit">搜索</button>
                </form>
                <div class="quick-nav">
                  <a class="quick-chip" href="https://www.baidu.com">百度</a>
                  <a class="quick-chip" href="https://cn.bing.com">必应</a>
                  <a class="quick-chip" href="https://github.com">GitHub</a>
                  <a class="quick-chip" href="https://gitee.com">Gitee</a>
                  <a class="quick-chip" href="https://www.bilibili.com">哔哩哔哩</a>
                  <a class="quick-chip" href="https://juejin.cn">稀土掘金</a>
                </div>
              </div>
              <div class="section-title">常访站点</div>
              <div class="grid">{{cardsHtml}}</div>
              {{emptyHtml}}
              <div class="footer-version">{{AppVersionInfo.ProductName}} v{{AppVersionInfo.CurrentVersion}} · {{AppVersionInfo.Slogan}}</div>
            <script>
              const searchTemplate = {{searchTemplateJs}};
              function refreshClock() {
                const now = new Date();
                const pad = v => String(v).padStart(2, "0");
                document.getElementById("clock").textContent = pad(now.getHours()) + ":" + pad(now.getMinutes());
                const hour = now.getHours();
                const greeting = hour < 6 ? "夜深了，注意休息"
                  : hour < 12 ? "早上好"
                  : hour < 14 ? "中午好"
                  : hour < 18 ? "下午好"
                  : "晚上好";
                const week = ["日","一","二","三","四","五","六"][now.getDay()];
                document.getElementById("greeting").textContent =
                  greeting + " · " + (now.getMonth() + 1) + "月" + now.getDate() + "日 星期" + week;
              }
              refreshClock();
              setInterval(refreshClock, 15000);
              document.getElementById("searchForm").addEventListener("submit", event => {
                event.preventDefault();
                const raw = document.getElementById("searchInput").value.trim();
                if (!raw) { return; }
                if (/^https?:\/\//i.test(raw)) {
                  location.href = raw;
                } else if (/^[\w-]+(\.[\w-]+)+(\/\S*)?$/.test(raw)) {
                  location.href = "https://" + raw;
                } else {
                  location.href = searchTemplate.replace("{query}", encodeURIComponent(raw));
                }
              });
            </script>
            </body>
            </html>
            """;
    }

    private sealed record TopSite(string Title, string Url, string Host);

    private static List<TopSite> CollectTopSites(
        IReadOnlyList<HistoryEntry> historyEntries,
        IReadOnlyList<BookmarkNode> bookmarkItems)
    {
        var sites = historyEntries
            .Select(entry => new { entry, uri = ToWebUri(entry.Url) })
            .Where(item => item.uri is not null)
            .GroupBy(item => item.uri!.Host, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => item.entry.VisitedAtUtc).First();
                return new
                {
                    Count = group.Count(),
                    latest.entry.VisitedAtUtc,
                    Site = new TopSite(
                        string.IsNullOrWhiteSpace(latest.entry.Title) ? latest.uri!.Host : latest.entry.Title.Trim(),
                        latest.entry.Url,
                        latest.uri!.Host)
                };
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.VisitedAtUtc)
            .Take(MaxTopSites)
            .Select(item => item.Site)
            .ToList();

        if (sites.Count >= MaxTopSites)
        {
            return sites;
        }

        var knownHosts = sites.Select(site => site.Host).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in bookmarkItems)
        {
            if (sites.Count >= MaxTopSites)
            {
                break;
            }

            if (node.IsFolder || ToWebUri(node.Url) is not { } uri || !knownHosts.Add(uri.Host))
            {
                continue;
            }

            sites.Add(new TopSite(
                string.IsNullOrWhiteSpace(node.Name) ? uri.Host : node.Name.Trim(),
                node.Url,
                uri.Host));
        }

        return sites;
    }

    private static Uri? ToWebUri(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var isWeb = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        return isWeb && !string.IsNullOrEmpty(uri.Host) ? uri : null;
    }

    private static string ResolveBadgeText(string title, string host)
    {
        var source = string.IsNullOrWhiteSpace(title) ? host : title.Trim();
        if (source.Length == 0)
        {
            return "?";
        }

        var first = source[0];
        return char.IsAsciiLetter(first) ? char.ToUpperInvariant(first).ToString() : first.ToString();
    }

    private static string ToCss(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
