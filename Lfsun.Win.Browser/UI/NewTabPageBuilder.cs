using System.Net;
using System.Text;
using System.Text.Json;
using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.History;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 新标签页速拨面板生成器。
/// 输出完整本地 HTML（NavigateToString 渲染）：问候语+时钟、默认引擎搜索框、快捷功能芯片、
/// 常访站点卡片（历史访问频次 Top8，不足时用书签补齐），配色跟随 AppTheme。
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
            ? "搜索"
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
            ? """<div class="empty">开始浏览后，这里会展示你的常访站点</div>"""
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
            <meta charset="utf-8">
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
                font-family: "Microsoft YaHei UI", "Segoe UI", sans-serif;
                padding: 8vh 24px 36px;
              }
              .clock { font-size: 46px; font-weight: 600; letter-spacing: 2px; }
              .greeting { margin-top: 6px; font-size: 15px; color: var(--text-2); }
              .search-box {
                margin-top: 32px; width: min(640px, 92vw); display: flex;
                background: var(--input-bg); border: 1px solid var(--border); border-radius: 24px;
                overflow: hidden; transition: box-shadow .18s ease, border-color .18s ease;
                box-shadow: 0 2px 10px rgba(0,0,0,.04);
              }
              .search-box:focus-within { border-color: var(--accent); box-shadow: 0 4px 22px rgba(0,0,0,.16); }
              .search-box input {
                flex: 1; border: 0; outline: 0; background: transparent; color: var(--text);
                font-size: 15px; padding: 13px 20px;
              }
              .search-box button {
                border: 0; cursor: pointer; background: var(--accent); color: #fff;
                font-size: 14px; padding: 0 26px; transition: opacity .15s ease;
              }
              .search-box button:hover { opacity: .92; }
              .section-title { margin: 38px 0 14px; font-size: 13px; color: var(--text-2); align-self: center; font-weight: 600; letter-spacing: .5px; }
              .grid {
                display: grid; grid-template-columns: repeat(4, 134px); gap: 14px;
              }
              @media (max-width: 640px) { .grid { grid-template-columns: repeat(2, 134px); } }
              .card {
                display: flex; flex-direction: column; align-items: center; gap: 7px;
                background: var(--surface); border: 1px solid var(--border); border-radius: 14px;
                padding: 16px 10px 12px; text-decoration: none; color: var(--text);
                transition: transform .15s ease, box-shadow .15s ease, border-color .15s ease;
                box-shadow: 0 2px 8px rgba(0,0,0,.03);
              }
              .card:hover {
                transform: translateY(-3px); border-color: var(--accent);
                box-shadow: 0 8px 22px rgba(0,0,0,.15);
              }
              .badge {
                width: 44px; height: 44px; border-radius: 12px; display: flex;
                align-items: center; justify-content: center; font-size: 18px; font-weight: 600;
                color: #fff; background: var(--accent);
              }
              .card-title {
                max-width: 112px; font-size: 12.5px; white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis;
              }
              .card-host {
                max-width: 112px; font-size: 11px; color: var(--text-2); white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis;
              }
              .empty { margin-top: 8px; font-size: 13px; color: var(--text-2); }
              .footer-version {
                margin-top: auto; padding-top: 48px; font-size: 12px; color: var(--text-2);
                opacity: .8;
              }
            </style>
            </head>
            <body>
              <div class="clock" id="clock">--:--</div>
              <div class="greeting" id="greeting"></div>
              <form class="search-box" id="searchForm">
                <input id="searchInput" type="text" autofocus autocomplete="off"
                       placeholder="使用 {{WebUtility.HtmlEncode(engineName)}} 搜索，或输入网址后回车">
                <button type="submit">搜索</button>
              </form>
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
                // 中文注释：形似网址直接访问，否则走默认搜索引擎模板。
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
        // 中文注释：按站点 host 聚合历史访问频次，取 Top8；标题与地址采用该站点最近一次访问的记录。
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

        // 中文注释：历史不足 8 个站点时，用书签栏顶层链接补齐（去重同 host）。
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

        // 中文注释：中文取首字，英文取首字母大写。
        var first = source[0];
        return char.IsAsciiLetter(first) ? char.ToUpperInvariant(first).ToString() : first.ToString();
    }

    private static string ToCss(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
