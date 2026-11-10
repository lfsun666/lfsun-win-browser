using System.Net;
using System.Text;
using System.Text.Json;
using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.History;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 新标签页速拨面板生成器。
/// 输出完整本地 HTML（NavigateToString 渲染）：问候语+数字时钟、搜索引擎即时切换器、快捷聚合芯片、
/// 开发者灵感寄语、常访站点卡片（历史访问频次 Top8，不足时用书签补齐），配色无缝跟随 AppTheme。
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
        var defaultEngine = string.IsNullOrWhiteSpace(config.DefaultSearchEngineName)
            ? "bing"
            : (config.DefaultSearchEngineName.Contains("百度") ? "baidu" : "bing");
        var searchTemplateJs = JsonSerializer.Serialize(config.DefaultSearchEngineUrlTemplate);

        var cardsHtml = new StringBuilder();
        foreach (var site in topSites)
        {
            var encodedUrl = WebUtility.HtmlEncode(site.Url);
            var encodedTitle = WebUtility.HtmlEncode(site.Title);
            var encodedHost = WebUtility.HtmlEncode(site.Host);
            var badge = WebUtility.HtmlEncode(ResolveBadgeText(site.Title, site.Host));
            var badgeColor = FaviconService.ResolveTileCssColor(site.Host);

            string badgeHtml;
            if (!string.IsNullOrWhiteSpace(site.FaviconDataUrl))
            {
                badgeHtml = $"""<span class="badge icon-badge"><img src="{site.FaviconDataUrl}" class="card-icon" alt="" /></span>""";
            }
            else
            {
                badgeHtml = $"""<span class="badge" style="background: {badgeColor};">{badge}</span>""";
            }

            cardsHtml.Append($"""
                <a class="card" href="{encodedUrl}" title="{encodedTitle}&#10;{encodedUrl}">
                  {badgeHtml}
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
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
                padding: 6vh 24px 36px;
                user-select: none;
                transition: background-color .2s ease;
              }
              .clock {
                font-size: 54px; font-weight: 700; letter-spacing: 2px;
                background: linear-gradient(135deg, var(--text) 30%, var(--accent));
                -webkit-background-clip: text; -webkit-text-fill-color: transparent;
              }
              .greeting { margin-top: 6px; font-size: 15px; color: var(--text-2); font-weight: 500; }
              .quote { margin-top: 8px; font-size: 12.5px; color: var(--text-2); opacity: 0.85; font-style: italic; }
              
              .search-container {
                margin-top: 28px; width: min(680px, 92vw); display: flex; flex-direction: column; align-items: center; gap: 14px;
              }
              .search-box {
                width: 100%; display: flex; align-items: center;
                background: var(--input-bg); border: 1.5px solid var(--border); border-radius: 28px;
                overflow: hidden; transition: all .2s cubic-bezier(0.16, 1, 0.3, 1);
                box-shadow: 0 4px 16px rgba(0,0,0,.04);
              }
              .search-box:focus-within {
                border-color: var(--accent);
                box-shadow: 0 6px 26px rgba(0,0,0,.14), 0 0 0 3px rgba(33, 115, 243, .18);
              }
              .engine-select {
                border: 0; outline: 0; background: transparent; color: var(--text);
                font-size: 13px; font-weight: 600; padding: 0 14px 0 18px; height: 100%;
                cursor: pointer; appearance: none;
                border-right: 1px solid var(--border);
                transition: background-color 0.15s ease;
              }
              .engine-select:hover { background-color: var(--surface-alt); }
              .search-box input {
                flex: 1; border: 0; outline: 0; background: transparent; color: var(--text);
                font-size: 15px; padding: 13px 18px;
              }
              .search-box button {
                border: 0; cursor: pointer; background: var(--accent); color: #fff;
                font-size: 14px; font-weight: 600; padding: 13px 26px;
                transition: opacity .15s ease, filter .15s ease;
              }
              .search-box button:hover { opacity: .92; filter: brightness(1.05); }
              
              .quick-nav {
                display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; max-width: 680px;
              }
              .quick-chip {
                display: inline-flex; align-items: center; gap: 5px;
                padding: 6px 13px; border-radius: 16px;
                font-size: 12px; color: var(--text-2); text-decoration: none;
                background: var(--surface-alt); border: 1px solid var(--border);
                transition: all .16s ease;
              }
              .quick-chip:hover {
                color: var(--accent); border-color: var(--accent); transform: translateY(-1.5px);
                box-shadow: 0 2px 8px rgba(0,0,0,0.06);
              }
              .chip-icon { font-size: 13px; }

              .section-title {
                margin: 36px 0 14px; font-size: 13px; color: var(--text-2);
                align-self: center; font-weight: 600; letter-spacing: 1px; text-transform: uppercase;
              }
              .grid {
                display: grid; grid-template-columns: repeat(4, 138px); gap: 16px;
              }
              @media (max-width: 640px) { .grid { grid-template-columns: repeat(2, 138px); } }
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
              .icon-badge {
                background: var(--surface-alt) !important;
                border: 1px solid var(--border);
                box-shadow: 0 2px 8px rgba(0,0,0,.04) !important;
              }
              .card-icon {
                width: 28px; height: 28px; object-fit: contain; border-radius: 6px;
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
                margin-top: auto; padding-top: 40px; font-size: 12px; color: var(--text-2);
                opacity: .85; letter-spacing: .3px;
              }
            </style>
            </head>
            <body>
              <div class="clock" id="clock">--:--</div>
              <div class="greeting" id="greeting"></div>
              <div class="quote" id="quote">“代码是写给人看的，顺便能在机器上运行。” — Martin Fowler</div>
              
              <div class="search-container">
                <form class="search-box" id="searchForm">
                  <select id="engineSelect" class="engine-select">
                    <option value="bing">必应</option>
                    <option value="baidu">百度</option>
                    <option value="google">谷歌</option>
                    <option value="github">GitHub</option>
                    <option value="juejin">稀土掘金</option>
                  </select>
                  <input id="searchInput" type="text" autofocus autocomplete="off"
                         placeholder="输入搜索内容，或输入网址后回车...">
                  <button type="submit">搜索</button>
                </form>
                <div class="quick-nav">
                  <a class="quick-chip" href="https://github.com"><span class="chip-icon">🐙</span> GitHub</a>
                  <a class="quick-chip" href="https://gitee.com"><span class="chip-icon">🐎</span> Gitee</a>
                  <a class="quick-chip" href="https://juejin.cn"><span class="chip-icon">💎</span> 稀土掘金</a>
                  <a class="quick-chip" href="https://v2ex.com"><span class="chip-icon">💬</span> V2EX</a>
                  <a class="quick-chip" href="https://developer.mozilla.org/zh-CN/"><span class="chip-icon">📚</span> MDN</a>
                  <a class="quick-chip" href="https://stackoverflow.com"><span class="chip-icon">🥞</span> StackOverflow</a>
                  <a class="quick-chip" href="https://www.bilibili.com"><span class="chip-icon">📺</span> 哔哩哔哩</a>
                  <a class="quick-chip" href="https://sspai.com"><span class="chip-icon">⚡</span> 少数派</a>
                </div>
              </div>
              <div class="section-title">常访站点</div>
              <div class="grid">{{cardsHtml}}</div>
              {{emptyHtml}}
              <div class="footer-version">{{AppVersionInfo.ProductName}} v{{AppVersionInfo.CurrentVersion}} · {{AppVersionInfo.Slogan}}</div>
            <script>
              const engineTemplates = {
                bing: "https://cn.bing.com/search?q={query}",
                baidu: "https://www.baidu.com/s?wd={query}",
                google: "https://www.google.com/search?q={query}",
                github: "https://github.com/search?q={query}",
                juejin: "https://juejin.cn/search?query={query}"
              };

              const quotes = [
                "“代码是写给人看的，顺便能在机器上运行。” — Martin Fowler",
                "“简单的事情应该简单，复杂的事情应该成为可能。” — Alan Kay",
                "“提前优化是万恶之源。” — Donald Knuth",
                "“好工具拓展思维边界，极简设计带来专注体验。”",
                "“每一个伟大的应用，都源自持续迭代与打磨。”",
                "“Talk is cheap. Show me the code.” — Linus Torvalds"
              ];

              const enginePlaceholders = {
                bing: "使用必应搜索，或输入网址后回车...",
                baidu: "使用百度搜索，或输入网址后回车...",
                google: "使用谷歌搜索，或输入网址后回车...",
                github: "在 GitHub 搜索仓库或代码...",
                juejin: "在稀土掘金搜索技术文章..."
              };

              const engineSelect = document.getElementById("engineSelect");
              const searchInput = document.getElementById("searchInput");
              function updateSearchPlaceholder() {
                const sel = engineSelect.value;
                if (searchInput) {
                  searchInput.placeholder = enginePlaceholders[sel] || "输入搜索内容，或输入网址后回车...";
                }
              }

              const savedEngine = localStorage.getItem("lfsun_fav_engine");
              if (savedEngine && engineTemplates[savedEngine]) {
                engineSelect.value = savedEngine;
              } else {
                engineSelect.value = "{{defaultEngine}}";
              }
              updateSearchPlaceholder();

              engineSelect.addEventListener("change", () => {
                localStorage.setItem("lfsun_fav_engine", engineSelect.value);
                updateSearchPlaceholder();
              });

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

              // 随机展示每日名言
              const qIndex = Math.floor(Math.random() * quotes.length);
              document.getElementById("quote").textContent = quotes[qIndex];

              document.getElementById("searchForm").addEventListener("submit", event => {
                event.preventDefault();
                const raw = document.getElementById("searchInput").value.trim();
                if (!raw) { return; }
                if (/^https?:\/\//i.test(raw)) {
                  location.href = raw;
                } else if (/^[\w-]+(\.[\w-]+)+(\/\S*)?$/.test(raw)) {
                  location.href = "https://" + raw;
                } else {
                  const sel = engineSelect.value;
                  const tmpl = engineTemplates[sel] || "https://cn.bing.com/search?q={query}";
                  location.href = tmpl.replace("{query}", encodeURIComponent(raw));
                }
              });
            </script>
            </body>
            </html>
            """;
    }

    private sealed record TopSite(string Title, string Url, string Host, string? FaviconDataUrl = null);

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
                var favicon = FaviconService.TryGetFaviconDataUrl(latest.entry.Url);
                return new
                {
                    Count = group.Count(),
                    latest.entry.VisitedAtUtc,
                    Site = new TopSite(
                        string.IsNullOrWhiteSpace(latest.entry.Title) ? latest.uri!.Host : latest.entry.Title.Trim(),
                        latest.entry.Url,
                        latest.uri!.Host,
                        favicon)
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

            var favicon = FaviconService.TryGetFaviconDataUrl(node.Url, node.Favicon);
            sites.Add(new TopSite(
                string.IsNullOrWhiteSpace(node.Name) ? uri.Host : node.Name.Trim(),
                node.Url,
                uri.Host,
                favicon));
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
