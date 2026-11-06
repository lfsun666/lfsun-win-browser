using System.Runtime.InteropServices;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Core;

public sealed class BrowserConfig
{
    private static readonly string ConfigDirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LfsunBrowser",
        "config");
    private static readonly string DefaultConfigPath = Path.Combine(ConfigDirectoryPath, "browser-config.json");
    private static readonly string LegacyConfigPath = Path.Combine(AppContext.BaseDirectory, "browser-config.json");
    public static string DefaultDownloadDirectory { get; } = ResolveSystemDownloadDirectory();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    private static extern string SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken = 0);

    private static string ResolveSystemDownloadDirectory()
    {
        try
        {
            // KNOWNFOLDERID for the user's Downloads folder.
            var downloadsFolderId = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            var path = SHGetKnownFolderPath(downloadsFolderId, 0);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
            // Fall through to the profile-based fallback.
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    public string StartPage { get; set; } = "about:blank";
    public int MaxLiveTabs { get; set; } = 3;
    public int SuspendAfterMinutes { get; set; } = 3;
    public string PluginsDirectoryName { get; set; } = "plugins";
    public bool EnforcePluginPermissions { get; set; } = true;
    public string[] AllowedPluginPermissions { get; set; } = ["content:dom_read", "content:dom_write", "content:inject", "net:fetch"];
    // 中文注释：插件跨域抓取权限（net:fetch）默认进入白名单，但插件仍须显式声明才会生效；
    // 宿主侧真实发请求时还会再校验一次插件是否声明了该权限。
    public const string DefaultNetFetchPermission = "net:fetch";
    // 中文注释：固定到顶部工具栏的插件 id 列表，顺序即按钮显示顺序。
    public List<string> PinnedPluginIds { get; set; } = [];
    // 中文注释：CORS 绕过开关——启动时为 WebView2 内核追加 --disable-web-security，
    // 彻底关闭同源策略/跨域限制（仅开发者自用浏览器场景，默认关闭，开启会降低安全性）。
    public bool DisableWebSecurity { get; set; }
    // 中文注释：追加到 WebView2 启动参数的自由文本（空格分隔），例如
    // "--disable-web-security --proxy-server=..."；与 DisableWebSecurity 叠加生效。
    public string AdditionalBrowserArguments { get; set; } = string.Empty;
    // 中文注释：跨域抓取桥的全局默认代理地址（如 "http://127.0.0.1:7890"）；
    // 留空表示直连。插件可在 chrome.net.fetch 的 init.proxy 中按请求临时覆盖。
    public string FetchProxyUrl { get; set; } = string.Empty;
    // 中文注释：跨域抓取桥的 User-Agent 候选池；开启“轮换 UA”时按请求轮流使用，避免被单一 UA 限流。
    public List<string> FetchUserAgents { get; set; } = new();
    // 中文注释：跨域抓取桥默认重试次数（1~5）；网络层异常（超时/连接失败）才会重试，HTTP 4xx/5xx 不重试。
    public int FetchRetry { get; set; } = 1;
    // 中文注释：跨域抓取桥重试之间的退避间隔（毫秒，0~5000）。
    public int FetchRetryDelayMs { get; set; } = 500;
    public bool IsGeneralAutofillEnabled { get; set; } = true;
    public bool IsPasswordAutosaveEnabled { get; set; } = true;
    public bool IsCredentialAutofillEnabled { get; set; } = true;
    public bool RestoreLastSessionOnStartup { get; set; } = true;
    public bool AskBeforeRestoringSessionOnStartup { get; set; } = false;
    public bool EnableAddressBarSuggestions { get; set; } = true;
    public bool ShowBookmarksBar { get; set; } = true;
    public string UiTheme { get; set; } = "Light";
    public string UiDensity { get; set; } = "Normal";
    public string UiAccent { get; set; } = "Blue";
    public string UiAccentStrength { get; set; } = "Standard";
    public string UiFont { get; set; } = "YaHei";
    public bool ZenMode { get; set; }
    public bool DetailedStatusBar { get; set; } = true;
    public string DefaultSearchEngineName { get; set; } = "Bing";
    public string DefaultSearchEngineUrlTemplate { get; set; } = "https://www.bing.com/search?q={query}";
    public List<CustomSearchEngine> CustomSearchEngines { get; set; } = [];
    public bool AdBlockEnabled { get; set; }
    public List<string> AdBlockCustomDomains { get; set; } = [];
    public string WorkspaceOpenPolicy { get; set; } = "Dedupe";
    public string TaskbarIconPath { get; set; } = string.Empty;
    public string DownloadDirectory { get; set; } = DefaultDownloadDirectory;
    public string BookmarksFileName { get; set; } = "bookmarks.json";
    public string CredentialsFileName { get; set; } = "credentials.json";
    public string HistoryFileName { get; set; } = "history.json";
    public bool ShortcutAddBookmarkEnabled { get; set; } = true;
    public string ShortcutAddBookmark { get; set; } = "Ctrl+D";
    public bool ShortcutNewTabEnabled { get; set; } = true;
    public string ShortcutNewTab { get; set; } = "Ctrl+T";
    public bool ShortcutCloseTabEnabled { get; set; } = true;
    public string ShortcutCloseTab { get; set; } = "Ctrl+W";
    public bool ShortcutFocusAddressEnabled { get; set; } = true;
    public string ShortcutFocusAddress { get; set; } = "Ctrl+L";
    public bool ShortcutRefreshEnabled { get; set; } = true;
    public string ShortcutRefresh { get; set; } = "F5";
    public bool ShortcutDevToolsEnabled { get; set; } = true;
    public string ShortcutDevTools { get; set; } = "F12";
    public bool ShortcutToggleBookmarksBarEnabled { get; set; } = true;
    public string ShortcutToggleBookmarksBar { get; set; } = "Ctrl+B";
    public bool ShortcutRestoreClosedTabEnabled { get; set; } = true;
    public string ShortcutRestoreClosedTab { get; set; } = "Ctrl+Shift+T";
    public bool ShortcutOpenHistoryEnabled { get; set; } = true;
    public string ShortcutOpenHistory { get; set; } = "Ctrl+H";
    public bool ShortcutFindInPageEnabled { get; set; } = true;
    public string ShortcutFindInPage { get; set; } = "Ctrl+F";

    /// <summary>
    /// 中文注释：按域名记忆的页面缩放倍率（键为主机名，值 0.25~5.0；100% 不记录）。
    /// </summary>
    public Dictionary<string, double> ZoomOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ToolbarButtonLayout> ToolbarButtons { get; set; } = CreateDefaultToolbarButtons();
    public List<AddressCommandLayout> AddressCommands { get; set; } = CreateDefaultAddressCommands();
    public List<string> PinnedAddressCommandIds { get; set; } = [];
    public List<RecentAddressCommandLayout> RecentAddressCommands { get; set; } = [];

    public static string GetDefaultConfigPath() => DefaultConfigPath;

    public static BrowserConfig LoadDefault()
    {
        TryMigrateLegacyConfig();

        if (!File.Exists(DefaultConfigPath))
        {
            return new BrowserConfig();
        }

        var loaded = JsonFileStore.Load<BrowserConfig>(DefaultConfigPath);
        if (loaded is null)
        {
            AppLogger.Warn($"配置文件读取失败，已回退默认配置: {DefaultConfigPath}");
            return new BrowserConfig();
        }

        return Normalize(loaded);
    }

    public static bool SaveDefault(BrowserConfig config, out string error)
    {
        var normalized = Normalize(config);
        return JsonFileStore.Save(DefaultConfigPath, normalized, out error);
    }

    private static void TryMigrateLegacyConfig()
    {
        try
        {
            if (File.Exists(DefaultConfigPath) || !File.Exists(LegacyConfigPath))
            {
                return;
            }

            Directory.CreateDirectory(ConfigDirectoryPath);
            File.Copy(LegacyConfigPath, DefaultConfigPath, overwrite: false);
        }
        catch (Exception ex)
        {
            // Ignore migration errors and fall back to default config behavior.
            AppLogger.Warn($"旧配置迁移失败: {ex.Message}");
        }
    }

    private static BrowserConfig Normalize(BrowserConfig config)
    {
        config.AllowedPluginPermissions ??= [];
        // 中文注释：旧版本默认白名单只有 dom_read/dom_write，升级后自动补上 content:inject，
        // 否则 Chrome 转换插件会一直“权限拒绝: content:inject”；用户自定义过的白名单不受影响。
        if (config.AllowedPluginPermissions.Length == 2 &&
            config.AllowedPluginPermissions.Contains("content:dom_read", StringComparer.OrdinalIgnoreCase) &&
            config.AllowedPluginPermissions.Contains("content:dom_write", StringComparer.OrdinalIgnoreCase))
        {
            config.AllowedPluginPermissions = ["content:dom_read", "content:dom_write", "content:inject", DefaultNetFetchPermission];
        }
        // 中文注释：已经是“dom_read/dom_write/inject”三件套默认值时，补上 net:fetch（跨域抓取权限）。
        else if (config.AllowedPluginPermissions.Length == 3 &&
            config.AllowedPluginPermissions.Contains("content:dom_read", StringComparer.OrdinalIgnoreCase) &&
            config.AllowedPluginPermissions.Contains("content:dom_write", StringComparer.OrdinalIgnoreCase) &&
            config.AllowedPluginPermissions.Contains("content:inject", StringComparer.OrdinalIgnoreCase))
        {
            config.AllowedPluginPermissions = ["content:dom_read", "content:dom_write", "content:inject", DefaultNetFetchPermission];
        }
        // 中文注释：兜底：只要白名单不是空的，就确保新增的 net:fetch 在里面；
        // 白名单为空（Length==0）视为用户主动关闭所有权限，不强制追加。
        if (config.AllowedPluginPermissions.Length > 0 &&
            !config.AllowedPluginPermissions.Contains(DefaultNetFetchPermission, StringComparer.OrdinalIgnoreCase))
        {
            config.AllowedPluginPermissions = config.AllowedPluginPermissions
                .Append(DefaultNetFetchPermission)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        config.AdditionalBrowserArguments = string.IsNullOrWhiteSpace(config.AdditionalBrowserArguments)
            ? string.Empty
            : config.AdditionalBrowserArguments.Trim();
        config.FetchProxyUrl = string.IsNullOrWhiteSpace(config.FetchProxyUrl)
            ? string.Empty
            : config.FetchProxyUrl.Trim();
        config.FetchUserAgents = (config.FetchUserAgents ?? [])
            .Select(ua => (ua ?? string.Empty).Trim())
            .Where(ua => ua.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.FetchRetry = Math.Clamp(config.FetchRetry, 1, 5);
        config.FetchRetryDelayMs = Math.Clamp(config.FetchRetryDelayMs, 0, 5000);
        config.DownloadDirectory = string.IsNullOrWhiteSpace(config.DownloadDirectory)
            ? DefaultDownloadDirectory
            : config.DownloadDirectory.Trim();
        config.StartPage = string.IsNullOrWhiteSpace(config.StartPage)
            ? "about:blank"
            : config.StartPage.Trim();
        config.DefaultSearchEngineName = string.IsNullOrWhiteSpace(config.DefaultSearchEngineName)
            ? "Bing"
            : config.DefaultSearchEngineName.Trim();
        config.TaskbarIconPath = NormalizeTaskbarIconPath(config.TaskbarIconPath);
        config.UiTheme = NormalizeUiTheme(config.UiTheme);
        config.UiDensity = NormalizeUiDensity(config.UiDensity);
        config.UiAccent = NormalizeUiAccent(config.UiAccent);
        config.UiAccentStrength = NormalizeUiAccentStrength(config.UiAccentStrength);
        config.UiFont = NormalizeUiFont(config.UiFont);
        config.WorkspaceOpenPolicy = NormalizeWorkspaceOpenPolicy(config.WorkspaceOpenPolicy);
        config.DefaultSearchEngineUrlTemplate = NormalizeSearchTemplate(config.DefaultSearchEngineUrlTemplate);
        if (config.MaxLiveTabs < 1)
        {
            config.MaxLiveTabs = 1;
        }
        if (config.SuspendAfterMinutes < 1)
        {
            config.SuspendAfterMinutes = 1;
        }
        config.BookmarksFileName = string.IsNullOrWhiteSpace(config.BookmarksFileName)
            ? "bookmarks.json"
            : config.BookmarksFileName.Trim();
        config.CredentialsFileName = string.IsNullOrWhiteSpace(config.CredentialsFileName)
            ? "credentials.json"
            : config.CredentialsFileName.Trim();
        config.HistoryFileName = string.IsNullOrWhiteSpace(config.HistoryFileName)
            ? "history.json"
            : config.HistoryFileName.Trim();
        config.ShortcutAddBookmark = NormalizeShortcut(config.ShortcutAddBookmark, "Ctrl+D");
        config.ShortcutNewTab = NormalizeShortcut(config.ShortcutNewTab, "Ctrl+T");
        config.ShortcutCloseTab = NormalizeShortcut(config.ShortcutCloseTab, "Ctrl+W");
        config.ShortcutFocusAddress = NormalizeShortcut(config.ShortcutFocusAddress, "Ctrl+L");
        config.ShortcutRefresh = NormalizeShortcut(config.ShortcutRefresh, "F5");
        config.ShortcutDevTools = NormalizeShortcut(config.ShortcutDevTools, "F12");
        config.ShortcutToggleBookmarksBar = NormalizeShortcut(config.ShortcutToggleBookmarksBar, "Ctrl+B");
        config.ShortcutRestoreClosedTab = NormalizeShortcut(config.ShortcutRestoreClosedTab, "Ctrl+Shift+T");
        config.ShortcutOpenHistory = NormalizeShortcut(config.ShortcutOpenHistory, "Ctrl+H");
        config.ShortcutFindInPage = NormalizeShortcut(config.ShortcutFindInPage, "Ctrl+F");
        config.ZoomOverrides = NormalizeZoomOverrides(config.ZoomOverrides);
        config.CustomSearchEngines = NormalizeCustomSearchEngines(config.CustomSearchEngines);
        config.AdBlockCustomDomains = NormalizeAdBlockCustomDomains(config.AdBlockCustomDomains);
        config.ToolbarButtons = NormalizeToolbarButtons(config.ToolbarButtons);
        config.AddressCommands = NormalizeAddressCommands(config.AddressCommands);
        config.PinnedAddressCommandIds = NormalizePinnedAddressCommandIds(config.PinnedAddressCommandIds);
        config.RecentAddressCommands = NormalizeRecentAddressCommands(config.RecentAddressCommands);
        return config;
    }

    private static string NormalizeShortcut(string raw, string fallback)
    {
        if (ShortcutParser.TryParse(raw, out var keyData))
        {
            return ShortcutParser.ToDisplayString(keyData);
        }

        return fallback;
    }

    private static Dictionary<string, double> NormalizeZoomOverrides(Dictionary<string, double>? overrides)
    {
        // 中文注释：过滤非法主机名与贴近 100% 的条目，并把倍率限制在 WebView2 支持的 0.25~5.0 区间。
        var normalized = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (overrides is null)
        {
            return normalized;
        }

        foreach (var (host, factor) in overrides)
        {
            if (string.IsNullOrWhiteSpace(host) || double.IsNaN(factor))
            {
                continue;
            }

            var clamped = Math.Clamp(factor, 0.25, 5.0);
            if (Math.Abs(clamped - 1.0) < 0.005)
            {
                continue;
            }

            normalized[host.Trim()] = Math.Round(clamped, 2);
        }

        return normalized;
    }

    private static List<CustomSearchEngine> NormalizeCustomSearchEngines(List<CustomSearchEngine>? engines)
    {
        // 中文注释：名称必填、模板必须含 {query}；关键字只保留字母数字，名称/关键字重复时保留先出现的条目。
        var normalized = new List<CustomSearchEngine>();
        if (engines is null)
        {
            return normalized;
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var engine in engines)
        {
            var name = engine?.Name?.Trim() ?? string.Empty;
            var template = engine?.Template?.Trim() ?? string.Empty;
            if (name.Length == 0 ||
                !template.Contains("{query}", StringComparison.OrdinalIgnoreCase) ||
                !usedNames.Add(name))
            {
                continue;
            }

            var keyword = new string((engine?.Keyword ?? string.Empty)
                .Trim()
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();
            if (keyword.Length > 0 && !usedKeywords.Add(keyword))
            {
                keyword = string.Empty;
            }

            normalized.Add(new CustomSearchEngine
            {
                Name = name,
                Template = template,
                Keyword = keyword
            });
        }

        return normalized;
    }

    private static List<string> NormalizeAdBlockCustomDomains(List<string>? domains)
    {
        // 中文注释：去除空白、协议前缀与路径，只保留小写域名，去重。
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in domains ?? [])
        {
            var value = (domain ?? string.Empty).Trim().ToLowerInvariant();
            if (value.StartsWith("http://", StringComparison.Ordinal) ||
                value.StartsWith("https://", StringComparison.Ordinal))
            {
                value = value[(value.IndexOf("//", StringComparison.Ordinal) + 2)..];
            }

            var slashIndex = value.IndexOf('/');
            if (slashIndex >= 0)
            {
                value = value[..slashIndex];
            }

            if (value.Length == 0 || !value.Contains('.') || !seen.Add(value))
            {
                continue;
            }

            normalized.Add(value);
        }

        return normalized;
    }

    private static string NormalizeSearchTemplate(string rawTemplate)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return "https://www.bing.com/search?q={query}";
        }

        var template = rawTemplate.Trim();
        if (!template.Contains("{query}", StringComparison.OrdinalIgnoreCase))
        {
            return "https://www.bing.com/search?q={query}";
        }

        return template;
    }

    private static string NormalizeWorkspaceOpenPolicy(string rawPolicy)
    {
        if (string.IsNullOrWhiteSpace(rawPolicy))
        {
            return "Dedupe";
        }

        var normalized = rawPolicy.Trim();
        if (normalized.Equals("KeepAll", StringComparison.OrdinalIgnoreCase))
        {
            return "KeepAll";
        }

        if (normalized.Equals("Dedupe", StringComparison.OrdinalIgnoreCase))
        {
            return "Dedupe";
        }

        if (normalized.Equals("Clean", StringComparison.OrdinalIgnoreCase))
        {
            return "Clean";
        }

        return "Dedupe";
    }

    private static string NormalizeTaskbarIconPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        return rawPath.Trim();
    }

    private static string NormalizeUiTheme(string rawTheme)
    {
        if (string.IsNullOrWhiteSpace(rawTheme))
        {
            return ThemeOptionNames.ThemeLight;
        }

        var trimmed = rawTheme.Trim();
        if (trimmed.Equals(ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.ThemeSystem;
        }

        return trimmed.Equals(ThemeOptionNames.ThemeDark, StringComparison.OrdinalIgnoreCase)
            ? ThemeOptionNames.ThemeDark
            : ThemeOptionNames.ThemeLight;
    }

    private static string NormalizeUiDensity(string rawDensity)
    {
        if (string.IsNullOrWhiteSpace(rawDensity))
        {
            return ThemeOptionNames.DensityNormal;
        }

        return rawDensity.Trim().Equals(ThemeOptionNames.DensityCompact, StringComparison.OrdinalIgnoreCase)
            ? ThemeOptionNames.DensityCompact
            : ThemeOptionNames.DensityNormal;
    }

    private static string NormalizeUiAccent(string rawAccent)
    {
        if (string.IsNullOrWhiteSpace(rawAccent))
        {
            return ThemeOptionNames.AccentBlue;
        }

        var normalized = rawAccent.Trim();
        if (normalized.Equals(ThemeOptionNames.AccentGreen, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentGreen;
        }

        if (normalized.Equals(ThemeOptionNames.AccentOrange, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentOrange;
        }

        if (normalized.Equals(ThemeOptionNames.AccentRose, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentRose;
        }

        return ThemeOptionNames.AccentBlue;
    }

    private static string NormalizeUiAccentStrength(string rawAccentStrength)
    {
        if (string.IsNullOrWhiteSpace(rawAccentStrength))
        {
            return ThemeOptionNames.AccentStrengthStandard;
        }

        var normalized = rawAccentStrength.Trim();
        if (normalized.Equals(ThemeOptionNames.AccentStrengthSoft, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentStrengthSoft;
        }

        if (normalized.Equals(ThemeOptionNames.AccentStrengthVivid, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.AccentStrengthVivid;
        }

        return ThemeOptionNames.AccentStrengthStandard;
    }

    private static string NormalizeUiFont(string rawFont)
    {
        if (string.IsNullOrWhiteSpace(rawFont))
        {
            return ThemeOptionNames.FontYaHei;
        }

        var normalized = rawFont.Trim();
        if (normalized.Equals(ThemeOptionNames.FontDengXian, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.FontDengXian;
        }

        if (normalized.Equals(ThemeOptionNames.FontSong, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeOptionNames.FontSong;
        }

        return ThemeOptionNames.FontYaHei;
    }

    public static List<ToolbarButtonLayout> CreateDefaultToolbarButtons()
    {
        return
        [
            CreateToolbarButton(ToolbarButtonIds.Back, "后退", 0),
            CreateToolbarButton(ToolbarButtonIds.Forward, "前进", 1),
            CreateToolbarButton(ToolbarButtonIds.Refresh, "刷新", 2),
            CreateToolbarButton(ToolbarButtonIds.NewTab, "新建标签", 3),
            CreateToolbarButton(ToolbarButtonIds.CloseTab, "关闭标签", 4),
            // 中文注释：多开窗口按钮默认放在地址栏左侧（紧跟新建/关闭标签），
            // 因为地址栏是弹簧控件会占满剩余宽度，排在其右侧的按钮容易被挤进溢出菜单看不到。
            CreateToolbarButton(ToolbarButtonIds.MultiWindow, "多开窗口", 5),
            CreateToolbarButton(ToolbarButtonIds.Downloads, "下载管理", 6),
            CreateToolbarButton(ToolbarButtonIds.History, "历史", 7),
            CreateToolbarButton(ToolbarButtonIds.Bookmark, "收藏当前页", 8),
            CreateToolbarButton(ToolbarButtonIds.BookmarkBar, "书签栏开关", 9),
            CreateToolbarButton(ToolbarButtonIds.Settings, "设置", 10),
            CreateToolbarButton(ToolbarButtonIds.SearchEngine, "搜索引擎", 11),
            CreateToolbarButton(ToolbarButtonIds.Appearance, "外观", 12),
            CreateToolbarButton(ToolbarButtonIds.Plugins, "插件", 13),
            CreateToolbarButton(ToolbarButtonIds.DevTools, "开发者工具", 14)
        ];
    }

    public static List<AddressCommandLayout> CreateDefaultAddressCommands()
    {
        return
        [
            CreateAddressCommand("settings", "打开设置", "settings", 0),
            CreateAddressCommand("history", "打开历史记录", "history", 1),
            CreateAddressCommand("downloads", "打开下载管理", "downloads", 2),
            CreateAddressCommand("sessions", "打开会话中心", "sessions", 3),
            CreateAddressCommand("bookmarks", "打开书签管理", "bookmarks", 4),
            CreateAddressCommand("newtab", "新建标签页", "newtab", 5),
            CreateAddressCommand("tabswitch", "快速切换标签", "tabswitch", 6),
            CreateAddressCommand("hardrefresh", "强制刷新当前标签", "hardrefresh", 7),
            CreateAddressCommand("reloadplugins", "重载插件", "reloadplugins", 8),
            CreateAddressCommand("darkmode", "切换深色主题", "darkmode", 9),
            CreateAddressCommand("lightmode", "切换浅色主题", "lightmode", 10),
            CreateAddressCommand("compactui", "切换紧凑密度", "compactui", 11),
            CreateAddressCommand("normalui", "切换标准密度", "normalui", 12),
            CreateAddressCommand("togglebookmarksbar", "切换书签栏显示", "togglebookmarksbar", 13),
            CreateAddressCommand("theme", "设置主题", "theme", 14),
            CreateAddressCommand("density", "设置界面密度", "density", 15),
            CreateAddressCommand("accent", "设置强调色", "accent", 16),
            CreateAddressCommand("accentstrength", "设置强调强度", "accentstrength", 17),
            CreateAddressCommand("uipreset", "应用外观预设", "uipreset", 18),
            CreateAddressCommand("recentcommands", "管理最近命令", "recentcommands", 19),
            CreateAddressCommand("pincommand", "固定常用命令", "pincommand", 20),
            CreateAddressCommand("resetui", "重置界面外观", "resetui", 21),
            CreateAddressCommand("font", "设置界面字体", "font", 22),
            CreateAddressCommand("bookmarksbar", "设置书签栏显示模式", "bookmarksbar", 23),
            CreateAddressCommand("statusbar", "设置状态栏模式", "statusbar", 24),
            CreateAddressCommand("tabname", "设置当前标签名称", "tabname", 25),
            CreateAddressCommand("tabcolor", "设置当前标签色标", "tabcolor", 26),
            CreateAddressCommand("tabops", "标签批处理", "tabops", 30),
            CreateAddressCommand("zen", "切换沉浸模式", "zen", 27),
            CreateAddressCommand("help", "查看指令帮助", "help", 28),
            CreateAddressCommand("palette", "打开命令面板", "palette", 29),
            CreateAddressCommand("devtools", "打开开发者工具", "devtools", 31),
            CreateAddressCommand("viewsource", "查看页面源码", "viewsource", 32)
        ];
    }

    private static ToolbarButtonLayout CreateToolbarButton(string id, string displayName, int order)
    {
        return new ToolbarButtonLayout
        {
            Id = id,
            DisplayName = displayName,
            Visible = true,
            Order = order
        };
    }

    private static AddressCommandLayout CreateAddressCommand(string id, string displayName, string command, int order)
    {
        return new AddressCommandLayout
        {
            Id = id,
            DisplayName = displayName,
            Enabled = true,
            Command = command,
            Order = order
        };
    }

    private static List<ToolbarButtonLayout> NormalizeToolbarButtons(List<ToolbarButtonLayout>? toolbarButtons)
    {
        var defaults = CreateDefaultToolbarButtons();
        var source = toolbarButtons ?? [];
        var sourceById = source
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var normalized = new List<ToolbarButtonLayout>();
        foreach (var fallback in defaults)
        {
            if (sourceById.TryGetValue(fallback.Id, out var current))
            {
                normalized.Add(new ToolbarButtonLayout
                {
                    Id = fallback.Id,
                    DisplayName = fallback.DisplayName,
                    Visible = current.Visible,
                    Order = current.Order
                });
                continue;
            }

            normalized.Add(new ToolbarButtonLayout
            {
                Id = fallback.Id,
                DisplayName = fallback.DisplayName,
                Visible = fallback.Visible,
                Order = fallback.Order
            });
        }

        normalized.Sort((left, right) => left.Order.CompareTo(right.Order));
        for (var i = 0; i < normalized.Count; i++)
        {
            normalized[i].Order = i;
        }

        return normalized;
    }

    private static List<AddressCommandLayout> NormalizeAddressCommands(List<AddressCommandLayout>? addressCommands)
    {
        var defaults = CreateDefaultAddressCommands();
        var source = addressCommands ?? [];
        var sourceById = source
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var normalized = new List<AddressCommandLayout>();
        foreach (var fallback in defaults)
        {
            if (sourceById.TryGetValue(fallback.Id, out var current))
            {
                normalized.Add(new AddressCommandLayout
                {
                    Id = fallback.Id,
                    DisplayName = fallback.DisplayName,
                    Enabled = current.Enabled,
                    Command = NormalizeAddressCommandToken(current.Command, fallback.Command),
                    Order = current.Order
                });
                continue;
            }

            normalized.Add(new AddressCommandLayout
            {
                Id = fallback.Id,
                DisplayName = fallback.DisplayName,
                Enabled = fallback.Enabled,
                Command = fallback.Command,
                Order = fallback.Order
            });
        }

        normalized.Sort((left, right) => left.Order.CompareTo(right.Order));
        var usedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < normalized.Count; i++)
        {
            normalized[i].Order = i;
            var fallback = defaults.First(item => string.Equals(item.Id, normalized[i].Id, StringComparison.OrdinalIgnoreCase));
            var command = NormalizeAddressCommandToken(normalized[i].Command, fallback.Command);
            if (usedCommands.Contains(command))
            {
                command = NormalizeAddressCommandToken(fallback.Command, fallback.Command);
            }

            usedCommands.Add(command);
            normalized[i].Command = command;
        }

        return normalized;
    }

    private static List<RecentAddressCommandLayout> NormalizeRecentAddressCommands(List<RecentAddressCommandLayout>? recentCommands)
    {
        var source = recentCommands ?? [];
        var normalized = new List<RecentAddressCommandLayout>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source
                     .Where(item => !string.IsNullOrWhiteSpace(item.CommandId))
                     .OrderByDescending(item => item.UsedAtUtc)
                     .Take(40))
        {
            var commandId = item.CommandId.Trim();
            var argumentToken = string.IsNullOrWhiteSpace(item.ArgumentToken)
                ? string.Empty
                : item.ArgumentToken.Trim().ToLowerInvariant();
            var dedupeKey = $"{commandId}|{argumentToken}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            normalized.Add(new RecentAddressCommandLayout
            {
                CommandId = commandId,
                ArgumentToken = argumentToken,
                UsedAtUtc = item.UsedAtUtc == default ? DateTime.UtcNow : item.UsedAtUtc
            });
            if (normalized.Count >= 18)
            {
                break;
            }
        }

        return normalized;
    }

    private static List<string> NormalizePinnedAddressCommandIds(List<string>? pinnedCommandIds)
    {
        var source = pinnedCommandIds ?? [];
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            var normalizedId = item.Trim();
            if (!seen.Add(normalizedId))
            {
                continue;
            }

            normalized.Add(normalizedId);
            if (normalized.Count >= 10)
            {
                break;
            }
        }

        return normalized;
    }

    private static string NormalizeAddressCommandToken(string? rawCommand, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            return fallback;
        }

        var trimmed = rawCommand.Trim().ToLowerInvariant();
        if (trimmed.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_')))
        {
            return fallback;
        }

        return trimmed;
    }
}

