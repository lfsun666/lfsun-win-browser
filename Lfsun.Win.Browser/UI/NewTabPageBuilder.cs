using System.Net;
using System.Text;
using System.Text.Encodings.Web;
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
    private const int MaxTopSites = 12;

    // 中文注释：注入页内 JS 的字符串序列化选项：非 ASCII（中文引擎名等）不转义，
    // 生成的页面脚本可读且与页面声明一致。
    private static readonly JsonSerializerOptions JsLiteralOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build(
        BrowserConfig config,
        IReadOnlyList<HistoryEntry> historyEntries,
        IReadOnlyList<BookmarkNode> bookmarkItems)
    {
        // 中文注释：搜索引擎下拉直接接真实目录（内置 + 用户自定义同名覆盖），
        // 新标签页搜索与地址栏口径一致；不再硬编码 5 个引擎。
        var engines = SearchEngineCatalog.GetAll(config);
        var defaultEngineId = ResolveDefaultEngineId(engines, config);
        var defaultEngineTemplate = engines
            .FirstOrDefault(engine => string.Equals(engine.Name, defaultEngineId, StringComparison.Ordinal))?.Template
            ?? "https://www.bing.com/search?q={query}";

        var engineOptionsHtml = new StringBuilder();
        var engineTemplatesJson = new System.Text.Json.Nodes.JsonObject();
        var enginePlaceholdersJson = new System.Text.Json.Nodes.JsonObject();
        foreach (var engine in engines)
        {
            var encodedName = WebUtility.HtmlEncode(engine.Name);
            engineOptionsHtml.AppendLine($"""<option value="{encodedName}">{encodedName}</option>""");
            engineTemplatesJson[engine.Name] = engine.Template;
            enginePlaceholdersJson[engine.Name] = $"用 {engine.Name} 搜索，或输入网址后回车...";
        }

        // 中文注释：非 ASCII 不转义，保证注入的 JS 字面量与测试断言/页面阅读一致。
        var engineTemplatesJs = engineTemplatesJson.ToJsonString(JsLiteralOptions);
        var enginePlaceholdersJs = enginePlaceholdersJson.ToJsonString(JsLiteralOptions);
        var defaultEngineIdJs = JsonSerializer.Serialize(defaultEngineId, JsLiteralOptions);
        var defaultEngineTemplateJs = JsonSerializer.Serialize(defaultEngineTemplate, JsLiteralOptions);

        var topSites = CollectTopSites(historyEntries, bookmarkItems);

        // 中文注释：搜索框聚焦光环颜色由强调色计算，不再硬编码蓝色，
        // 换 Green/Orange/Rose 强调色时新标签页同步跟随。
        var accent = AppTheme.Accent;
        var accentRingCss = $"rgba({accent.R}, {accent.G}, {accent.B}, 0.22)";

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
                --accent-ring: {{accentRingCss}};
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
              .quote {
                margin-top: 10px; font-size: 12px; color: var(--text-2); opacity: 0.9;
                font-style: italic; padding: 4px 14px; border-radius: 12px;
                background: var(--surface-alt); border: 1px solid var(--border);
              }
              
              .search-container {
                margin-top: 28px; width: min(740px, 92vw); display: flex; flex-direction: column; align-items: center; gap: 16px;
              }
              .search-box {
                width: 100%; display: flex; align-items: center;
                background: var(--input-bg); border: 1.5px solid var(--border); border-radius: 28px;
                overflow: hidden; transition: all .2s cubic-bezier(0.16, 1, 0.3, 1);
                box-shadow: 0 4px 16px rgba(0,0,0,.05);
                backdrop-filter: blur(12px); -webkit-backdrop-filter: blur(12px);
              }
              .search-box:focus-within {
                border-color: var(--accent);
                box-shadow: 0 8px 30px rgba(0,0,0,.14), 0 0 0 3.5px var(--accent-ring);
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
                display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; max-width: 740px;
              }
              .quick-chip {
                display: inline-flex; align-items: center; gap: 5px;
                padding: 6px 14px; border-radius: 18px;
                font-size: 12px; font-weight: 500; color: var(--text-2); text-decoration: none;
                background: var(--surface-alt); border: 1px solid var(--border);
                transition: all .2s cubic-bezier(0.16, 1, 0.3, 1);
                backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px);
              }
              .quick-chip:hover {
                color: var(--accent); border-color: var(--accent); transform: translateY(-2px);
                background: var(--surface);
                box-shadow: 0 4px 12px rgba(0,0,0,0.08);
              }
              .chip-icon { font-size: 13px; }

              .section-title {
                margin: 36px 0 14px; font-size: 13px; color: var(--text-2);
                align-self: center; font-weight: 600; letter-spacing: 1px; text-transform: uppercase;
              }
              .grid {
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(136px, 156px));
                justify-content: center;
                gap: 16px;
                width: 100%;
                max-width: 880px;
              }
              @media (max-width: 640px) { .grid { grid-template-columns: repeat(2, 1fr); gap: 12px; } }
              .card {
                display: flex; flex-direction: column; align-items: center; gap: 8px;
                background: var(--surface); border: 1px solid var(--border); border-radius: 16px;
                padding: 16px 12px 14px; text-decoration: none; color: var(--text);
                transition: transform .22s cubic-bezier(0.16, 1, 0.3, 1), box-shadow .22s ease, border-color .22s ease, background-color .22s ease;
                box-shadow: 0 2px 8px rgba(0,0,0,.04);
                backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px);
              }
              .card:hover {
                transform: translateY(-5px); border-color: var(--accent);
                background: var(--surface-alt);
                box-shadow: 0 12px 28px rgba(0,0,0,.14);
              }
              .badge {
                width: 48px; height: 48px; border-radius: 14px; display: flex;
                align-items: center; justify-content: center; font-size: 20px; font-weight: 700;
                color: #fff; background: var(--accent);
                box-shadow: 0 4px 12px rgba(0,0,0,.15);
                transition: transform .2s ease;
              }
              .card:hover .badge { transform: scale(1.06); }
              .icon-badge {
                background: var(--surface-alt) !important;
                border: 1px solid var(--border);
                box-shadow: 0 2px 8px rgba(0,0,0,.04) !important;
              }
              .card-icon {
                width: 28px; height: 28px; object-fit: contain; border-radius: 6px;
              }
              .card-title {
                max-width: 130px; font-size: 12.5px; font-weight: 600; white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis; text-align: center;
              }
              .card-host {
                max-width: 130px; font-size: 11px; color: var(--text-2); white-space: nowrap;
                overflow: hidden; text-overflow: ellipsis; text-align: center;
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
                    {{engineOptionsHtml}}
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
              const engineTemplates = {{engineTemplatesJs}};
              const enginePlaceholders = {{enginePlaceholdersJs}};
              const defaultEngineId = {{defaultEngineIdJs}};
              const defaultEngineTemplate = {{defaultEngineTemplateJs}};
              // 中文注释：旧版硬编码引擎 id（bing/baidu/google/github/juejin）迁移到新目录同名引擎。
              const legacyEngineIds = { bing: "Bing", baidu: "百度", google: "Google", github: "GitHub", juejin: "稀土拱金" };

              const quotes = [
                "“代码是写给人看的，顺便能在机器上运行。” — Martin Fowler",
                "“简单的事情应该简单，复杂的事情应该成为可能。” — Alan Kay",
                "“提前优化是万恶之源。” — Donald Knuth",
                "“好工具拓展思维边界，极简设计带来专注体验。”",
                "“每一个伟大的应用，都源自持续迭代与打磨。”",
                "“Talk is cheap. Show me the code.” — Linus Torvalds"
              ];

              const engineSelect = document.getElementById("engineSelect");
              const searchInput = document.getElementById("searchInput");
              function updateSearchPlaceholder() {
                const sel = engineSelect.value;
                if (searchInput) {
                  searchInput.placeholder = enginePlaceholders[sel] || "输入搜索内容，或输入网址后回车...";
                }
              }

              // 中文注释：NavigateToString 页面是 about:blank 不透明源，Chromium 直接访问 localStorage
              // 会抛 SecurityError 并杀死整段脚本（时钟/问候语/引擎 placeholder 全部不生效的存量 bug）。
              // 统一封装安全读写，失败时静默降级（引擎选择不跨会话记忆，当次仍可用）。
              function readSavedEngine() {
                try { return localStorage.getItem("lfsun_fav_engine"); } catch (e) { return null; }
              }
              function persistEngine(value) {
                try { localStorage.setItem("lfsun_fav_engine", value); } catch (e) {}
              }

              const savedEngine = readSavedEngine();
              const savedEngineId = (savedEngine && engineTemplates[savedEngine])
                ? savedEngine
                : legacyEngineIds[savedEngine ?? ""];
              engineSelect.value = (savedEngineId && engineTemplates[savedEngineId])
                ? savedEngineId
                : defaultEngineId;
              updateSearchPlaceholder();

              engineSelect.addEventListener("change", () => {
                persistEngine(engineSelect.value);
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

              // 每日名言：按日期取模，当天内多次打开新标签页保持同一句。
              const dayKey = new Date().toISOString().slice(0, 10);
              let dayHash = 0;
              for (const ch of dayKey) { dayHash = (dayHash * 31 + ch.charCodeAt(0)) >>> 0; }
              const qIndex = dayHash % quotes.length;
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
                  const tmpl = engineTemplates[sel] || defaultEngineTemplate;
                  location.href = tmpl.replace("{query}", encodeURIComponent(raw));
                }
              });
            </script>
            </body>
            </html>
            """;
    }

    private sealed record TopSite(string Title, string Url, string Host, string? FaviconDataUrl = null);

    // 中文注释：默认引擎优先按模板精确匹配（与地址栏默认引擎一致），其次按名称，最后回退首个。
    private static string ResolveDefaultEngineId(IReadOnlyList<SearchEngineOption> engines, BrowserConfig config)
    {
        var match = engines.FirstOrDefault(engine =>
                string.Equals(engine.Template, config.DefaultSearchEngineUrlTemplate, StringComparison.OrdinalIgnoreCase))
            ?? engines.FirstOrDefault(engine =>
                string.Equals(engine.Name, config.DefaultSearchEngineName, StringComparison.OrdinalIgnoreCase));
        return match?.Name ?? engines.FirstOrDefault()?.Name ?? "Bing";
    }

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
