using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
using System.Runtime.InteropServices;
using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.History;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.Security;
using Lfsun.Win.Browser.Sessions;
using Lfsun.Win.Browser.Settings;
using Lfsun.Win.Browser.UI;
using Lfsun.Win.Browser.UI.Chrome;
using Lfsun.Win.Browser.UI.SidePanel;
using Lfsun.Win.Browser.UI.SplitView;

namespace Lfsun.Win.Browser;

public sealed class MainWindow : Form
{
    private const int WsMaximizeBox = 0x00010000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsSysMenu = 0x00080000;
    private const int WmSetIcon = 0x0080;
    private const int GclHIcon = -14;
    private const int GclHIconSm = -34;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int WindowTransitionTickMs = 16;
    private const int WindowTransitionSteps = 10;
    private const double WindowFadeOutMinimumOpacity = 0.22;
    private const double WindowFadeInStartOpacity = 0.24;

    private enum AddressSuggestionHintMode
    {
        Default,
        TabSwitch,
        CommandGeneral,
        CommandPinned,
        CommandRecent,
        CommandArgument
    }
    private readonly BrowserConfig _config;
    private readonly AppLaunchOptions _launchOptions;
    private readonly ToolStrip _titleBar = new();
    private readonly ToolStrip _topBar = new();
    private readonly ToolStrip _addressBar = new();
    private readonly ToolStrip _bookmarksBar = new();
    private readonly StatusBarPanel _statusBarPanel = new();
    private readonly ToolStripButton _backButton = new("后退");
    private readonly ToolStripButton _forwardButton = new("前进");
    private readonly ToolStripButton _refreshButton = new("刷新");
    private readonly ToolStripButton _newTabButton = new("新建标签");
    private readonly ToolStripButton _closeTabButton = new("关闭标签");
    private readonly ToolStripButton _downloadButton = new("下载管理");
    private readonly ToolStripButton _historyButton = new("历史");
    private readonly ToolStripButton _addBookmarkButton = new("收藏当前页");
    private readonly ToolStripButton _toggleBookmarksBarButton = new("隐藏书签栏");
    private readonly ToolStripButton _settingsButton = new("设置");
    // 中文注释：插件固定入口：下拉列出已安装插件可直接启停，并提供重载/管理入口，任意页面可见。
    private readonly ToolStripDropDownButton _pluginButton = new("插件");
    private readonly BrowserSidePanel _sidePanel;
    private readonly ToolStripDropDownButton _sidePanelButton = new("◧ 侧栏");
    private readonly SplitBrowserPanel _splitPanel;
    private readonly VerticalTabStrip _verticalTabStrip;
    private readonly ToolStripButton _verticalTabsToggleButton = new("📑 垂直标签");
    private readonly ToolStripButton _splitViewButton = new("◫ 分屏");
    private readonly ToolStripButton _readerModeButton = new("📖 阅读");
    private readonly ToolStripButton _zoomPillButton = new("100% 🔍");

    // 中文注释：固定到工具栏的插件快捷按钮（按配置 PinnedPluginIds 动态生成，点击即启/停插件）。
    private readonly List<ToolStripButton> _pinnedPluginButtons = new();

    // 中文注释：标题栏“插件”按钮：任意页面快捷打开插件下拉（启停/固定到工具栏/管理）。
    private readonly ToolStripDropDownButton _titlePluginButton = new("插件");
    private readonly ToolStripButton _devToolsButton = new("开发者工具");
    private readonly ToolStripDropDownButton _searchEngineButton = new("搜索:Bing");
    private readonly ToolStripDropDownButton _appearanceButton = new("外观");
    private readonly ToolStripDropDownButton _multiWindowButton = new("多开窗口");
    private readonly ToolStripDropDownButton _recentTabsButton = new("最近关闭");
    private readonly ToolStripDropDownButton _sessionButton = new("会话");
    private readonly ToolStripDropDownButton _tabQuickButton = new("当前标签");
    private readonly ToolStripDropDownButton _workspaceButton = new("工作区");
    private readonly ToolStripButton _commandPaletteButton = new("⌘");
    private readonly ToolStripButton _tabSwitchButton = new("@");
    private readonly ToolStripSpringLabel _titleDragSpacer = new();
    private readonly ToolStripButton _titleNewTabButton = new("+");
    private readonly ToolStripButton _titleWindowMinButton = new("—");
    private readonly ToolStripButton _titleWindowMaxButton = new("□");
    private readonly ToolStripButton _titleWindowCloseButton = new("×");
    private readonly ToolStripLabel _appTitleLabel = new("Lfsun浏览器");
    private readonly ToolStripLabel _titleTabStatsLabel = new("Tab 0");
    private readonly ToolStripLabel _titleMemoryStatsLabel = new("Mem 0MB");
    private readonly ToolStripLabel _siteBadgeLabel = new("当前站点");
    private readonly ToolStripSpringTextBox _addressTextBox = new();
    private readonly ModernTabControl _tabControl = new();
    private readonly System.Windows.Forms.Timer _maintenanceTimer = new();
    private readonly System.Windows.Forms.Timer _refreshClickTimer = new();
    private readonly System.Windows.Forms.Timer _toastTimer = new();
    private readonly System.Windows.Forms.Timer _addressSuggestionRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _ambientMotionTimer = new();
    private readonly System.Windows.Forms.Timer _windowTransitionTimer = new();
    private readonly DownloadManager _downloadManager;
    private readonly BookmarkManager _bookmarkManager;
    private readonly PluginManager _pluginManager;
    private readonly CredentialStore _credentialStore;
    private readonly CredentialAutofillService _credentialAutofillService;
    private readonly AdBlockService _adBlockService;
    private readonly HistoryManager _historyManager;
    private readonly PublicIpInfoService _publicIpInfoService = new();
    private readonly BrowserSessionStore _sessionStore;
    private readonly NamedSessionStore _namedSessionStore;
    private readonly SessionLaunchPayloadStore _launchPayloadStore;
    private readonly TabManager _tabManager;
    private readonly ContextMenuStrip _bookmarkContextMenu = new();
    private readonly ContextMenuStrip _tabContextMenu = new();
    private readonly ContextMenuStrip _windowModeContextMenu = new();
    private readonly ContextMenuStrip _addressContextMenu = new();
    private readonly ToolStripMenuItem _bookmarkCtxOpen = new("打开");
    private readonly ToolStripMenuItem _bookmarkCtxOpenNewTab = new("在新标签页打开");
    private readonly ToolStripMenuItem _bookmarkCtxOpenNewTabBackground = new("在后台新标签页打开");
    private readonly ToolStripMenuItem _bookmarkCtxCopyUrl = new("复制链接地址");
    private readonly ToolStripMenuItem _bookmarkCtxOpenAllInFolder = new("在此文件夹中打开全部书签");
    private readonly ToolStripMenuItem _bookmarkCtxAdd = new("新增书签");
    private readonly ToolStripMenuItem _bookmarkCtxAddFolder = new("新增文件夹");
    private readonly ToolStripMenuItem _bookmarkCtxEdit = new("编辑");
    private readonly ToolStripMenuItem _bookmarkCtxDelete = new("删除");
    private readonly ToolStripMenuItem _bookmarkCtxViewInSettings = new("在设置中查看");
    private readonly ToolStripMenuItem _tabCtxCloseCurrent = new("关闭当前");
    private readonly ToolStripMenuItem _tabCtxCloseOthers = new("关闭其他");
    private readonly ToolStripMenuItem _tabCtxCloseAll = new("关闭所有");
    private readonly ToolStripMenuItem _tabCtxCloseLeft = new("关闭左侧");
    private readonly ToolStripMenuItem _tabCtxCloseRight = new("关闭右侧");
    private readonly ToolStripMenuItem _tabCtxTogglePin = new("固定标签");
    private readonly ToolStripMenuItem _tabCtxColor = new("标签色标");
    private readonly ToolStripMenuItem _tabCtxRename = new("重命名标签");
    private readonly ToolStripMenuItem _tabCtxOps = new("标签批处理");
    private readonly ToolStripMenuItem _tabCtxOpsCloseOthers = new("关闭其他 (保留固定)");
    private readonly ToolStripMenuItem _tabCtxOpsCloseLeft = new("关闭左侧 (保留固定)");
    private readonly ToolStripMenuItem _tabCtxOpsCloseRight = new("关闭右侧 (保留固定)");
    private readonly ToolStripMenuItem _tabCtxOpsCloseHost = new("关闭同站标签 (保留当前/固定)");
    private readonly ToolStripMenuItem _tabCtxOpsDedupe = new("清理重复标签 (保留当前/固定)");
    private readonly ToolStripMenuItem _tabCtxOpsSuspendOthers = new("休眠其他 (保留固定)");
    private readonly ToolStripMenuItem _tabCtxOpsResumeAll = new("恢复全部休眠标签");
    private readonly ToolStripMenuItem _tabCtxDuplicate = new("复制标签");
    private readonly ToolStripMenuItem _tabCtxReload = new("重新加载 (Ctrl+R)");
    private readonly ToolStripMenuItem _tabCtxHardRefresh = new("强制刷新 (Ctrl+F5)");
    private readonly ToolStripMenuItem _tabCtxCopyUrl = new("复制页面链接 (URL)");
    private readonly ToolStripMenuItem _tabCtxToggleMute = new("🔇 静音标签页 (Ctrl+M)");
    private readonly ToolStripMenuItem _tabCtxCaptureScreenshot = new("📸 网页快照截图 (Ctrl+Shift+S)");
    private readonly ToolStripMenuItem _tabCtxQrCode = new("📱 生成手机浏览二维码");
    private readonly ToolStripMenuItem _tabCtxReaderMode = new("📖 沉浸阅读模式 (Alt+R)");
    private readonly ToolStripMenuItem _tabCtxPictureInPicture = new("📺 视频独立画中画");
    private readonly ToolStripMenuItem _tabCtxSplitView = new("◫ 开启双屏分屏 (Alt+D)");
    private readonly ToolStripMenuItem _tabCtxReopenClosed = new("恢复最近关闭");
    private readonly ToolStripMenuItem _windowCtxOpenPrivate = new("新开无痕浏览器");
    private readonly ToolStripMenuItem _windowCtxOpenIsolated = new("新开独立双开浏览器");
    private readonly ToolStripMenuItem _windowCtxOpenShared = new("新开共享双开浏览器");
    private readonly ToolStripMenuItem _addressCtxCopy = new("复制地址");
    private readonly ToolStripMenuItem _addressCtxCopyMarkdown = new("以 Markdown 格式复制");
    private readonly ToolStripMenuItem _addressCtxQrCode = new("生成网址二维码");
    private readonly ToolStripMenuItem _addressCtxPasteGo = new("粘贴并访问");
    private readonly ToolStripMenuItem _addressCtxClear = new("清空地址栏");
    private readonly ToolStripMenuItem _addressCtxTabSwitch = new("快速切换标签 (Ctrl+K)");
    private readonly ToolStripMenuItem _addressCtxCommandPalette = new("打开命令面板 (Ctrl+Shift+P)");
    private readonly ToolStripMenuItem _addressCtxSearch = new("用默认搜索引擎搜索");
    private readonly ToolStripButton _appMenuButton = new("☰");
    private readonly ContextMenuStrip _appMenu = new();
    private ShortcutsOverviewWindow? _shortcutsOverviewWindow;
    private readonly Panel _toastPanel = new();
    private readonly FindBar _findBar = new();
    private Guid? _findTabId;
    private readonly Label _toastLabel = new();
    private readonly Panel _addressSuggestionPanel = new();
    private readonly ListBox _addressSuggestionListBox = new();
    private readonly Label _addressSuggestionHintLabel = new();
    private readonly List<string> _pinnedAddressCommandIds = [];
    private readonly LinkedList<RecentAddressCommandEntry> _recentAddressCommands = [];
    private readonly ToolStripRenderer _toolbarRenderer = new ModernToolStripRenderer();
    private readonly Dictionary<string, ToolStripItem> _toolbarItemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ToolStripItem, Color> _hoverBaseColors = new();
    private readonly AutoCompleteStringCollection _addressSuggestions = [];
    private const string AddressSuggestionHintDefault = "Enter 执行  |  Tab 补全  |  ↑↓ 选择  |  Esc 关闭";
    private const int MaxPinnedAddressCommandCount = 10;
    private const int MaxRecentAddressCommandCount = 18;
    private const int ToastDurationMs = 2600;
    private const int ToastTickMs = 40;
    private const string WorkspaceSessionPrefix = "工作区-";
    private const string WorkspaceOpenPolicyKeepAll = "KeepAll";
    private const string WorkspaceOpenPolicyDedupe = "Dedupe";
    private const string WorkspaceOpenPolicyClean = "Clean";
    private const int MaxVisibleTitleTabs = 12;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private static readonly UiAppearancePreset[] UiAppearancePresets =
    [
        new("balanced", "平衡", "Light", "Normal", "Blue", "Standard", "YaHei"),
        new("focus", "专注", "Dark", "Compact", "Green", "Soft", "DengXian"),
        new("vivid", "鲜明", "Dark", "Normal", "Rose", "Vivid", "YaHei")
    ];
    private static readonly TabColorOption[] TabColorOptions =
    [
        new("Blue", "蓝色"),
        new("Green", "青绿"),
        new("Orange", "琥珀"),
        new("Rose", "玫红")
    ];
    private static readonly AddressCommandDefinition[] AddressCommandCatalog =
    [
        new("settings", "settings", "打开设置", "打开浏览器设置窗口", ["set", "config", "设置"]),
        new("history", "history", "打开历史记录", "打开历史记录窗口", ["his", "log", "历史"]),
        new("downloads", "downloads", "打开下载管理", "打开下载管理窗口", ["dl", "download", "下载"]),
        new("sessions", "sessions", "打开会话中心", "管理和恢复会话", ["session", "会话"]),
        new("bookmarks", "bookmarks", "打开书签管理", "打开书签设置窗口", ["bookmark", "fav", "书签"]),
        new("newtab", "newtab", "新建标签页", "使用启动页模板打开新标签", ["tab", "new", "新建"]),
        new("tabswitch", "tabswitch", "快速切换标签", "进入 @ 标签快速切换模式", ["switchtab", "quicktab", "标签切换"]),
        new("tabops", "tabops", "标签批处理", "用法: :tabops closeothers|closeall|closeleft|closeright|closehost|focushost|closeblank|dedupe|suspendhost|suspendothers|suspendall|resumehost|resumeall|hoststats|stats", ["tabbatch", "tabmanage", "标签管理"]),
        new("hardrefresh", "hardrefresh", "强制刷新当前标签", "清缓存后刷新当前页面", ["refresh", "reload", "刷新"]),
        new("reloadplugins", "reloadplugins", "重载插件", "重新扫描并加载插件目录", ["plugins", "plugin", "插件"]),
        new("darkmode", "darkmode", "切换深色主题", "快速切换到深色外观", ["dark", "night", "深色"]),
        new("lightmode", "lightmode", "切换浅色主题", "快速切换到浅色外观", ["light", "day", "浅色"]),
        new("compactui", "compactui", "切换紧凑密度", "压缩工具栏和控件间距", ["compact", "dense", "紧凑"]),
        new("normalui", "normalui", "切换标准密度", "恢复标准间距与字号", ["normal", "default", "标准"]),
        new("togglebookmarksbar", "togglebookmarksbar", "切换书签栏显示", "显示或隐藏顶部书签栏", ["bookmarkbar", "bar", "书签栏"]),
        new("theme", "theme", "设置主题", "用法: :theme dark|light", ["ui-theme", "主题"]),
        new("density", "density", "设置界面密度", "用法: :density compact|normal", ["ui-density", "密度"]),
        new("accent", "accent", "设置强调色", "用法: :accent blue|green|orange|rose", ["color", "强调色"]),
        new("accentstrength", "accentstrength", "设置强调强度", "用法: :accentstrength soft|standard|vivid", ["intensity", "strength", "强度"]),
        new("uipreset", "uipreset", "应用外观预设", "用法: :uipreset balanced|focus|vivid|cycle", ["preset", "skin", "预设"]),
        new("recentcommands", "recentcommands", "管理最近命令", "用法: :recentcommands clear|show", ["recent", "cmdhistory", "最近命令"]),
        new("pincommand", "pincommand", "固定常用命令", "用法: :pincommand add|remove|list|clear <command>", ["pin", "favorite", "常用命令"]),
        new("resetui", "resetui", "重置界面外观", "恢复主题/密度/强调色/字体到默认", ["uireset", "restoreui", "重置外观"]),
        new("font", "font", "设置界面字体", "用法: :font yahei|dengxian|song", ["ui-font", "字体"]),
        new("bookmarksbar", "bookmarksbar", "设置书签栏显示", "用法: :bookmarksbar on|off|toggle", ["bar-mode", "书签栏模式"]),
        new("statusbar", "statusbar", "设置状态栏模式", "用法: :statusbar detail|compact", ["status", "bar", "状态栏"]),
        new("tabname", "tabname", "设置当前标签名称", "用法: :tabname <name>|clear", ["renametab", "labeltab", "标签命名"]),
        new("tabcolor", "tabcolor", "设置当前标签色标", "用法: :tabcolor blue|green|orange|rose|clear", ["tabtag", "label", "标签色标"]),
        new("zen", "zen", "切换沉浸模式", "用法: :zen on|off|toggle", ["immersive", "focus", "沉浸"]),
        new("help", "help", "查看指令帮助", "显示地址栏快捷指令说明", ["?", "usage", "帮助"]),
        new("palette", "palette", "打开命令面板", "聚焦地址栏并打开命令建议", ["command", "palette", "面板"]),
        new("reader", "reader", "切换沉浸阅读", "开启/退出当前网页沉浸阅读模式", ["read", "readermode", "阅读"]),
        new("split", "split", "切换双屏分屏", "开启/退出网页左右双屏并行分屏", ["splitview", "dual", "分屏"]),
        new("verticaltabs", "verticaltabs", "切换垂直标签栏", "开启/退出左侧垂直标签栏", ["vtabs", "vertical", "垂直标签"]),
        new("pip", "pip", "切换画中画", "切换当前网页视频系统画中画浮窗", ["pictureinpicture", "floatvideo", "画中画"]),
        new("freememory", "freememory", "休眠后台标签释放内存", "一键休眠所有后台空闲标签释放系统内存", ["cleanmem", "hibernate", "释放内存"]),
        new("sidepanel", "sidepanel", "切换侧边工具栏", "展开/收起右侧多功能抽屉栏", ["sidebar", "panel", "侧边栏"]),
        new("qrcode", "qrcode", "生成当前页二维码", "跨端手机扫码继续浏览", ["qr", "扫码", "二维码"]),
        new("screenshot", "screenshot", "网页整页截图", "截取当前网页并复制到剪贴板", ["shot", "capture", "截图"]),
        new("perf", "perf", "网页性能与诊断", "展示 DNS/TCP/TTFB/DOM 加载耗时阶梯与资源统计", ["diagnostics", "timing", "性能", "诊断"])
    ];
    private static readonly Dictionary<string, AddressCommandArgumentOption[]> AddressCommandArgumentCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["theme"] =
        [
            new("dark", "切换到深色主题", ["night", "d", "深色"]),
            new("light", "切换到浅色主题", ["day", "l", "浅色"])
        ],
        ["density"] =
        [
            new("compact", "切换到紧凑密度", ["dense", "tight", "紧凑"]),
            new("normal", "切换到标准密度", ["default", "standard", "标准"])
        ],
        ["accent"] =
        [
            new("blue", "设置强调色为蓝色", ["b", "蓝"]),
            new("green", "设置强调色为青绿", ["g", "绿"]),
            new("orange", "设置强调色为琥珀", ["amber", "o", "橙"]),
            new("rose", "设置强调色为玫红", ["pink", "r", "红"])
        ],
        ["accentstrength"] =
        [
            new("soft", "设置强调强度为柔和", ["s", "柔和"]),
            new("standard", "设置强调强度为标准", ["normal", "std", "标准"]),
            new("vivid", "设置强调强度为鲜明", ["strong", "v", "鲜明"])
        ],
        ["uipreset"] =
        [
            new("balanced", "预设: 平衡（浅色/标准/蓝色/标准）", ["default", "normal", "平衡"]),
            new("focus", "预设: 专注（深色/紧凑/青绿/柔和）", ["work", "study", "专注"]),
            new("vivid", "预设: 鲜明（深色/标准/玫红/鲜明）", ["strong", "bold", "鲜明"]),
            new("cycle", "循环切换到下一个预设", ["next", "switch", "轮换"])
        ],
        ["recentcommands"] =
        [
            new("clear", "清空最近命令记录", ["reset", "clean", "清空"]),
            new("show", "在状态栏显示最近命令摘要", ["list", "ls", "显示"])
        ],
        ["pincommand"] =
        [
            new("add", "固定命令，用法: :pincommand add settings", ["pin", "save", "添加"]),
            new("remove", "取消固定，用法: :pincommand remove settings", ["unpin", "delete", "移除"]),
            new("list", "查看已固定命令", ["show", "ls", "列表"]),
            new("clear", "清空固定命令", ["reset", "clean", "清空"])
        ],
        ["statusbar"] =
        [
            new("detail", "状态栏显示详细统计", ["full", "detailed", "详细"]),
            new("compact", "状态栏显示简洁统计", ["simple", "brief", "简洁"])
        ],
        ["tabname"] =
        [
            new("clear", "清除自定义标签名称", ["reset", "default", "清除"])
        ],
        ["tabops"] =
        [
            new("closeothers", "关闭当前之外的非固定标签", ["co", "others", "关闭其他"]),
            new("closeall", "关闭所有非固定标签", ["ca", "all", "关闭全部"]),
            new("closeleft", "关闭当前左侧非固定标签", ["cl", "left", "关闭左侧"]),
            new("closeright", "关闭当前右侧非固定标签", ["cr", "right", "关闭右侧"]),
            new("closehost", "关闭与当前标签同站点的非固定标签", ["ch", "samehost", "同站"]),
            new("focushost", "保留同站标签并关闭其他非固定标签", ["fh", "keephost", "同站聚焦"]),
            new("closeblank", "关闭空白标签（保留当前与固定）", ["cb", "blanktabs", "空白标签"]),
            new("dedupe", "清理重复 URL 标签（保留当前与固定）", ["cleanup", "unique", "去重"]),
            new("suspendhost", "休眠与当前标签同站点的非固定标签", ["sh", "sleephost", "同站休眠"]),
            new("suspendothers", "休眠当前之外的非固定标签", ["so", "sleepothers", "休眠其他"]),
            new("suspendall", "休眠所有非固定标签（保留当前）", ["sa", "sleepall", "休眠全部"]),
            new("resumehost", "恢复与当前标签同站点的休眠标签", ["rh", "wakehost", "同站恢复"]),
            new("resumeall", "恢复所有休眠标签", ["ra", "wakeall", "恢复全部"]),
            new("hoststats", "显示标签站点分布（Top）", ["hosts", "domains", "站点分布"]),
            new("stats", "显示标签统计摘要", ["show", "summary", "统计"])
        ],
        ["tabcolor"] =
        [
            new("blue", "当前标签色标设为蓝色", ["b", "蓝"]),
            new("green", "当前标签色标设为青绿", ["g", "绿"]),
            new("orange", "当前标签色标设为琥珀", ["o", "橙"]),
            new("rose", "当前标签色标设为玫红", ["r", "红"]),
            new("clear", "清除当前标签色标", ["none", "reset", "清除"])
        ],
        ["font"] =
        [
            new("yahei", "设置字体为雅黑", ["yh", "雅黑"]),
            new("dengxian", "设置字体为等线", ["dx", "等线"]),
            new("song", "设置字体为宋体", ["st", "宋体"])
        ],
        ["bookmarksbar"] =
        [
            new("on", "显示书签栏", ["show", "open", "开"]),
            new("off", "隐藏书签栏", ["hide", "close", "关"]),
            new("toggle", "切换书签栏显示状态", ["switch", "t", "切换"])
        ],
        ["zen"] =
        [
            new("on", "启用沉浸模式", ["enable", "start", "开"]),
            new("off", "关闭沉浸模式", ["disable", "stop", "关"]),
            new("toggle", "切换沉浸模式状态", ["switch", "t", "切换"])
        ]
    };

    private Guid? _contextNodeId;
    private Guid? _contextParentFolderId;
    private int _tabContextIndex = -1;
    private bool _isRestoringSession;
    private bool _isClosingCleanly;
    private bool _suppressAddressSuggestionRefresh;
    private bool _isZenMode;
    private bool _addressTextBoxFocused;
    private bool _pendingTitleBarRebuild;
    private bool _startupCompleted;
    private bool _isWindowTransitionRunning;
    private bool _isWindowFadeOutPhase;
    private bool _animateNextRestore;
    private bool _titleTabDragging;
    private bool _titleTabDragMoved;
    private int _windowTransitionStep;
    private int _cachedWorkspaceCount;
    private int _cachedWorkspaceDuplicateCount;
    private int _cachedWorkspaceHeavyCount;
    private int _cachedWorkspaceStaleCount;
    private FormWindowState _lastWindowState = FormWindowState.Normal;
    private int _toastRemainingMs;
    private float _ambientMotionPhase;
    private Guid? _titleTabDragSourceId;
    private Point _titleTabDragStartPoint;
    private Icon? _windowIcon;
    private DownloadsWindow? _downloadsWindow;
    private HistoryWindow? _historyWindow;
    private SettingsWindow? _settingsWindow;
    private BookmarksSettingsWindow? _bookmarksSettingsWindow;
    private SessionsWindow? _sessionsWindow;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            // Keep borderless look while allowing taskbar-driven minimize/maximize behavior.
            createParams.Style |= WsSysMenu | WsMinimizeBox | WsMaximizeBox;
            return createParams;
        }
    }

    public MainWindow(BrowserConfig config, AppLaunchOptions? launchOptions = null)
    {
        _config = config;
        _launchOptions = launchOptions ?? new AppLaunchOptions();
        AppTheme.Configure(_config.UiTheme, _config.UiDensity, _config.UiAccent, _config.UiFont, _config.UiAccentStrength);
        Text = " ";
        ShowIcon = true;
        ShowInTaskbar = true;
        MinimizeBox = true;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1080, 680);
        Width = 1420;
        Height = 920;
        KeyPreview = true;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        ApplyWindowIconFromConfig();

        var downloadDirectory = string.IsNullOrWhiteSpace(_config.DownloadDirectory)
            ? BrowserConfig.DefaultDownloadDirectory
            : _config.DownloadDirectory.Trim();
        var bookmarkFileName = string.IsNullOrWhiteSpace(_config.BookmarksFileName)
            ? "bookmarks.json"
            : _config.BookmarksFileName.Trim();
        var credentialFileName = string.IsNullOrWhiteSpace(_config.CredentialsFileName)
            ? "credentials.json"
            : _config.CredentialsFileName.Trim();
        var historyFileName = string.IsNullOrWhiteSpace(_config.HistoryFileName)
            ? "history.json"
            : _config.HistoryFileName.Trim();
        // 中文注释：插件安装到用户数据目录，避免重新打包/升级时程序目录被清空导致插件丢失（每次都要重新导入）；
        // 启动时把程序目录 Plugins（内置示例或旧版本安装的插件）自动迁移过去。
        var pluginRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfsunBrowser",
            "plugins");
        TryMigrateLegacyPlugins(Path.Combine(AppContext.BaseDirectory, _config.PluginsDirectoryName), pluginRoot);
        var bookmarkPath = ResolveUserDataFilePath(bookmarkFileName, "bookmarks.json");
        var credentialPath = ResolveUserDataFilePath(credentialFileName, "credentials.json");
        var historyPath = ResolveUserDataFilePath(historyFileName, "history.json");
        TryMigrateLegacyDataFile(bookmarkFileName, bookmarkPath, "书签");
        TryMigrateLegacyDataFile(credentialFileName, credentialPath, "凭据");
        TryMigrateLegacyDataFile(historyFileName, historyPath, "历史记录");

        _downloadManager = new DownloadManager(downloadDirectory, Log);
        _downloadManager.DownloadStarted += item =>
        {
            try
            {
                BeginInvoke(() =>
                {
                    ShowToast($"📥 开始下载: {item.FileName}");
                    RefreshMetrics();
                });
            }
            catch { }
        };
        _downloadManager.DownloadCompleted += item =>
        {
            try
            {
                BeginInvoke(() =>
                {
                    ShowToast($"✅ 下载完成: {item.FileName}");
                    RefreshMetrics();
                });
            }
            catch { }
        };
        _downloadManager.DownloadFailed += item =>
        {
            try
            {
                BeginInvoke(() =>
                {
                    ShowToast($"❌ 下载中断: {item.FileName}");
                    RefreshMetrics();
                });
            }
            catch { }
        };
        _bookmarkManager = new BookmarkManager(bookmarkPath, Log);
        _pluginManager = new PluginManager(pluginRoot, _config, Log);
        _credentialStore = new CredentialStore(credentialPath, Log);
        _credentialAutofillService = new CredentialAutofillService(_config, _credentialStore, Log);
        _historyManager = new HistoryManager(historyPath, Log);
        _sessionStore = new BrowserSessionStore(Path.Combine(_launchOptions.ProfileStoragePath, "session.json"), Log);
        _namedSessionStore = new NamedSessionStore(Path.Combine(_launchOptions.ProfileStoragePath, "named-sessions"), Log);
        _launchPayloadStore = new SessionLaunchPayloadStore(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LfsunBrowser",
                "launch-payloads"),
            Log);
        _tabManager = new TabManager(
            _tabControl,
            _config,
            _downloadManager,
            _pluginManager,
            _credentialAutofillService,
            _launchOptions.ProfileStoragePath,
            Log);
        // 中文注释：注入新标签页速拨面板内容，about:blank 新标签展示本地速拨页。
        _tabManager.NewTabPageHtmlProvider = BuildNewTabPageHtml;
        _tabManager.ZoomOverrideResolver = host =>
            _config.ZoomOverrides.TryGetValue(host, out var zoomFactor) ? zoomFactor : null;
        // 中文注释：全局共享一个广告过滤服务实例，开关与自定义域名读自配置。
        _adBlockService = new AdBlockService(_config);
        _tabManager.AdBlockService = _adBlockService;

        _sidePanel = new BrowserSidePanel(
            _bookmarkManager,
            _historyManager,
            _downloadManager,
            () => _tabManager.ActiveTab,
            async (uri, openNewTab) =>
            {
                if (openNewTab)
                {
                    await _tabManager.CreateTabAsync(uri);
                }
                else
                {
                    await NavigateActiveTabAsync(uri);
                }
                RefreshToolbar();
            },
            ShowToast,
            () => ShowBookmarksSettingsWindow(),
            () => ShowHistoryWindow(),
            () => ShowDownloadManager(),
            () => _ = ToggleSplitViewAsync(),
            () => _tabManager.Tabs,
            async () =>
            {
                var freed = await _tabManager.SuspendAllBackgroundTabsAsync();
                RefreshMetrics();
                return freed;
            },
            () =>
            {
                var muted = _tabManager.ToggleMuteAllTabs();
                RefreshToolbar();
                return muted;
            },
            () => ShowPagePerformanceCard());
        _sidePanel.CloseRequested += () => _sidePanel.Visible = false;

        _verticalTabStrip = new VerticalTabStrip(
            () => _tabManager.Tabs,
            () => _tabManager.ActiveTab,
            async tab =>
            {
                await _tabManager.SelectTabAsync(tab);
                RefreshToolbar();
            },
            async tab =>
            {
                await _tabManager.CloseTabAsync(tab);
                RefreshToolbar();
            },
            async () =>
            {
                await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
                RefreshToolbar();
            },
            (tab, screenPoint) =>
            {
                _tabContextIndex = _tabManager.IndexOf(tab);
                _tabContextMenu.Show(screenPoint);
            });

        _verticalTabsToggleButton.ToolTipText = "切换水平标签 / 垂直侧边标签栏 (Ctrl+Shift+V)";
        _verticalTabsToggleButton.Click += (_, _) => ToggleVerticalTabs();

        _splitPanel = new SplitBrowserPanel();
        _splitPanel.InitializeUserDataDir(_launchOptions.ProfileStoragePath);
        _splitPanel.SplitViewClosed += () =>
        {
            _splitPanel.DeactivateSplit(_tabControl, this);
            RefreshToolbar();
        };

        _splitViewButton.ToolTipText = "双屏分屏并行浏览 (Alt+D)";
        _splitViewButton.Click += async (_, _) => await ToggleSplitViewAsync();

        _readerModeButton.ToolTipText = "沉浸式阅读模式 (Alt+R)";
        _readerModeButton.Click += async (_, _) => await ToggleReaderModeForActiveTabAsync();

        _zoomPillButton.Visible = false;
        _zoomPillButton.ToolTipText = "当前页面已自定义缩放，点击恢复 100% (Ctrl+0)";
        _zoomPillButton.Click += (_, _) =>
        {
            if (_tabManager.ActiveTab is not null)
            {
                _tabManager.ActiveTab.ZoomFactor = 1.0;
                _zoomPillButton.Visible = false;
                ShowToast("已恢复默认 100% 缩放");
                RefreshToolbar();
            }
        };

        _sidePanelButton.Click += (_, _) =>
        {
            _sidePanel.Visible = !_sidePanel.Visible;
            if (_sidePanel.Visible)
            {
                _sidePanel.BringToFront();
                _sidePanel.RefreshActiveContent();
            }
        };
        _sidePanelButton.DropDownOpening += (_, _) =>
        {
            _sidePanelButton.DropDownItems.Clear();
            var itemToolbox = new ToolStripMenuItem("🛠️ 页面实用工具箱", null, (_, _) => { _sidePanel.SwitchTo(SidePanelMode.Toolbox); _sidePanel.Visible = true; _sidePanel.BringToFront(); });
            var itemBookmarks = new ToolStripMenuItem("🔖 快速书签侧栏", null, (_, _) => { _sidePanel.SwitchTo(SidePanelMode.Bookmarks); _sidePanel.Visible = true; _sidePanel.BringToFront(); });
            var itemHistory = new ToolStripMenuItem("🕒 快速历史侧栏", null, (_, _) => { _sidePanel.SwitchTo(SidePanelMode.History); _sidePanel.Visible = true; _sidePanel.BringToFront(); });
            var itemDownloads = new ToolStripMenuItem("⬇️ 快速下载侧栏", null, (_, _) => { _sidePanel.SwitchTo(SidePanelMode.Downloads); _sidePanel.Visible = true; _sidePanel.BringToFront(); });
            var itemScratchpad = new ToolStripMenuItem("📝 随手便签记事本", null, (_, _) => { _sidePanel.SwitchTo(SidePanelMode.Scratchpad); _sidePanel.Visible = true; _sidePanel.BringToFront(); });
            var toggleItem = new ToolStripMenuItem(_sidePanel.Visible ? "✕ 折叠侧边栏 (Alt+S)" : "◧ 展开侧边栏 (Alt+S)", null, (_, _) => { _sidePanel.Visible = !_sidePanel.Visible; });

            _sidePanelButton.DropDownItems.AddRange([itemToolbox, itemBookmarks, itemHistory, itemDownloads, itemScratchpad, new ToolStripSeparator(), toggleItem]);
            _sidePanelButton.DropDown.Renderer = _toolbarRenderer;
        };

        BuildLayout();
        BuildBookmarkContextMenu();
        BuildTabContextMenu();
        BuildWindowModeContextMenu();
        BuildMultiWindowMenu();
        BuildAddressContextMenu();
        BuildAppMenu();
        LoadPinnedAddressCommandsFromConfig();
        LoadRecentAddressCommandsFromConfig();
        ApplyTheme();
        ApplyZenMode(_config.ZenMode, persist: false, showToast: false);
        WireEvents();
    }

    /// <summary>
    /// 中文注释：生成新标签页速拨面板 HTML；失败时返回 null 回退普通 about:blank。
    /// </summary>
    private string? BuildNewTabPageHtml()
    {
        try
        {
            return NewTabPageBuilder.Build(_config, _historyManager.Entries, _bookmarkManager.TopLevelItems);
        }
        catch (Exception exception)
        {
            Log($"生成新标签页失败: {exception.Message}");
            return null;
        }
    }

    private void BuildLayout()
    {
        _titleBar.GripStyle = ToolStripGripStyle.Hidden;
        _titleBar.AutoSize = false;
        _titleBar.Height = AppTheme.IsCompact ? 32 : 36;
        _titleBar.Padding = new Padding(8, 0, 0, 0);
        _titleBar.Dock = DockStyle.Top;
        _titleBar.ImageScalingSize = new Size(16, 16);
        _titleBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _titleBar.CanOverflow = false;

        _topBar.GripStyle = ToolStripGripStyle.Hidden;
        _topBar.AutoSize = false;
        _topBar.Height = AppTheme.IsCompact ? 38 : 44;
        _topBar.Padding = new Padding(8, 3, 8, 3);
        _topBar.Dock = DockStyle.Top;
        _topBar.ImageScalingSize = new Size(20, 20);
        _topBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _topBar.CanOverflow = true;

        _addressBar.GripStyle = ToolStripGripStyle.Hidden;
        _addressBar.AutoSize = false;
        _addressBar.Height = 44;
        _addressBar.Padding = new Padding(10, 4, 10, 4);
        _addressBar.Dock = DockStyle.Top;
        _addressBar.ImageScalingSize = new Size(18, 18);
        _addressBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _addressBar.CanOverflow = true;

        _addressTextBox.AutoSize = false;
        _addressTextBox.Size = new Size(520, 30);
        _addressTextBox.Margin = new Padding(1, 2, 4, 2);
        _addressTextBox.BorderStyle = BorderStyle.FixedSingle;
        UpdateAddressCommandTooltip();
        _addressTextBox.Overflow = ToolStripItemOverflow.Never;
        _siteBadgeLabel.AutoSize = false;
        _siteBadgeLabel.Width = 34;
        _siteBadgeLabel.Margin = new Padding(4, 2, 1, 2);
        _siteBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _siteBadgeLabel.Overflow = ToolStripItemOverflow.AsNeeded;
        _tabQuickButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _recentTabsButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _sessionButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _workspaceButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _titleDragSpacer.Overflow = ToolStripItemOverflow.Never;
        _titleDragSpacer.Text = string.Empty;
        _titleDragSpacer.AutoSize = false;
        _titleDragSpacer.Margin = new Padding(8, 0, 8, 0);
        RegisterToolbarItems();
        RebuildTitleBarItems();
        RebuildPinnedPluginButtons();

        _bookmarksBar.GripStyle = ToolStripGripStyle.Hidden;
        _bookmarksBar.AutoSize = false;
        _bookmarksBar.Height = AppTheme.IsCompact ? 28 : 30;
        _bookmarksBar.Padding = AppTheme.IsCompact ? new Padding(6, 1, 6, 1) : new Padding(8, 1, 8, 1);
        _bookmarksBar.Dock = DockStyle.Top;
        _bookmarksBar.ImageScalingSize = new Size(16, 16);
        _bookmarksBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _bookmarksBar.CanOverflow = true;

        _tabControl.Dock = DockStyle.Fill;
        _tabControl.HideTabHeader = true;
        _addressBar.Visible = false;

        _statusBarPanel.Strip.Dock = DockStyle.Bottom;
        UpdateModeLabelText();

        _toastPanel.Visible = false;
        _toastPanel.Size = new Size(320, 44);
        _toastPanel.BackColor = AppTheme.Accent;
        _toastPanel.Padding = new Padding(14, 10, 14, 10);
        _toastPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _toastLabel.Dock = DockStyle.Fill;
        _toastLabel.TextAlign = ContentAlignment.MiddleCenter;
        _toastLabel.ForeColor = Color.White;
        _toastLabel.Font = AppTheme.CreateFont(9.5F, FontStyle.Bold);
        _toastLabel.BackColor = Color.Transparent;
        _toastPanel.Controls.Add(_toastLabel);
        _toastPanel.Paint += (_, eventArgs) => DrawToastSurface(eventArgs.Graphics);

        _addressSuggestionPanel.Visible = false;
        _addressSuggestionPanel.Size = new Size(520, 220);
        _addressSuggestionPanel.BorderStyle = BorderStyle.None;
        _addressSuggestionPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _addressSuggestionPanel.Padding = new Padding(2);
        _addressSuggestionPanel.Paint += (_, eventArgs) => DrawAddressSuggestionSurface(eventArgs.Graphics);

        _addressSuggestionListBox.Dock = DockStyle.Fill;
        _addressSuggestionListBox.BorderStyle = BorderStyle.None;
        _addressSuggestionListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _addressSuggestionListBox.ItemHeight = 42;
        _addressSuggestionHintLabel.Dock = DockStyle.Bottom;
        _addressSuggestionHintLabel.Height = 24;
        _addressSuggestionHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        _addressSuggestionHintLabel.Padding = new Padding(10, 0, 10, 0);
        _addressSuggestionHintLabel.Text = AddressSuggestionHintDefault;
        _addressSuggestionPanel.Controls.Add(_addressSuggestionListBox);
        _addressSuggestionPanel.Controls.Add(_addressSuggestionHintLabel);

        Controls.Add(_sidePanel);
        Controls.Add(_verticalTabStrip);
        Controls.Add(_splitPanel);
        Controls.Add(_tabControl);
        Controls.Add(_bookmarksBar);
        Controls.Add(_addressBar);
        Controls.Add(_topBar);
        Controls.Add(_titleBar);
        Controls.Add(_addressSuggestionPanel);
        Controls.Add(_toastPanel);
        Controls.Add(_findBar);
        Controls.Add(_statusBarPanel.Strip);
        UpdateToastLocation();
    }

    private void BuildBookmarkContextMenu()
    {
        _bookmarkContextMenu.Items.AddRange(
        [
            _bookmarkCtxOpen,
            _bookmarkCtxOpenNewTab,
            _bookmarkCtxOpenNewTabBackground,
            _bookmarkCtxCopyUrl,
            _bookmarkCtxOpenAllInFolder,
            new ToolStripSeparator(),
            _bookmarkCtxAdd,
            _bookmarkCtxAddFolder,
            new ToolStripSeparator(),
            _bookmarkCtxEdit,
            _bookmarkCtxDelete,
            new ToolStripSeparator(),
            _bookmarkCtxViewInSettings
        ]);

        _bookmarkCtxOpen.Click += async (_, _) => await OpenContextBookmarkAsync(false);
        _bookmarkCtxOpenNewTab.Click += async (_, _) => await OpenContextBookmarkAsync(true);
        _bookmarkCtxOpenNewTabBackground.Click += async (_, _) => await OpenContextBookmarkAsync(true);
        _bookmarkCtxCopyUrl.Click += (_, _) =>
        {
            if (_contextNodeId is { } id && _bookmarkManager.FindNode(id) is { IsLink: true } node && !string.IsNullOrWhiteSpace(node.Url))
            {
                Clipboard.SetText(node.Url);
                ShowToast("书签链接已复制到剪贴板！📋");
            }
        };
        _bookmarkCtxOpenAllInFolder.Click += async (_, _) =>
        {
            if (_contextNodeId is { } id && _bookmarkManager.FindNode(id) is { IsFolder: true } folder)
            {
                var links = folder.Children.Where(c => c.IsLink && !string.IsNullOrWhiteSpace(c.Url)).ToList();
                foreach (var link in links)
                {
                    await NavigateToBookmarkAsync(link.Url, inNewTab: true);
                }
                ShowToast($"已在新标签页中打开文件夹内 {links.Count} 个书签");
            }
        };
        _bookmarkCtxAdd.Click += (_, _) => AddBookmarkByContext();
        _bookmarkCtxAddFolder.Click += (_, _) => AddFolderByContext();
        _bookmarkCtxEdit.Click += (_, _) => EditContextBookmarkNode();
        _bookmarkCtxDelete.Click += (_, _) => DeleteContextBookmarkNode();
        _bookmarkCtxViewInSettings.Click += (_, _) => ViewContextNodeInSettings();
    }

    private void BuildTabContextMenu()
    {
        var tabCtxOpsResumeHost = new ToolStripMenuItem("恢复同站休眠标签");
        var tabCtxOpsSuspendHost = new ToolStripMenuItem("休眠同站标签 (保留当前/固定)");
        var tabCtxOpsFocusHost = new ToolStripMenuItem("聚焦当前站点 (关闭其他非固定)");
        var tabCtxOpsCloseBlank = new ToolStripMenuItem("关闭空白标签 (保留当前/固定)");
        _tabContextMenu.Items.AddRange(
        [
            _tabCtxTogglePin,
            _tabCtxToggleMute,
            _tabCtxColor,
            _tabCtxRename,
            _tabCtxDuplicate,
            _tabCtxReload,
            _tabCtxHardRefresh,
            _tabCtxCopyUrl,
            _tabCtxReopenClosed,
            new ToolStripSeparator(),
            _tabCtxReaderMode,
            _tabCtxSplitView,
            _tabCtxPictureInPicture,
            _tabCtxCaptureScreenshot,
            _tabCtxQrCode,
            new ToolStripSeparator(),
            _tabCtxOps,
            new ToolStripSeparator(),
            _tabCtxCloseCurrent,
            _tabCtxCloseOthers,
            _tabCtxCloseAll,
            _tabCtxCloseLeft,
            _tabCtxCloseRight
        ]);

        _tabContextMenu.Opening += (_, _) =>
        {
            var hasTab = _tabContextIndex >= 0 && _tabContextIndex < _tabControl.TabCount;
            var contextTab = hasTab ? ResolveTabByContextIndex() : null;
            _tabCtxTogglePin.Enabled = hasTab;
            _tabCtxTogglePin.Text = contextTab?.IsPinned == true ? "取消固定标签" : "固定标签";
            _tabCtxToggleMute.Enabled = hasTab;
            _tabCtxToggleMute.Text = contextTab?.IsMuted == true ? "🔊 取消静音标签页 (Ctrl+M)" : "🔇 静音标签页 (Ctrl+M)";
            _tabCtxReload.Enabled = hasTab;
            _tabCtxHardRefresh.Enabled = hasTab;
            _tabCtxCopyUrl.Enabled = contextTab?.CurrentUri is not null;
            _tabCtxReaderMode.Enabled = hasTab;
            _tabCtxSplitView.Enabled = hasTab;
            _tabCtxPictureInPicture.Enabled = hasTab;
            _tabCtxCaptureScreenshot.Enabled = hasTab;
            _tabCtxQrCode.Enabled = contextTab?.CurrentUri is not null;
            _tabCtxColor.Enabled = hasTab;
            var colorText = ResolveTabColorText(contextTab?.ColorTagKey);
            _tabCtxColor.Text = string.IsNullOrWhiteSpace(colorText) ? "标签色标" : $"标签色标({colorText})";
            BuildTabColorMenu(contextTab);
            _tabCtxRename.Enabled = hasTab;
            _tabCtxOps.Enabled = hasTab;
            _tabCtxOpsCloseLeft.Enabled = hasTab && _tabContextIndex > 0;
            _tabCtxOpsCloseRight.Enabled = hasTab && _tabContextIndex < _tabControl.TabCount - 1;
            _tabCtxOpsCloseHost.Enabled = hasTab;
            tabCtxOpsFocusHost.Enabled = contextTab?.CurrentUri is { Host.Length: > 0 };
            tabCtxOpsCloseBlank.Enabled = hasTab && _tabManager.Tabs.Any(tab => !tab.IsPinned && !ReferenceEquals(tab, contextTab) && IsBlankTab(tab));
            tabCtxOpsSuspendHost.Enabled = contextTab?.CurrentUri is { Host.Length: > 0 };
            tabCtxOpsResumeHost.Enabled = contextTab?.CurrentUri is { Host.Length: > 0 };
            _tabCtxOpsDedupe.Enabled = hasTab && _tabControl.TabCount > 1;
            _tabCtxDuplicate.Enabled = hasTab;
            _tabCtxReopenClosed.Enabled = _tabManager.RecentlyClosedTabs.Count > 0;
            _tabCtxCloseCurrent.Enabled = hasTab;
            _tabCtxCloseOthers.Enabled = hasTab && _tabControl.TabCount > 1;
            _tabCtxCloseAll.Enabled = _tabControl.TabCount > 0;
            _tabCtxCloseLeft.Enabled = hasTab && _tabContextIndex > 0;
            _tabCtxCloseRight.Enabled = hasTab && _tabContextIndex < _tabControl.TabCount - 1;
        };

        _tabCtxTogglePin.Click += (_, _) => ToggleTabPinByContext();
        _tabCtxRename.Click += (_, _) => RenameTabByContext();
        _tabCtxReload.Click += (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            tab?.Reload();
        };
        _tabCtxHardRefresh.Click += async (_, _) =>
        {
            await ExecuteAddressCommandAsync("hardrefresh");
        };
        _tabCtxCopyUrl.Click += (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab?.CurrentUri is not null)
            {
                Clipboard.SetText(tab.CurrentUri.AbsoluteUri);
                ShowToast("页面链接已复制到剪贴板！📋");
            }
        };
        _tabCtxOpsCloseOthers.Click += async (_, _) => await ApplyTabOpsFromContextAsync("closeothers");
        _tabCtxOpsCloseLeft.Click += async (_, _) => await ApplyTabOpsFromContextAsync("closeleft");
        _tabCtxOpsCloseRight.Click += async (_, _) => await ApplyTabOpsFromContextAsync("closeright");
        _tabCtxOpsCloseHost.Click += async (_, _) => await ApplyTabOpsFromContextAsync("closehost");
        tabCtxOpsFocusHost.Click += async (_, _) => await ApplyTabOpsFromContextAsync("focushost");
        tabCtxOpsCloseBlank.Click += async (_, _) => await ApplyTabOpsFromContextAsync("closeblank");
        tabCtxOpsSuspendHost.Click += async (_, _) => await ApplyTabOpsFromContextAsync("suspendhost");
        tabCtxOpsResumeHost.Click += async (_, _) => await ApplyTabOpsFromContextAsync("resumehost");
        _tabCtxOpsDedupe.Click += async (_, _) => await ApplyTabOpsFromContextAsync("dedupe");
        _tabCtxOpsSuspendOthers.Click += async (_, _) => await ApplyTabOpsFromContextAsync("suspendothers");
        _tabCtxOpsResumeAll.Click += async (_, _) => await ApplyTabOpsFromContextAsync("resumeall");
        _tabCtxDuplicate.Click += async (_, _) => await DuplicateTabByContextAsync();
        _tabCtxToggleMute.Click += (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab is not null)
            {
                tab.ToggleMute();
                ShowToast(tab.IsMuted ? "已静音标签 🔇" : "已恢复标签声音 🔊");
                RefreshToolbar();
            }
        };
        _tabCtxCaptureScreenshot.Click += async (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab is not null)
            {
                var bmp = await tab.CaptureScreenshotAsync();
                if (bmp is not null)
                {
                    Clipboard.SetImage(bmp);
                    ShowToast("网页快照已复制到剪贴板！📸");
                }
                else
                {
                    ShowToast("快照截图失败");
                }
            }
        };
        _tabCtxQrCode.Click += (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab?.CurrentUri is not null)
            {
                using var qr = new QrCodeCardDialog(tab.CurrentUri.AbsoluteUri, tab.Title);
                qr.ShowDialog(this);
            }
        };
        _tabCtxReaderMode.Click += async (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab is not null)
            {
                var active = await tab.ToggleReaderModeAsync();
                ShowToast(active ? "已开启沉浸式阅读模式 (Esc 退出)" : "已退出沉浸阅读模式");
                RefreshToolbar();
            }
        };
        _tabCtxPictureInPicture.Click += async (_, _) =>
        {
            var tab = ResolveTabByContextIndex();
            if (tab is not null)
            {
                var res = await tab.TogglePictureInPictureAsync();
                ShowToast(res.message);
            }
        };
        _tabCtxSplitView.Click += async (_, _) => await ToggleSplitViewAsync();
        _tabCtxReopenClosed.Click += async (_, _) => await ReopenClosedTabFromTabMenuAsync();
        _tabCtxCloseCurrent.Click += async (_, _) => await CloseTabMenuSelectionAsync(closeCurrent: true, closeOthers: false, closeLeft: false, closeRight: false, closeAll: false);
        _tabCtxCloseOthers.Click += async (_, _) => await CloseTabMenuSelectionAsync(closeCurrent: false, closeOthers: true, closeLeft: false, closeRight: false, closeAll: false);
        _tabCtxCloseAll.Click += async (_, _) => await CloseTabMenuSelectionAsync(closeCurrent: false, closeOthers: false, closeLeft: false, closeRight: false, closeAll: true);
        _tabCtxCloseLeft.Click += async (_, _) => await CloseTabMenuSelectionAsync(closeCurrent: false, closeOthers: false, closeLeft: true, closeRight: false, closeAll: false);
        _tabCtxCloseRight.Click += async (_, _) => await CloseTabMenuSelectionAsync(closeCurrent: false, closeOthers: false, closeLeft: false, closeRight: true, closeAll: false);

        _tabCtxOps.DropDownItems.AddRange(
        [
            _tabCtxOpsCloseOthers,
            _tabCtxOpsCloseLeft,
            _tabCtxOpsCloseRight,
            _tabCtxOpsCloseHost,
            tabCtxOpsFocusHost,
            tabCtxOpsCloseBlank,
            _tabCtxOpsDedupe,
            new ToolStripSeparator(),
            tabCtxOpsSuspendHost,
            tabCtxOpsResumeHost,
            _tabCtxOpsSuspendOthers,
            _tabCtxOpsResumeAll
        ]);
    }

    private void BuildWindowModeContextMenu()
    {
        _windowModeContextMenu.Items.AddRange(
        [
            _windowCtxOpenPrivate,
            _windowCtxOpenIsolated,
            _windowCtxOpenShared
        ]);

        _windowCtxOpenPrivate.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: true, useCurrentProfile: false);
        _windowCtxOpenIsolated.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: false);
        _windowCtxOpenShared.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: true);
    }

    // 中文注释：工具栏“多开窗口”下拉菜单，与标题栏右键菜单同功能，但更易发现（菜单项不能共用于两个宿主，故单独创建）。
    private void BuildMultiWindowMenu()
    {
        _multiWindowButton.DropDownItems.Clear();

        var openIsolated = new ToolStripMenuItem("新开独立双开浏览器")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+M",
            ToolTipText = "全新独立数据目录，Cookie/登录态与当前窗口互不共享，退出后数据保留"
        };
        openIsolated.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: false);
        _multiWindowButton.DropDownItems.Add(openIsolated);

        var openShared = new ToolStripMenuItem("新开共享双开浏览器")
        {
            ToolTipText = "与当前窗口共用同一份数据（同一登录态）"
        };
        openShared.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: true);
        _multiWindowButton.DropDownItems.Add(openShared);

        _multiWindowButton.DropDownItems.Add(new ToolStripSeparator());

        var openPrivate = new ToolStripMenuItem("新开无痕浏览器")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+N",
            ToolTipText = "临时数据目录，关窗自动清除"
        };
        openPrivate.Click += (_, _) => LaunchBrowserProcess(isPrivateWindow: true, useCurrentProfile: false);
        _multiWindowButton.DropDownItems.Add(openPrivate);
    }

    private void BuildAddressContextMenu()
    {
        _addressContextMenu.Items.AddRange(
        [
            _addressCtxCopy,
            _addressCtxPasteGo,
            _addressCtxClear,
            _addressCtxTabSwitch,
            _addressCtxCommandPalette,
            new ToolStripSeparator(),
            _addressCtxSearch
        ]);

        _addressContextMenu.Opening += (_, _) =>
        {
            _addressCtxCopy.Enabled = !string.IsNullOrWhiteSpace(_addressTextBox.Text);
            _addressCtxClear.Enabled = !string.IsNullOrWhiteSpace(_addressTextBox.Text);
            _addressCtxSearch.Enabled = !string.IsNullOrWhiteSpace(_addressTextBox.Text.Trim());
        };
        _addressCtxCopyMarkdown.Click += (_, _) =>
        {
            var tab = _tabManager.ActiveTab;
            if (tab?.CurrentUri is not null)
            {
                Clipboard.SetText($"[{tab.DisplayTitle}]({tab.CurrentUri.AbsoluteUri})");
                ShowToast("已复制 Markdown 链接");
            }
        };
        _addressCtxQrCode.Click += (_, _) => ShowQrCodeForActiveTab();
        _addressCtxCopy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_addressTextBox.Text))
            {
                Clipboard.SetText(_addressTextBox.Text);
            }
        };
        _addressCtxPasteGo.Click += async (_, _) =>
        {
            var pastedText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(pastedText))
            {
                return;
            }

            _addressTextBox.Text = pastedText;
            await NavigateFromAddressBarAsync();
        };
        _addressCtxClear.Click += (_, _) =>
        {
            _addressTextBox.Clear();
            _addressTextBox.Focus();
        };
        _addressCtxTabSwitch.Click += (_, _) => OpenTabQuickSwitchPalette();
        _addressCtxCommandPalette.Click += (_, _) => OpenCommandPalette();
        _addressCtxSearch.Click += async (_, _) =>
        {
            var query = _addressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var encoded = Uri.EscapeDataString(query);
            var url = _config.DefaultSearchEngineUrlTemplate.Replace("{query}", encoded, StringComparison.OrdinalIgnoreCase);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri is null || _tabManager.ActiveTab is null)
            {
                return;
            }

            await _tabManager.ActiveTab.NavigateAsync(uri);
            RefreshToolbar();
        };
    }

    // 中文注释：应用主菜单（汉堡菜单）。把常用功能按「文件 / 查看 / 书签 / 工具 / 帮助」分组，
    // 并在「帮助」中提供「快捷键一览…」入口，集中查看浏览器/插件/地址栏命令的全部快捷键。
    private void BuildAppMenu()
    {
        _appMenu.Items.Clear();

        _appMenu.Items.Add(HeaderItem("文件"));
        _appMenu.Items.Add(MenuItem("新建标签页", "Ctrl+T", async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
            RefreshToolbar();
            RefreshMetrics();
        }));
        _appMenu.Items.Add(MenuItem("新建无痕窗口", "Ctrl+Shift+N", (_, _) =>
            LaunchBrowserProcess(isPrivateWindow: true, useCurrentProfile: false)));
        _appMenu.Items.Add(MenuItem("新建独立双开窗口", "Ctrl+Shift+M", (_, _) =>
            LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: false)));
        _appMenu.Items.Add(new ToolStripSeparator());
        _appMenu.Items.Add(MenuItem("保存当前为工作区", "Ctrl+Shift+S", (_, _) => SaveWorkspaceSnapshot()));
        _appMenu.Items.Add(MenuItem("会话 / 工作区管理", "Ctrl+Shift+O", (_, _) => ShowSessionsWindow(workspaceOnlyMode: true)));
        _appMenu.Items.Add(new ToolStripSeparator());
        _appMenu.Items.Add(MenuItem("退出", "Alt+F4", (_, _) => Close()));

        _appMenu.Items.Add(HeaderItem("查看"));
        _appMenu.Items.Add(MenuItem("后退", "Alt+←", (_, _) =>
        {
            if (_tabManager.ActiveTab?.CanGoBack == true)
            {
                _tabManager.ActiveTab.GoBack();
                RefreshToolbar();
            }
        }));
        _appMenu.Items.Add(MenuItem("前进", "Alt+→", (_, _) =>
        {
            if (_tabManager.ActiveTab?.CanGoForward == true)
            {
                _tabManager.ActiveTab.GoForward();
                RefreshToolbar();
            }
        }));
        _appMenu.Items.Add(MenuItem("刷新", "F5", (_, _) => _tabManager.ActiveTab?.Reload()));
        _appMenu.Items.Add(new ToolStripSeparator());
        _appMenu.Items.Add(MenuItem("聚焦地址栏", "Ctrl+L", (_, _) =>
        {
            _addressTextBox.Focus();
            _addressTextBox.SelectAll();
        }));
        _appMenu.Items.Add(MenuItem("页面放大", "Ctrl++", (_, _) => AdjustActiveTabZoom(1)));
        _appMenu.Items.Add(MenuItem("页面缩小", "Ctrl+-", (_, _) => AdjustActiveTabZoom(-1)));
        _appMenu.Items.Add(MenuItem("页面缩放重置", "Ctrl+0", (_, _) => _tabManager.ActiveTab?.SetZoomFactor(1.0)));
        _appMenu.Items.Add(new ToolStripSeparator());
        _appMenu.Items.Add(MenuItem("开发者工具", "F12", (_, _) => _tabManager.ActiveTab?.OpenDevTools()));
        _appMenu.Items.Add(MenuItem("沉浸（禅）模式", "Ctrl+Shift+Z", (_, _) =>
            ApplyZenMode(!_isZenMode, persist: true, showToast: true)));

        _appMenu.Items.Add(HeaderItem("书签"));
        _appMenu.Items.Add(MenuItem("收藏当前页", "Ctrl+D", (_, _) => AddCurrentPageBookmark()));
        _appMenu.Items.Add(MenuItem("书签管理", string.Empty, (_, _) => ShowBookmarksSettingsWindow()));
        _appMenu.Items.Add(MenuItem("显示 / 隐藏书签栏", "Ctrl+B", (_, _) => ToggleBookmarksBarVisibility()));

        _appMenu.Items.Add(HeaderItem("工具"));
        _appMenu.Items.Add(MenuItem("历史记录", "Ctrl+H", (_, _) => ShowHistoryWindow()));
        _appMenu.Items.Add(MenuItem("下载管理", string.Empty, (_, _) => ShowDownloadManager()));
        _appMenu.Items.Add(MenuItem("插件", string.Empty, (_, _) => _pluginButton.ShowDropDown()));
        _appMenu.Items.Add(MenuItem("设置", string.Empty, (_, _) => ShowSettings()));

        _appMenu.Items.Add(HeaderItem("帮助"));
        var shortcutsItem = MenuItem("快捷键一览…", string.Empty, (_, _) => ShowShortcutsOverview());
        shortcutsItem.Font = AppTheme.CreateFont(9F, FontStyle.Bold);
        _appMenu.Items.Add(shortcutsItem);
        _appMenu.Items.Add(MenuItem("关于 LfsunBrowser", string.Empty, (_, _) => ShowAbout()));

        AppTheme.ApplyContextMenu(_appMenu);
        _appMenuButton.Click += (_, _) =>
        {
            var popupPoint = new Point(_appMenuButton.Bounds.Left, _appMenuButton.Bounds.Bottom + 1);
            _appMenu.Show(_topBar, popupPoint);
        };
    }

    private static ToolStripMenuItem HeaderItem(string text)
    {
        return new ToolStripMenuItem(text)
        {
            Enabled = false,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold)
        };
    }

    private static ToolStripMenuItem MenuItem(string text, string shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            item.ShortcutKeyDisplayString = shortcut;
        }

        item.Click += onClick;
        return item;
    }

    private void RegisterToolbarItems()
    {
        _toolbarItemsById.Clear();
        _toolbarItemsById[ToolbarButtonIds.Back] = _backButton;
        _toolbarItemsById[ToolbarButtonIds.Forward] = _forwardButton;
        _toolbarItemsById[ToolbarButtonIds.Refresh] = _refreshButton;
        _toolbarItemsById[ToolbarButtonIds.NewTab] = _newTabButton;
        _toolbarItemsById[ToolbarButtonIds.CloseTab] = _closeTabButton;
        _toolbarItemsById[ToolbarButtonIds.Downloads] = _downloadButton;
        _toolbarItemsById[ToolbarButtonIds.History] = _historyButton;
        _toolbarItemsById[ToolbarButtonIds.Bookmark] = _addBookmarkButton;
        _toolbarItemsById[ToolbarButtonIds.BookmarkBar] = _toggleBookmarksBarButton;
        _toolbarItemsById[ToolbarButtonIds.Settings] = _settingsButton;
        _toolbarItemsById[ToolbarButtonIds.SearchEngine] = _searchEngineButton;
        _toolbarItemsById[ToolbarButtonIds.Appearance] = _appearanceButton;
        _toolbarItemsById[ToolbarButtonIds.Plugins] = _pluginButton;
        _toolbarItemsById[ToolbarButtonIds.MultiWindow] = _multiWindowButton;
        _toolbarItemsById[ToolbarButtonIds.DevTools] = _devToolsButton;
        _toolbarItemsById[ToolbarButtonIds.SidePanel] = _sidePanelButton;
        _toolbarItemsById[ToolbarButtonIds.SplitView] = _splitViewButton;
        _toolbarItemsById[ToolbarButtonIds.ReaderMode] = _readerModeButton;
        _toolbarItemsById["commandPalette"] = _commandPaletteButton;
        _toolbarItemsById["tabSwitch"] = _tabSwitchButton;
    }

    private void RebuildTitleBarItems()
    {
        _titleBar.SuspendLayout();
        _titleBar.Items.Clear();
        _titleBar.Items.Add(_appTitleLabel);
        _titleBar.Items.Add(new ToolStripSeparator());
        _workspaceButton.Alignment = ToolStripItemAlignment.Left;
        _titleBar.Items.Add(_workspaceButton);
        _verticalTabsToggleButton.Alignment = ToolStripItemAlignment.Left;
        _titleBar.Items.Add(_verticalTabsToggleButton);
        BuildTitleTabItems();
        _titleNewTabButton.Alignment = ToolStripItemAlignment.Left;
        _titleBar.Items.Add(_titleNewTabButton);
        _titlePluginButton.Alignment = ToolStripItemAlignment.Left;
        _titleBar.Items.Add(_titlePluginButton);
        _titleDragSpacer.Alignment = ToolStripItemAlignment.Left;
        _titleBar.Items.Add(_titleDragSpacer);
        _titleWindowCloseButton.Alignment = ToolStripItemAlignment.Right;
        _titleBar.Items.Add(_titleWindowCloseButton);
        _titleWindowMaxButton.Alignment = ToolStripItemAlignment.Right;
        _titleBar.Items.Add(_titleWindowMaxButton);
        _titleWindowMinButton.Alignment = ToolStripItemAlignment.Right;
        _titleBar.Items.Add(_titleWindowMinButton);
        _titleBar.ResumeLayout();
    }

    private void BuildTitleTabItems()
    {
        var tabs = _tabManager.Tabs;
        if (tabs.Count == 0)
        {
            var empty = new ToolStripLabel("无标签")
            {
                ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(4, 1, 4, 1)
            };
            _titleBar.Items.Add(empty);
            return;
        }

        var activeIndex = tabs
            .Select((tab, index) => new { tab, index })
            .FirstOrDefault(item => ReferenceEquals(item.tab, _tabManager.ActiveTab))
            ?.index ?? 0;
        var tabAreaBudget = CalculateTitleTabAreaBudget();
        var tabSlotWidth = AppTheme.IsCompact ? 122 : 142;
        var dynamicVisibleTabs = Math.Clamp(
            tabAreaBudget / Math.Max(1, tabSlotWidth),
            1,
            MaxVisibleTitleTabs);
        var compactTabVisual = dynamicVisibleTabs >= 6 || tabAreaBudget < 560;
        var visibleIndices = BuildVisibleTitleTabIndices(tabs, activeIndex, dynamicVisibleTabs);
        var visibleSet = new HashSet<int>(visibleIndices);
        var hiddenIndices = Enumerable.Range(0, tabs.Count)
            .Where(index => !visibleSet.Contains(index))
            .ToList();
        if (hiddenIndices.Count > 0)
        {
            _titleBar.Items.Add(BuildTitleOverflowDropDownByIndices(
                tabs,
                hiddenIndices,
                $"…{hiddenIndices.Count}",
                $"还有 {hiddenIndices.Count} 个标签未展开"));
        }

        var orderedVisible = visibleIndices
            .OrderByDescending(index => tabs[index].IsPinned)
            .ThenBy(index => index)
            .ToList();
        var hasPinned = orderedVisible.Any(index => tabs[index].IsPinned);
        var insertedPinnedSeparator = false;
        foreach (var i in orderedVisible)
        {
            var tab = tabs[i];
            var isActive = ReferenceEquals(tab, _tabManager.ActiveTab);
            if (!tab.IsPinned && hasPinned && !insertedPinnedSeparator)
            {
                insertedPinnedSeparator = true;
                _titleBar.Items.Add(new ToolStripSeparator());
            }

            var title = string.IsNullOrWhiteSpace(tab.DisplayTitle) ? "新标签页" : tab.DisplayTitle.Trim();
            var maxTitleLength = dynamicVisibleTabs >= 7 ? 10 : dynamicVisibleTabs >= 5 ? 14 : 20;
            var shortTitle = title.Length > maxTitleLength ? $"{title[..maxTitleLength]}..." : title;
            var targetTabId = tab.Id;
            var canQuickClose = !tab.IsPinned;
            var suspendedTag = tab.IsSuspended ? "⏾ " : string.Empty;
            var pinPrefix = tab.IsPinned ? "📌 " : string.Empty;
            var audioTag = tab.IsMuted ? " 🔇" : (tab.IsPlayingAudio ? " 🔊" : string.Empty);
            var closeSuffix = canQuickClose ? " ×" : string.Empty;
            var displayText = $"{pinPrefix}{suspendedTag}{shortTitle}{audioTag}{closeSuffix}";

            var maxTabButtonWidth = Math.Clamp(
                (tabAreaBudget / Math.Max(1, dynamicVisibleTabs)) - 10,
                compactTabVisual ? 100 : 120,
                compactTabVisual ? 190 : 250);
            var siteDetailText = tab.CurrentUri is null
                ? "站点: about:blank"
                : $"站点: {tab.CurrentUri.Host}";
            var stateDetailText = tab.IsPinned
                ? "状态: 固定标签"
                : tab.IsSuspended
                    ? "状态: 休眠标签"
                    : tab.HasCustomTitle
                        ? "状态: 自定义标题"
                        : "状态: 普通标签";

            var icon = FaviconService.GetIcon(tab.CurrentUri?.AbsoluteUri, tab.DisplayTitle);
            var textWidth = TextRenderer.MeasureText(displayText, _titleBar.Font).Width;
            var desiredWidth = Math.Clamp(textWidth + 28 + (canQuickClose ? 16 : 8), compactTabVisual ? 100 : 120, maxTabButtonWidth);

            var tabButton = new ToolStripButton(displayText)
            {
                Image = icon,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageScaling = ToolStripItemImageScaling.None,
                AutoSize = false,
                Width = desiredWidth,
                Height = AppTheme.IsCompact ? 26 : 29,
                Margin = isActive ? new Padding(2, 1, 2, 0) : new Padding(2, 2, 2, 2),
                Padding = new Padding(4, 1, 4, 1),
                BackColor = isActive
                    ? (AppTheme.IsDark ? AppTheme.SurfaceAlt : Color.White)
                    : Color.Transparent,
                ForeColor = isActive
                    ? (AppTheme.IsDark ? Color.FromArgb(252, 255, 255) : Color.FromArgb(15, 23, 42))
                    : (AppTheme.IsDark ? Color.FromArgb(200, 210, 225) : Color.FromArgb(51, 65, 85)),
                ToolTipText = canQuickClose
                    ? $"{(isActive ? "当前标签" : "点击切换标签")} #{i + 1}/{tabs.Count}\n{title}\n{siteDetailText}\n{stateDetailText}\n{(tab.CurrentUri?.AbsoluteUri ?? "about:blank")}\n点击右侧 × 或中键可关闭，Ctrl+点击可复制标签，Shift+点击可只保留当前标签，Ctrl+Shift+PgUp/PgDn 或 Ctrl+Shift+←/→ 可调整顺序"
                    : $"{(isActive ? "当前标签(固定)" : "固定标签")} #{i + 1}/{tabs.Count}\n{title}\n{siteDetailText}\n{stateDetailText}\n{(tab.CurrentUri?.AbsoluteUri ?? "about:blank")}\n固定标签不会在标题栏被误关，Ctrl+点击可复制标签，Shift+点击可只保留当前标签，Ctrl+Shift+PgUp/PgDn 或 Ctrl+Shift+←/→ 可调整顺序",
                Font = AppTheme.CreateFont(AppTheme.IsCompact ? 8.9F : 9.2F, isActive ? FontStyle.Bold : FontStyle.Regular),
                Checked = isActive,
                Tag = isActive ? "title-tab-active" : "title-tab"
            };

            if (!isActive)
            {
                var hoverColor = AppTheme.IsDark
                    ? Color.FromArgb(36, 255, 255, 255)
                    : Color.FromArgb(22, 0, 0, 0);
                tabButton.MouseEnter += (_, _) => ToolStripItemColorAnimator.AnimateBackColor(tabButton, hoverColor, 100);
                tabButton.MouseLeave += (_, _) => ToolStripItemColorAnimator.AnimateBackColor(tabButton, Color.Transparent, 150);
            }
            tabButton.MouseDown += (_, eventArgs) =>
            {
                if (eventArgs.Button != MouseButtons.Left)
                {
                    return;
                }

                _titleTabDragSourceId = targetTabId;
                _titleTabDragStartPoint = eventArgs.Location;
                _titleTabDragging = false;
                _titleTabDragMoved = false;
            };
            tabButton.MouseMove += (_, eventArgs) =>
            {
                if ((eventArgs.Button & MouseButtons.Left) != MouseButtons.Left || _titleTabDragSourceId is null)
                {
                    return;
                }

                if (!_titleTabDragging)
                {
                    var dx = Math.Abs(eventArgs.X - _titleTabDragStartPoint.X);
                    var dy = Math.Abs(eventArgs.Y - _titleTabDragStartPoint.Y);
                    if (dx < 6 && dy < 6)
                    {
                        return;
                    }

                    _titleTabDragging = true;
                }

                if (_titleTabDragSourceId.Value == targetTabId)
                {
                    return;
                }

                // 中文注释：拖拽过程中按“源标签ID -> 当前悬停标签ID”移动，可在标题栏快速调整顺序。
                if (TryMoveTabById(_titleTabDragSourceId.Value, targetTabId))
                {
                    _titleTabDragMoved = true;
                }
            };
            tabButton.DoubleClick += async (_, _) =>
            {
                var tabIndex = _tabManager.Tabs
                    .Select((candidate, index) => new { candidate, index })
                    .FirstOrDefault(item => item.candidate.Id == targetTabId)
                    ?.index ?? -1;
                if (tabIndex < 0)
                {
                    return;
                }

                if (!tab.IsPinned)
                {
                    await CloseTabByIndexAsync(tabIndex);
                }
                else
                {
                    _tabContextIndex = tabIndex;
                    ToggleTabPinByContext();
                }
                ResetTitleTabDragState();
            };
            tabButton.MouseUp += async (_, eventArgs) =>
            {
                var tabIndex = _tabManager.Tabs
                    .Select((candidate, index) => new { candidate, index })
                    .FirstOrDefault(item => item.candidate.Id == targetTabId)
                    ?.index ?? -1;
                if (tabIndex < 0)
                {
                    ResetTitleTabDragState();
                    return;
                }

                if (eventArgs.Button == MouseButtons.Left && _titleTabDragging)
                {
                    ResetTitleTabDragState(logIfMoved: true);
                    return;
                }

                if (eventArgs.Button == MouseButtons.Left)
                {
                    var closeZoneStart = Math.Max(0, tabButton.Width - (AppTheme.IsCompact ? 22 : 24));
                    if (canQuickClose && eventArgs.X >= closeZoneStart)
                    {
                        await CloseTabByIndexAsync(tabIndex);
                        ResetTitleTabDragState();
                        return;
                    }

                    if ((ModifierKeys & Keys.Control) == Keys.Control)
                    {
                        await DuplicateTabByIndexAsync(tabIndex);
                        ResetTitleTabDragState();
                        return;
                    }
                    
                    if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        await CloseOtherTabsByIndexAsync(tabIndex);
                        ResetTitleTabDragState();
                        return;
                    }

                    if (tabIndex >= _tabControl.TabCount)
                    {
                        ResetTitleTabDragState();
                        return;
                    }

                    _tabControl.SelectedIndex = tabIndex;
                    ResetTitleTabDragState();
                    return;
                }

                if (eventArgs.Button != MouseButtons.Middle)
                {
                    if (eventArgs.Button == MouseButtons.Right)
                    {
                        _tabContextIndex = tabIndex;
                        if (tabIndex >= 0 && tabIndex < _tabControl.TabCount)
                        {
                            _tabControl.SelectedIndex = tabIndex;
                        }

                        DismissTransientMenus();
                        _tabContextMenu.Show(Cursor.Position);
                    }

                    ResetTitleTabDragState();
                    return;
                }

                if (!canQuickClose)
                {
                    ResetTitleTabDragState();
                    return;
                }

                await CloseTabByIndexAsync(tabIndex);
                ResetTitleTabDragState();
            };
            _titleBar.Items.Add(tabButton);
        }
    }

    private static List<int> BuildVisibleTitleTabIndices(
        IReadOnlyList<BrowserTab> tabs,
        int activeIndex,
        int maxVisibleCount)
    {
        var count = tabs.Count;
        if (count == 0)
        {
            return [];
        }

        var capacity = Math.Clamp(maxVisibleCount, 1, count);
        var visible = new HashSet<int>();
        var pinnedIndices = tabs
            .Select((tab, index) => new { tab, index })
            .Where(item => item.tab.IsPinned)
            .Select(item => item.index)
            .ToList();
        var pinnedBudget = Math.Min(pinnedIndices.Count, Math.Max(1, capacity / 3));
        foreach (var pinned in pinnedIndices.Take(pinnedBudget))
        {
            visible.Add(pinned);
        }

        visible.Add(Math.Clamp(activeIndex, 0, count - 1));
        for (var step = 1; visible.Count < capacity && (activeIndex - step >= 0 || activeIndex + step < count); step++)
        {
            if (activeIndex - step >= 0)
            {
                visible.Add(activeIndex - step);
            }

            if (visible.Count >= capacity)
            {
                break;
            }

            if (activeIndex + step < count)
            {
                visible.Add(activeIndex + step);
            }
        }

        if (visible.Count < capacity)
        {
            for (var i = 0; i < count && visible.Count < capacity; i++)
            {
                visible.Add(i);
            }
        }

        return visible
            .OrderBy(index => index)
            .ToList();
    }

    private ToolStripDropDownButton BuildTitleOverflowDropDownByIndices(
        IReadOnlyList<BrowserTab> tabs,
        IReadOnlyList<int> indices,
        string glyph,
        string? hiddenTip = null)
    {
        var hiddenCount = indices.Count;
        var buttonWidth = Math.Clamp(TextRenderer.MeasureText(glyph, _titleBar.Font).Width + 16, AppTheme.IsCompact ? 26 : 30, AppTheme.IsCompact ? 52 : 58);
        var dropDown = new ToolStripDropDownButton(glyph)
        {
            AutoSize = false,
            Width = buttonWidth,
            Height = AppTheme.IsCompact ? 23 : 26,
            Margin = new Padding(2, 1, 2, 1),
            BackColor = AppTheme.AccentMuted,
            ForeColor = AppTheme.TextPrimary,
            ShowDropDownArrow = false,
            Font = AppTheme.CreateFont(AppTheme.IsCompact ? 8.8F : 9.2F, FontStyle.Bold),
            ToolTipText = string.IsNullOrWhiteSpace(hiddenTip) ? "展开隐藏标签列表" : $"{hiddenTip}\n点击展开隐藏标签列表"
        };

        if (hiddenCount > 0)
        {
            dropDown.DropDownItems.Add(new ToolStripMenuItem($"隐藏标签 {hiddenCount} 个")
            {
                Enabled = false
            });
            dropDown.DropDownItems.Add(new ToolStripSeparator());
        }

        foreach (var index in indices.OrderByDescending(i => tabs[i].IsPinned).ThenBy(i => i))
        {
            if (index < 0 || index >= tabs.Count)
            {
                continue;
            }

            var tab = tabs[index];
            var title = string.IsNullOrWhiteSpace(tab.DisplayTitle) ? "新标签页" : tab.DisplayTitle.Trim();
            var host = tab.CurrentUri?.Host;
            var display = string.IsNullOrWhiteSpace(host) ? title : $"[{host}] {title}";
            var shortDisplay = display.Length > 48 ? $"{display[..48]}..." : display;
            var marker = ReferenceEquals(tab, _tabManager.ActiveTab)
                ? "◆"
                : tab.IsPinned
                    ? "●"
                    : " ";
            var item = new ToolStripMenuItem($"{marker} #{index + 1} {shortDisplay}")
            {
                ToolTipText = $"{(tab.CurrentUri?.AbsoluteUri ?? "about:blank")}\n{(tab.IsPinned ? "固定标签" : "普通标签")}",
                Checked = ReferenceEquals(tab, _tabManager.ActiveTab)
            };
            item.Click += (_, _) =>
            {
                if (index >= 0 && index < _tabControl.TabCount)
                {
                    _tabControl.SelectedIndex = index;
                }
            };
            dropDown.DropDownItems.Add(item);
        }

        if (dropDown.DropDownItems.Count == 0)
        {
            dropDown.DropDownItems.Add(new ToolStripMenuItem("暂无隐藏标签") { Enabled = false });
        }

        return dropDown;
    }

    private int CalculateTitleTabAreaBudget()
    {
        // 中文注释：为右上角窗口控制按钮预留稳定空间，避免标签过多时把最小化/最大化/关闭挤出可视区。
        var windowButtonsWidth = (_titleWindowMinButton.Width + _titleWindowMaxButton.Width + _titleWindowCloseButton.Width) + 44;
        var brandWidth = TextRenderer.MeasureText(_appTitleLabel.Text, _titleBar.Font).Width + 48;
        var workspaceWidth = _workspaceButton.Width + _titleNewTabButton.Width + _titlePluginButton.Width + 86;
        var reserved = windowButtonsWidth + brandWidth + workspaceWidth;
        var minBudget = AppTheme.IsCompact ? 260 : 300;
        return Math.Max(minBudget, ClientSize.Width - reserved);
    }

    private ToolStripDropDownButton BuildTitleOverflowDropDown(
        IReadOnlyList<BrowserTab> tabs,
        int startIndex,
        int endIndex,
        string glyph,
        string? hiddenTip = null)
    {
        var hiddenCount = Math.Max(0, endIndex - startIndex + 1);
        var buttonWidth = Math.Clamp(TextRenderer.MeasureText(glyph, _titleBar.Font).Width + 16, AppTheme.IsCompact ? 26 : 30, AppTheme.IsCompact ? 46 : 52);
        var dropDown = new ToolStripDropDownButton(glyph)
        {
            AutoSize = false,
            Width = buttonWidth,
            Height = AppTheme.IsCompact ? 23 : 26,
            Margin = new Padding(2, 1, 2, 1),
            BackColor = AppTheme.AccentMuted,
            ForeColor = AppTheme.TextPrimary,
            ShowDropDownArrow = false,
            Font = AppTheme.CreateFont(AppTheme.IsCompact ? 8.8F : 9.2F, FontStyle.Bold),
            ToolTipText = string.IsNullOrWhiteSpace(hiddenTip) ? "展开隐藏标签列表" : $"{hiddenTip}\n点击展开隐藏标签列表"
        };

        if (hiddenCount > 0)
        {
            dropDown.DropDownItems.Add(new ToolStripMenuItem($"隐藏标签 {hiddenCount} 个")
            {
                Enabled = false
            });
            dropDown.DropDownItems.Add(new ToolStripSeparator());
        }

        for (var i = startIndex; i <= endIndex && i < tabs.Count; i++)
        {
            if (i < 0)
            {
                continue;
            }

            var tab = tabs[i];
            var title = string.IsNullOrWhiteSpace(tab.DisplayTitle) ? "新标签页" : tab.DisplayTitle.Trim();
            var host = tab.CurrentUri?.Host;
            var display = string.IsNullOrWhiteSpace(host) ? title : $"[{host}] {title}";
            var shortDisplay = display.Length > 48 ? $"{display[..48]}..." : display;
            var index = i;
            var marker = ReferenceEquals(tab, _tabManager.ActiveTab)
                ? "◆"
                : tab.IsPinned
                    ? "●"
                    : " ";
            var item = new ToolStripMenuItem($"{marker} #{index + 1} {shortDisplay}")
            {
                ToolTipText = $"{(tab.CurrentUri?.AbsoluteUri ?? "about:blank")}\n{(tab.IsPinned ? "固定标签" : "普通标签")}",
                Checked = ReferenceEquals(tab, _tabManager.ActiveTab)
            };
            item.Click += (_, _) =>
            {
                if (index >= 0 && index < _tabControl.TabCount)
                {
                    _tabControl.SelectedIndex = index;
                }
            };
            dropDown.DropDownItems.Add(item);
        }

        if (dropDown.DropDownItems.Count == 0)
        {
            dropDown.DropDownItems.Add(new ToolStripMenuItem("暂无隐藏标签") { Enabled = false });
        }

        return dropDown;
    }

    private void RebuildTopBarItems()
    {
        _topBar.SuspendLayout();
        _topBar.Items.Clear();

        // 1. 左侧核心导航：应用菜单与前进/后退/刷新
        _appMenuButton.Alignment = ToolStripItemAlignment.Left;
        _topBar.Items.Add(_appMenuButton);

        var navIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ToolbarButtonIds.Back,
            ToolbarButtonIds.Forward,
            ToolbarButtonIds.Refresh
        };

        var orderedButtons = _config.ToolbarButtons
            .Where(item => item.Visible)
            .OrderBy(item => item.Order)
            .ToList();

        foreach (var button in orderedButtons.Where(b => navIds.Contains(b.Id)))
        {
            if (_toolbarItemsById.TryGetValue(button.Id, out var item))
            {
                item.Overflow = ToolStripItemOverflow.Never;
                _topBar.Items.Add(item);
            }
        }

        // 2. 中央核心地址栏（紧随导航键，自然伸展占满剩余视域）
        _siteBadgeLabel.Alignment = ToolStripItemAlignment.Left;
        _topBar.Items.Add(_siteBadgeLabel);
        _topBar.Items.Add(_addressTextBox);
        _topBar.Items.Add(_zoomPillButton);
        _topBar.Items.Add(_commandPaletteButton);
        _topBar.Items.Add(_tabSwitchButton);

        // 3. 右侧工具与快捷功能按键集群（统一单分隔线，集群化无冗余分割线）
        AppendTopBarSeparatorIfNeeded();
        var insertedPinnedPlugins = false;

        foreach (var button in orderedButtons.Where(b => !navIds.Contains(b.Id)))
        {
            if (_toolbarItemsById.TryGetValue(button.Id, out var item))
            {
                item.Overflow = ToolStripItemOverflow.AsNeeded;
                _topBar.Items.Add(item);

                if (string.Equals(button.Id, ToolbarButtonIds.Plugins, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var pinnedButton in _pinnedPluginButtons)
                    {
                        pinnedButton.Overflow = ToolStripItemOverflow.AsNeeded;
                        _topBar.Items.Add(pinnedButton);
                    }

                    insertedPinnedPlugins = true;
                }
            }
        }

        if (!insertedPinnedPlugins && _pinnedPluginButtons.Count > 0)
        {
            foreach (var pinnedButton in _pinnedPluginButtons)
            {
                pinnedButton.Overflow = ToolStripItemOverflow.AsNeeded;
                _topBar.Items.Add(pinnedButton);
            }
        }

        _recentTabsButton.Alignment = ToolStripItemAlignment.Left;
        _recentTabsButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _topBar.Items.Add(_recentTabsButton);
        _sessionButton.Alignment = ToolStripItemAlignment.Left;
        _sessionButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _topBar.Items.Add(_sessionButton);
        _tabQuickButton.Alignment = ToolStripItemAlignment.Left;
        _tabQuickButton.Overflow = ToolStripItemOverflow.AsNeeded;
        _topBar.Items.Add(_tabQuickButton);

        _topBar.ResumeLayout();
    }

    private void AppendTopBarSeparatorIfNeeded()
    {
        if (_topBar.Items.Count == 0 || _topBar.Items[^1] is ToolStripSeparator)
        {
            return;
        }

        _topBar.Items.Add(new ToolStripSeparator());
    }

    private static int ResolveToolbarGroup(string? toolbarId)
    {
        return toolbarId?.Trim() switch
        {
            "back" or "forward" or "refresh" => 0,
            "newTab" or "closeTab" or "multiWindow" => 1,
            "downloads" or "history" or "bookmark" or "bookmarkBar" => 2,
            "settings" or "searchEngine" or "appearance" or "plugins" or "devTools" or "sidePanel" or "splitView" or "readerMode" => 3,
            _ => 9
        };
    }

    private void UpdateToastLocation()
    {
        var anchorTop = _topBar.Visible ? _topBar.Bottom + 8 : 14;
        _toastPanel.Location = new Point(
            Math.Max(12, ClientSize.Width - _toastPanel.Width - 18),
            anchorTop);
        SetRoundedRegion(_toastPanel, radius: 16);
        _toastPanel.BringToFront();
        UpdateFindBarLocation();
    }

    private void UpdateFindBarLocation()
    {
        _findBar.Location = new Point(
            Math.Max(12, ClientSize.Width - _findBar.Width - 18),
            _tabControl.Top + 6);
        if (_findBar.Visible)
        {
            _findBar.BringToFront();
        }
    }

    /// <summary>
    /// 中文注释：显示页内查找条并聚焦输入框（Ctrl+F 入口）。
    /// </summary>
    private void ShowFindBar()
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null)
        {
            return;
        }

        _findTabId = tab.Id;
        UpdateFindBarLocation();
        _findBar.ShowAndFocus();
    }

    /// <summary>
    /// 中文注释：关闭查找条并停止当前标签的查找会话。
    /// </summary>
    private void HideFindBar(bool focusPage = false)
    {
        if (!_findBar.Visible)
        {
            return;
        }

        var findTab = _tabManager.Tabs.FirstOrDefault(tab => tab.Id == _findTabId) ?? _tabManager.ActiveTab;
        findTab?.StopFind();
        _findBar.ResetSearch();
        _findBar.Visible = false;
        _findTabId = null;
        if (focusPage)
        {
            _tabManager.ActiveTab?.FocusPage();
        }
    }

    // 中文注释：与 Chromium 一致的缩放档位序列，快捷键按档位逐级调整。
    private static readonly double[] ZoomSteps =
        [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 5.0];

    /// <summary>
    /// 中文注释：把当前标签缩放切到上一档/下一档（direction 为 ±1）。
    /// </summary>
    private void AdjustActiveTabZoom(int direction)
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null)
        {
            return;
        }

        var current = tab.ZoomFactor;
        var index = 0;
        for (var i = 0; i < ZoomSteps.Length; i++)
        {
            if (Math.Abs(ZoomSteps[i] - current) < Math.Abs(ZoomSteps[index] - current))
            {
                index = i;
            }
        }

        var nextIndex = Math.Clamp(index + direction, 0, ZoomSteps.Length - 1);
        tab.SetZoomFactor(ZoomSteps[nextIndex]);
    }

    /// <summary>
    /// 中文注释：缩放变化后持久化域名记忆并刷新状态栏（100% 时移除记忆）。
    /// </summary>
    private void HandleTabZoomChanged(BrowserTab tab, double zoomFactor)
    {
        var host = tab.CurrentUri?.Host;
        if (!string.IsNullOrEmpty(host))
        {
            var rounded = Math.Round(Math.Clamp(zoomFactor, 0.25, 5.0), 2);
            var changed = false;
            if (Math.Abs(rounded - 1.0) < 0.005)
            {
                changed = _config.ZoomOverrides.Remove(host);
            }
            else if (!_config.ZoomOverrides.TryGetValue(host, out var stored) || Math.Abs(stored - rounded) >= 0.005)
            {
                _config.ZoomOverrides[host] = rounded;
                changed = true;
            }

            if (changed)
            {
                SaveConfigSilently();
            }
        }

        if (ReferenceEquals(tab, _tabManager.ActiveTab))
        {
            _statusBarPanel.SetZoomFactor(zoomFactor);
        }
    }

    private void ApplyTheme()
    {
        var compact = AppTheme.IsCompact;
        if (IsHandleCreated)
        {
            WindowBackdrop.Apply(this, AppTheme.IsDark);
        }

        _titleBar.Height = compact ? 33 : 36;
        _findBar.ApplyTheme();
        _sidePanel?.ApplyTheme();
        _splitPanel?.ApplyTheme();
        _verticalTabStrip?.ApplyTheme();
        _titleBar.Padding = compact ? new Padding(8, 2, 8, 2) : new Padding(8, 2, 8, 2);
        _topBar.Height = compact ? 38 : 42;
        _topBar.Padding = compact ? new Padding(8, 3, 8, 3) : new Padding(10, 4, 10, 4);
        _addressBar.Height = compact ? 34 : 38;
        _addressBar.Padding = compact ? new Padding(8, 3, 8, 3) : new Padding(10, 4, 10, 4);
        _bookmarksBar.Height = compact ? 28 : 30;
        _bookmarksBar.Padding = compact ? new Padding(6, 1, 6, 1) : new Padding(8, 1, 8, 1);
        _addressTextBox.Size = new Size(compact ? 480 : 620, compact ? 24 : 28);
        _addressTextBox.Margin = compact ? new Padding(1, 1, 3, 1) : new Padding(1, 2, 4, 2);
        _addressSuggestionListBox.ItemHeight = compact ? 34 : 40;
        _addressSuggestionHintLabel.Height = compact ? 22 : 24;
        _addressSuggestionHintLabel.Padding = compact ? new Padding(8, 0, 8, 0) : new Padding(10, 0, 10, 0);
        _addressSuggestionPanel.Padding = compact ? new Padding(2) : new Padding(3);
        _tabControl.ApplyDensity(compact);

        BackColor = AppTheme.WindowBackground;
        _titleBar.BackColor = AppTheme.Surface;
        _topBar.BackColor = AppTheme.Surface;
        _addressBar.BackColor = AppTheme.SurfaceAlt;
        _bookmarksBar.BackColor = AppTheme.SurfaceAlt;
        _statusBarPanel.ApplyTheme(compact);
        _tabControl.Appearance = TabAppearance.Normal;
        _tabControl.Multiline = false;
        _titleBar.RenderMode = ToolStripRenderMode.Professional;
        _topBar.RenderMode = ToolStripRenderMode.Professional;
        _addressBar.RenderMode = ToolStripRenderMode.Professional;
        _bookmarksBar.RenderMode = ToolStripRenderMode.Professional;
        _titleBar.Renderer = _toolbarRenderer;
        _topBar.Renderer = _toolbarRenderer;
        _addressBar.Renderer = _toolbarRenderer;
        _bookmarksBar.Renderer = _toolbarRenderer;
        _bookmarkContextMenu.Renderer = _toolbarRenderer;
        _tabContextMenu.Renderer = _toolbarRenderer;
        _windowModeContextMenu.Renderer = _toolbarRenderer;
        _addressContextMenu.Renderer = _toolbarRenderer;
        _appMenu.Renderer = _toolbarRenderer;
        ToolStripManager.Renderer = _toolbarRenderer;

        var menuFont = AppTheme.CreateFont(9F);
        _bookmarkContextMenu.Font = menuFont;
        _tabContextMenu.Font = menuFont;
        _windowModeContextMenu.Font = menuFont;
        _addressContextMenu.Font = menuFont;
        _appMenu.Font = menuFont;
        _titleBar.Font = AppTheme.CreateFont(9.2F, FontStyle.Bold);
        _topBar.Font = AppTheme.CreateFont(10F, FontStyle.Bold);
        _addressBar.Font = AppTheme.CreateFont(9.6F);
        _bookmarksBar.Font = AppTheme.CreateFont(9.2F);
        _tabControl.Font = AppTheme.CreateFont(9.2F);

        foreach (var item in _toolbarItemsById.Values)
        {
            item.ForeColor = AppTheme.TextPrimary;
        }
        _hoverBaseColors.Clear();
        // 中文注释：主题切换时取消所有在途的悬停渐变，避免旧主题色被动画写回。
        ToolStripItemColorAnimator.CancelAll();

        foreach (var button in _toolbarItemsById.Values.OfType<ToolStripButton>())
        {
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.AutoSize = false;
            button.Width = compact ? 34 : 38;
            button.Height = compact ? 28 : 32;
            button.Margin = new Padding(3, 1, 3, 1);
            button.ForeColor = AppTheme.TextPrimary;
            button.BackColor = Color.Transparent;
            button.Font = AppTheme.CreateFont(compact ? 10.8F : 11.6F, FontStyle.Bold);
        }

        _appMenuButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _appMenuButton.AutoSize = false;
        _appMenuButton.Width = compact ? 34 : 38;
        _appMenuButton.Height = compact ? 28 : 32;
        _appMenuButton.Margin = new Padding(3, 1, 3, 1);
        _appMenuButton.BackColor = Color.Transparent;
        _appMenuButton.ForeColor = AppTheme.TextPrimary;
        _appMenuButton.Font = AppTheme.CreateFont(compact ? 12F : 13F, FontStyle.Bold);
        _appMenuButton.ToolTipText = "主菜单\n点击打开 文件 / 查看 / 书签 / 工具 / 帮助";

        ConfigureToolbarIconButton(_backButton, AppIconNames.Back, "←", "后退", shortcutText: "Alt+←");
        ConfigureToolbarIconButton(_forwardButton, AppIconNames.Forward, "→", "前进", shortcutText: "Alt+→");
        ConfigureToolbarIconButton(_refreshButton, AppIconNames.Refresh, "↻", "刷新", shortcutText: "F5 / Ctrl+R");
        ConfigureToolbarIconButton(_newTabButton, AppIconNames.NewTab, "+", "新建标签", shortcutText: "Ctrl+T", emphasized: true);
        ConfigureToolbarIconButton(_closeTabButton, AppIconNames.CloseTab, "×", "关闭当前标签", shortcutText: "Ctrl+W");
        ConfigureToolbarIconButton(_downloadButton, AppIconNames.Downloads, "↓", "下载管理");
        ConfigureToolbarIconButton(_historyButton, AppIconNames.History, "⧖", "历史");
        ConfigureToolbarIconButton(_addBookmarkButton, AppIconNames.Bookmark, "★", "收藏当前页");
        ConfigureToolbarIconButton(_settingsButton, AppIconNames.Settings, "⚙", "设置", emphasized: true);
        _pluginButton.AutoSize = false;
        _pluginButton.ToolTipText = "插件\n查看/启停已安装插件，支持重载与管理";
        _pluginButton.ShowDropDownArrow = false;
        _pluginButton.Width = compact ? 30 : 34;
        _pluginButton.Height = compact ? 28 : 32;
        _pluginButton.Margin = new Padding(2, 1, 2, 1);
        _pluginButton.BackColor = Color.Transparent;
        _pluginButton.ForeColor = AppTheme.TextPrimary;
        _pluginButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _pluginButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _pluginButton.Text = "✇";
        _pluginButton.ToolTipText = "插件与扩展\n查看/启停已安装插件，支持重载与管理";
        ConfigureToolbarIconButton(_devToolsButton, AppIconNames.DevTools, "</>", "开发者工具", shortcutText: "F12");
        ConfigureToolbarIconButton(_commandPaletteButton, AppIconNames.CommandPalette, "⌘", "命令面板", shortcutText: "Ctrl+Shift+P");
        ConfigureToolbarIconButton(_tabSwitchButton, string.Empty, "@", "标签快速切换", shortcutText: "Ctrl+K");
        _commandPaletteButton.Width = compact ? 30 : 34;
        _tabSwitchButton.Width = compact ? 30 : 34;
        _commandPaletteButton.Margin = compact ? new Padding(3, 1, 2, 1) : new Padding(3, 1, 2, 1);
        _tabSwitchButton.Margin = compact ? new Padding(2, 1, 4, 1) : new Padding(2, 1, 4, 1);
        _toggleBookmarksBarButton.Width = compact ? 34 : 38;
        _toggleBookmarksBarButton.BackColor = Color.Transparent;
        UpdateBookmarkBarToggleButtonText();

        _addressTextBox.BackColor = AppTheme.InputBackground;
        _addressTextBox.ForeColor = AppTheme.TextPrimary;
        // 中文注释：地址栏用等宽字体，URL 字符对齐更清晰，更贴近开发者工具。
        _addressTextBox.Font = AppTheme.CreateMonoFont(9.6F);

        _searchEngineButton.AutoSize = false;
        _searchEngineButton.Width = compact ? 30 : 34;
        _searchEngineButton.Height = compact ? 28 : 32;
        _searchEngineButton.Margin = new Padding(2, 1, 2, 1);
        _searchEngineButton.BackColor = Color.Transparent;
        _searchEngineButton.ForeColor = AppTheme.TextPrimary;
        _searchEngineButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _searchEngineButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _searchEngineButton.Text = "🔍";

        _appearanceButton.AutoSize = false;
        _appearanceButton.Width = compact ? 30 : 34;
        _appearanceButton.Height = compact ? 28 : 32;
        _appearanceButton.Margin = new Padding(2, 1, 2, 1);
        _appearanceButton.BackColor = Color.Transparent;
        _appearanceButton.ForeColor = AppTheme.TextPrimary;
        _appearanceButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _appearanceButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _appearanceButton.Text = "🎨";

        _multiWindowButton.AutoSize = false;
        _multiWindowButton.Width = compact ? 30 : 34;
        _multiWindowButton.Height = compact ? 28 : 32;
        _multiWindowButton.Margin = new Padding(2, 1, 2, 1);
        _multiWindowButton.BackColor = Color.Transparent;
        _multiWindowButton.ForeColor = AppTheme.TextPrimary;
        _multiWindowButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _multiWindowButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _multiWindowButton.Text = "❐";
        _multiWindowButton.ToolTipText = "多开 / 双开窗口：独立、共享或无痕";

        _recentTabsButton.AutoSize = false;
        _recentTabsButton.ToolTipText = "最近关闭标签\n快捷键: Ctrl+Shift+T";
        _recentTabsButton.ShowDropDownArrow = false;
        _recentTabsButton.Width = compact ? 30 : 34;
        _recentTabsButton.Height = compact ? 28 : 32;
        _recentTabsButton.Margin = new Padding(1, 1, 1, 1);
        _recentTabsButton.BackColor = Color.Transparent;
        _recentTabsButton.ForeColor = AppTheme.TextPrimary;
        _recentTabsButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _recentTabsButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _recentTabsButton.Text = "⟲";

        _sessionButton.AutoSize = false;
        _sessionButton.ToolTipText = "会话中心\n包含恢复、共享打开、无痕打开";
        _sessionButton.ShowDropDownArrow = false;
        _sessionButton.Width = compact ? 30 : 34;
        _sessionButton.Height = compact ? 28 : 32;
        _sessionButton.Margin = new Padding(1, 1, 1, 1);
        _sessionButton.BackColor = Color.Transparent;
        _sessionButton.ForeColor = AppTheme.TextPrimary;
        _sessionButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _sessionButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _sessionButton.Text = "☰";

        _tabQuickButton.AutoSize = false;
        _tabQuickButton.ShowDropDownArrow = false;
        _tabQuickButton.Width = compact ? 30 : 34;
        _tabQuickButton.Height = compact ? 28 : 32;
        _tabQuickButton.Margin = new Padding(1, 1, 1, 1);
        _tabQuickButton.BackColor = Color.Transparent;
        _tabQuickButton.ForeColor = AppTheme.TextPrimary;
        _tabQuickButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _tabQuickButton.Font = AppTheme.CreateFont(compact ? 10F : 11F, FontStyle.Bold);
        _tabQuickButton.Text = "❖";

        _workspaceButton.AutoSize = false;
        _workspaceButton.Width = compact ? 102 : 120;
        _workspaceButton.Height = compact ? 24 : 28;
        _workspaceButton.Margin = compact ? new Padding(5, 1, 6, 1) : new Padding(6, 1, 7, 1);
        _workspaceButton.BackColor = Color.Transparent;
        _workspaceButton.ForeColor = AppTheme.TextPrimary;
        _workspaceButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _workspaceButton.ShowDropDownArrow = false;
        ApplyFluentIcon(_workspaceButton, AppIconNames.Workspace, compact ? 13 : 14, AppTheme.TextPrimary);
        _workspaceButton.Text = ComposeIconButtonText(_workspaceButton, "▦", "工作区");
        _workspaceButton.ToolTipText = "工作区中心：保存、打开、追加、管理";
        _workspaceButton.Font = AppTheme.CreateFont(compact ? 9F : 9.6F, FontStyle.Bold);

        _verticalTabsToggleButton.AutoSize = false;
        _verticalTabsToggleButton.Width = compact ? 30 : 34;
        _verticalTabsToggleButton.Height = compact ? 24 : 26;
        _verticalTabsToggleButton.Margin = compact ? new Padding(3, 1, 3, 1) : new Padding(4, 1, 4, 1);
        _verticalTabsToggleButton.BackColor = _verticalTabStrip?.Visible == true ? (AppTheme.IsDark ? AppTheme.SurfaceAlt : Color.White) : Color.Transparent;
        _verticalTabsToggleButton.ForeColor = AppTheme.TextPrimary;
        _verticalTabsToggleButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _verticalTabsToggleButton.Font = AppTheme.CreateFont(compact ? 9.5F : 10.5F, FontStyle.Bold);
        _verticalTabsToggleButton.Text = "▤";
        _verticalTabsToggleButton.ToolTipText = "切换垂直标签栏 / 水平标签栏 (Ctrl+Shift+V)";

        _titleNewTabButton.AutoSize = false;
        _titleNewTabButton.Width = compact ? 30 : 34;
        _titleNewTabButton.Height = compact ? 24 : 26;
        _titleNewTabButton.Margin = compact ? new Padding(4, 1, 4, 1) : new Padding(5, 1, 5, 1);
        _titleNewTabButton.BackColor = Color.Transparent;
        _titleNewTabButton.ForeColor = AppTheme.TextPrimary;
        _titleNewTabButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _titleNewTabButton.Font = AppTheme.CreateFont(compact ? 11F : 12F, FontStyle.Bold);
        _titleNewTabButton.ToolTipText = "新建空白标签\n快捷键: Ctrl+T";
        ApplyFluentIcon(_titleNewTabButton, AppIconNames.NewTab, compact ? 12 : 13, AppTheme.TextPrimary);
        _titleNewTabButton.Text = ComposeIconButtonText(_titleNewTabButton, "+");

        _titlePluginButton.AutoSize = false;
        _titlePluginButton.Width = compact ? 30 : 34;
        _titlePluginButton.Height = compact ? 24 : 26;
        _titlePluginButton.Margin = compact ? new Padding(3, 1, 3, 1) : new Padding(4, 1, 4, 1);
        _titlePluginButton.BackColor = Color.Transparent;
        _titlePluginButton.ForeColor = AppTheme.TextPrimary;
        _titlePluginButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _titlePluginButton.ShowDropDownArrow = false;
        _titlePluginButton.Font = AppTheme.CreateFont(compact ? 9.5F : 10.5F, FontStyle.Bold);
        _titlePluginButton.ToolTipText = "插件与扩展 (查看/管理/重载)";
        _titlePluginButton.Image = null;
        _titlePluginButton.Text = "✇";

        _titleDragSpacer.Height = compact ? 24 : 26;
        _titleDragSpacer.Margin = compact ? new Padding(8, 1, 8, 1) : new Padding(10, 1, 10, 1);
        _titleDragSpacer.BackColor = Color.Transparent;
        _titleDragSpacer.ForeColor = AppTheme.TextSecondary;
        _titleDragSpacer.ToolTipText = "双击此空白区可最大化/还原";

        _titleWindowMinButton.Tag = "window-min";
        _titleWindowMinButton.AutoSize = false;
        _titleWindowMinButton.Width = compact ? 38 : 46;
        _titleWindowMinButton.Height = _titleBar.Height;
        _titleWindowMinButton.Margin = new Padding(0, 0, 0, 0);
        _titleWindowMinButton.BackColor = Color.Transparent;
        _titleWindowMinButton.ForeColor = AppTheme.TextPrimary;
        _titleWindowMinButton.Font = AppTheme.CreateFont(compact ? 10F : 10.6F, FontStyle.Bold);
        _titleWindowMinButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        ApplyFluentIcon(_titleWindowMinButton, AppIconNames.WindowMinimize, 12, AppTheme.TextPrimary);
        _titleWindowMinButton.Text = ComposeIconButtonText(_titleWindowMinButton, "—");

        _titleWindowMaxButton.Tag = "window-max";
        _titleWindowMaxButton.AutoSize = false;
        _titleWindowMaxButton.Width = compact ? 38 : 46;
        _titleWindowMaxButton.Height = _titleBar.Height;
        _titleWindowMaxButton.Margin = new Padding(0, 0, 0, 0);
        _titleWindowMaxButton.BackColor = Color.Transparent;
        _titleWindowMaxButton.ForeColor = AppTheme.TextPrimary;
        _titleWindowMaxButton.Font = AppTheme.CreateFont(compact ? 9.4F : 9.8F, FontStyle.Bold);
        _titleWindowMaxButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        ApplyFluentIcon(
            _titleWindowMaxButton,
            WindowState == FormWindowState.Maximized ? AppIconNames.WindowRestore : AppIconNames.WindowMaximize,
            12,
            AppTheme.TextPrimary);
        _titleWindowMaxButton.Text = ComposeIconButtonText(_titleWindowMaxButton, WindowState == FormWindowState.Maximized ? "❐" : "□");

        _titleWindowCloseButton.Tag = "window-close";
        _titleWindowCloseButton.AutoSize = false;
        _titleWindowCloseButton.Width = compact ? 38 : 46;
        _titleWindowCloseButton.Height = _titleBar.Height;
        _titleWindowCloseButton.Margin = new Padding(0, 0, 0, 0);
        _titleWindowCloseButton.BackColor = Color.Transparent;
        _titleWindowCloseButton.ForeColor = AppTheme.TextPrimary;
        _titleWindowCloseButton.Font = AppTheme.CreateFont(compact ? 10F : 10.6F, FontStyle.Bold);
        _titleWindowCloseButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        ApplyFluentIcon(_titleWindowCloseButton, AppIconNames.WindowClose, 12, AppTheme.TextPrimary);
        _titleWindowCloseButton.Text = ComposeIconButtonText(_titleWindowCloseButton, "×");

        _appTitleLabel.Font = AppTheme.CreateFont(9.8F, FontStyle.Bold);
        // 中文注释：无痕窗口在标题栏显示徽标并用紫色系区分。
        if (_launchOptions.IsPrivateWindow)
        {
            _appTitleLabel.ForeColor = AppTheme.IsDark ? Color.FromArgb(196, 160, 255) : Color.FromArgb(112, 64, 189);
            _appTitleLabel.Text = "Lfsun浏览器 · 无痕";
        }
        else
        {
            _appTitleLabel.ForeColor = AppTheme.TextSecondary;
            _appTitleLabel.Text = "Lfsun浏览器";
        }

        _appTitleLabel.Margin = compact ? new Padding(4, 1, 6, 1) : new Padding(6, 1, 8, 1);
        _titleTabStatsLabel.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        _titleTabStatsLabel.BackColor = AppTheme.AccentMuted;
        _titleTabStatsLabel.ForeColor = AppTheme.TextPrimary;
        _titleTabStatsLabel.Padding = compact ? new Padding(6, 2, 6, 2) : new Padding(7, 2, 7, 2);
        _titleTabStatsLabel.Margin = compact ? new Padding(0, 1, 4, 1) : new Padding(0, 1, 5, 1);
        _titleTabStatsLabel.IsLink = true;
        _titleMemoryStatsLabel.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        _titleMemoryStatsLabel.BackColor = AppTheme.AccentMuted;
        _titleMemoryStatsLabel.ForeColor = AppTheme.TextPrimary;
        _titleMemoryStatsLabel.Padding = compact ? new Padding(6, 2, 6, 2) : new Padding(7, 2, 7, 2);
        _titleMemoryStatsLabel.Margin = compact ? new Padding(0, 1, 4, 1) : new Padding(0, 1, 5, 1);
        _titleMemoryStatsLabel.IsLink = true;

        _siteBadgeLabel.Visible = true;
        _siteBadgeLabel.BackColor = AppTheme.IsDark ? Color.FromArgb(62, 79, 102) : Color.FromArgb(219, 234, 253);
        _siteBadgeLabel.ForeColor = AppTheme.TextPrimary;
        _siteBadgeLabel.Font = AppTheme.CreateFont(8.6F, FontStyle.Bold);
        _siteBadgeLabel.Width = compact ? 32 : 34;
        _siteBadgeLabel.Margin = compact ? new Padding(3, 1, 1, 1) : new Padding(4, 2, 1, 2);
        _siteBadgeLabel.Padding = compact ? new Padding(4, 1, 4, 1) : new Padding(6, 2, 6, 2);

        _zoomPillButton.Font = AppTheme.CreateFont(compact ? 8.4F : 8.8F, FontStyle.Bold);
        _zoomPillButton.BackColor = AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, AppTheme.IsDark ? 0.30F : 0.18F);
        _zoomPillButton.ForeColor = AppTheme.Accent;
        _zoomPillButton.Margin = compact ? new Padding(2, 1, 2, 1) : new Padding(3, 1, 3, 1);
        _zoomPillButton.Padding = compact ? new Padding(5, 1, 5, 1) : new Padding(6, 2, 6, 2);
        UpdateTabQuickButtonText();
        UpdateWorkspaceButtonBadge();

        _addressSuggestionPanel.BackColor = AppTheme.PanelBorder;
        _addressSuggestionListBox.BackColor = AppTheme.InputBackground;
        _addressSuggestionListBox.ForeColor = AppTheme.TextPrimary;
        _addressSuggestionListBox.Font = AppTheme.CreateFont(9F);
        ApplyAddressSuggestionHintVisual(AddressSuggestionHintMode.Default);
        _addressSuggestionHintLabel.Font = AppTheme.CreateFont(8.4F, FontStyle.Bold);

        _toastPanel.BackColor = AppTheme.Accent;
        _toastLabel.ForeColor = Color.White;
        _toastLabel.Font = AppTheme.CreateFont(9.5F, FontStyle.Bold);
        _toastLabel.BackColor = Color.Transparent;
        _toastPanel.Invalidate();

        AppTheme.ApplyContextMenu(_bookmarkContextMenu);
        AppTheme.ApplyContextMenu(_tabContextMenu);
        AppTheme.ApplyContextMenu(_windowModeContextMenu);
        AppTheme.ApplyContextMenu(_addressContextMenu);
        SetRoundedRegion(_toastPanel, radius: 16);
        SetRoundedRegion(_addressSuggestionPanel, radius: 12);
        UpdateAddressCommandTooltip();
        UpdateModeLabelText();
        RequestTitleBarRebuild();
        UpdateResponsiveTopBarVisuals();
        _titleBar.PerformLayout();
        _topBar.PerformLayout();
        _addressBar.PerformLayout();
        UpdateToastLocation();
        UpdateAddressSuggestionLocation();
        UpdateSiteBadge();
        UpdateTitleStatusLabels();
        UpdateResponsiveStatusBarVisuals();
    }

    private void WireEvents()
    {
        KeyDown += async (_, eventArgs) =>
        {
            if (!await HandleShortcutAsync(eventArgs))
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        };

        _bookmarksBar.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            DismissTransientMenus();
            ShowBookmarkContextMenu(nodeId: null, parentFolderId: null, Cursor.Position);
        };
        _addressTextBox.TextBox.ContextMenuStrip = _addressContextMenu;
        _tabQuickButton.DropDownOpening += (_, _) => BuildTabQuickDropDown();
        _recentTabsButton.DropDownOpening += (_, _) => BuildRecentTabsDropDown();
        _sessionButton.DropDownOpening += (_, _) => BuildSessionDropDown();
        _workspaceButton.DropDownOpening += (_, _) => BuildWorkspaceDropDown();
        _workspaceButton.DropDownClosed += (_, _) =>
        {
            if (_pendingTitleBarRebuild)
            {
                _pendingTitleBarRebuild = false;
                RebuildTitleBarItems();
            }
        };
        _workspaceButton.MouseEnter += (_, _) =>
        {
            if (!_workspaceButton.DropDown.Visible)
            {
                _workspaceButton.ShowDropDown();
            }
        };
        _titleBar.MouseWheel += (_, eventArgs) =>
        {
            if (TryCycleTabSelection(forward: eventArgs.Delta < 0))
            {
                RefreshToolbar();
            }
        };
        _titleBar.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            TryShowTitleBarQuickMenu(eventArgs);
        };
        _titleBar.MouseDown += (_, eventArgs) => BeginWindowDragFromTitleArea(eventArgs);
        _appTitleLabel.MouseDown += (_, eventArgs) => BeginWindowDragFromTitleArea(eventArgs);
        _titleTabStatsLabel.Click += (_, _) => ShowTabStatsDialog();
        _titleMemoryStatsLabel.Click += (_, _) => ShowTabStatsDialog();
        _appTitleLabel.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            ShowBrandQuickMenu(eventArgs.Location);
        };
        _appTitleLabel.DoubleClick += (_, _) =>
        {
            ShowSessionsWindow(workspaceOnlyMode: true);
            Log("已打开工作区中心");
        };
        _titleDragSpacer.MouseDown += (_, eventArgs) => BeginWindowDragFromTitleArea(eventArgs);
        _titleBar.MouseDoubleClick += (_, eventArgs) => HandleTitleStripDoubleClick(_titleBar, eventArgs);
        _titleDragSpacer.DoubleClick += (_, _) => ToggleWindowMaximizeState();
        _topBar.MouseDoubleClick += (_, eventArgs) => HandleTitleStripDoubleClick(_topBar, eventArgs);
        _siteBadgeLabel.Click += (_, _) =>
        {
            var activeTab = _tabManager.ActiveTab;
            var uri = activeTab?.CurrentUri;
            if (uri is null)
            {
                ShowToast("当前标签暂无站点信息");
                return;
            }

            using var siteCard = new SiteInfoCardDialog(
                uri,
                activeTab?.AdBlockedCount ?? 0,
                activeTab?.LastLoadMilliseconds ?? -1,
                () => _ = ClearCookiesAndDataForCurrentHostAsync(),
                () => _ = HardRefreshActiveTabAsync(),
                () => _ = ViewActiveTabSourceAsync(),
                () => ShowQrCodeForActiveTab());
            siteCard.ShowDialog(this);
        };
        _siteBadgeLabel.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            ShowSiteBadgeContextMenu(eventArgs.Location);
        };
        WireToolbarHoverEffects();
        WireStatusBarActions();

        Resize += (_, _) =>
        {
            UpdateMaximizedBoundsForCurrentScreen();
            HandleWindowStateTransition();
            UpdateResponsiveTopBarVisuals();
            UpdateResponsiveStatusBarVisuals();
            _titleBar.PerformLayout();
            _topBar.PerformLayout();
            _addressBar.PerformLayout();
            UpdateToastLocation();
            UpdateAddressSuggestionLocation();
        };
        Move += (_, _) => UpdateMaximizedBoundsForCurrentScreen();
        // 中文注释：监听系统主题变化，在“跟随系统”模式下自动切换深浅主题。
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemUserPreferenceChanged;
        FormClosing += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemUserPreferenceChanged;
            _isClosingCleanly = true;
            _ambientMotionTimer.Stop();
            _windowTransitionTimer.Stop();
            Opacity = 1D;
            PersistSessionState();
        };

        Shown += async (_, _) =>
        {
            try
            {
                UpdateMaximizedBoundsForCurrentScreen();
                WindowState = FormWindowState.Normal;
                Activate();
                _bookmarkManager.Reload();
                RebuildBookmarksBar();
                _pluginManager.Reload();
                ApplyRuntimeConfig();
                await OpenStartupTabsAsync();
                RefreshToolbar();
                RefreshMetrics();
                _ = LoadPublicIpInfoAsync(); // 异步后台并发执行，绝不阻塞界面启动与标签渲染
            }
            catch (Exception ex)
            {
                Log($"启动流程异常: {ex.Message}");
            }
            finally
            {
                _startupCompleted = true;
            }
        };

        RegisterGlobalMenuDismissHandlers();

        _tabControl.SelectedIndexChanged += async (_, _) =>
        {
            await _tabManager.ActivateSelectedTabAsync();
            RefreshToolbar();
            RefreshMetrics();
        };
        _tabControl.TabCloseRequested += async tabIndex => await CloseTabByIndexAsync(tabIndex);
        _tabControl.TabOrderChangeRequested += (fromIndex, toIndex) =>
        {
            _tabManager.MoveTab(fromIndex, toIndex);
            RefreshToolbar();
        };
        _tabControl.EmptyAreaDoubleClicked += async () =>
        {
            await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
            RefreshToolbar();
            RefreshMetrics();
        };
        _tabManager.TabAudioStateChanged += _ =>
        {
            _tabControl.Invalidate();
            _verticalTabStrip?.RebuildTabs();
            RefreshToolbar();
        };

        _tabManager.ActiveTabChanged += tab =>
        {
            if (_findBar.Visible && tab?.Id != _findTabId)
            {
                HideFindBar();
            }

            _suppressAddressSuggestionRefresh = true;
            if (tab?.CurrentUri is not null)
            {
                _addressTextBox.Text = tab.CurrentUri.AbsoluteUri;
            }
            else
            {
                _addressTextBox.Text = string.Empty;
            }
            _suppressAddressSuggestionRefresh = false;
            HideAddressSuggestionPanel();

            RefreshToolbar();
            UpdateWindowCaption();
        _sidePanel?.RefreshActiveContent();
            PersistSessionState();
            _statusBarPanel.SetZoomFactor(tab?.ZoomFactor ?? 1.0);
            _statusBarPanel.SetAdBlockedCount(_config.AdBlockEnabled, tab?.AdBlockedCount ?? 0);
            _statusBarPanel.SetLoadTime(tab?.LastLoadMilliseconds ?? -1);
        };

        _findBar.SearchRequested += async (term, matchCase) =>
        {
            var tab = _tabManager.ActiveTab;
            if (tab is null)
            {
                return;
            }

            if (string.IsNullOrEmpty(term))
            {
                tab.StopFind();
                _findBar.UpdateMatches(0, 0);
                return;
            }

            await tab.StartFindAsync(term, matchCase);
        };
        _findBar.NextRequested += () => _tabManager.ActiveTab?.FindNext();
        _findBar.PreviousRequested += () => _tabManager.ActiveTab?.FindPrevious();
        _findBar.CloseRequested += () => HideFindBar(focusPage: true);
        _tabManager.TabFindMatchesChanged += (tab, activeIndex, matchCount) =>
        {
            if (_findBar.Visible && ReferenceEquals(tab, _tabManager.ActiveTab))
            {
                _findBar.UpdateMatches(activeIndex, matchCount);
            }
        };
        _tabManager.TabZoomChanged += HandleTabZoomChanged;
        _tabManager.TabAdBlockedCountChanged += (tab, blockedCount) =>
        {
            // 中文注释：仅活动标签刷新状态栏拦截计数。
            if (ReferenceEquals(tab, _tabManager.ActiveTab))
            {
                _statusBarPanel.SetAdBlockedCount(_config.AdBlockEnabled, blockedCount);
            }
        };

        _tabManager.TabsChanged += () =>
        {
            RefreshMetrics();
            PersistSessionState();
            UpdateAddressSuggestions();
            // 中文注释：标签状态变化（含导航完成）时刷新活动标签的耗时芯片。
            _statusBarPanel.SetLoadTime(_tabManager.ActiveTab?.LastLoadMilliseconds ?? -1);
        };
        _tabManager.PageVisited += (uri, title) =>
        {
            // 中文注释：无痕窗口不写入全局历史记录。
            if (!_launchOptions.IsPrivateWindow)
            {
                _historyManager.AddEntry(uri, title);
                if (_historyWindow is not null && !_historyWindow.IsDisposed)
                {
                    _historyWindow.RefreshData();
                }
            }

            UpdateWindowCaption();
        _sidePanel?.RefreshActiveContent();
            UpdateAddressSuggestions();
            PersistSessionState();
        };
        _tabManager.TabSurfaceInteracted += DismissTransientMenus;
        _tabManager.InternalUrlNavigated += (tab, url) =>
        {
            var html = InternalUrlRouter.BuildInternalRedirectHtml(url);
            _ = tab.NavigateToStringAsync(html);
        };
        _tabManager.InternalUrlActionRequested += (_, action) =>
        {
            switch (action)
            {
                case "appearance":
                    ShowSettings(1);
                    break;
                case "bookmarks":
                    ShowBookmarksSettingsWindow();
                    break;
                case "history":
                    ShowHistoryWindow();
                    break;
                case "settings":
                    ShowSettings(0);
                    break;
            }
        };

        FaviconService.FaviconDownloaded += (host, img, _) =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(() =>
                {
                    foreach (ToolStripItem item in _bookmarksBar.Items)
                    {
                        if (item.Tag is BookmarkBarTag tag)
                        {
                            var node = _bookmarkManager.FindNode(tag.NodeId);
                            if (node?.IsLink == true && Uri.TryCreate(node.Url, UriKind.Absolute, out var uri) &&
                                string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
                            {
                                item.Image = img;
                            }
                        }
                    }

                    RequestTitleBarRebuild();
                    _verticalTabStrip?.Invalidate();
                });
            }
            catch
            {
            }
        };

        _backButton.Click += (_, _) =>
        {
            _tabManager.ActiveTab?.GoBack();
            RefreshToolbar();
        };

        _forwardButton.Click += (_, _) =>
        {
            _tabManager.ActiveTab?.GoForward();
            RefreshToolbar();
        };

        _refreshButton.MouseUp += async (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            if (eventArgs.Clicks >= 2)
            {
                _refreshClickTimer.Stop();
                await HardRefreshActiveTabAsync();
                return;
            }

            _refreshClickTimer.Stop();
            _refreshClickTimer.Start();
        };
        _downloadButton.Click += (_, _) => ShowDownloadManager();
        _historyButton.Click += (_, _) => ShowHistoryWindow();
        _addBookmarkButton.Click += (_, _) => AddCurrentPageBookmark();
        _toggleBookmarksBarButton.Click += (_, _) => ToggleBookmarksBarVisibility();
        _settingsButton.Click += (_, _) => ShowSettings();
        _commandPaletteButton.Click += (_, _) => OpenCommandPalette();
        _tabSwitchButton.Click += (_, _) => OpenTabQuickSwitchPalette();

        _newTabButton.Click += async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
            RefreshToolbar();
            RefreshMetrics();
        };
        _titleNewTabButton.Click += async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri("about:blank"));
            RefreshToolbar();
            RefreshMetrics();
        };
        _titleWindowMinButton.Click += (_, _) => BeginMinimizeTransition();
        _titleWindowMaxButton.Click += (_, _) => ToggleWindowMaximizeState();
        _titleWindowCloseButton.Click += (_, _) => Close();

        _closeTabButton.Click += async (_, _) =>
        {
            await _tabManager.CloseActiveTabAsync();
            RefreshToolbar();
            RefreshMetrics();
        };

        _pluginButton.DropDownOpening += (_, _) => BuildPluginDropDown(_pluginButton);
        _titlePluginButton.DropDownOpening += (_, _) => BuildPluginDropDown(_titlePluginButton);
        _devToolsButton.Click += (_, _) => _tabManager.ActiveTab?.OpenDevTools();

        _addressTextBox.KeyDown += async (_, eventArgs) =>
        {
            if (_config.EnableAddressBarSuggestions && HandleAddressSuggestionKeyDown(eventArgs))
            {
                return;
            }

            if (eventArgs.Control && eventArgs.KeyCode == Keys.Enter)
            {
                var expandedUrl = BuildCtrlEnterAddress(_addressTextBox.Text);
                if (!string.IsNullOrWhiteSpace(expandedUrl))
                {
                    // 中文注释：模拟主流浏览器 Ctrl+Enter 行为，快速补全为 https://www.xxx.com 并立即访问。
                    _addressTextBox.Text = expandedUrl;
                    eventArgs.SuppressKeyPress = true;
                    eventArgs.Handled = true;
                    await NavigateFromAddressBarAsync();
                    return;
                }
            }

            if (eventArgs.KeyCode == Keys.Escape)
            {
                eventArgs.SuppressKeyPress = true;
                eventArgs.Handled = true;
                RestoreAddressBarTextFromActiveTab();
                HideAddressSuggestionPanel();
                return;
            }

            if (eventArgs.KeyCode != Keys.Enter)
            {
                return;
            }

            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            await NavigateFromAddressBarAsync();
        };
        _addressTextBox.TextChanged += (_, _) =>
        {
            if (_suppressAddressSuggestionRefresh)
            {
                return;
            }

            ScheduleAddressSuggestionRefresh(immediate: false);
        };
        _addressTextBox.Enter += (_, _) => ScheduleAddressSuggestionRefresh(immediate: true);
        _addressTextBox.TextBox.Enter += (_, _) =>
        {
            _addressTextBoxFocused = true;
            Invalidate();
        };
        _addressTextBox.Leave += (_, _) =>
        {
            _addressSuggestionRefreshTimer.Stop();
            BeginInvoke((Action)(() =>
            {
                if (!_addressSuggestionListBox.Focused)
                {
                    HideAddressSuggestionPanel();
                }
            }));
        };
        _addressTextBox.TextBox.Leave += (_, _) =>
        {
            _addressTextBoxFocused = false;
            Invalidate();
        };
        _addressSuggestionListBox.DrawItem += DrawAddressSuggestionItem;
        _addressSuggestionListBox.MouseDown += async (_, eventArgs) =>
        {
            var index = _addressSuggestionListBox.IndexFromPoint(eventArgs.Location);
            if (index < 0 || index >= _addressSuggestionListBox.Items.Count)
            {
                return;
            }

            _addressSuggestionListBox.SelectedIndex = index;
            if (_addressSuggestionListBox.Items[index] is AddressSuggestionEntry suggestion && !suggestion.IsHeader)
            {
                await AcceptAddressSuggestionAsync(suggestion, navigateImmediately: true);
            }
        };
        _addressSuggestionListBox.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Enter)
            {
                eventArgs.SuppressKeyPress = true;
                eventArgs.Handled = true;
                if (_addressSuggestionListBox.SelectedItem is AddressSuggestionEntry suggestion && !suggestion.IsHeader)
                {
                    await AcceptAddressSuggestionAsync(suggestion, navigateImmediately: true);
                }

                return;
            }

            if (eventArgs.KeyCode == Keys.Escape)
            {
                eventArgs.SuppressKeyPress = true;
                eventArgs.Handled = true;
                HideAddressSuggestionPanel();
                _addressTextBox.Focus();
                return;
            }

            if (eventArgs.KeyCode == Keys.Tab &&
                _addressSuggestionListBox.SelectedItem is AddressSuggestionEntry tabEntry &&
                !tabEntry.IsHeader)
            {
                eventArgs.SuppressKeyPress = true;
                eventArgs.Handled = true;
                await AcceptAddressSuggestionAsync(tabEntry, navigateImmediately: false);
            }
        };

        _tabControl.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            _tabContextIndex = _tabControl.GetTabIndexAt(eventArgs.Location);
            if (_tabContextIndex < 0)
            {
                ShowTabStripEmptyAreaMenu(eventArgs.Location);
                return;
            }

            _tabControl.SelectedIndex = _tabContextIndex;
            DismissTransientMenus();
            _tabContextMenu.Show(_tabControl, eventArgs.Location);
        };

        _refreshClickTimer.Interval = 220;
        _refreshClickTimer.Tick += (_, _) =>
        {
            _refreshClickTimer.Stop();
            _tabManager.ActiveTab?.Reload();
        };

        _toastTimer.Interval = ToastTickMs;
        _toastTimer.Tick += (_, _) =>
        {
            if (!_toastPanel.Visible)
            {
                _toastTimer.Stop();
                return;
            }

            _toastRemainingMs -= ToastTickMs;
            if (_toastRemainingMs <= 0)
            {
                _toastTimer.Stop();
                _toastRemainingMs = 0;
                _toastPanel.Visible = false;
                return;
            }

            _toastPanel.Invalidate();
        };

        _addressSuggestionRefreshTimer.Interval = 120;
        _addressSuggestionRefreshTimer.Tick += (_, _) =>
        {
            _addressSuggestionRefreshTimer.Stop();
            RefreshAddressSuggestionPanel();
        };

        _maintenanceTimer.Interval = 30_000;
        _maintenanceTimer.Tick += async (_, _) =>
        {
            await _tabManager.SuspendIdleTabsAsync();
            RefreshMetrics();
        };
        _maintenanceTimer.Start();

        _ambientMotionTimer.Interval = 120;
        _ambientMotionTimer.Tick += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            _ambientMotionPhase += 0.045F;
            if (_ambientMotionPhase > MathF.Tau)
            {
                _ambientMotionPhase -= MathF.Tau;
            }

            Invalidate();
        };
        _ambientMotionTimer.Start();

        _windowTransitionTimer.Interval = WindowTransitionTickMs;
        _windowTransitionTimer.Tick += (_, _) => OnWindowTransitionTick();
    }

    private void ApplyRuntimeConfig()
    {
        AppTheme.Configure(_config.UiTheme, _config.UiDensity, _config.UiAccent, _config.UiFont, _config.UiAccentStrength);
        ApplyWindowIconFromConfig();
        ApplyTheme();
        _bookmarksBar.Visible = _config.ShowBookmarksBar;
        UpdateBookmarkBarToggleButtonText();
        RebuildTopBarItems();
        BuildSearchEngineMenu();
        BuildAppearanceMenu();
        RebuildBookmarksBar();
        ConfigureAddressBarSuggestions();
        UpdateAddressSuggestions();
        ApplyZenMode(_config.ZenMode, persist: false, showToast: false);
        // 中文注释：设置保存后同步状态栏拦截计数显示（开关可能变化）。
        _statusBarPanel.SetAdBlockedCount(_config.AdBlockEnabled, _tabManager.ActiveTab?.AdBlockedCount ?? 0);
        RefreshToolbar();
        RefreshMetrics();
    }

    private async Task OpenStartupTabsAsync()
    {
        var startupPageUri = new Uri(_config.StartPage);
        if (await TryApplyStartupLaunchPayloadAsync())
        {
            Log("已打开启动会话");
            return;
        }

        if (await TryRestoreSessionAsync())
        {
            Log("已恢复上次会话");
            return;
        }

        await _tabManager.CreateTabAsync(startupPageUri);
        PersistSessionState();
    }

    private async Task<bool> TryApplyStartupLaunchPayloadAsync()
    {
        if (string.IsNullOrWhiteSpace(_launchOptions.RestoreSessionPayloadPath))
        {
            return false;
        }

        var session = _launchPayloadStore.LoadAndDelete(_launchOptions.RestoreSessionPayloadPath);
        if (session is null || session.Tabs.Count == 0)
        {
            return false;
        }

        _isRestoringSession = true;
        try
        {
            return await ApplySessionToCurrentWindowAsync(session, replaceCurrentTabs: false);
        }
        finally
        {
            _isRestoringSession = false;
            UpdateAddressSuggestions();
            PersistSessionState();
        }
    }

    private async Task<bool> TryRestoreSessionAsync()
    {
        if (_launchOptions.IsPrivateWindow)
        {
            return false;
        }

        var session = _sessionStore.Load();
        if (session is null || session.Tabs.Count == 0)
        {
            return false;
        }

        if (!_config.RestoreLastSessionOnStartup)
        {
            return false;
        }

        _isRestoringSession = true;
        try
        {
            return await ApplySessionToCurrentWindowAsync(session, replaceCurrentTabs: false);
        }
        finally
        {
            _isRestoringSession = false;
            UpdateAddressSuggestions();
            PersistSessionState();
        }
    }

    private async Task<bool> ApplySessionToCurrentWindowAsync(BrowserSessionState session, bool replaceCurrentTabs)
    {
        if (session.Tabs.Count == 0)
        {
            return false;
        }

        var validTabs = session.Tabs
            .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
            .ToArray();
        if (validTabs.Length == 0)
        {
            return false;
        }

        if (replaceCurrentTabs)
        {
            var keeper = _tabManager.ActiveTab ?? _tabManager.Tabs.FirstOrDefault();
            foreach (var tab in _tabManager.Tabs.Where(tab => !ReferenceEquals(tab, keeper)).ToArray())
            {
                await _tabManager.CloseTabAsync(tab);
            }

            keeper ??= _tabManager.ActiveTab;
            if (keeper is null)
            {
                keeper = await _tabManager.CreateTabAsync(new Uri(validTabs[0].Url));
            }
            else
            {
                await keeper.NavigateAsync(new Uri(validTabs[0].Url));
            }

            _tabManager.SetPinned(keeper, validTabs[0].IsPinned);
            _tabManager.SetColorTag(keeper, validTabs[0].TabColorKey);
            keeper.SetCustomTitle(validTabs[0].CustomTitle);

            foreach (var savedTab in validTabs.Skip(1))
            {
                var created = await _tabManager.CreateTabAsync(new Uri(savedTab.Url));
                if (savedTab.IsPinned)
                {
                    _tabManager.SetPinned(created, true);
                }

                _tabManager.SetColorTag(created, savedTab.TabColorKey);
                created.SetCustomTitle(savedTab.CustomTitle);
            }
        }
        else
        {
            foreach (var savedTab in validTabs)
            {
                var created = await _tabManager.CreateTabAsync(new Uri(savedTab.Url));
                if (savedTab.IsPinned)
                {
                    _tabManager.SetPinned(created, true);
                }

                _tabManager.SetColorTag(created, savedTab.TabColorKey);
                created.SetCustomTitle(savedTab.CustomTitle);
            }
        }

        if (_tabControl.TabCount == 0)
        {
            return false;
        }

        var selectedIndex = Math.Clamp(session.ActiveIndex, 0, Math.Min(validTabs.Length, _tabControl.TabCount) - 1);
        _tabControl.SelectedIndex = selectedIndex;
        await _tabManager.ActivateSelectedTabAsync();
        return true;
    }

    private void ConfigureAddressBarSuggestions()
    {
        var textBox = _addressTextBox.TextBox;
        textBox.AutoCompleteCustomSource = _addressSuggestions;
        if (_config.EnableAddressBarSuggestions)
        {
            textBox.AutoCompleteMode = AutoCompleteMode.None;
            textBox.AutoCompleteSource = AutoCompleteSource.None;
            return;
        }

        textBox.AutoCompleteMode = AutoCompleteMode.None;
        textBox.AutoCompleteSource = AutoCompleteSource.None;
        _addressSuggestionRefreshTimer.Stop();
        HideAddressSuggestionPanel();
    }

    private void UpdateAddressSuggestions()
    {
        _addressSuggestions.Clear();
        if (!_config.EnableAddressBarSuggestions)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in _tabManager.Tabs)
        {
            AddSuggestionVariants(tab.CurrentUri, seen);
        }

        foreach (var entry in _historyManager.Entries.Take(80))
        {
            if (Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) && uri is not null)
            {
                AddSuggestionVariants(uri, seen);
            }
        }

        foreach (var bookmarkUrl in EnumerateBookmarkUrls(_bookmarkManager.TopLevelItems).Take(80))
        {
            if (Uri.TryCreate(bookmarkUrl, UriKind.Absolute, out var uri) && uri is not null)
            {
                AddSuggestionVariants(uri, seen);
            }
        }

        foreach (var entry in _tabManager.RecentlyClosedTabs.Take(20))
        {
            if (Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) && uri is not null)
            {
                AddSuggestionVariants(uri, seen);
            }
        }
    }

    private void LoadPinnedAddressCommandsFromConfig()
    {
        _pinnedAddressCommandIds.Clear();
        if (_config.PinnedAddressCommandIds.Count == 0)
        {
            return;
        }

        foreach (var commandId in _config.PinnedAddressCommandIds
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Select(item => item.Trim())
                     .Take(MaxPinnedAddressCommandCount))
        {
            if (_pinnedAddressCommandIds.Any(item => string.Equals(item, commandId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _pinnedAddressCommandIds.Add(commandId);
        }
    }

    private void PersistPinnedAddressCommandsToConfig()
    {
        _config.PinnedAddressCommandIds = _pinnedAddressCommandIds.ToList();
        SaveConfigSilently();
    }

    private void LoadRecentAddressCommandsFromConfig()
    {
        _recentAddressCommands.Clear();
        if (_config.RecentAddressCommands.Count == 0)
        {
            return;
        }

        foreach (var item in _config.RecentAddressCommands
                     .Where(item => !string.IsNullOrWhiteSpace(item.CommandId))
                     .OrderByDescending(item => item.UsedAtUtc)
                     .Take(MaxRecentAddressCommandCount))
        {
            var commandId = item.CommandId.Trim();
            var argumentToken = string.IsNullOrWhiteSpace(item.ArgumentToken)
                ? string.Empty
                : item.ArgumentToken.Trim().ToLowerInvariant();
            var usedAtUtc = item.UsedAtUtc == default ? DateTime.UtcNow : item.UsedAtUtc;
            _recentAddressCommands.AddLast(new RecentAddressCommandEntry(commandId, argumentToken, usedAtUtc));
        }
    }

    private void PersistRecentAddressCommandsToConfig()
    {
        _config.RecentAddressCommands = _recentAddressCommands
            .Select(item => new RecentAddressCommandLayout
            {
                CommandId = item.CommandId,
                ArgumentToken = item.ArgumentToken,
                UsedAtUtc = item.UsedAtUtc
            })
            .ToList();
        SaveConfigSilently();
    }

    private IEnumerable<string> EnumerateBookmarkUrls(IEnumerable<BookmarkNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLink && !string.IsNullOrWhiteSpace(node.Url))
            {
                yield return node.Url;
            }

            foreach (var nested in EnumerateBookmarkUrls(node.Children))
            {
                yield return nested;
            }
        }
    }

    private void AddSuggestionVariants(Uri? uri, HashSet<string> seen)
    {
        if (uri is null)
        {
            return;
        }

        AddAddressSuggestion(uri.AbsoluteUri, seen);
        if ((string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            AddAddressSuggestion(uri.Host, seen);
        }
    }

    private void AddAddressSuggestion(string value, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
        {
            return;
        }

        _addressSuggestions.Add(value);
    }

    private void RefreshAddressSuggestionPanel()
    {
        if (!_config.EnableAddressBarSuggestions || !_addressTextBox.Focused)
        {
            HideAddressSuggestionPanel();
            return;
        }

        var query = _addressTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            HideAddressSuggestionPanel();
            return;
        }

        var entries = BuildAddressSuggestionEntries(query).ToArray();
        if (entries.Length == 0)
        {
            HideAddressSuggestionPanel();
            return;
        }

        UpdateAddressSuggestionHint(query, entries);

        _addressSuggestionListBox.BeginUpdate();
        _addressSuggestionListBox.Items.Clear();
        foreach (var entry in entries)
        {
            _addressSuggestionListBox.Items.Add(entry);
        }
        // 中文注释：输入后默认不选中任何候选，只有用户按 ↓ 时才进入首个候选项。
        _addressSuggestionListBox.SelectedIndex = -1;
        _addressSuggestionListBox.EndUpdate();

        UpdateAddressSuggestionLocation();
        _addressSuggestionPanel.Visible = true;
        _addressSuggestionPanel.BringToFront();
    }

    private void ScheduleAddressSuggestionRefresh(bool immediate)
    {
        if (!_config.EnableAddressBarSuggestions)
        {
            _addressSuggestionRefreshTimer.Stop();
            HideAddressSuggestionPanel();
            return;
        }

        _addressSuggestionRefreshTimer.Stop();
        if (immediate)
        {
            RefreshAddressSuggestionPanel();
            return;
        }

        _addressSuggestionRefreshTimer.Start();
    }

    private IEnumerable<AddressSuggestionEntry> BuildAddressSuggestionEntries(string query)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groupedEntries = new List<AddressSuggestionEntry>();
        if (normalizedQuery.StartsWith(":", StringComparison.Ordinal))
        {
            var activeCommands = GetActiveAddressCommands();
            var pinnedEntries = BuildPinnedCommandSuggestions(query, seen, activeCommands).ToArray();
            var recentEntries = BuildRecentCommandSuggestions(query, seen, activeCommands).ToArray();
            var commandEntries = BuildCommandSuggestions(query, seen, activeCommands).ToArray();
            AppendSuggestionGroup(groupedEntries, "常用命令", pinnedEntries);
            AppendSuggestionGroup(groupedEntries, "最近命令", recentEntries);
            AppendSuggestionGroup(
                groupedEntries,
                "快捷指令",
                commandEntries.Where(entry => string.Equals(entry.Category, "快捷指令", StringComparison.Ordinal)));
            AppendSuggestionGroup(
                groupedEntries,
                "参数建议",
                commandEntries.Where(entry => string.Equals(entry.Category, "快捷参数", StringComparison.Ordinal)));
            return groupedEntries.Take(16).ToArray();
        }

        if (normalizedQuery.StartsWith("@", StringComparison.Ordinal))
        {
            AppendSuggestionGroup(groupedEntries, "快速切换", BuildTabQuickSwitchSuggestions(normalizedQuery));
            return groupedEntries.Take(12).ToArray();
        }

        AppendSuggestionGroup(groupedEntries, "当前标签", BuildTabSuggestions(normalizedQuery, seen));
        AppendSuggestionGroup(groupedEntries, "历史记录", BuildHistorySuggestions(normalizedQuery, seen));
        AppendSuggestionGroup(groupedEntries, "书签", BuildBookmarkSuggestions(normalizedQuery, seen));
        return groupedEntries.Take(16).ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildPinnedCommandSuggestions(
        string query,
        HashSet<string> seen,
        IReadOnlyList<AddressCommandDefinition> activeCommands)
    {
        if (_pinnedAddressCommandIds.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var rawInput = query.TrimStart();
        if (!rawInput.StartsWith(":", StringComparison.Ordinal))
        {
            return [];
        }

        var rawPayload = rawInput.Length > 1 ? rawInput[1..] : string.Empty;
        var payload = rawPayload.Trim();
        var commandKeyword = payload
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        var activeById = activeCommands
            .GroupBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var entries = new List<AddressSuggestionEntry>();
        foreach (var commandId in _pinnedAddressCommandIds)
        {
            if (!activeById.TryGetValue(commandId, out var command))
            {
                continue;
            }

            var entry = CreatePinnedCommandSuggestionEntry(command, commandKeyword, seen);
            if (entry is null)
            {
                continue;
            }

            entries.Add(entry);
        }

        return entries
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(5)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildRecentCommandSuggestions(
        string query,
        HashSet<string> seen,
        IReadOnlyList<AddressCommandDefinition> activeCommands)
    {
        if (_recentAddressCommands.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var rawInput = query.TrimStart();
        if (!rawInput.StartsWith(":", StringComparison.Ordinal))
        {
            return [];
        }

        var payload = rawInput.Length > 1 ? rawInput[1..].Trim() : string.Empty;
        var activeById = activeCommands
            .GroupBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var entries = new List<AddressSuggestionEntry>();
        foreach (var recent in _recentAddressCommands)
        {
            if (!activeById.TryGetValue(recent.CommandId, out var command))
            {
                continue;
            }

            var score = CalculateRecentCommandSuggestionScore(command, recent.ArgumentToken, payload);
            if (score is null)
            {
                continue;
            }

            var commandValue = string.IsNullOrWhiteSpace(recent.ArgumentToken)
                ? $":{command.Command}"
                : $":{command.Command} {recent.ArgumentToken}";
            if (!seen.Add(commandValue))
            {
                continue;
            }

            var primaryText = string.IsNullOrWhiteSpace(recent.ArgumentToken)
                ? command.DisplayName
                : $"{command.DisplayName} · {recent.ArgumentToken}";
            var secondaryText = $"{commandValue}  最近使用: {FormatRecentCommandAge(recent.UsedAtUtc)}";
            entries.Add(new AddressSuggestionEntry(
                "最近命令",
                primaryText,
                secondaryText,
                commandValue,
                IsHeader: false,
                MatchScore: score.Value,
                CommandId: command.CommandId,
                BadgeText: "RECENT"));
        }

        return entries
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(5)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildCommandSuggestions(
        string query,
        HashSet<string> seen,
        IReadOnlyList<AddressCommandDefinition> activeCommands)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var rawInput = query.TrimStart();
        if (!rawInput.StartsWith(":", StringComparison.Ordinal))
        {
            return [];
        }

        var rawPayload = rawInput.Length > 1 ? rawInput[1..] : string.Empty;
        var payload = rawPayload.Trim();
        var parts = payload.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var commandKeyword = parts.FirstOrDefault() ?? string.Empty;
        var argumentKeyword = parts.Length > 1
            ? string.Join(' ', parts.Skip(1))
            : string.Empty;
        var hasArgumentContext = rawPayload.Contains(' ', StringComparison.Ordinal);

        var results = new List<AddressSuggestionEntry>();
        var matchedCommands = activeCommands
            .Select(command => CreateCommandSuggestionEntry(command, commandKeyword, seen))
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(8)
            .ToArray();
        results.AddRange(matchedCommands);

        var targetCommand = activeCommands.FirstOrDefault(command =>
            string.Equals(command.Command, commandKeyword, StringComparison.OrdinalIgnoreCase) ||
            command.Aliases.Any(alias => string.Equals(alias, commandKeyword, StringComparison.OrdinalIgnoreCase)));
        if (targetCommand is not null && (hasArgumentContext || !string.IsNullOrWhiteSpace(commandKeyword)))
        {
            var argumentSuggestions = BuildCommandArgumentSuggestions(targetCommand, argumentKeyword, seen, activeCommands)
                .Take(6);
            results.AddRange(argumentSuggestions);
        }

        return results
            .Take(12)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildCommandArgumentSuggestions(
        AddressCommandDefinition command,
        string argumentKeyword,
        HashSet<string> seen,
        IReadOnlyList<AddressCommandDefinition> activeCommands)
    {
        var entries = new List<AddressSuggestionEntry>();
        if (AddressCommandArgumentCatalog.TryGetValue(command.CommandId, out var options) && options.Length > 0)
        {
            entries.AddRange(options
            .Select(option => CreateCommandArgumentSuggestionEntry(command, option, argumentKeyword, seen))
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .ToArray());
        }

        if (string.Equals(command.CommandId, "pincommand", StringComparison.OrdinalIgnoreCase))
        {
            entries.AddRange(BuildPinCommandTargetSuggestions(command, argumentKeyword, seen, activeCommands));
        }

        return entries
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(10)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildPinCommandTargetSuggestions(
        AddressCommandDefinition pinCommand,
        string argumentKeyword,
        HashSet<string> seen,
        IReadOnlyList<AddressCommandDefinition> activeCommands)
    {
        var tokens = argumentKeyword
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return [];
        }

        var action = tokens[0].Trim().ToLowerInvariant();
        if (action is not ("add" or "pin" or "save" or "remove" or "unpin" or "delete"))
        {
            return [];
        }

        var commandKeyword = tokens.Length > 1 ? string.Join(' ', tokens.Skip(1)) : string.Empty;
        var candidates = activeCommands
            .Where(item => !string.Equals(item.CommandId, pinCommand.CommandId, StringComparison.OrdinalIgnoreCase))
            .Where(item => action is "add" or "pin" or "save"
                ? !_pinnedAddressCommandIds.Any(pinned => string.Equals(pinned, item.CommandId, StringComparison.OrdinalIgnoreCase))
                : _pinnedAddressCommandIds.Any(pinned => string.Equals(pinned, item.CommandId, StringComparison.OrdinalIgnoreCase)));
        var entries = new List<AddressSuggestionEntry>();
        foreach (var candidate in candidates)
        {
            var score = CalculateCommandSuggestionScore(candidate, commandKeyword);
            if (score is null)
            {
                continue;
            }

            var normalizedAction = action is "add" or "pin" or "save" ? "add" : "remove";
            var commandValue = $":{pinCommand.Command} {normalizedAction} {candidate.Command}";
            if (!seen.Add(commandValue))
            {
                continue;
            }

            var primaryText = normalizedAction == "add"
                ? $"固定命令 · {candidate.Command}"
                : $"取消固定 · {candidate.Command}";
            var secondaryText = $"{commandValue}  {candidate.DisplayName}";
            entries.Add(new AddressSuggestionEntry(
                "快捷参数",
                primaryText,
                secondaryText,
                commandValue,
                IsHeader: false,
                MatchScore: score.Value,
                CommandId: pinCommand.CommandId,
                BadgeText: "ARG"));
        }

        return entries;
    }

    private IReadOnlyList<AddressCommandDefinition> GetActiveAddressCommands()
    {
        var configuredById = _config.AddressCommands
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var active = new List<AddressCommandDefinition>();
        foreach (var layout in _config.AddressCommands.OrderBy(item => item.Order))
        {
            if (string.IsNullOrWhiteSpace(layout.Id))
            {
                continue;
            }

            var template = AddressCommandCatalog.FirstOrDefault(item =>
                string.Equals(item.CommandId, layout.Id, StringComparison.OrdinalIgnoreCase));
            if (template is null || !layout.Enabled)
            {
                continue;
            }

            var normalizedCommand = NormalizeAddressCommandToken(layout.Command, template.Command);
            active.Add(template with { Command = normalizedCommand });
        }

        foreach (var fallback in AddressCommandCatalog)
        {
            if (active.Any(item => string.Equals(item.CommandId, fallback.CommandId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!configuredById.TryGetValue(fallback.CommandId, out var configured) || !configured.Enabled)
            {
                continue;
            }

            var normalizedCommand = NormalizeAddressCommandToken(configured.Command, fallback.Command);
            active.Add(fallback with { Command = normalizedCommand });
        }

        var uniqueCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in active.ToArray())
        {
            if (uniqueCommands.Add(command.Command))
            {
                continue;
            }

            var fallback = AddressCommandCatalog.First(item =>
                string.Equals(item.CommandId, command.CommandId, StringComparison.OrdinalIgnoreCase));
            var normalizedFallback = NormalizeAddressCommandToken(fallback.Command, fallback.Command);
            var replacement = fallback with { Command = normalizedFallback };
            active.Remove(command);
            if (uniqueCommands.Add(replacement.Command))
            {
                active.Add(replacement);
            }
        }

        return active;
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

    private string BuildAddressCommandHint(int maxCount = 4)
    {
        var targetCount = Math.Max(1, maxCount);
        var activeCommands = GetActiveAddressCommands();
        var commandById = activeCommands
            .GroupBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var preferredCommands = _pinnedAddressCommandIds
            .Select(commandId => commandById.TryGetValue(commandId, out var command) ? command : null)
            .Where(command => command is not null)
            .Cast<AddressCommandDefinition>()
            .Concat(activeCommands)
            .Select(item => $":{item.Command}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(targetCount)
            .ToArray();
        if (preferredCommands.Length == 0)
        {
            return ":settings";
        }

        return string.Join(" ", preferredCommands);
    }

    private void UpdateAddressCommandTooltip()
    {
        var commandExample = BuildAddressCommandHint(1);
        _addressTextBox.ToolTipText = $"输入网址或搜索关键词，按 Enter 搜索/访问；Ctrl+Enter 可补全为 https://www.xxx.com；输入 : 可使用快捷指令（如 {commandExample}），输入 @ 可快速切换标签（Ctrl+K），或按 Ctrl+Shift+P 打开命令面板";
    }

    private IEnumerable<AddressSuggestionEntry> BuildTabSuggestions(string query, HashSet<string> seen)
    {
        return _tabManager.Tabs
            .Select(tab => CreateSuggestionEntry(
                "当前标签",
                tab.DisplayTitle,
                tab.CurrentUri?.AbsoluteUri ?? string.Empty,
                tab.CurrentUri?.AbsoluteUri ?? string.Empty,
                query,
                seen))
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(5)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildTabQuickSwitchSuggestions(string normalizedQuery)
    {
        var keyword = normalizedQuery.Length > 1 ? normalizedQuery[1..].Trim() : string.Empty;
        return _tabManager.Tabs
            .Select(tab =>
            {
                var title = string.IsNullOrWhiteSpace(tab.DisplayTitle) ? "新标签页" : tab.DisplayTitle.Trim();
                var url = tab.CurrentUri?.AbsoluteUri ?? "about:blank";
                var score = string.IsNullOrWhiteSpace(keyword)
                    ? 0
                    : CalculateSuggestionScore(title, url, keyword);
                if (score is null)
                {
                    return null;
                }

                var pinText = tab.IsPinned ? "固定" : "普通";
                var customText = tab.HasCustomTitle ? " | 自定义名" : string.Empty;
                var colorText = ResolveTabColorText(tab.ColorTagKey);
                var colorSegment = string.IsNullOrWhiteSpace(colorText) ? string.Empty : $" | 色标:{colorText}";
                return new AddressSuggestionEntry(
                    "快速切换",
                    title,
                    $"@{title}  {pinText}{customText}{colorSegment}  {url}",
                    $"@{title}",
                    IsHeader: false,
                    MatchScore: score.Value,
                    BadgeText: "TAB",
                    TargetTabId: tab.Id);
            })
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(10)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildHistorySuggestions(string query, HashSet<string> seen)
    {
        return _historyManager.Entries
            .Take(400)
            .Select(entry => CreateSuggestionEntry("历史记录", entry.Title, entry.Url, entry.Url, query, seen))
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(6)
            .ToArray();
    }

    private IEnumerable<AddressSuggestionEntry> BuildBookmarkSuggestions(string query, HashSet<string> seen)
    {
        return EnumerateBookmarkEntries(_bookmarkManager.TopLevelItems)
            .Take(400)
            .Select(entry => CreateSuggestionEntry("书签", entry.Name, entry.Url, entry.Url, query, seen))
            .Where(entry => entry is not null)
            .Cast<AddressSuggestionEntry>()
            .OrderBy(entry => entry.MatchScore)
            .ThenBy(entry => entry.PrimaryText.Length)
            .Take(6)
            .ToArray();
    }

    private IEnumerable<(string Name, string Url)> EnumerateBookmarkEntries(IEnumerable<BookmarkNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLink && !string.IsNullOrWhiteSpace(node.Url))
            {
                yield return (node.Name, node.Url);
            }

            foreach (var child in EnumerateBookmarkEntries(node.Children))
            {
                yield return child;
            }
        }
    }

    private static AddressSuggestionEntry? CreateSuggestionEntry(
        string category,
        string primaryText,
        string secondaryText,
        string value,
        string query,
        HashSet<string> seen,
        string commandId = "")
    {
        if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
        {
            return null;
        }

        var score = CalculateSuggestionScore(primaryText, secondaryText, query);
        if (score is null)
        {
            return null;
        }

        var normalizedPrimary = string.IsNullOrWhiteSpace(primaryText) ? value : primaryText.Trim();
        var normalizedSecondary = string.IsNullOrWhiteSpace(secondaryText) ? value : secondaryText.Trim();
        return new AddressSuggestionEntry(
            category,
            normalizedPrimary,
            normalizedSecondary,
            value,
            IsHeader: false,
            MatchScore: score.Value,
            CommandId: commandId,
            BadgeText: ResolveSuggestionBadgeText(category, commandId));
    }

    private static string ResolveSuggestionBadgeText(string category, string commandId)
    {
        if (!string.IsNullOrWhiteSpace(commandId))
        {
            return "COMMAND";
        }

        return category switch
        {
            "当前标签" => "LIVE",
            "历史记录" => "HIS",
            "书签" => "BM",
            "快速切换" => "TAB",
            _ => string.Empty
        };
    }

    private static AddressSuggestionEntry? CreateCommandSuggestionEntry(
        AddressCommandDefinition command,
        string keyword,
        HashSet<string> seen)
    {
        var score = CalculateCommandSuggestionScore(command, keyword);
        if (score is null)
        {
            return null;
        }

        var commandValue = $":{command.Command}";
        if (!seen.Add(commandValue))
        {
            return null;
        }

        var secondaryText = string.IsNullOrWhiteSpace(command.Description)
            ? commandValue
            : $"{commandValue}  {command.Description}";
        return new AddressSuggestionEntry(
            "快捷指令",
            command.DisplayName,
            secondaryText,
            commandValue,
            IsHeader: false,
            MatchScore: score.Value,
            CommandId: command.CommandId,
            BadgeText: "COMMAND");
    }

    private static AddressSuggestionEntry? CreatePinnedCommandSuggestionEntry(
        AddressCommandDefinition command,
        string keyword,
        HashSet<string> seen)
    {
        var score = CalculateCommandSuggestionScore(command, keyword);
        if (score is null)
        {
            return null;
        }

        var commandValue = $":{command.Command}";
        if (!seen.Add(commandValue))
        {
            return null;
        }

        var secondaryText = string.IsNullOrWhiteSpace(command.Description)
            ? commandValue
            : $"{commandValue}  {command.Description}";
        return new AddressSuggestionEntry(
            "常用命令",
            command.DisplayName,
            secondaryText,
            commandValue,
            IsHeader: false,
            MatchScore: score.Value,
            CommandId: command.CommandId,
            BadgeText: "PIN");
    }

    private static AddressSuggestionEntry? CreateCommandArgumentSuggestionEntry(
        AddressCommandDefinition command,
        AddressCommandArgumentOption option,
        string keyword,
        HashSet<string> seen)
    {
        var score = CalculateCommandArgumentSuggestionScore(option, keyword);
        if (score is null)
        {
            return null;
        }

        var commandValue = $":{command.Command} {option.Token}";
        if (!seen.Add(commandValue))
        {
            return null;
        }

        return new AddressSuggestionEntry(
            "快捷参数",
            $"{command.DisplayName} · {option.Token}",
            $"{commandValue}  {option.Description}",
            commandValue,
            IsHeader: false,
            MatchScore: score.Value,
            CommandId: command.CommandId,
            BadgeText: "ARG");
    }

    private static int? CalculateSuggestionScore(string primaryText, string secondaryText, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        if (primaryText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (secondaryText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (primaryText.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (secondaryText.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return null;
    }

    private static int? CalculateCommandSuggestionScore(AddressCommandDefinition command, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return 0;
        }

        if (command.Command.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (command.DisplayName.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (command.Command.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (command.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (command.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (command.Aliases.Any(alias => alias.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return 5;
        }

        return null;
    }

    private static int? CalculateCommandArgumentSuggestionScore(AddressCommandArgumentOption option, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return 0;
        }

        if (option.Token.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (option.Description.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (option.Token.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (option.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (option.Aliases.Any(alias => alias.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return 4;
        }

        return null;
    }

    private static int? CalculateRecentCommandSuggestionScore(
        AddressCommandDefinition command,
        string argumentToken,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return 0;
        }

        var commandValue = string.IsNullOrWhiteSpace(argumentToken)
            ? command.Command
            : $"{command.Command} {argumentToken}";
        if (commandValue.StartsWith(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (command.Command.StartsWith(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(argumentToken) &&
            argumentToken.StartsWith(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (command.DisplayName.Contains(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (commandValue.Contains(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (command.Description.Contains(payload, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (command.Aliases.Any(alias => alias.Contains(payload, StringComparison.OrdinalIgnoreCase)))
        {
            return 5;
        }

        return null;
    }

    private static string FormatRecentCommandAge(DateTime usedAtUtc)
    {
        var elapsed = DateTime.UtcNow - usedAtUtc;
        if (elapsed < TimeSpan.FromSeconds(45))
        {
            return "刚刚";
        }

        if (elapsed < TimeSpan.FromMinutes(60))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} 分钟前";
        }

        if (elapsed < TimeSpan.FromHours(24))
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)} 小时前";
        }

        return $"{Math.Max(1, (int)elapsed.TotalDays)} 天前";
    }

    private static void AppendSuggestionGroup(List<AddressSuggestionEntry> target, string category, IEnumerable<AddressSuggestionEntry> suggestions)
    {
        var groupItems = suggestions.ToArray();
        if (groupItems.Length == 0)
        {
            return;
        }

        target.Add(new AddressSuggestionEntry(category, category, string.Empty, string.Empty, IsHeader: true, MatchScore: int.MinValue));
        target.AddRange(groupItems);
    }

    private void UpdateAddressSuggestionLocation()
    {
        if (!_topBar.Visible)
        {
            HideAddressSuggestionPanel();
            return;
        }

        if (!IsHandleCreated || _addressTextBox.TextBox is null)
        {
            return;
        }

        var screenLocation = _addressTextBox.TextBox.PointToScreen(Point.Empty);
        var clientLocation = PointToClient(screenLocation);
        var width = Math.Max(320, _addressTextBox.TextBox.Width);
        var hintHeight = _addressSuggestionHintLabel.Height;
        var listHeight = Math.Max(110, _addressSuggestionListBox.Items.Count * _addressSuggestionListBox.ItemHeight + 4);
        var availableWidth = ClientSize.Width - clientLocation.X - 16;
        if (availableWidth < 220)
        {
            HideAddressSuggestionPanel();
            return;
        }

        var desiredHeight = Math.Min(352, listHeight + hintHeight + 2);
        var defaultTop = clientLocation.Y + _addressTextBox.TextBox.Height + 2;
        var bottomOverflow = (defaultTop + desiredHeight) - (ClientSize.Height - 8);
        var top = defaultTop;
        if (bottomOverflow > 0)
        {
            var aboveTop = clientLocation.Y - desiredHeight - 2;
            if (aboveTop >= 0)
            {
                top = aboveTop;
            }
            else
            {
                top = Math.Max(0, defaultTop - bottomOverflow);
            }
        }

        _addressSuggestionPanel.Location = new Point(clientLocation.X, top);
        _addressSuggestionPanel.Width = Math.Max(220, Math.Min(width, availableWidth));
        _addressSuggestionPanel.Height = desiredHeight;
        SetRoundedRegion(_addressSuggestionPanel, radius: 12);
    }

    private void HideAddressSuggestionPanel()
    {
        _addressSuggestionPanel.Visible = false;
        _addressSuggestionHintLabel.Text = AddressSuggestionHintDefault;
        ApplyAddressSuggestionHintVisual(AddressSuggestionHintMode.Default);
    }

    private void UpdateAddressSuggestionHint(string query, IReadOnlyList<AddressSuggestionEntry> entries)
    {
        var normalized = query.TrimStart();
        var mode = AddressSuggestionHintMode.Default;
        var hintText = AddressSuggestionHintDefault;
        if (normalized.StartsWith("@", StringComparison.Ordinal))
        {
            mode = AddressSuggestionHintMode.TabSwitch;
            hintText = "Enter 切换到标签  |  Tab 预填关键词  |  ↑↓ 选择  |  Esc 关闭";
            _addressSuggestionHintLabel.Text = hintText;
            ApplyAddressSuggestionHintVisual(mode);
            return;
        }

        if (!normalized.StartsWith(":", StringComparison.Ordinal))
        {
            _addressSuggestionHintLabel.Text = hintText;
            ApplyAddressSuggestionHintVisual(mode);
            return;
        }

        var hasRecent = entries.Any(entry =>
            !entry.IsHeader && string.Equals(entry.Category, "最近命令", StringComparison.Ordinal));
        var hasPinned = entries.Any(entry =>
            !entry.IsHeader && string.Equals(entry.Category, "常用命令", StringComparison.Ordinal));
        var hasArgument = entries.Any(entry =>
            !entry.IsHeader && string.Equals(entry.Category, "快捷参数", StringComparison.Ordinal));
        if (hasArgument)
        {
            mode = AddressSuggestionHintMode.CommandArgument;
            hintText = "Enter 执行  |  Tab 仅补全参数  |  ↑↓ 选择  |  Esc 关闭";
            _addressSuggestionHintLabel.Text = hintText;
            ApplyAddressSuggestionHintVisual(mode);
            return;
        }

        if (hasPinned)
        {
            mode = AddressSuggestionHintMode.CommandPinned;
            hintText = "Enter 执行常用命令  |  Tab 预填  |  ↑↓ 选择  |  Esc 关闭";
            _addressSuggestionHintLabel.Text = hintText;
            ApplyAddressSuggestionHintVisual(mode);
            return;
        }

        if (hasRecent)
        {
            mode = AddressSuggestionHintMode.CommandRecent;
            hintText = "Enter 执行最近命令  |  Tab 预填  |  ↑↓ 选择  |  Esc 关闭";
            _addressSuggestionHintLabel.Text = hintText;
            ApplyAddressSuggestionHintVisual(mode);
            return;
        }

        mode = AddressSuggestionHintMode.CommandGeneral;
        hintText = "Enter 执行命令  |  Tab 预填  |  ↑↓ 选择  |  Esc 关闭";
        _addressSuggestionHintLabel.Text = hintText;
        ApplyAddressSuggestionHintVisual(mode);
    }

    private void ApplyAddressSuggestionHintVisual(AddressSuggestionHintMode mode)
    {
        var (baseColor, textColor) = mode switch
        {
            AddressSuggestionHintMode.TabSwitch => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.66F)
                    : AppTheme.Mix(AppTheme.Success, Color.White, 0.86F),
                AppTheme.IsDark ? Color.FromArgb(196, 240, 220) : Color.FromArgb(21, 97, 66)),
            AddressSuggestionHintMode.CommandPinned => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.66F)
                    : AppTheme.Mix(AppTheme.Warning, Color.White, 0.88F),
                AppTheme.IsDark ? Color.FromArgb(252, 224, 171) : Color.FromArgb(118, 74, 9)),
            AddressSuggestionHintMode.CommandRecent => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.62F)
                    : AppTheme.Mix(AppTheme.Accent, Color.White, 0.86F),
                AppTheme.IsDark ? Color.FromArgb(196, 224, 255) : Color.FromArgb(26, 70, 132)),
            AddressSuggestionHintMode.CommandArgument => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.64F)
                    : AppTheme.Mix(AppTheme.Danger, Color.White, 0.88F),
                AppTheme.IsDark ? Color.FromArgb(249, 210, 218) : Color.FromArgb(122, 40, 52)),
            AddressSuggestionHintMode.CommandGeneral => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.AccentMuted, AppTheme.SurfaceAlt, 0.70F)
                    : AppTheme.Mix(AppTheme.AccentMuted, Color.White, 0.88F),
                AppTheme.TextSecondary),
            _ => (
                AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.20F)
                    : AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.10F),
                AppTheme.TextSecondary)
        };
        _addressSuggestionHintLabel.BackColor = baseColor;
        _addressSuggestionHintLabel.ForeColor = textColor;
    }

    private bool HandleAddressSuggestionKeyDown(KeyEventArgs eventArgs)
    {
        if (!_addressSuggestionPanel.Visible || _addressSuggestionListBox.Items.Count == 0)
        {
            return false;
        }

        if (eventArgs.KeyCode == Keys.Down)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            MoveAddressSuggestionSelection(1);
            return true;
        }

        if (eventArgs.KeyCode == Keys.Up)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            MoveAddressSuggestionSelection(-1);
            return true;
        }

        if (eventArgs.KeyCode == Keys.Escape)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            HideAddressSuggestionPanel();
            return true;
        }

        if (eventArgs.KeyCode == Keys.Enter &&
            _addressSuggestionListBox.SelectedItem is AddressSuggestionEntry suggestion &&
            !suggestion.IsHeader)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            _ = AcceptAddressSuggestionAsync(suggestion, navigateImmediately: true);
            return true;
        }

        if (eventArgs.KeyCode == Keys.Tab &&
            _addressSuggestionListBox.SelectedItem is AddressSuggestionEntry tabSuggestion &&
            !tabSuggestion.IsHeader)
        {
            eventArgs.SuppressKeyPress = true;
            eventArgs.Handled = true;
            _ = AcceptAddressSuggestionAsync(tabSuggestion, navigateImmediately: false);
            return true;
        }

        return false;
    }

    private void MoveAddressSuggestionSelection(int direction)
    {
        if (_addressSuggestionListBox.Items.Count == 0)
        {
            return;
        }

        var index = _addressSuggestionListBox.SelectedIndex;
        if (index < 0)
        {
            // 中文注释：默认无选中时，↓ 选第一个候选，↑ 选最后一个候选。
            var fallbackIndex = direction >= 0
                ? FindNextSelectableSuggestionIndex(0, 1)
                : FindNextSelectableSuggestionIndex(_addressSuggestionListBox.Items.Count - 1, -1);
            if (fallbackIndex >= 0)
            {
                _addressSuggestionListBox.SelectedIndex = fallbackIndex;
            }

            return;
        }

        var nextIndex = index;
        do
        {
            nextIndex += direction;
            if (nextIndex < 0 || nextIndex >= _addressSuggestionListBox.Items.Count)
            {
                break;
            }
        }
        while (_addressSuggestionListBox.Items[nextIndex] is AddressSuggestionEntry entry && entry.IsHeader);

        if (nextIndex >= 0 &&
            nextIndex < _addressSuggestionListBox.Items.Count &&
            _addressSuggestionListBox.Items[nextIndex] is AddressSuggestionEntry nextEntry &&
            !nextEntry.IsHeader)
        {
            _addressSuggestionListBox.SelectedIndex = nextIndex;
        }
    }

    private int FindNextSelectableSuggestionIndex(int startIndex, int direction)
    {
        var index = startIndex;
        while (index >= 0 && index < _addressSuggestionListBox.Items.Count)
        {
            if (_addressSuggestionListBox.Items[index] is AddressSuggestionEntry entry && !entry.IsHeader)
            {
                return index;
            }

            index += direction;
        }

        return -1;
    }

    private async Task AcceptAddressSuggestionAsync(AddressSuggestionEntry suggestion, bool navigateImmediately)
    {
        if (suggestion.TargetTabId is Guid tabId && navigateImmediately)
        {
            HideAddressSuggestionPanel();
            await ActivateTabByIdAsync(tabId, showToast: true);
            return;
        }

        var suggestionValue = suggestion.Value;
        var shouldSuggestArguments = !navigateImmediately &&
            !string.IsNullOrWhiteSpace(suggestion.CommandId) &&
            !suggestionValue.Contains(' ', StringComparison.Ordinal) &&
            AddressCommandArgumentCatalog.ContainsKey(suggestion.CommandId);
        if (shouldSuggestArguments)
        {
            suggestionValue = $"{suggestionValue} ";
        }

        _suppressAddressSuggestionRefresh = true;
        _addressTextBox.Text = suggestionValue;
        _suppressAddressSuggestionRefresh = false;
        HideAddressSuggestionPanel();
        _addressTextBox.Focus();
        if (_addressTextBox.TextBox is { } textBox)
        {
            textBox.SelectionStart = textBox.TextLength;
            textBox.SelectionLength = 0;
        }

        if (shouldSuggestArguments)
        {
            ScheduleAddressSuggestionRefresh(immediate: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(suggestion.CommandId))
        {
            if (navigateImmediately)
            {
                await TryHandleAddressCommandAsync(suggestionValue);
            }

            return;
        }

        if (navigateImmediately)
        {
            await NavigateFromAddressBarAsync();
        }
    }

    private async Task<bool> ActivateTabByIdAsync(Guid tabId, bool showToast)
    {
        var targetTab = _tabManager.Tabs.FirstOrDefault(tab => tab.Id == tabId);
        if (targetTab is null)
        {
            return false;
        }

        _tabControl.SelectedTab = targetTab.TabPage;
        await _tabManager.ActivateSelectedTabAsync();
        RefreshToolbar();
        RefreshMetrics();
        HideAddressSuggestionPanel();
        if (showToast)
        {
            var title = string.IsNullOrWhiteSpace(targetTab.DisplayTitle) ? "新标签页" : targetTab.DisplayTitle.Trim();
            ShowToast($"已切换到标签: {title}");
        }

        return true;
    }

    private void DrawAddressSuggestionItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= _addressSuggestionListBox.Items.Count)
        {
            return;
        }

        using (var clearBrush = new SolidBrush(_addressSuggestionListBox.BackColor))
        {
            eventArgs.Graphics.FillRectangle(clearBrush, eventArgs.Bounds);
        }

        if (_addressSuggestionListBox.Items[eventArgs.Index] is not AddressSuggestionEntry entry)
        {
            return;
        }

        var selected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        var query = _addressTextBox.Text.Trim();
        var highlightQuery = query.StartsWith(":", StringComparison.Ordinal)
            ? query[1..].Trim()
            : query.StartsWith("@", StringComparison.Ordinal)
                ? query[1..].Trim()
            : query;
        var background = entry.IsHeader
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.18F)
                : AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.08F))
            : selected
                ? AppTheme.InputBackground
                : string.Equals(entry.Category, "常用命令", StringComparison.Ordinal)
                    ? (AppTheme.IsDark
                        ? AppTheme.Mix(AppTheme.InputBackground, AppTheme.Accent, 0.10F)
                        : AppTheme.Mix(AppTheme.InputBackground, AppTheme.Accent, 0.06F))
                : string.Equals(entry.Category, "快速切换", StringComparison.Ordinal)
                    ? (AppTheme.IsDark
                        ? AppTheme.Mix(AppTheme.InputBackground, AppTheme.Accent, 0.13F)
                        : AppTheme.Mix(AppTheme.InputBackground, AppTheme.Accent, 0.08F))
                : AppTheme.InputBackground;
        var rowBounds = Rectangle.Inflate(eventArgs.Bounds, -2, -1);
        using var backgroundBrush = new SolidBrush(background);
        using var rowPath = BuildRoundedRectanglePath(rowBounds, radius: 7);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.FillPath(backgroundBrush, rowPath);
        var categoryAccent = ResolveSuggestionCategoryAccent(entry);
        if (!entry.IsHeader && categoryAccent.A > 0)
        {
            var accentRect = new Rectangle(rowBounds.Left + 2, rowBounds.Top + 5, 3, Math.Max(8, rowBounds.Height - 10));
            using var accentBrush = new SolidBrush(categoryAccent);
            using var accentPath = BuildRoundedRectanglePath(accentRect, radius: 2);
            eventArgs.Graphics.FillPath(accentBrush, accentPath);
        }

        if (selected && !entry.IsHeader)
        {
            var selectionRect = Rectangle.Inflate(eventArgs.Bounds, -6, -2);
            var selectionBack = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.26F)
                : AppTheme.Mix(AppTheme.AccentHover, Color.White, 0.56F);
            var selectionBorder = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.24F)
                : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.18F);
            using var selectionBrush = new SolidBrush(selectionBack);
            using var selectionPen = new Pen(selectionBorder, 1F);
            using var selectionPath = BuildRoundedRectanglePath(selectionRect, radius: 8);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.FillPath(selectionBrush, selectionPath);
            eventArgs.Graphics.DrawPath(selectionPen, selectionPath);
        }

        if (entry.IsHeader)
        {
            var headerDot = new Rectangle(eventArgs.Bounds.Left + 10, eventArgs.Bounds.Top + (eventArgs.Bounds.Height - 8) / 2, 8, 8);
            using var dotBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.FillEllipse(dotBrush, headerDot);
            using var headerFont = new Font(_addressSuggestionListBox.Font, FontStyle.Bold);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                entry.PrimaryText,
                headerFont,
                new Rectangle(eventArgs.Bounds.Left + 24, eventArgs.Bounds.Top + 2, eventArgs.Bounds.Width - 26, eventArgs.Bounds.Height - 4),
                AppTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        if (selected)
        {
            using var indicatorBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.FillRectangle(indicatorBrush, eventArgs.Bounds.Left + 7, eventArgs.Bounds.Top + 5, 3, Math.Max(8, eventArgs.Bounds.Height - 10));
        }

        var iconLeft = eventArgs.Bounds.Left + 12;
        var iconTop = eventArgs.Bounds.Top + (eventArgs.Bounds.Height - 16) / 2;

        var urlCandidate = !string.IsNullOrWhiteSpace(entry.Value) && (entry.Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || entry.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            ? entry.Value
            : (!string.IsNullOrWhiteSpace(entry.SecondaryText) && (entry.SecondaryText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || entry.SecondaryText.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ? entry.SecondaryText : null);

        if (urlCandidate is not null)
        {
            var fav = FaviconService.GetIcon(urlCandidate, entry.PrimaryText);
            eventArgs.Graphics.DrawImage(fav, new Rectangle(iconLeft, iconTop, 16, 16));
        }
        else if (!string.IsNullOrWhiteSpace(entry.CommandId) || entry.Value.StartsWith(":"))
        {
            using var cmdFont = AppTheme.CreateFont(8.5F, FontStyle.Bold);
            using var cmdBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.DrawString("⚡", cmdFont, cmdBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }
        else if (entry.TargetTabId.HasValue || entry.Category.Contains("标签") || entry.Category.Contains("切换"))
        {
            using var iconFont = AppTheme.CreateFont(8.5F);
            using var iconBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.DrawString("📑", iconFont, iconBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }
        else if (entry.Category.Contains("书签"))
        {
            using var iconFont = AppTheme.CreateFont(8.5F);
            using var iconBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.DrawString("🔖", iconFont, iconBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }
        else if (entry.Category.Contains("历史"))
        {
            using var iconFont = AppTheme.CreateFont(8.5F);
            using var iconBrush = new SolidBrush(AppTheme.TextSecondary);
            eventArgs.Graphics.DrawString("🕒", iconFont, iconBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }
        else if (entry.Category.Contains("搜索") || string.Equals(entry.Category, "搜索引擎", StringComparison.Ordinal))
        {
            using var iconFont = AppTheme.CreateFont(8.5F);
            using var iconBrush = new SolidBrush(AppTheme.Accent);
            eventArgs.Graphics.DrawString("🔍", iconFont, iconBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }
        else
        {
            using var iconFont = AppTheme.CreateFont(8.5F);
            using var iconBrush = new SolidBrush(AppTheme.TextSecondary);
            eventArgs.Graphics.DrawString("🌐", iconFont, iconBrush, new Rectangle(iconLeft - 1, iconTop - 1, 18, 18));
        }

        const int offset = 24;
        var primaryBounds = new Rectangle(eventArgs.Bounds.Left + 10 + offset, eventArgs.Bounds.Top + 5, eventArgs.Bounds.Width - 20 - offset, 18);
        var secondaryBounds = new Rectangle(eventArgs.Bounds.Left + 10 + offset, eventArgs.Bounds.Top + 21, eventArgs.Bounds.Width - 20 - offset, 16);
        if (!string.IsNullOrWhiteSpace(entry.BadgeText))
        {
            using var badgeFont = AppTheme.CreateFont(6.7F, FontStyle.Bold);
            var badgeWidth = Math.Max(50, TextRenderer.MeasureText(entry.BadgeText, badgeFont).Width + 14);
            var commandBadge = new Rectangle(eventArgs.Bounds.Right - badgeWidth - 10, eventArgs.Bounds.Top + 6, badgeWidth, 16);
            var badgeBase = entry.BadgeText.ToUpperInvariant() switch
            {
                "ARG" => AppTheme.Mix(AppTheme.Accent, AppTheme.Warning, 0.42F),
                "RECENT" => AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.36F),
                "PIN" => AppTheme.Mix(AppTheme.Accent, AppTheme.Warning, 0.28F),
                "TAB" => AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.28F),
                "LIVE" => AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.24F),
                "HIS" => AppTheme.Mix(AppTheme.Accent, AppTheme.Warning, 0.20F),
                "BM" => AppTheme.Mix(AppTheme.Accent, AppTheme.AccentMuted, 0.42F),
                _ => AppTheme.Accent
            };
            using var badgeBrush = new SolidBrush(AppTheme.IsDark
                ? AppTheme.Mix(badgeBase, Color.White, 0.20F)
                : AppTheme.Mix(badgeBase, Color.White, 0.40F));
            using var badgePen = new Pen(AppTheme.IsDark
                ? AppTheme.Mix(badgeBase, Color.White, 0.35F)
                : AppTheme.Mix(badgeBase, Color.Black, 0.18F));
            using var badgePath = BuildRoundedRectanglePath(commandBadge, radius: 7);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.FillPath(badgeBrush, badgePath);
            eventArgs.Graphics.DrawPath(badgePen, badgePath);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                entry.BadgeText,
                badgeFont,
                commandBadge,
                AppTheme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            primaryBounds.Width -= badgeWidth + 8;
            secondaryBounds.Width -= badgeWidth + 8;
        }

        using var primaryFont = new Font(_addressSuggestionListBox.Font, FontStyle.Bold);
        DrawSuggestionTextWithHighlight(
            eventArgs.Graphics,
            entry.PrimaryText,
            primaryFont,
            primaryBounds,
            AppTheme.TextPrimary,
            AppTheme.IsDark ? Color.FromArgb(255, 239, 172) : Color.FromArgb(123, 72, 16),
            AppTheme.IsDark ? Color.FromArgb(84, 104, 34) : Color.FromArgb(248, 228, 136),
            highlightQuery,
            boldText: true);
        DrawSuggestionTextWithHighlight(
            eventArgs.Graphics,
            entry.SecondaryText,
            _addressSuggestionListBox.Font,
            secondaryBounds,
            AppTheme.TextSecondary,
            AppTheme.IsDark ? Color.FromArgb(249, 234, 173) : Color.FromArgb(111, 68, 14),
            AppTheme.IsDark ? Color.FromArgb(77, 92, 39) : Color.FromArgb(246, 234, 167),
            highlightQuery,
            boldText: false);
        if (eventArgs.Index < _addressSuggestionListBox.Items.Count - 1)
        {
            using var dividerPen = new Pen(AppTheme.IsDark
                ? Color.FromArgb(57, 72, 95)
                : Color.FromArgb(213, 228, 246));
            eventArgs.Graphics.DrawLine(dividerPen, eventArgs.Bounds.Left + 8, eventArgs.Bounds.Bottom - 1, eventArgs.Bounds.Right - 8, eventArgs.Bounds.Bottom - 1);
        }
    }

    private static Color ResolveSuggestionCategoryAccent(AddressSuggestionEntry entry)
    {
        if (entry.IsHeader)
        {
            return Color.Transparent;
        }

        if (string.Equals(entry.Category, "快速切换", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.Accent, 0.34F)
                : AppTheme.Mix(AppTheme.Success, AppTheme.Accent, 0.22F);
        }

        if (string.Equals(entry.Category, "常用命令", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.Accent, 0.34F)
                : AppTheme.Mix(AppTheme.Warning, AppTheme.Accent, 0.20F);
        }

        if (string.Equals(entry.Category, "最近命令", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.28F)
                : AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.18F);
        }

        return AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.30F)
            : AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.18F);
    }

    private static void DrawSuggestionTextWithHighlight(
        Graphics graphics,
        string text,
        Font font,
        Rectangle bounds,
        Color textColor,
        Color highlightTextColor,
        Color highlightBackColor,
        string query,
        bool boldText)
    {
        var drawFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        TextRenderer.DrawText(graphics, text, font, bounds, textColor, drawFlags);
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var highlightStart = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (highlightStart < 0)
        {
            return;
        }

        var safeQueryLength = Math.Min(query.Length, text.Length - highlightStart);
        var prefix = highlightStart == 0 ? string.Empty : text[..highlightStart];
        var highlightedText = text.Substring(highlightStart, safeQueryLength);
        var measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var prefixWidth = TextRenderer.MeasureText(graphics, prefix, font, new Size(int.MaxValue, int.MaxValue), measureFlags).Width;
        var highlightWidth = TextRenderer.MeasureText(graphics, highlightedText, font, new Size(int.MaxValue, int.MaxValue), measureFlags).Width;
        var highlightLeft = bounds.Left + prefixWidth;
        if (highlightLeft >= bounds.Right)
        {
            return;
        }

        var adjustedHighlightWidth = Math.Max(2, Math.Min(highlightWidth, bounds.Right - highlightLeft - 1));
        var highlightRect = new Rectangle(highlightLeft, bounds.Top + 2, adjustedHighlightWidth, Math.Max(4, bounds.Height - 4));
        using var highlightBrush = new SolidBrush(highlightBackColor);
        graphics.FillRectangle(highlightBrush, highlightRect);

        using var effectiveFont = boldText ? new Font(font, FontStyle.Bold) : null;
        TextRenderer.DrawText(
            graphics,
            highlightedText,
            effectiveFont ?? font,
            new Rectangle(highlightLeft, bounds.Top, bounds.Right - highlightLeft, bounds.Height),
            highlightTextColor,
            drawFlags);
    }

    private void PersistSessionState()
    {
        if (_launchOptions.IsPrivateWindow || _isRestoringSession)
        {
            return;
        }

        var currentSession = CaptureCurrentSessionState();
        var tabs = currentSession.Tabs.ToArray();
        if (tabs.Length == 0)
        {
            _sessionStore.Clear();
            return;
        }

        _sessionStore.Save(tabs, currentSession.ActiveIndex, exitedCleanly: _isClosingCleanly);
    }

    private BrowserSessionState CaptureCurrentSessionState()
    {
        var tabs = _tabManager.Tabs
            .Select(tab => new BrowserSessionTabState
            {
                Url = tab.CurrentUri?.AbsoluteUri ?? "about:blank",
                Title = tab.Title,
                CustomTitle = tab.CustomTitle,
                IsPinned = tab.IsPinned,
                TabColorKey = tab.ColorTagKey
            })
            .ToList();
        var activeTab = _tabManager.ActiveTab;
        var activeIndex = activeTab is null
            ? 0
            : _tabManager.Tabs
                .Select((tab, index) => new { tab, index })
                .FirstOrDefault(item => ReferenceEquals(item.tab, activeTab))
                ?.index ?? 0;
        return new BrowserSessionState
        {
            SavedAtUtc = DateTime.UtcNow,
            ActiveIndex = activeIndex,
            ExitedCleanly = _isClosingCleanly,
            Tabs = tabs
        };
    }

    private void SaveCurrentSessionSnapshot()
    {
        PersistSessionState();
        ShowToast("当前会话已保存");
        Log("当前会话已保存");
    }

    private void SaveNamedSessionSnapshot()
    {
        var currentSession = CaptureCurrentSessionState();
        if (currentSession.Tabs.Count == 0)
        {
            Log("当前没有可保存的标签页");
            return;
        }

        var defaultName = $"会话-{DateTime.Now:MMdd-HHmm}";
        if (!SessionNameDialog.ShowDialog(this, defaultName, out var sessionName))
        {
            return;
        }

        if (!_namedSessionStore.Save(sessionName, currentSession, out var error))
        {
            MessageBox.Show($"保存命名会话失败：{error}", "会话", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _sessionsWindow?.RefreshData();
        ShowToast($"已保存命名会话：{sessionName}");
        Log($"已保存命名会话：{sessionName}");
    }

    private void SaveWorkspaceSnapshot()
    {
        var currentSession = CaptureCurrentSessionState();
        if (currentSession.Tabs.Count == 0)
        {
            Log("当前没有可保存的标签页");
            return;
        }

        var deduplicatedTabs = DeduplicateWorkspaceTabs(currentSession.Tabs, out var removedCount);
        if (deduplicatedTabs.Count == 0)
        {
            Log("当前没有可保存的有效标签页");
            return;
        }

        var activeTabKey = currentSession.ActiveIndex >= 0 &&
                           currentSession.ActiveIndex < currentSession.Tabs.Count &&
                           TryBuildWorkspaceTabKey(currentSession.Tabs[currentSession.ActiveIndex], out var key)
            ? key
            : string.Empty;
        var resolvedActiveIndex = 0;
        if (!string.IsNullOrWhiteSpace(activeTabKey))
        {
            var matchedIndex = deduplicatedTabs.FindIndex(tab => TryBuildWorkspaceTabKey(tab, out var tabKey) &&
                                                                 string.Equals(tabKey, activeTabKey, StringComparison.OrdinalIgnoreCase));
            resolvedActiveIndex = matchedIndex >= 0 ? matchedIndex : 0;
        }

        var normalizedSession = new BrowserSessionState
        {
            SavedAtUtc = DateTime.UtcNow,
            ActiveIndex = Math.Clamp(resolvedActiveIndex, 0, deduplicatedTabs.Count - 1),
            ExitedCleanly = true,
            Tabs = deduplicatedTabs
        };

        var defaultName = $"{WorkspaceSessionPrefix}{DateTime.Now:MMdd-HHmm}";
        if (!SessionNameDialog.ShowDialog(this, defaultName, out var workspaceName))
        {
            return;
        }

        var normalizedWorkspaceName = NormalizeWorkspaceSessionName(workspaceName);
        if (!_namedSessionStore.Save(normalizedWorkspaceName, normalizedSession, out var error))
        {
            MessageBox.Show($"保存工作区失败：{error}", "工作区", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var displayName = GetWorkspaceDisplayName(normalizedWorkspaceName);
        _sessionsWindow?.RefreshData();
        if (removedCount > 0)
        {
            ShowToast($"已保存工作区：{displayName}（自动去重 {removedCount} 个重复页面）");
            Log($"已保存工作区：{displayName}（去重 {removedCount}）");
            return;
        }

        ShowToast($"已保存工作区：{displayName}");
        Log($"已保存工作区：{displayName}");
    }

    private void AppendActiveTabToWorkspace(string workspaceName)
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab?.CurrentUri is null)
        {
            Log("当前标签页地址无效，无法加入工作区");
            return;
        }

        var workspace = _namedSessionStore.Load(workspaceName);
        if (workspace is null || workspace.Tabs.Count == 0)
        {
            Log("目标工作区不存在或为空");
            return;
        }

        var appendedTab = new BrowserSessionTabState
        {
            Url = activeTab.CurrentUri.AbsoluteUri,
            Title = activeTab.DisplayTitle,
            CustomTitle = activeTab.CustomTitle,
            IsPinned = activeTab.IsPinned,
            TabColorKey = activeTab.ColorTagKey
        };
        if (!TryBuildWorkspaceTabKey(appendedTab, out var targetKey))
        {
            Log("当前标签页地址无效，无法加入工作区");
            return;
        }

        var tabs = DeduplicateWorkspaceTabs(workspace.Tabs, out var removedDuplicates);
        var existingIndex = tabs.FindIndex(tab => TryBuildWorkspaceTabKey(tab, out var tabKey) &&
                                                  string.Equals(tabKey, targetKey, StringComparison.OrdinalIgnoreCase));
        var appended = false;
        if (existingIndex < 0)
        {
            tabs.Add(appendedTab);
            appended = true;
        }
        else
        {
            tabs[existingIndex] = appendedTab;
        }

        var updatedSession = new BrowserSessionState
        {
            SavedAtUtc = DateTime.UtcNow,
            ActiveIndex = appended
                ? Math.Clamp(workspace.ActiveIndex, 0, Math.Max(0, tabs.Count - 1))
                : Math.Clamp(existingIndex, 0, Math.Max(0, tabs.Count - 1)),
            ExitedCleanly = true,
            Tabs = tabs
        };

        if (!_namedSessionStore.Save(workspace.Name, updatedSession, out var error))
        {
            MessageBox.Show($"追加标签到工作区失败：{error}", "工作区", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var displayName = GetWorkspaceDisplayName(workspace.Name);
        _sessionsWindow?.RefreshData();
        if (appended)
        {
            var dedupeHint = removedDuplicates > 0 ? $"（已清理 {removedDuplicates} 个重复页面）" : string.Empty;
            ShowToast($"已加入到工作区：{displayName}{dedupeHint}");
            Log($"当前标签已加入工作区：{displayName}");
            return;
        }

        ShowToast($"该页面已在工作区中，已更新标签信息：{displayName}");
        Log($"工作区已去重：{displayName}");
    }

    private void DedupeAllWorkspaces()
    {
        var workspaces = _namedSessionStore.ListSessions()
            .Where(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (workspaces.Count == 0)
        {
            Log("当前没有可维护的工作区");
            return;
        }

        var updatedCount = 0;
        var removedTotal = 0;
        var failedCount = 0;

        foreach (var workspace in workspaces)
        {
            var loaded = _namedSessionStore.Load(workspace.Name);
            if (loaded is null || loaded.Tabs.Count == 0)
            {
                continue;
            }

            var deduplicated = DeduplicateWorkspaceTabs(loaded.Tabs, out var removedCount);
            if (removedCount <= 0)
            {
                continue;
            }

            if (deduplicated.Count == 0)
            {
                failedCount++;
                continue;
            }

            var updatedSession = new BrowserSessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(loaded.ActiveIndex, 0, deduplicated.Count - 1),
                ExitedCleanly = true,
                Tabs = deduplicated
            };

            if (!_namedSessionStore.Save(workspace.Name, updatedSession, out _))
            {
                failedCount++;
                continue;
            }

            updatedCount++;
            removedTotal += removedCount;
        }

        _sessionsWindow?.RefreshData();
        UpdateWorkspaceButtonBadge(workspaces.Count);
        if (updatedCount == 0)
        {
            Log("工作区维护完成：没有发现可清理的重复页面");
            ShowToast("工作区去重完成：无重复页面");
            return;
        }

        var failureHint = failedCount > 0 ? $"，失败 {failedCount} 个" : string.Empty;
        ShowToast($"工作区去重完成：更新 {updatedCount} 个，清理重复 {removedTotal} 个{failureHint}");
        Log($"工作区去重完成：更新 {updatedCount} 个，清理重复 {removedTotal} 个{failureHint}");
    }

    private static string NormalizeWorkspaceSessionName(string rawName)
    {
        var trimmed = string.IsNullOrWhiteSpace(rawName)
            ? $"{WorkspaceSessionPrefix}{DateTime.Now:MMdd-HHmm}"
            : rawName.Trim();
        return trimmed.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{WorkspaceSessionPrefix}{trimmed}";
    }

    private static string GetWorkspaceDisplayName(string sessionName)
    {
        if (!sessionName.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return sessionName;
        }

        var trimmed = sessionName[WorkspaceSessionPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "未命名工作区" : trimmed;
    }

    private static List<BrowserSessionTabState> DeduplicateWorkspaceTabs(
        IEnumerable<BrowserSessionTabState> tabs,
        out int removedCount)
    {
        removedCount = 0;
        var uniqueTabs = new List<BrowserSessionTabState>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in tabs)
        {
            if (!TryBuildWorkspaceTabKey(tab, out var key))
            {
                continue;
            }

            if (!keys.Add(key))
            {
                removedCount++;
                continue;
            }

            uniqueTabs.Add(tab);
        }

        return uniqueTabs;
    }

    private static List<BrowserSessionTabState> CloneSessionTabs(IEnumerable<BrowserSessionTabState> tabs)
    {
        return tabs
            .Select(tab => new BrowserSessionTabState
            {
                Url = tab.Url,
                Title = tab.Title,
                CustomTitle = tab.CustomTitle,
                IsPinned = tab.IsPinned,
                TabColorKey = tab.TabColorKey
            })
            .ToList();
    }

    private static bool IsBlankWorkspaceTab(BrowserSessionTabState tab)
    {
        if (!Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) || uri is null)
        {
            return true;
        }

        return uri.IsAbsoluteUri &&
               string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveWorkspaceOpenPolicy()
    {
        var policy = _config.WorkspaceOpenPolicy;
        if (string.Equals(policy, WorkspaceOpenPolicyKeepAll, StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceOpenPolicyKeepAll;
        }

        if (string.Equals(policy, WorkspaceOpenPolicyClean, StringComparison.OrdinalIgnoreCase))
        {
            return WorkspaceOpenPolicyClean;
        }

        return WorkspaceOpenPolicyDedupe;
    }

    private static string GetWorkspaceOpenPolicyDisplayText(string policy)
    {
        return policy switch
        {
            WorkspaceOpenPolicyKeepAll => "完整打开",
            WorkspaceOpenPolicyClean => "深度整理",
            _ => "自动去重"
        };
    }

    private void UpdateWorkspaceOpenPolicy(string nextPolicy)
    {
        var normalized = nextPolicy switch
        {
            WorkspaceOpenPolicyKeepAll => WorkspaceOpenPolicyKeepAll,
            WorkspaceOpenPolicyClean => WorkspaceOpenPolicyClean,
            _ => WorkspaceOpenPolicyDedupe
        };
        if (string.Equals(_config.WorkspaceOpenPolicy, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.WorkspaceOpenPolicy = normalized;
        if (!BrowserConfig.SaveDefault(_config, out var error))
        {
            MessageBox.Show($"保存工作区打开策略失败：{error}", "工作区", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var policyText = GetWorkspaceOpenPolicyDisplayText(normalized);
        ShowToast($"工作区打开策略已切换：{policyText}");
        Log($"工作区打开策略已切换：{policyText}");
        UpdateWorkspaceButtonBadge();
    }

    private List<BrowserSessionTabState> ApplyWorkspaceOpenPolicyToTabs(
        IReadOnlyList<BrowserSessionTabState> sourceTabs,
        out int removedDuplicates,
        out int removedBlankTabs)
    {
        removedDuplicates = 0;
        removedBlankTabs = 0;
        var policy = ResolveWorkspaceOpenPolicy();
        var cloned = CloneSessionTabs(sourceTabs);

        if (string.Equals(policy, WorkspaceOpenPolicyKeepAll, StringComparison.OrdinalIgnoreCase))
        {
            return cloned;
        }

        var deduped = DeduplicateWorkspaceTabs(cloned, out removedDuplicates);
        if (!string.Equals(policy, WorkspaceOpenPolicyClean, StringComparison.OrdinalIgnoreCase))
        {
            return deduped;
        }

        var cleaned = deduped
            .Where(tab => !IsBlankWorkspaceTab(tab))
            .ToList();
        removedBlankTabs = Math.Max(0, deduped.Count - cleaned.Count);
        return cleaned.Count == 0 ? deduped : cleaned;
    }

    private static bool TryBuildWorkspaceTabKey(BrowserSessionTabState tab, out string key)
    {
        if (!Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) || uri is null)
        {
            key = string.Empty;
            return false;
        }

        key = BuildWorkspaceTabKey(uri);
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string BuildWorkspaceTabKey(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var authority = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        var path = uri.AbsolutePath;
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        return $"{scheme}://{authority}{path}{uri.Query}";
    }

    private async Task RestoreSavedSessionIntoCurrentWindowAsync()
    {
        var session = _sessionStore.Load();
        if (session is null || session.Tabs.Count == 0)
        {
            Log("没有可恢复的已保存会话");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "恢复已保存会话会用保存的标签页覆盖当前窗口，是否继续？",
            "恢复已保存会话",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _isRestoringSession = true;
        try
        {
            if (!await ApplySessionToCurrentWindowAsync(session, replaceCurrentTabs: true))
            {
                Log("恢复会话失败");
                return;
            }
        }
        finally
        {
            _isRestoringSession = false;
            UpdateAddressSuggestions();
            PersistSessionState();
        }

        ShowToast("已恢复保存的会话");
        Log("已恢复保存的会话");
        RefreshToolbar();
        RefreshMetrics();
    }

    private async Task RestoreNamedSessionIntoCurrentWindowAsync(string sessionName)
    {
        var namedSession = _namedSessionStore.Load(sessionName);
        if (namedSession is null || namedSession.Tabs.Count == 0)
        {
            Log("命名会话不存在或内容为空");
            return;
        }

        var sessionTabs = ApplyWorkspaceOpenPolicyToTabs(
            namedSession.Tabs,
            out var removedDuplicates,
            out var removedBlankTabs);
        if (sessionTabs.Count == 0)
        {
            Log("命名会话在当前策略下无可恢复标签");
            MessageBox.Show("该会话在当前工作区打开策略下无可恢复标签，请调整策略后重试。", "恢复命名会话", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"恢复命名会话“{sessionName}”会覆盖当前窗口标签，是否继续？",
            "恢复命名会话",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _isRestoringSession = true;
        try
        {
            var session = new BrowserSessionState
            {
                SavedAtUtc = namedSession.SavedAtUtc,
                ActiveIndex = Math.Clamp(namedSession.ActiveIndex, 0, Math.Max(0, sessionTabs.Count - 1)),
                ExitedCleanly = true,
                Tabs = sessionTabs
            };
            if (!await ApplySessionToCurrentWindowAsync(session, replaceCurrentTabs: true))
            {
                Log("恢复命名会话失败");
                return;
            }
        }
        finally
        {
            _isRestoringSession = false;
            UpdateAddressSuggestions();
            PersistSessionState();
        }

        var policy = ResolveWorkspaceOpenPolicy();
        var policyText = GetWorkspaceOpenPolicyDisplayText(policy);
        var cleanHint = removedDuplicates > 0 || removedBlankTabs > 0
            ? $"（去重 {removedDuplicates}，清空白 {removedBlankTabs}）"
            : string.Empty;
        ShowToast($"已恢复命名会话：{sessionName} [{policyText}]{cleanHint}");
        Log($"已恢复命名会话：{sessionName}，策略={policyText}，去重={removedDuplicates}，空白={removedBlankTabs}");
        _sessionsWindow?.RefreshData();
        RefreshToolbar();
        RefreshMetrics();
    }

    private void OpenNamedSessionInSharedWindow(string sessionName)
    {
        OpenNamedSessionInNewWindow(sessionName, useCurrentProfile: true, isPrivateWindow: false);
    }

    private void OpenNamedSessionInIsolatedWindow(string sessionName)
    {
        OpenNamedSessionInNewWindow(sessionName, useCurrentProfile: false, isPrivateWindow: false);
    }

    private void OpenNamedSessionInPrivateWindow(string sessionName)
    {
        OpenNamedSessionInNewWindow(sessionName, useCurrentProfile: false, isPrivateWindow: true);
    }

    private void OpenNamedSessionInNewWindow(string sessionName, bool useCurrentProfile, bool isPrivateWindow)
    {
        var namedSession = _namedSessionStore.Load(sessionName);
        if (namedSession is null || namedSession.Tabs.Count == 0)
        {
            Log("命名会话不存在或内容为空");
            return;
        }

        var sessionTabs = ApplyWorkspaceOpenPolicyToTabs(
            namedSession.Tabs,
            out var removedDuplicates,
            out var removedBlankTabs);
        if (sessionTabs.Count == 0)
        {
            Log("命名会话在当前策略下无可打开标签");
            MessageBox.Show("该会话在当前工作区打开策略下无可打开标签，请调整策略后重试。", "命名会话", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var session = new BrowserSessionState
        {
            SavedAtUtc = namedSession.SavedAtUtc,
            ActiveIndex = Math.Clamp(namedSession.ActiveIndex, 0, Math.Max(0, sessionTabs.Count - 1)),
            ExitedCleanly = true,
            Tabs = sessionTabs
        };

        if (!LaunchBrowserProcess(isPrivateWindow, useCurrentProfile, startupSession: session))
        {
            MessageBox.Show("新窗口打开命名会话失败。", "命名会话", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var modeText = isPrivateWindow
            ? "无痕窗口"
            : useCurrentProfile
                ? "共享窗口"
                : "独立窗口";
        var policy = ResolveWorkspaceOpenPolicy();
        var policyText = GetWorkspaceOpenPolicyDisplayText(policy);
        var cleanHint = removedDuplicates > 0 || removedBlankTabs > 0
            ? $"（去重 {removedDuplicates}，清空白 {removedBlankTabs}）"
            : string.Empty;
        ShowToast($"已在新{modeText}打开：{sessionName} [{policyText}]{cleanHint}");
        Log($"已在新{modeText}打开命名会话：{sessionName}，策略={policyText}，去重={removedDuplicates}，空白={removedBlankTabs}");
    }

    private void ClearSavedSessionSnapshot()
    {
        var confirm = MessageBox.Show(
            this,
            "确认清除本配置目录下保存的会话快照？",
            "清除会话",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _sessionStore.Clear();
        ShowToast("已清除保存的会话");
        Log("已清除保存的会话");
    }

    private void DeleteNamedSessionSnapshot(string sessionName)
    {
        var confirm = MessageBox.Show(
            this,
            $"确认删除命名会话“{sessionName}”？",
            "删除命名会话",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (!_namedSessionStore.Delete(sessionName, out var error))
        {
            MessageBox.Show($"删除命名会话失败：{error}", "会话", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _sessionsWindow?.RefreshData();
        ShowToast($"已删除命名会话：{sessionName}");
        Log($"已删除命名会话：{sessionName}");
    }

    private async Task NavigateFromAddressBarAsync()
    {
        if (await TryHandleAddressCommandAsync(_addressTextBox.Text))
        {
            return;
        }

        if (await TryHandleTabQuickSwitchAsync(_addressTextBox.Text))
        {
            return;
        }

        if (TryHandleInternalBrowserUrl(_addressTextBox.Text, userInitiated: true))
        {
            HideAddressSuggestionPanel();
            return;
        }

        var active = _tabManager.ActiveTab;
        if (active is null)
        {
            return;
        }

        HideAddressSuggestionPanel();

        if (!AddressBarInputResolver.TryResolve(_addressTextBox.Text, _config, out var uri, out var treatedAsSearch))
        {
            Log("地址无效");
            return;
        }

        await active.NavigateAsync(uri);
        if (treatedAsSearch)
        {
            Log($"搜索：{_addressTextBox.Text.Trim()}");
        }
        RefreshToolbar();
    }

    private async Task<bool> TryHandleTabQuickSwitchAsync(string rawInput)
    {
        var trimmed = rawInput.Trim();
        if (!trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            return false;
        }

        var keyword = trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ShowToast("输入 @ + 关键词可快速切换标签");
            Log("示例: @文档  或  @github");
            return true;
        }

        var target = _tabManager.Tabs
            .Select(tab => new
            {
                Tab = tab,
                Score = CalculateSuggestionScore(tab.DisplayTitle, tab.CurrentUri?.AbsoluteUri ?? string.Empty, keyword)
            })
            .Where(item => item.Score is not null)
            .OrderBy(item => item.Score)
            .ThenBy(item => item.Tab.DisplayTitle.Length)
            .FirstOrDefault();
        if (target is null)
        {
            ShowToast($"没有匹配的标签: {keyword}");
            return true;
        }

        await ActivateTabByIdAsync(target.Tab.Id, showToast: true);
        return true;
    }

    private async Task<bool> TryHandleAddressCommandAsync(string rawInput)
    {
        var trimmed = rawInput.Trim();
        if (!trimmed.StartsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        var keyword = trimmed[1..].Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            var commandHint = BuildAddressCommandHint(5);
            ShowToast($"可用指令: {commandHint}");
            Log($"输入 : + 指令名，可执行快捷操作（例如 {BuildAddressCommandHint(1)}）");
            return true;
        }

        var tokens = keyword
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var commandToken = tokens.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(commandToken))
        {
            return true;
        }

        var activeCommands = GetActiveAddressCommands();
        var command = activeCommands.FirstOrDefault(item =>
            string.Equals(item.Command, commandToken, StringComparison.OrdinalIgnoreCase) ||
            item.Aliases.Any(alias => string.Equals(alias, commandToken, StringComparison.OrdinalIgnoreCase)));
        if (command is null)
        {
            var commandHint = BuildAddressCommandHint(4);
            ShowToast($"未知指令: {commandToken}");
            Log($"未知指令: {commandToken}（可尝试 {commandHint}）");
            return true;
        }

        var commandArgs = tokens.Skip(1).ToArray();
        RememberRecentAddressCommand(command, commandArgs);
        await ExecuteAddressCommandAsync(command.CommandId, commandArgs);
        return true;
    }

    private void RememberRecentAddressCommand(AddressCommandDefinition command, IReadOnlyList<string> commandArgs)
    {
        var argumentToken = commandArgs.FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;
        var node = _recentAddressCommands.First;
        while (node is not null)
        {
            var next = node.Next;
            if (string.Equals(node.Value.CommandId, command.CommandId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(node.Value.ArgumentToken, argumentToken, StringComparison.OrdinalIgnoreCase))
            {
                _recentAddressCommands.Remove(node);
            }

            node = next;
        }

        _recentAddressCommands.AddFirst(new RecentAddressCommandEntry(command.CommandId, argumentToken, DateTime.UtcNow));
        while (_recentAddressCommands.Count > MaxRecentAddressCommandCount)
        {
            _recentAddressCommands.RemoveLast();
        }

        PersistRecentAddressCommandsToConfig();
    }

    private async Task ExecuteAddressCommandAsync(string commandId, IReadOnlyList<string>? args = null)
    {
        var commandArgs = args ?? [];
        switch (commandId)
        {
            case "settings":
                ShowSettings();
                ShowToast("已打开设置");
                Log("已执行指令: settings");
                return;
            case "history":
                ShowHistoryWindow();
                ShowToast("已打开历史记录");
                Log("已执行指令: history");
                return;
            case "downloads":
                ShowDownloadManager();
                ShowToast("已打开下载管理");
                Log("已执行指令: downloads");
                return;
            case "sessions":
                ShowSessionsWindow();
                ShowToast("已打开会话中心");
                Log("已执行指令: sessions");
                return;
            case "bookmarks":
                ShowBookmarksSettingsWindow();
                ShowToast("已打开书签管理");
                Log("已执行指令: bookmarks");
                return;
            case "newtab":
                await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
                RefreshToolbar();
                RefreshMetrics();
                ShowToast("已新建标签页");
                Log("已执行指令: newtab");
                return;
            case "tabswitch":
                await ApplyTabSwitchByCommandAsync(commandArgs);
                Log("已执行指令: tabswitch");
                return;
            case "tabops":
                await ApplyTabOpsByCommandAsync(commandArgs);
                Log("已执行指令: tabops");
                return;
            case "hardrefresh":
                await HardRefreshActiveTabAsync();
                Log("已执行指令: hardrefresh");
                return;
            case "reloadplugins":
                _pluginManager.Reload();
                ShowToast("插件已重载");
                Log("已执行指令: reloadplugins");
                return;
            case "devtools":
                _tabManager.ActiveTab?.OpenDevTools();
                Log("已执行指令: devtools");
                return;
            case "viewsource":
                await OpenActiveTabSourceViewAsync();
                Log("已执行指令: viewsource");
                return;
            case "darkmode":
                ApplyUiThemeFromMenu("Dark");
                Log("已执行指令: darkmode");
                return;
            case "lightmode":
                ApplyUiThemeFromMenu("Light");
                Log("已执行指令: lightmode");
                return;
            case "compactui":
                ApplyUiDensityFromMenu("Compact");
                Log("已执行指令: compactui");
                return;
            case "normalui":
                ApplyUiDensityFromMenu("Normal");
                Log("已执行指令: normalui");
                return;
            case "togglebookmarksbar":
                ToggleBookmarksBarVisibility();
                Log("已执行指令: togglebookmarksbar");
                return;
            case "theme":
                ApplyThemeByCommand(commandArgs);
                Log("已执行指令: theme");
                return;
            case "density":
                ApplyDensityByCommand(commandArgs);
                Log("已执行指令: density");
                return;
            case "accent":
                ApplyAccentByCommand(commandArgs);
                Log("已执行指令: accent");
                return;
            case "accentstrength":
                ApplyAccentStrengthByCommand(commandArgs);
                Log("已执行指令: accentstrength");
                return;
            case "uipreset":
                ApplyUiPresetByCommand(commandArgs);
                Log("已执行指令: uipreset");
                return;
            case "recentcommands":
                ApplyRecentCommandsByCommand(commandArgs);
                Log("已执行指令: recentcommands");
                return;
            case "pincommand":
                ApplyPinCommandByCommand(commandArgs);
                Log("已执行指令: pincommand");
                return;
            case "resetui":
                ResetUiAppearanceByCommand();
                Log("已执行指令: resetui");
                return;
            case "font":
                ApplyFontByCommand(commandArgs);
                Log("已执行指令: font");
                return;
            case "bookmarksbar":
                ApplyBookmarksBarByCommand(commandArgs);
                Log("已执行指令: bookmarksbar");
                return;
            case "statusbar":
                ApplyStatusBarByCommand(commandArgs);
                Log("已执行指令: statusbar");
                return;
            case "tabname":
                ApplyTabNameByCommand(commandArgs);
                Log("已执行指令: tabname");
                return;
            case "tabcolor":
                ApplyTabColorByCommand(commandArgs);
                Log("已执行指令: tabcolor");
                return;
            case "zen":
                ApplyZenByCommand(commandArgs);
                Log("已执行指令: zen");
                return;
            case "help":
                ShowAddressCommandHelp();
                Log("已执行指令: help");
                return;
            case "palette":
                OpenCommandPalette();
                Log("已执行指令: palette");
                return;
            case "reader":
                await ToggleReaderModeForActiveTabAsync();
                Log("已执行指令: reader");
                return;
            case "split":
                await ToggleSplitViewAsync();
                Log("已执行指令: split");
                return;
            case "verticaltabs":
            case "vtabs":
                ToggleVerticalTabs();
                Log("已执行指令: verticaltabs");
                return;
            case "pip":
                if (_tabManager.ActiveTab is not null)
                {
                    var pipResult = await _tabManager.ActiveTab.TogglePictureInPictureAsync();
                    ShowToast(pipResult.message);
                }
                Log("已执行指令: pip");
                return;
            case "freememory":
            case "cleanmem":
                var freedCount = await _tabManager.SuspendAllBackgroundTabsAsync();
                RefreshMetrics();
                ShowToast(freedCount > 0 ? $"已休眠 {freedCount} 个后台标签，内存已即时释放！" : "当前无空闲后台标签需要休眠");
                Log("已执行指令: freememory");
                return;
            case "sidepanel":
                ToggleSidePanel();
                Log("已执行指令: sidepanel");
                return;
            case "qrcode":
                ShowQrCodeForActiveTab();
                Log("已执行指令: qrcode");
                return;
            case "screenshot":
                await CaptureScreenshotForActiveTabAsync();
                Log("已执行指令: screenshot");
                return;
            case "perf":
            case "diagnostics":
                ShowPagePerformanceCard();
                Log("已执行指令: perf");
                return;
            default:
                ShowToast($"未支持的指令: {commandId}");
                Log($"未支持的指令: {commandId}");
                return;
        }
    }

    private void ApplyThemeByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :theme dark|light|system", "示例: :theme dark");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "dark" or "night" or "d")
        {
            ApplyUiThemeFromMenu("Dark");
            return;
        }

        if (normalized is "light" or "day" or "l")
        {
            ApplyUiThemeFromMenu("Light");
            return;
        }

        if (normalized is "system" or "auto" or "s")
        {
            ApplyUiThemeFromMenu(ThemeOptionNames.ThemeSystem);
            return;
        }

        ShowCommandUsage("可选主题: dark | light | system", "示例: :theme system");
    }

    private async Task ApplyTabSwitchByCommandAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.All(item => string.IsNullOrWhiteSpace(item)))
        {
            OpenTabQuickSwitchPalette();
            return;
        }

        var keyword = string.Join(' ', args.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));
        if (string.IsNullOrWhiteSpace(keyword))
        {
            OpenTabQuickSwitchPalette();
            return;
        }

        await TryHandleTabQuickSwitchAsync($"@{keyword}");
    }

    private async Task ApplyTabOpsByCommandAsync(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :tabops closeothers|closeall|closeleft|closeright|closehost|focushost|closeblank|dedupe|suspendhost|suspendothers|suspendall|resumehost|resumeall|hoststats|stats", "示例: :tabops closehost");
            return;
        }

        var active = _tabManager.ActiveTab;
        if (active is null)
        {
            ShowToast("当前没有可操作的标签");
            return;
        }

        var action = raw.Trim().ToLowerInvariant();
        if (action is "stats" or "show" or "summary")
        {
            var summary = BuildTabOpsSummary();
            ShowToast($"标签统计: {summary}");
            Log($"标签统计: {summary}");
            return;
        }

        if (action is "hoststats" or "hosts" or "domains")
        {
            var hostSummary = BuildHostDistributionSummary(maxCount: 4);
            ShowToast($"站点分布: {hostSummary}");
            Log($"站点分布: {hostSummary}");
            return;
        }

        if (action is "closeothers" or "co" or "others")
        {
            var count = await CloseTabsAsync(_tabManager.Tabs.Where(tab => !ReferenceEquals(tab, active) && !tab.IsPinned));
            ShowToast(count == 0 ? "没有可关闭的其他标签" : $"已关闭其他标签: {count}");
            return;
        }

        if (action is "closeall" or "ca" or "all")
        {
            var count = await CloseTabsAsync(_tabManager.Tabs.Where(tab => !tab.IsPinned));
            ShowToast(count == 0 ? "没有可关闭的标签（固定标签已保留）" : $"已关闭标签: {count}");
            return;
        }

        if (action is "closeleft" or "cl" or "left")
        {
            var index = _tabControl.TabPages.IndexOf(active.TabPage);
            var count = await CloseTabsAsync(_tabManager.Tabs.Where(tab => _tabControl.TabPages.IndexOf(tab.TabPage) < index && !tab.IsPinned));
            ShowToast(count == 0 ? "左侧没有可关闭标签" : $"已关闭左侧标签: {count}");
            return;
        }

        if (action is "closeright" or "cr" or "right")
        {
            var index = _tabControl.TabPages.IndexOf(active.TabPage);
            var count = await CloseTabsAsync(_tabManager.Tabs.Where(tab => _tabControl.TabPages.IndexOf(tab.TabPage) > index && !tab.IsPinned));
            ShowToast(count == 0 ? "右侧没有可关闭标签" : $"已关闭右侧标签: {count}");
            return;
        }

        if (action is "closehost" or "ch" or "samehost")
        {
            var count = await CloseSameHostTabsAsync(active);
            ShowToast(count == 0 ? "没有可关闭的同站标签" : $"已关闭同站标签: {count}");
            return;
        }

        if (action is "focushost" or "fh" or "keephost" or "isolatehost")
        {
            var count = await FocusHostTabsAsync(active);
            ShowToast(count == 0 ? "没有可关闭的跨站标签" : $"已聚焦同站标签，关闭: {count}");
            return;
        }

        if (action is "closeblank" or "cb" or "blank" or "blanktabs")
        {
            var count = await CloseBlankTabsAsync(active);
            ShowToast(count == 0 ? "没有可关闭的空白标签" : $"已关闭空白标签: {count}");
            return;
        }

        if (action is "dedupe" or "cleanup" or "unique")
        {
            var count = await CloseDuplicateTabsAsync(active);
            ShowToast(count == 0 ? "没有可清理的重复标签" : $"已清理重复标签: {count}");
            return;
        }

        if (action is "suspendhost" or "sh" or "sleephost")
        {
            var count = await SuspendSameHostTabsAsync(active);
            ShowToast(count == 0 ? "没有可休眠的同站标签" : $"已休眠同站标签: {count}");
            return;
        }

        if (action is "suspendothers" or "so" or "sleepothers")
        {
            var count = await SuspendTabsAsync(_tabManager.Tabs.Where(tab => !ReferenceEquals(tab, active) && !tab.IsPinned));
            ShowToast(count == 0 ? "没有可休眠的其他标签" : $"已休眠其他标签: {count}");
            return;
        }

        if (action is "suspendall" or "sa" or "sleepall")
        {
            var count = await SuspendTabsAsync(_tabManager.Tabs.Where(tab => !ReferenceEquals(tab, active) && !tab.IsPinned));
            ShowToast(count == 0 ? "没有可休眠的标签" : $"已休眠标签: {count}");
            return;
        }

        if (action is "resumeall" or "ra" or "wakeall")
        {
            var count = await ResumeTabsAsync(_tabManager.Tabs.Where(tab => tab.IsSuspended));
            ShowToast(count == 0 ? "没有休眠标签需要恢复" : $"已恢复休眠标签: {count}");
            return;
        }

        if (action is "resumehost" or "rh" or "wakehost")
        {
            var count = await ResumeSameHostTabsAsync(active);
            ShowToast(count == 0 ? "没有可恢复的同站休眠标签" : $"已恢复同站休眠标签: {count}");
            return;
        }

        ShowCommandUsage("可选操作: closeothers | closeall | closeleft | closeright | closehost | focushost | closeblank | dedupe | suspendhost | suspendothers | suspendall | resumehost | resumeall | hoststats | stats", "示例: :tabops closeothers");
    }

    private async Task<int> CloseTabsAsync(IEnumerable<BrowserTab> tabs)
    {
        var count = 0;
        foreach (var tab in tabs.ToArray())
        {
            if (_tabManager.Tabs.All(item => !ReferenceEquals(item, tab)))
            {
                continue;
            }

            await _tabManager.CloseTabAsync(tab);
            count++;
        }

        RefreshToolbar();
        RefreshMetrics();
        if (count > 0)
        {
            Log($"标签批处理已完成，影响 {count} 个标签");
        }

        return count;
    }

    private async Task<int> SuspendTabsAsync(IEnumerable<BrowserTab> tabs)
    {
        var count = 0;
        foreach (var tab in tabs.ToArray())
        {
            if (tab.IsSuspended)
            {
                continue;
            }

            await tab.SuspendAsync();
            count++;
        }

        await _tabManager.ActivateSelectedTabAsync();
        RefreshToolbar();
        RefreshMetrics();
        if (count > 0)
        {
            Log($"标签批处理已完成，休眠 {count} 个标签");
        }

        return count;
    }

    private async Task<int> ResumeTabsAsync(IEnumerable<BrowserTab> tabs)
    {
        var count = 0;
        foreach (var tab in tabs.ToArray())
        {
            if (!tab.IsSuspended)
            {
                continue;
            }

            await tab.ResumeIfNeededAsync();
            count++;
        }

        await _tabManager.ActivateSelectedTabAsync();
        RefreshToolbar();
        RefreshMetrics();
        if (count > 0)
        {
            Log($"标签批处理已完成，恢复 {count} 个标签");
        }

        return count;
    }

    private async Task<int> CloseDuplicateTabsAsync(BrowserTab activeTab)
    {
        var tabs = _tabManager.Tabs.ToArray();
        if (tabs.Length <= 1)
        {
            return 0;
        }

        var activeKey = BuildTabIdentityKey(activeTab);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<BrowserTab>();

        foreach (var tab in tabs)
        {
            if (tab.IsPinned)
            {
                continue;
            }

            var key = BuildTabIdentityKey(tab);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(activeKey) &&
                string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase))
            {
                if (ReferenceEquals(tab, activeTab))
                {
                    seen.Add(key);
                }
                else
                {
                    duplicates.Add(tab);
                }

                continue;
            }

            if (!seen.Add(key))
            {
                duplicates.Add(tab);
            }
        }

        if (duplicates.Count == 0)
        {
            return 0;
        }

        return await CloseTabsAsync(duplicates);
    }

    private async Task<int> CloseSameHostTabsAsync(BrowserTab activeTab)
    {
        var activeHostKey = BuildTabHostKey(activeTab);
        if (string.IsNullOrWhiteSpace(activeHostKey))
        {
            return 0;
        }

        var sameHostTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab))
            .Where(tab => !tab.IsPinned)
            .Where(tab => string.Equals(BuildTabHostKey(tab), activeHostKey, StringComparison.OrdinalIgnoreCase));
        return await CloseTabsAsync(sameHostTabs);
    }

    private async Task<int> FocusHostTabsAsync(BrowserTab activeTab)
    {
        var activeHostKey = BuildTabHostKey(activeTab);
        if (string.IsNullOrWhiteSpace(activeHostKey))
        {
            return 0;
        }

        var tabsToClose = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab))
            .Where(tab => !tab.IsPinned)
            .Where(tab => !string.Equals(BuildTabHostKey(tab), activeHostKey, StringComparison.OrdinalIgnoreCase));
        return await CloseTabsAsync(tabsToClose);
    }

    private async Task<int> CloseBlankTabsAsync(BrowserTab activeTab)
    {
        var blankTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab))
            .Where(tab => !tab.IsPinned)
            .Where(IsBlankTab);
        return await CloseTabsAsync(blankTabs);
    }

    private async Task<int> SuspendSameHostTabsAsync(BrowserTab activeTab)
    {
        var activeHostKey = BuildTabHostKey(activeTab);
        if (string.IsNullOrWhiteSpace(activeHostKey))
        {
            return 0;
        }

        var sameHostTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab))
            .Where(tab => !tab.IsPinned)
            .Where(tab => !tab.IsSuspended)
            .Where(tab => string.Equals(BuildTabHostKey(tab), activeHostKey, StringComparison.OrdinalIgnoreCase));
        return await SuspendTabsAsync(sameHostTabs);
    }

    private async Task<int> ResumeSameHostTabsAsync(BrowserTab activeTab)
    {
        var activeHostKey = BuildTabHostKey(activeTab);
        if (string.IsNullOrWhiteSpace(activeHostKey))
        {
            return 0;
        }

        var sameHostTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab))
            .Where(tab => tab.IsSuspended)
            .Where(tab => string.Equals(BuildTabHostKey(tab), activeHostKey, StringComparison.OrdinalIgnoreCase));
        return await ResumeTabsAsync(sameHostTabs);
    }

    private static bool IsBlankTab(BrowserTab tab)
    {
        var uri = tab.CurrentUri;
        if (uri is null)
        {
            return true;
        }

        return uri.IsAbsoluteUri &&
               string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTabIdentityKey(BrowserTab tab)
    {
        var uri = tab.CurrentUri;
        if (uri is null)
        {
            return string.Empty;
        }

        if (uri.IsAbsoluteUri &&
            string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var absolute = uri.AbsoluteUri.Trim();
        if (string.IsNullOrWhiteSpace(absolute))
        {
            return string.Empty;
        }

        return absolute.TrimEnd('/').ToLowerInvariant();
    }

    private static string BuildTabHostKey(BrowserTab tab)
    {
        var uri = tab.CurrentUri;
        if (uri is null || string.IsNullOrWhiteSpace(uri.Host))
        {
            return string.Empty;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return uri.Host.Trim().ToLowerInvariant();
    }

    private string BuildTabOpsSummary()
    {
        var total = _tabManager.Tabs.Count;
        var pinned = _tabManager.Tabs.Count(tab => tab.IsPinned);
        var suspended = _tabManager.Tabs.Count(tab => tab.IsSuspended);
        var renamed = _tabManager.Tabs.Count(tab => tab.HasCustomTitle);
        var colored = _tabManager.Tabs.Count(tab => !string.IsNullOrWhiteSpace(BrowserTab.NormalizeColorTagKey(tab.ColorTagKey)));
        var duplicateUrls = _tabManager.Tabs
            .Select(BuildTabIdentityKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count() - 1);
        return $"总数:{total} 固定:{pinned} 休眠:{suspended} 命名:{renamed} 色标:{colored} 重复URL:{duplicateUrls}";
    }

    private string BuildHostDistributionSummary(int maxCount)
    {
        var groups = _tabManager.Tabs
            .Select(BuildTabHostKey)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .GroupBy(host => host, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxCount))
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToArray();
        return groups.Length == 0 ? "无有效站点标签" : string.Join(" | ", groups);
    }

    private void ApplyDensityByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :density compact|normal", "示例: :density compact");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "compact" or "dense" or "tight")
        {
            ApplyUiDensityFromMenu("Compact");
            return;
        }

        if (normalized is "normal" or "default" or "standard")
        {
            ApplyUiDensityFromMenu("Normal");
            return;
        }

        ShowCommandUsage("可选密度: compact | normal", "示例: :density normal");
    }

    private void ApplyAccentByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :accent blue|green|orange|rose", "示例: :accent green");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "blue" or "b")
        {
            ApplyUiAccentFromMenu("Blue");
            return;
        }

        if (normalized is "green" or "g")
        {
            ApplyUiAccentFromMenu("Green");
            return;
        }

        if (normalized is "orange" or "amber" or "o")
        {
            ApplyUiAccentFromMenu("Orange");
            return;
        }

        if (normalized is "rose" or "pink" or "r")
        {
            ApplyUiAccentFromMenu("Rose");
            return;
        }

        ShowCommandUsage("可选强调色: blue | green | orange | rose", "示例: :accent rose");
    }

    private void ApplyAccentStrengthByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :accentstrength soft|standard|vivid", "示例: :accentstrength vivid");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "soft" or "s")
        {
            ApplyUiAccentStrengthFromMenu("Soft");
            return;
        }

        if (normalized is "standard" or "normal" or "std")
        {
            ApplyUiAccentStrengthFromMenu("Standard");
            return;
        }

        if (normalized is "vivid" or "strong" or "v")
        {
            ApplyUiAccentStrengthFromMenu("Vivid");
            return;
        }

        ShowCommandUsage("可选强调强度: soft | standard | vivid", "示例: :accentstrength soft");
    }

    private void ApplyUiPresetByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :uipreset balanced|focus|vivid|cycle", "示例: :uipreset focus");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "cycle" or "next" or "switch")
        {
            var nextPreset = ResolveNextUiAppearancePreset();
            ApplyUiAppearancePreset(nextPreset);
            return;
        }

        var preset = ResolveUiAppearancePreset(normalized);
        if (preset is null)
        {
            ShowCommandUsage("可选预设: balanced | focus | vivid | cycle", "示例: :uipreset vivid");
            return;
        }

        ApplyUiAppearancePreset(preset);
    }

    private void ApplyRecentCommandsByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :recentcommands clear|show", "示例: :recentcommands clear");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "clear" or "reset" or "clean")
        {
            if (_recentAddressCommands.Count == 0)
            {
                ShowToast("最近命令已经为空");
                return;
            }

            _recentAddressCommands.Clear();
            PersistRecentAddressCommandsToConfig();
            HideAddressSuggestionPanel();
            ShowToast("已清空最近命令");
            Log("最近命令记录已清空");
            return;
        }

        if (normalized is "show" or "list" or "ls")
        {
            var summary = BuildRecentCommandsSummary(maxCount: 4);
            ShowToast($"最近命令: {summary}");
            Log($"最近命令: {summary}");
            return;
        }

        ShowCommandUsage("可选操作: clear | show", "示例: :recentcommands show");
    }

    private void ApplyPinCommandByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var actionRaw))
        {
            ShowCommandUsage("用法: :pincommand add|remove|list|clear <command>", "示例: :pincommand add settings");
            return;
        }

        var action = actionRaw.Trim().ToLowerInvariant();
        if (action is "list" or "show" or "ls")
        {
            var summary = BuildPinnedCommandsSummary(maxCount: 6);
            ShowToast($"常用命令: {summary}");
            Log($"常用命令: {summary}");
            return;
        }

        if (action is "clear" or "reset" or "clean")
        {
            if (_pinnedAddressCommandIds.Count == 0)
            {
                ShowToast("常用命令已经为空");
                return;
            }

            _pinnedAddressCommandIds.Clear();
            PersistPinnedAddressCommandsToConfig();
            ShowToast("已清空常用命令");
            Log("已清空常用命令");
            return;
        }

        if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            ShowCommandUsage("需要提供命令名，例如 settings 或 :settings", "示例: :pincommand remove settings");
            return;
        }

        if (!TryResolveAddressCommandByToken(args[1], out var targetCommand))
        {
            var commandHint = BuildAddressCommandHint(4);
            ShowToast($"命令不存在: {args[1]}");
            Log($"命令不存在: {args[1]}（可尝试 {commandHint}）");
            return;
        }

        if (action is "add" or "pin" or "save")
        {
            AddPinnedCommand(targetCommand.CommandId);
            ShowToast($"已固定命令: :{targetCommand.Command}");
            Log($"已固定常用命令: :{targetCommand.Command}");
            return;
        }

        if (action is "remove" or "unpin" or "delete")
        {
            var removed = RemovePinnedCommand(targetCommand.CommandId);
            if (!removed)
            {
                ShowToast($"命令未固定: :{targetCommand.Command}");
                return;
            }

            ShowToast($"已取消固定: :{targetCommand.Command}");
            Log($"已取消固定常用命令: :{targetCommand.Command}");
            return;
        }

        ShowCommandUsage("可选操作: add | remove | list | clear", "示例: :pincommand list");
    }

    private void ApplyStatusBarByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :statusbar detail|compact", "示例: :statusbar compact");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "detail" or "full" or "detailed")
        {
            ApplyStatusBarDetailFromMenu(detailed: true);
            return;
        }

        if (normalized is "compact" or "simple" or "brief")
        {
            ApplyStatusBarDetailFromMenu(detailed: false);
            return;
        }

        ShowCommandUsage("可选模式: detail | compact", "示例: :statusbar detail");
    }

    private void ApplyTabNameByCommand(IReadOnlyList<string> args)
    {
        var active = _tabManager.ActiveTab;
        if (active is null)
        {
            ShowToast("当前没有可重命名的标签");
            return;
        }

        if (args.Count == 0 || args.All(item => string.IsNullOrWhiteSpace(item)))
        {
            ShowCommandUsage("用法: :tabname <name>|clear", "示例: :tabname 工作文档");
            return;
        }

        var raw = string.Join(' ', args.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));
        var normalized = raw.ToLowerInvariant();
        if (normalized is "clear" or "reset" or "default")
        {
            active.SetCustomTitle(string.Empty);
            RefreshToolbar();
            RefreshMetrics();
            ShowToast("已恢复当前标签网页标题");
            return;
        }

        active.SetCustomTitle(raw);
        RefreshToolbar();
        RefreshMetrics();
        ShowToast($"当前标签已重命名为: {active.DisplayTitle}");
    }

    private void ApplyTabColorByCommand(IReadOnlyList<string> args)
    {
        var active = _tabManager.ActiveTab;
        if (active is null)
        {
            ShowToast("当前没有可设置色标的标签");
            return;
        }

        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :tabcolor blue|green|orange|rose|clear", "示例: :tabcolor orange");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        var targetKey = normalized switch
        {
            "blue" or "b" => "Blue",
            "green" or "g" => "Green",
            "orange" or "amber" or "o" => "Orange",
            "rose" or "pink" or "r" => "Rose",
            "clear" or "none" or "reset" => string.Empty,
            _ => null
        };
        if (targetKey is null)
        {
            ShowCommandUsage("可选色标: blue | green | orange | rose | clear", "示例: :tabcolor clear");
            return;
        }

        _tabManager.SetColorTag(active, targetKey);
        RefreshToolbar();
        RefreshMetrics();
        var label = ResolveTabColorText(active.ColorTagKey);
        ShowToast(string.IsNullOrWhiteSpace(label) ? "已清除当前标签色标" : $"当前标签色标: {label}");
    }

    private void ApplyZenByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :zen on|off|toggle", "示例: :zen on");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "toggle" or "switch" or "t")
        {
            ApplyZenMode(!_isZenMode, persist: true, showToast: true);
            return;
        }

        if (normalized is "on" or "enable" or "start")
        {
            ApplyZenMode(true, persist: true, showToast: true);
            return;
        }

        if (normalized is "off" or "disable" or "stop")
        {
            ApplyZenMode(false, persist: true, showToast: true);
            return;
        }

        ShowCommandUsage("可选模式: on | off | toggle", "示例: :zen toggle");
    }

    private void ApplyZenMode(bool enabled, bool persist, bool showToast)
    {
        _isZenMode = enabled;
        _topBar.Visible = !enabled;
        _addressBar.Visible = false;
        _statusBarPanel.Strip.Visible = !enabled;
        _bookmarksBar.Visible = !enabled && _config.ShowBookmarksBar;

        if (enabled)
        {
            HideAddressSuggestionPanel();
        }

        UpdateAppearanceButtonText();
        if (!enabled)
        {
            UpdateAddressSuggestionLocation();
        }

        UpdateToastLocation();
        RefreshToolbar();
        RefreshMetrics();
        Invalidate();

        if (persist)
        {
            _config.ZenMode = enabled;
            SaveConfigSilently();
            BuildAppearanceMenu();
        }

        if (showToast)
        {
            ShowToast(enabled ? "已开启沉浸模式（Ctrl+Shift+Z 可切换）" : "已退出沉浸模式");
        }
    }

    private void ResetUiAppearanceByCommand()
    {
        var changed = false;
        if (!string.Equals(_config.UiTheme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            _config.UiTheme = "Light";
            changed = true;
        }

        if (!string.Equals(_config.UiDensity, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            _config.UiDensity = "Normal";
            changed = true;
        }

        if (!string.Equals(_config.UiAccent, "Blue", StringComparison.OrdinalIgnoreCase))
        {
            _config.UiAccent = "Blue";
            changed = true;
        }

        if (!string.Equals(_config.UiAccentStrength, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            _config.UiAccentStrength = "Standard";
            changed = true;
        }

        if (!string.Equals(_config.UiFont, "YaHei", StringComparison.OrdinalIgnoreCase))
        {
            _config.UiFont = "YaHei";
            changed = true;
        }

        if (_config.ZenMode)
        {
            _config.ZenMode = false;
            changed = true;
        }

        if (!changed)
        {
            ShowToast("界面外观已是默认值");
            return;
        }

        ApplyRuntimeConfig();
        SaveConfigSilently();
        ShowToast("界面外观已恢复默认");
    }

    private void ApplyUiAppearancePreset(UiAppearancePreset preset)
    {
        var changed = false;
        if (!string.Equals(_config.UiTheme, preset.Theme, StringComparison.OrdinalIgnoreCase))
        {
            _config.UiTheme = preset.Theme;
            changed = true;
        }

        if (!string.Equals(_config.UiDensity, preset.Density, StringComparison.OrdinalIgnoreCase))
        {
            _config.UiDensity = preset.Density;
            changed = true;
        }

        if (!string.Equals(_config.UiAccent, preset.Accent, StringComparison.OrdinalIgnoreCase))
        {
            _config.UiAccent = preset.Accent;
            changed = true;
        }

        if (!string.Equals(_config.UiAccentStrength, preset.AccentStrength, StringComparison.OrdinalIgnoreCase))
        {
            _config.UiAccentStrength = preset.AccentStrength;
            changed = true;
        }

        if (!string.Equals(_config.UiFont, preset.Font, StringComparison.OrdinalIgnoreCase))
        {
            _config.UiFont = preset.Font;
            changed = true;
        }

        if (!changed)
        {
            ShowToast($"外观预设“{preset.DisplayName}”已生效");
            return;
        }

        ApplyRuntimeConfig();
        SaveConfigSilently();
        ShowToast($"已应用外观预设：{preset.DisplayName}");
    }

    private static UiAppearancePreset? ResolveUiAppearancePreset(string token)
    {
        return token switch
        {
            "balanced" or "default" or "normal" => UiAppearancePresets.First(preset => string.Equals(preset.Id, "balanced", StringComparison.OrdinalIgnoreCase)),
            "focus" or "work" or "study" => UiAppearancePresets.First(preset => string.Equals(preset.Id, "focus", StringComparison.OrdinalIgnoreCase)),
            "vivid" or "strong" or "bold" => UiAppearancePresets.First(preset => string.Equals(preset.Id, "vivid", StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private UiAppearancePreset ResolveNextUiAppearancePreset()
    {
        var currentIndex = Array.FindIndex(
            UiAppearancePresets,
            preset =>
                string.Equals(_config.UiTheme, preset.Theme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.UiDensity, preset.Density, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.UiAccent, preset.Accent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.UiAccentStrength, preset.AccentStrength, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.UiFont, preset.Font, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            return UiAppearancePresets[0];
        }

        var nextIndex = (currentIndex + 1) % UiAppearancePresets.Length;
        return UiAppearancePresets[nextIndex];
    }

    private bool TryResolveAddressCommandByToken(string rawToken, out AddressCommandDefinition command)
    {
        var token = string.IsNullOrWhiteSpace(rawToken)
            ? string.Empty
            : rawToken.Trim().TrimStart(':');
        if (string.IsNullOrWhiteSpace(token))
        {
            command = default!;
            return false;
        }

        command = GetActiveAddressCommands().FirstOrDefault(item =>
            string.Equals(item.CommandId, token, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Command, token, StringComparison.OrdinalIgnoreCase) ||
            item.Aliases.Any(alias => string.Equals(alias, token, StringComparison.OrdinalIgnoreCase)))!;
        return command is not null;
    }

    private void AddPinnedCommand(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return;
        }

        var normalizedId = commandId.Trim();
        _pinnedAddressCommandIds.RemoveAll(item => string.Equals(item, normalizedId, StringComparison.OrdinalIgnoreCase));
        _pinnedAddressCommandIds.Insert(0, normalizedId);
        if (_pinnedAddressCommandIds.Count > MaxPinnedAddressCommandCount)
        {
            _pinnedAddressCommandIds.RemoveRange(MaxPinnedAddressCommandCount, _pinnedAddressCommandIds.Count - MaxPinnedAddressCommandCount);
        }

        PersistPinnedAddressCommandsToConfig();
    }

    private bool RemovePinnedCommand(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return false;
        }

        var removedCount = _pinnedAddressCommandIds.RemoveAll(item =>
            string.Equals(item, commandId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            return false;
        }

        PersistPinnedAddressCommandsToConfig();
        return true;
    }

    private string BuildPinnedCommandsSummary(int maxCount)
    {
        var activeById = GetActiveAddressCommands()
            .GroupBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var entries = _pinnedAddressCommandIds
            .Take(Math.Max(1, maxCount))
            .Select(commandId =>
            {
                if (!activeById.TryGetValue(commandId, out var command))
                {
                    return null;
                }

                return $":{command.Command}";
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
        if (entries.Length == 0)
        {
            return "无";
        }

        return string.Join("  ", entries);
    }

    private string BuildRecentCommandsSummary(int maxCount)
    {
        var activeById = GetActiveAddressCommands()
            .GroupBy(item => item.CommandId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var entries = _recentAddressCommands
            .Take(Math.Max(1, maxCount))
            .Select(entry =>
            {
                if (!activeById.TryGetValue(entry.CommandId, out var command))
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(entry.ArgumentToken)
                    ? $":{command.Command}"
                    : $":{command.Command} {entry.ArgumentToken}";
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
        if (entries.Length == 0)
        {
            return "无";
        }

        return string.Join("  ", entries);
    }

    private static string ResolveTabColorText(string? colorKey)
    {
        var normalized = BrowserTab.NormalizeColorTagKey(colorKey);
        return normalized switch
        {
            "Blue" => "蓝色",
            "Green" => "青绿",
            "Orange" => "琥珀",
            "Rose" => "玫红",
            _ => string.Empty
        };
    }

    private void ApplyFontByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :font yahei|dengxian|song", "示例: :font yahei");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "yahei" or "yh")
        {
            ApplyUiFontFromMenu("YaHei");
            return;
        }

        if (normalized is "dengxian" or "dx")
        {
            ApplyUiFontFromMenu("DengXian");
            return;
        }

        if (normalized is "song" or "st")
        {
            ApplyUiFontFromMenu("Song");
            return;
        }

        ShowCommandUsage("可选字体: yahei | dengxian | song", "示例: :font song");
    }

    private void ApplyBookmarksBarByCommand(IReadOnlyList<string> args)
    {
        if (!TryGetFirstCommandArg(args, out var raw))
        {
            ShowCommandUsage("用法: :bookmarksbar on|off|toggle", "示例: :bookmarksbar off");
            return;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "toggle" or "switch" or "t")
        {
            ToggleBookmarksBarVisibility();
            return;
        }

        if (normalized is "on" or "show" or "open")
        {
            if (!_config.ShowBookmarksBar)
            {
                ToggleBookmarksBarVisibility();
            }
            else
            {
                ShowToast("书签栏已是显示状态");
            }

            return;
        }

        if (normalized is "off" or "hide" or "close")
        {
            if (_config.ShowBookmarksBar)
            {
                ToggleBookmarksBarVisibility();
            }
            else
            {
                ShowToast("书签栏已是隐藏状态");
            }

            return;
        }

        ShowCommandUsage("可选模式: on | off | toggle", "示例: :bookmarksbar toggle");
    }

    private void ShowCommandUsage(string usage, string example)
    {
        ShowToast(usage);
        Log($"{usage}，{example}");
    }

    private void OpenCommandPalette()
    {
        if (_isZenMode)
        {
            ApplyZenMode(false, persist: true, showToast: false);
        }

        _suppressAddressSuggestionRefresh = true;
        var current = _addressTextBox.Text ?? string.Empty;
        var normalized = current.TrimStart();
        if (!normalized.StartsWith(":", StringComparison.Ordinal))
        {
            _addressTextBox.Text = ":";
        }
        else if (string.IsNullOrWhiteSpace(normalized))
        {
            _addressTextBox.Text = ":";
        }

        _suppressAddressSuggestionRefresh = false;
        _addressTextBox.Focus();
        if (_addressTextBox.TextBox is { } textBox)
        {
            textBox.SelectionStart = textBox.TextLength;
            textBox.SelectionLength = 0;
        }

        ScheduleAddressSuggestionRefresh(immediate: true);
        ShowToast("命令面板已打开，输入关键词筛选");
    }

    private void OpenTabQuickSwitchPalette()
    {
        if (_isZenMode)
        {
            ApplyZenMode(false, persist: true, showToast: false);
        }

        _suppressAddressSuggestionRefresh = true;
        var current = _addressTextBox.Text ?? string.Empty;
        var normalized = current.TrimStart();
        if (!normalized.StartsWith("@", StringComparison.Ordinal))
        {
            _addressTextBox.Text = "@";
        }
        else if (string.IsNullOrWhiteSpace(normalized))
        {
            _addressTextBox.Text = "@";
        }

        _suppressAddressSuggestionRefresh = false;
        _addressTextBox.Focus();
        if (_addressTextBox.TextBox is { } textBox)
        {
            textBox.SelectionStart = textBox.TextLength;
            textBox.SelectionLength = 0;
        }

        ScheduleAddressSuggestionRefresh(immediate: true);
        ShowToast("标签快速切换已打开，输入 @关键词 跳转");
    }

    private void ShowAddressCommandHelp()
    {
        var commands = GetActiveAddressCommands()
            .OrderBy(item => item.Command)
            .Select(item =>
            {
                var description = string.IsNullOrWhiteSpace(item.Description) ? "无说明" : item.Description;
                return $":{item.Command} - {description}";
            })
            .ToArray();
        if (commands.Length == 0)
        {
            ShowToast("当前没有可用快捷指令");
            Log("当前没有可用快捷指令");
            return;
        }

        var previewLines = commands.Take(18).ToArray();
        var suffix = commands.Length > previewLines.Length
            ? $"{Environment.NewLine}... 共 {commands.Length} 条指令"
            : string.Empty;
        var pinnedSummary = BuildPinnedCommandsSummary(maxCount: 6);
        var header = $"常用命令: {pinnedSummary}{Environment.NewLine}{Environment.NewLine}";
        MessageBox.Show(
            this,
            header + string.Join(Environment.NewLine, previewLines) + suffix,
            "地址栏快捷指令帮助",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static bool TryGetFirstCommandArg(IReadOnlyList<string> args, out string argument)
    {
        if (args.Count == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            argument = string.Empty;
            return false;
        }

        argument = args[0];
        return true;
    }

    private async Task NavigateToBookmarkAsync(string bookmarkUrl, bool inNewTab)
    {
        if (TryHandleInternalBrowserUrl(bookmarkUrl, userInitiated: true))
        {
            return;
        }

        if (!Uri.TryCreate(bookmarkUrl, UriKind.Absolute, out var uri))
        {
            Log("书签地址无效");
            return;
        }

        if (inNewTab || _tabManager.ActiveTab is null)
        {
            await _tabManager.CreateTabAsync(uri);
            RefreshToolbar();
            return;
        }

        await _tabManager.ActiveTab.NavigateAsync(uri);
        RefreshToolbar();
    }

    private void RebuildBookmarksBar()
    {
        _bookmarksBar.Items.Clear();
        var titleButton = CreateBookmarkBarTitleButton();
        AttachBookmarkBarHoverEffect(titleButton);
        _bookmarksBar.Items.Add(titleButton);
        var isNarrow = ClientSize.Width < 1160;
        var isUltraNarrow = ClientSize.Width < 980;
        var topNameLength = isUltraNarrow ? 8 : isNarrow ? 12 : 16;
        var menuNameLength = isUltraNarrow ? 18 : 26;

        if (_bookmarkManager.TopLevelItems.Count == 0)
        {
            _bookmarksBar.Items.Add(CreateBookmarkBarEmptyLabel());
            UpdateAddressSuggestions();
            return;
        }

        foreach (var node in _bookmarkManager.TopLevelItems)
        {
            if (node.IsFolder)
            {
                var childCount = CountBookmarkNodes(node.Children);
                var folderDisplayName = BuildBookmarkDisplayText(node.Name, topNameLength);
                var folderButton = new ToolStripDropDownButton(folderDisplayName)
                {
                    Image = FaviconService.GetFolderIcon(),
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    TextImageRelation = TextImageRelation.ImageBeforeText,
                    ImageScaling = ToolStripItemImageScaling.None,
                    ShowDropDownArrow = false,
                    ToolTipText = $"{node.Name}\n文件夹（{childCount} 项）",
                    Tag = new BookmarkBarTag(node.Id, ParentFolderId: null)
                };
                StyleBookmarkBarItem(folderButton, isFolder: true);
                folderButton.MouseUp += BookmarkBarItemMouseUp;

                var capturedNode = node;
                var capturedMenuLength = menuNameLength;
                var isPopulated = false;
                folderButton.DropDownOpening += (_, _) =>
                {
                    if (isPopulated)
                    {
                        return;
                    }

                    isPopulated = true;
                    folderButton.DropDownItems.Clear();
                    AddFolderChildrenToDropDown(folderButton.DropDownItems, capturedNode.Children, capturedNode.Id, capturedMenuLength);
                };
                folderButton.DropDownItems.Add(new ToolStripMenuItem("加载中...") { Enabled = false });

                AttachBookmarkBarHoverEffect(folderButton);
                _bookmarksBar.Items.Add(folderButton);
                continue;
            }

            var bookmarkDisplayName = BuildBookmarkDisplayText(node.Name, topNameLength);
            var bookmarkButton = new ToolStripButton(bookmarkDisplayName)
            {
                Image = FaviconService.GetIcon(node),
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageScaling = ToolStripItemImageScaling.None,
                ToolTipText = $"{node.Name}\n{node.Url}",
                Tag = new BookmarkBarTag(node.Id, ParentFolderId: null)
            };
            StyleBookmarkBarItem(bookmarkButton, isFolder: false);
            bookmarkButton.Click += async (_, _) =>
            {
                var inNewTab = (ModifierKeys & Keys.Control) == Keys.Control;
                await NavigateToBookmarkAsync(node.Url, inNewTab: inNewTab);
            };
            bookmarkButton.MouseUp += BookmarkBarItemMouseUp;
            AttachBookmarkBarHoverEffect(bookmarkButton);
            _bookmarksBar.Items.Add(bookmarkButton);
        }

        var searchBookmarksButton = new ToolStripButton("🔍")
        {
            Alignment = ToolStripItemAlignment.Right,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "搜索书签 / 打开书签管理 (Ctrl+B)"
        };
        StyleBookmarkBarItem(searchBookmarksButton, isFolder: false);
        searchBookmarksButton.Click += (_, _) => ShowBookmarksSettingsWindow();
        AttachBookmarkBarHoverEffect(searchBookmarksButton);
        _bookmarksBar.Items.Add(searchBookmarksButton);

        _bookmarksBar.MouseUp -= BookmarkBarMouseUp;
        _bookmarksBar.MouseUp += BookmarkBarMouseUp;

        UpdateAddressSuggestions();
    }

    private void BookmarkBarMouseUp(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Right)
        {
            var hit = _bookmarksBar.GetItemAt(eventArgs.Location);
            if (hit is null)
            {
                ShowBookmarkContextMenu(null, null, Cursor.Position);
            }
        }
    }

    private static string BuildBookmarkDisplayText(string rawName, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(rawName) ? "(未命名)" : rawName.Trim();
        if (maxLength < 4 || normalized.Length <= maxLength)
        {
            return normalized;
        }

        return $"{normalized[..maxLength]}...";
    }

    private static int CountBookmarkNodes(IEnumerable<BookmarkNode> nodes)
    {
        var total = 0;
        foreach (var node in nodes)
        {
            total++;
            total += CountBookmarkNodes(node.Children);
        }

        return total;
    }

    private static string ResolveBookmarkGlyph(string? url, bool isUltraNarrow)
    {
        if (isUltraNarrow)
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri is null)
        {
            return "• ";
        }

        if (uri.IsFile)
        {
            return "文 ";
        }

        var host = uri.Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return "• ";
        }

        var token = host.FirstOrDefault(char.IsLetterOrDigit);
        if (token == default)
        {
            return "• ";
        }

        return $"{char.ToUpperInvariant(token)} ";
    }

    private void AddFolderChildrenToDropDown(
        ToolStripItemCollection items,
        IEnumerable<BookmarkNode> nodes,
        Guid? parentFolderId,
        int menuNameLength)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                var folderName = BuildBookmarkDisplayText(node.Name, menuNameLength);
                var folder = new ToolStripMenuItem(folderName)
                {
                    Image = FaviconService.GetFolderIcon(),
                    ImageScaling = ToolStripItemImageScaling.None,
                    ToolTipText = $"{node.Name}\n文件夹（{CountBookmarkNodes(node.Children)} 项）",
                    Tag = new BookmarkBarTag(node.Id, parentFolderId)
                };
                StyleBookmarkMenuItem(folder, isFolder: true);
                folder.MouseUp += BookmarkBarItemMouseUp;

                var capturedChild = node;
                var isChildPopulated = false;
                folder.DropDownOpening += (_, _) =>
                {
                    if (isChildPopulated)
                    {
                        return;
                    }

                    isChildPopulated = true;
                    folder.DropDownItems.Clear();
                    AddFolderChildrenToDropDown(folder.DropDownItems, capturedChild.Children, capturedChild.Id, menuNameLength);
                };
                folder.DropDownItems.Add(new ToolStripMenuItem("加载中...") { Enabled = false });

                items.Add(folder);
                continue;
            }

            var bookmarkName = BuildBookmarkDisplayText(node.Name, menuNameLength);
            var bookmark = new ToolStripMenuItem(bookmarkName)
            {
                Image = FaviconService.GetIcon(node),
                ImageScaling = ToolStripItemImageScaling.None,
                ToolTipText = $"{node.Name}\n{node.Url}",
                Tag = new BookmarkBarTag(node.Id, parentFolderId)
            };
            StyleBookmarkMenuItem(bookmark, isFolder: false);
            bookmark.Click += async (_, _) =>
            {
                var inNewTab = (ModifierKeys & Keys.Control) == Keys.Control;
                await NavigateToBookmarkAsync(node.Url, inNewTab: inNewTab);
            };
            bookmark.MouseUp += BookmarkBarItemMouseUp;
            items.Add(bookmark);
        }
    }

    private void BookmarkBarItemMouseUp(object? sender, MouseEventArgs eventArgs)
    {
        if (sender is not ToolStripItem item || item.Tag is not BookmarkBarTag tag)
        {
            return;
        }

        if (eventArgs.Button == MouseButtons.Middle)
        {
            var node = _bookmarkManager.FindNode(tag.NodeId);
            if (node?.IsLink == true && !string.IsNullOrWhiteSpace(node.Url))
            {
                _ = NavigateToBookmarkAsync(node.Url, inNewTab: true);
            }
            return;
        }

        if (eventArgs.Button != MouseButtons.Right)
        {
            return;
        }

        DismissTransientMenus();
        var parentFolderId = _bookmarkManager.FindParentId(tag.NodeId) ?? tag.ParentFolderId;
        ShowBookmarkContextMenu(tag.NodeId, parentFolderId, Cursor.Position);
    }

    private void ShowBookmarkContextMenu(Guid? nodeId, Guid? parentFolderId, Point screenPoint)
    {
        DismissTransientMenus();
        _contextNodeId = nodeId;
        _contextParentFolderId = parentFolderId;

        var node = nodeId is null ? null : _bookmarkManager.FindNode(nodeId.Value);
        var isLink = node?.IsLink == true;
        var hasNode = node is not null;

        _bookmarkCtxOpen.Enabled = isLink;
        _bookmarkCtxOpenNewTab.Enabled = isLink;
        _bookmarkCtxOpenNewTabBackground.Enabled = isLink;
        _bookmarkCtxCopyUrl.Enabled = isLink;
        _bookmarkCtxOpenAllInFolder.Enabled = node?.IsFolder == true && node.Children.Any(c => c.IsLink);
        _bookmarkCtxEdit.Enabled = hasNode;
        _bookmarkCtxDelete.Enabled = hasNode;
        _bookmarkCtxViewInSettings.Enabled = hasNode;
        _bookmarkCtxAdd.Enabled = true;
        _bookmarkCtxAddFolder.Enabled = true;

        _bookmarkContextMenu.Show(screenPoint);
    }

    private void RegisterGlobalMenuDismissHandlers()
    {
        AttachGlobalMouseDismissHandlers(this);
        _addressTextBox.TextBox.MouseDown -= HandleGlobalMouseDownDismiss;
        _addressTextBox.TextBox.MouseDown += HandleGlobalMouseDownDismiss;
    }

    private void AttachGlobalMouseDismissHandlers(Control root)
    {
        root.MouseDown -= HandleGlobalMouseDownDismiss;
        root.MouseDown += HandleGlobalMouseDownDismiss;
        root.ControlAdded -= HandleGlobalDismissControlAdded;
        root.ControlAdded += HandleGlobalDismissControlAdded;

        foreach (Control child in root.Controls)
        {
            AttachGlobalMouseDismissHandlers(child);
        }
    }

    private void HandleGlobalDismissControlAdded(object? sender, ControlEventArgs eventArgs)
    {
        if (eventArgs.Control is null)
        {
            return;
        }

        AttachGlobalMouseDismissHandlers(eventArgs.Control);
    }

    private void HandleGlobalMouseDownDismiss(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button is MouseButtons.Left or MouseButtons.Right or MouseButtons.Middle)
        {
            DismissTransientMenus();
        }
    }

    private void DismissTransientMenus()
    {
        CloseContextMenuIfVisible(_bookmarkContextMenu);
        CloseContextMenuIfVisible(_tabContextMenu);
        CloseContextMenuIfVisible(_windowModeContextMenu);
        CloseContextMenuIfVisible(_addressContextMenu);
        CloseVisibleDropDownItems(_titleBar.Items);
        CloseVisibleDropDownItems(_topBar.Items);
        CloseVisibleDropDownItems(_addressBar.Items);
        CloseVisibleDropDownItems(_bookmarksBar.Items);
    }

    private void TryShowTitleBarQuickMenu(MouseEventArgs eventArgs)
    {
        var hitItem = _titleBar.GetItemAt(eventArgs.Location);
        if (hitItem is not null && !ReferenceEquals(hitItem, _titleDragSpacer))
        {
            return;
        }

        DismissTransientMenus();
        var menu = new ContextMenuStrip();
        AppTheme.ApplyContextMenu(menu);
        // 中文注释：Closed 里同步 Dispose 会让 ModalMenuFilter 收尾时访问已释放菜单而崩溃，延迟到消息循环空闲后再释放。
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);

        var newTabItem = new ToolStripMenuItem("新建空白标签页");
        newTabItem.Click += async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri("about:blank"));
            RefreshToolbar();
        };
        menu.Items.Add(newTabItem);

        var reopenClosedItem = new ToolStripMenuItem("恢复最近关闭标签")
        {
            Enabled = _tabManager.RecentlyClosedTabs.Count > 0
        };
        reopenClosedItem.Click += async (_, _) =>
        {
            var reopened = await _tabManager.ReopenLastClosedTabAsync();
            if (reopened)
            {
                ShowToast("已恢复最近关闭标签");
                RefreshToolbar();
            }
        };
        menu.Items.Add(reopenClosedItem);

        var closeOthersItem = new ToolStripMenuItem("关闭其他非固定标签")
        {
            Enabled = _tabControl.TabCount > 1
        };
        closeOthersItem.Click += async (_, _) => await CloseOtherTabsKeepActiveAsync();
        menu.Items.Add(closeOthersItem);

        menu.Items.Add(new ToolStripSeparator());
        var saveWorkspaceItem = new ToolStripMenuItem("保存当前为工作区...");
        saveWorkspaceItem.Click += (_, _) => SaveWorkspaceSnapshot();
        menu.Items.Add(saveWorkspaceItem);

        var manageWorkspaceItem = new ToolStripMenuItem("管理工作区...");
        manageWorkspaceItem.Click += (_, _) => ShowSessionsWindow(workspaceOnlyMode: true);
        menu.Items.Add(manageWorkspaceItem);

        menu.Items.Add(new ToolStripSeparator());
        var toggleBookmarkBarItem = new ToolStripMenuItem(_config.ShowBookmarksBar ? "隐藏书签栏" : "显示书签栏");
        toggleBookmarkBarItem.Click += (_, _) => ToggleBookmarksBarVisibility();
        menu.Items.Add(toggleBookmarkBarItem);

        menu.Show(_titleBar, eventArgs.Location);
    }

    private void ShowSiteBadgeContextMenu(Point location)
    {
        DismissTransientMenus();
        var menu = new ContextMenuStrip();
        AppTheme.ApplyContextMenu(menu);
        // 中文注释：Closed 里同步 Dispose 会让 ModalMenuFilter 收尾时访问已释放菜单而崩溃，延迟到消息循环空闲后再释放。
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);

        var activeTab = _tabManager.ActiveTab;
        var activeUri = activeTab?.CurrentUri;
        var hasUri = activeUri is not null;
        var hasHost = hasUri && !string.IsNullOrWhiteSpace(activeUri!.Host);
        var activeHost = hasHost ? activeUri!.Host.Trim().ToLowerInvariant() : string.Empty;

        var copyUrlItem = new ToolStripMenuItem("复制完整地址")
        {
            Enabled = hasUri
        };
        copyUrlItem.Click += (_, _) =>
        {
            if (activeUri is null)
            {
                return;
            }

            Clipboard.SetText(activeUri.AbsoluteUri);
            ShowToast("已复制完整地址");
        };
        menu.Items.Add(copyUrlItem);

        var copyHostItem = new ToolStripMenuItem("复制域名")
        {
            Enabled = hasHost
        };
        copyHostItem.Click += (_, _) =>
        {
            if (!hasHost)
            {
                return;
            }

            Clipboard.SetText(activeUri!.Host);
            ShowToast($"已复制域名: {activeUri.Host}");
        };
        menu.Items.Add(copyHostItem);

        var openCurrentInNewTabItem = new ToolStripMenuItem("在新标签打开当前地址")
        {
            Enabled = hasUri
        };
        openCurrentInNewTabItem.Click += async (_, _) =>
        {
            if (activeUri is null)
            {
                return;
            }

            await _tabManager.CreateTabAsync(activeUri);
            RefreshToolbar();
            RefreshMetrics();
        };
        menu.Items.Add(openCurrentInNewTabItem);

        menu.Items.Add(new ToolStripSeparator());
        var closeSameHostItem = new ToolStripMenuItem("关闭同站非固定标签")
        {
            Enabled = activeTab is not null && hasHost
        };
        closeSameHostItem.Click += async (_, _) =>
        {
            if (activeTab is null)
            {
                return;
            }

            var count = await CloseSameHostTabsAsync(activeTab);
            ShowToast(count > 0 ? $"已关闭同站标签 {count} 个" : "没有可关闭的同站标签");
        };
        menu.Items.Add(closeSameHostItem);

        var suspendSameHostItem = new ToolStripMenuItem("休眠同站非固定标签")
        {
            Enabled = activeTab is not null && hasHost
        };
        suspendSameHostItem.Click += async (_, _) =>
        {
            if (activeTab is null)
            {
                return;
            }

            var count = await SuspendSameHostTabsAsync(activeTab);
            ShowToast(count > 0 ? $"已休眠同站标签 {count} 个" : "没有可休眠的同站标签");
        };
        menu.Items.Add(suspendSameHostItem);

        var resumeSameHostItem = new ToolStripMenuItem("恢复同站休眠标签")
        {
            Enabled = activeTab is not null && hasHost
        };
        resumeSameHostItem.Click += async (_, _) =>
        {
            if (activeTab is null)
            {
                return;
            }

            var count = await ResumeSameHostTabsAsync(activeTab);
            ShowToast(count > 0 ? $"已恢复同站休眠标签 {count} 个" : "没有可恢复的同站休眠标签");
        };
        menu.Items.Add(resumeSameHostItem);

        if (hasHost)
        {
            menu.Items.Add(new ToolStripSeparator());
            var hostStatsItem = new ToolStripMenuItem($"同站标签数: {_tabManager.Tabs.Count(tab => string.Equals(BuildTabHostKey(tab), activeHost, StringComparison.OrdinalIgnoreCase))}")
            {
                Enabled = false
            };
            menu.Items.Add(hostStatsItem);
        }

        if (_siteBadgeLabel.Owner is { } owner)
        {
            var anchor = new Point(_siteBadgeLabel.Bounds.Left + location.X, _siteBadgeLabel.Bounds.Top + location.Y);
            var screenPoint = owner.PointToScreen(anchor);
            menu.Show(screenPoint);
            return;
        }

        menu.Show(Cursor.Position);
    }

    private void ShowTabStripEmptyAreaMenu(Point location)
    {
        DismissTransientMenus();
        var menu = new ContextMenuStrip();
        AppTheme.ApplyContextMenu(menu);
        // 中文注释：Closed 里同步 Dispose 会让 ModalMenuFilter 收尾时访问已释放菜单而崩溃，延迟到消息循环空闲后再释放。
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);

        var newTabItem = new ToolStripMenuItem("新建标签页");
        newTabItem.Click += async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
            RefreshToolbar();
            RefreshMetrics();
        };
        menu.Items.Add(newTabItem);

        var reopenClosedItem = new ToolStripMenuItem("恢复最近关闭")
        {
            Enabled = _tabManager.RecentlyClosedTabs.Count > 0
        };
        reopenClosedItem.Click += async (_, _) =>
        {
            var reopened = await _tabManager.ReopenLastClosedTabAsync();
            if (!reopened)
            {
                return;
            }

            ShowToast("已恢复最近关闭标签");
            RefreshToolbar();
            RefreshMetrics();
        };
        menu.Items.Add(reopenClosedItem);

        menu.Items.Add(new ToolStripSeparator());
        var saveWorkspaceItem = new ToolStripMenuItem("保存当前为工作区...");
        saveWorkspaceItem.Click += (_, _) => SaveWorkspaceSnapshot();
        menu.Items.Add(saveWorkspaceItem);

        var manageWorkspaceItem = new ToolStripMenuItem("管理工作区...");
        manageWorkspaceItem.Click += (_, _) => ShowSessionsWindow(workspaceOnlyMode: true);
        menu.Items.Add(manageWorkspaceItem);

        menu.Show(_tabControl, location);
    }

    private void RestoreAddressBarTextFromActiveTab()
    {
        var current = _tabManager.ActiveTab?.CurrentUri?.AbsoluteUri ?? string.Empty;
        _addressTextBox.Text = current;
        _addressTextBox.SelectAll();
    }

    private void WireToolbarHoverEffects()
    {
        var interactiveItems = new ToolStripItem[]
        {
            _backButton, _forwardButton, _refreshButton, _newTabButton, _closeTabButton,
            _downloadButton, _historyButton, _addBookmarkButton, _toggleBookmarksBarButton,
            _settingsButton, _pluginButton, _searchEngineButton, _appearanceButton,
            _recentTabsButton, _sessionButton, _tabQuickButton, _workspaceButton,
            _commandPaletteButton, _tabSwitchButton,
            _verticalTabsToggleButton, _splitViewButton, _readerModeButton, _zoomPillButton,
            _titleNewTabButton
        };

        foreach (var item in interactiveItems)
        {
            AttachToolbarHoverEffect(item);
        }
    }

    private void AttachToolbarHoverEffect(ToolStripItem item)
    {
        item.MouseEnter += (_, _) =>
        {
            if (!_hoverBaseColors.ContainsKey(item))
            {
                _hoverBaseColors[item] = item.BackColor;
            }

            var isCloseButton = ReferenceEquals(item, _titleWindowCloseButton);
            var hoverColor = isCloseButton
                ? AppTheme.Mix(AppTheme.Danger, Color.White, AppTheme.IsDark ? 0.18F : 0.30F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, AppTheme.IsDark ? 0.42F : 0.66F);
            ToolStripItemColorAnimator.AnimateBackColor(item, hoverColor);
        };

        item.MouseLeave += (_, _) =>
        {
            if (_hoverBaseColors.TryGetValue(item, out var baseColor))
            {
                ToolStripItemColorAnimator.AnimateBackColor(item, baseColor, 180);
            }
        };
    }

    private void AttachBookmarkBarHoverEffect(ToolStripItem item)
    {
        item.MouseEnter += (_, _) =>
        {
            var hoverColor = AppTheme.IsDark
                ? Color.FromArgb(38, 255, 255, 255)
                : Color.FromArgb(20, 0, 0, 0);
            ToolStripItemColorAnimator.AnimateBackColor(item, hoverColor, 100);
        };

        item.MouseLeave += (_, _) =>
        {
            ToolStripItemColorAnimator.AnimateBackColor(item, Color.Transparent, 160);
        };
    }

    private void WireStatusBarActions()
    {
        _statusBarPanel.LoadTimeClicked += (_, _) => ShowPagePerformanceCard();
        _statusBarPanel.MemoryClicked += (_, _) => ShowTabStatsDialog();
        _statusBarPanel.ZoomResetClicked += (_, _) => _tabManager.ActiveTab?.SetZoomFactor(1.0);
        _statusBarPanel.AdBlockClicked += (_, _) => ShowSettings();

        _statusBarPanel.IpClicked += (_, _) =>
        {
            var text = string.IsNullOrWhiteSpace(_statusBarPanel.IpLabel.Text) ? "IP: 未知" : _statusBarPanel.IpLabel.Text.Trim();
            Clipboard.SetText(text);
            ShowToast("已复制IP状态信息");
            Log($"已复制IP状态: {text}");
        };

        _statusBarPanel.ModeClicked += (_, _) =>
        {
            _appearanceButton.ShowDropDown();
            Log("已打开外观菜单");
        };

        _statusBarPanel.ContextMenuRequested += (_, location) => ShowStatusBarContextMenu(location);
    }

    private void ShowTabStatsDialog()
    {
        var summary = BuildTabOpsSummary();
        var hostSummary = BuildHostDistributionSummary(maxCount: 6);
        MessageBox.Show(
            this,
            $"标签统计\n\n{summary}\n\n站点分布\n{hostSummary}",
            "标签统计",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        Log("已打开标签统计详情");
    }

    private void ShowStatusBarContextMenu(Point location)
    {
        var menu = new ContextMenuStrip();
        AppTheme.ApplyContextMenu(menu);
        // 中文注释：Closed 里同步 Dispose 会让 ModalMenuFilter 收尾时访问已释放菜单而崩溃，延迟到消息循环空闲后再释放。
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);

        var copyStatusItem = new ToolStripMenuItem("复制状态栏信息");
        copyStatusItem.Click += (_, _) =>
        {
            var text = $"状态: {_statusBarPanel.StatusLabel.Text} | {_statusBarPanel.ModeLabel.Text} | {_statusBarPanel.IpLabel.Text} | {_statusBarPanel.MemoryLabel.Text}";
            Clipboard.SetText(text);
            ShowToast("已复制状态栏信息");
        };
        menu.Items.Add(copyStatusItem);

        menu.Items.Add(new ToolStripSeparator());
        var toggleDetailItem = new ToolStripMenuItem(_config.DetailedStatusBar ? "切换为简洁状态栏" : "切换为详细状态栏");
        toggleDetailItem.Click += (_, _) => ApplyStatusBarDetailFromMenu(!_config.DetailedStatusBar);
        menu.Items.Add(toggleDetailItem);

        var refreshIpItem = new ToolStripMenuItem("刷新公网IP");
        refreshIpItem.Click += async (_, _) =>
        {
            _statusBarPanel.IpLabel.Text = "IP: 刷新中...";
            await LoadPublicIpInfoAsync();
            ShowToast("已刷新IP信息");
        };
        menu.Items.Add(refreshIpItem);

        menu.Items.Add(new ToolStripSeparator());
        var tabStatsItem = new ToolStripMenuItem("打开标签统计");
        tabStatsItem.Click += (_, _) => ShowTabStatsDialog();
        menu.Items.Add(tabStatsItem);

        // 中文注释：状态栏右键菜单聚合高频维护动作，减少在设置和命令里来回切换。
        menu.Show(_statusBarPanel.Strip, location);
    }

    private static string BuildCtrlEnterAddress(string? input)
    {
        var raw = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains(' ') ||
            raw.Contains('/'))
        {
            return string.Empty;
        }

        var hostCandidate = raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? raw
            : $"www.{raw}";
        if (!hostCandidate.Contains('.', StringComparison.Ordinal))
        {
            hostCandidate = $"{hostCandidate}.com";
        }
        else if (!hostCandidate.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
        {
            hostCandidate = $"{hostCandidate}.com";
        }

        return $"https://{hostCandidate}";
    }

    private static void CloseContextMenuIfVisible(ContextMenuStrip menu)
    {
        if (menu.Visible)
        {
            menu.Close(ToolStripDropDownCloseReason.AppClicked);
        }
    }

    private static void CloseVisibleDropDownItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is not ToolStripDropDownItem dropDownItem)
            {
                continue;
            }

            if (dropDownItem.DropDown.Visible)
            {
                dropDownItem.DropDown.Close(ToolStripDropDownCloseReason.AppClicked);
            }

            if (dropDownItem.HasDropDownItems)
            {
                CloseVisibleDropDownItems(dropDownItem.DropDownItems);
            }
        }
    }

    private Guid? ResolveContextAddFolderId()
    {
        if (_contextNodeId is null)
        {
            return null;
        }

        var node = _bookmarkManager.FindNode(_contextNodeId.Value);
        if (node?.IsFolder == true)
        {
            return node.Id;
        }

        return _contextParentFolderId;
    }

    private void AddBookmarkByContext()
    {
        var defaultUrl = TryGetActiveUri(out var currentUri) ? currentUri.AbsoluteUri : "https://example.com";
        if (!BookmarkEditDialog.ShowDialogForBookmark(
                this,
                "新增书签",
                defaultName: "新书签",
                defaultUrl: defaultUrl,
                out var name,
                out var url))
        {
            return;
        }

        var created = _bookmarkManager.AddBookmark(ResolveContextAddFolderId(), name, url);
        if (created is null)
        {
            MessageBox.Show("URL 无效。", "新增失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RebuildBookmarksBar();
        Log("已新增书签");
    }

    private void AddFolderByContext()
    {
        if (!BookmarkEditDialog.ShowDialogForFolder(this, "新增文件夹", "新建文件夹", out var name))
        {
            return;
        }

        _bookmarkManager.AddFolder(ResolveContextAddFolderId(), name);
        RebuildBookmarksBar();
        Log("已新增文件夹");
    }

    private void EditContextBookmarkNode()
    {
        if (_contextNodeId is null)
        {
            return;
        }

        var node = _bookmarkManager.FindNode(_contextNodeId.Value);
        if (node is null)
        {
            return;
        }

        if (node.IsFolder)
        {
            if (!BookmarkEditDialog.ShowDialogForFolder(this, "编辑文件夹", node.Name, out var folderName))
            {
                return;
            }

            if (!_bookmarkManager.UpdateNode(node.Id, folderName, null, out var folderError))
            {
                MessageBox.Show(folderError, "编辑失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            if (!BookmarkEditDialog.ShowDialogForBookmark(this, "编辑书签", node.Name, node.Url, out var name, out var url))
            {
                return;
            }

            if (!_bookmarkManager.UpdateNode(node.Id, name, url, out var error))
            {
                MessageBox.Show(error, "编辑失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        RebuildBookmarksBar();
        Log("书签已更新");
    }

    private void DeleteContextBookmarkNode()
    {
        if (_contextNodeId is null)
        {
            return;
        }

        var node = _bookmarkManager.FindNode(_contextNodeId.Value);
        if (node is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除“{node.Name}”？",
            "删除确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (!_bookmarkManager.RemoveNode(node.Id))
        {
            MessageBox.Show("删除失败。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RebuildBookmarksBar();
        Log("书签已删除");
    }

    private async Task OpenContextBookmarkAsync(bool inNewTab)
    {
        if (_contextNodeId is null)
        {
            return;
        }

        var node = _bookmarkManager.FindNode(_contextNodeId.Value);
        if (node?.IsLink != true)
        {
            return;
        }

        await NavigateToBookmarkAsync(node.Url, inNewTab);
    }

    private void ViewContextNodeInSettings()
    {
        if (_contextNodeId is null)
        {
            return;
        }

        if (_bookmarksSettingsWindow is null || _bookmarksSettingsWindow.IsDisposed)
        {
            _bookmarksSettingsWindow = new BookmarksSettingsWindow(
                _bookmarkManager,
                RebuildBookmarksBar,
                Log);
        }

        ShowOwnedWindow(_bookmarksSettingsWindow);
        _bookmarksSettingsWindow.FocusNode(_contextNodeId.Value);
    }

    private void ShowBookmarksSettingsWindow()
    {
        if (_bookmarksSettingsWindow is null || _bookmarksSettingsWindow.IsDisposed)
        {
            _bookmarksSettingsWindow = new BookmarksSettingsWindow(
                _bookmarkManager,
                RebuildBookmarksBar,
                Log);
        }

        ShowOwnedWindow(_bookmarksSettingsWindow);
    }

    private void AddCurrentPageBookmark()
    {
        if (!TryGetActiveUri(out var uri))
        {
            Log("当前页面不可收藏");
            return;
        }

        var defaultName = _tabManager.ActiveTab?.DisplayTitle ?? uri.AbsoluteUri;
        if (!BookmarkAddToFolderDialog.ShowDialog(this, _bookmarkManager, defaultName, out var folderId, out var bookmarkName))
        {
            return;
        }

        _bookmarkManager.AddOrUpdateByUrl(uri, bookmarkName, folderId);
        RebuildBookmarksBar();
        Log("已收藏当前页");
    }

    private void ShowSettings(int initialCategoryIndex = 0)
    {
        if (_settingsWindow is null || _settingsWindow.IsDisposed)
        {
            _settingsWindow = new SettingsWindow(
                _config,
                _downloadManager,
                _pluginManager,
                _credentialStore,
                _bookmarkManager,
                RebuildBookmarksBar,
                ApplyRuntimeConfig,
                Log,
                ShowShortcutsOverview);
        }

        if (initialCategoryIndex >= 0)
        {
            _settingsWindow.NavigateToCategory(initialCategoryIndex);
        }

        ShowOwnedWindow(_settingsWindow);
    }

    private bool TryHandleInternalBrowserUrl(string? rawUrl, bool userInitiated = false)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        // 启动尚未就绪、正在恢复会话或非用户主动显式操作时，绝不自动弹出任何外部窗口
        if (!_startupCompleted || _isRestoringSession || !userInitiated)
        {
            if (InternalUrlRouter.IsInternalBrowserUrl(rawUrl))
            {
                var html = InternalUrlRouter.BuildInternalRedirectHtml(rawUrl);
                if (_tabManager.ActiveTab is { } activeTab)
                {
                    _ = activeTab.NavigateToStringAsync(html);
                }
                return true;
            }
            return false;
        }

        var handled = InternalUrlRouter.TryRoute(
            rawUrl,
            categoryIndex => ShowSettings(categoryIndex),
            () => ShowBookmarksSettingsWindow(),
            () => ShowHistoryWindow(),
            () => ShowDownloadManager(),
            msg => ShowToast(msg),
            out var redirectHtml);

        if (handled)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(redirectHtml) && _tabManager.ActiveTab is { } currentTab)
        {
            _ = currentTab.NavigateToStringAsync(redirectHtml);
            return true;
        }

        return false;
    }

    private void ShowPagePerformanceCard()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is null || activeTab.CurrentUri is null)
        {
            ShowToast("当前无可用的页面用于性能诊断");
            return;
        }

        using var dialog = new PagePerformanceCardDialog(activeTab, ShowToast);
        dialog.ShowDialog(this);
    }

    // 中文注释：统一的快捷键一览入口；单例复用，每次打开前刷新数据（配置/插件可能已变更）。
    private void ShowShortcutsOverview()
    {
        if (_shortcutsOverviewWindow is null || _shortcutsOverviewWindow.IsDisposed)
        {
            _shortcutsOverviewWindow = new ShortcutsOverviewWindow(_config, _pluginManager.GetRunShortcutBindings());
        }

        _shortcutsOverviewWindow.RefreshData();
        ShowOwnedWindow(_shortcutsOverviewWindow);
    }

    private void ShowAbout()
    {
        using var aboutDialog = new AboutWindow();
        aboutDialog.ShowDialog(this);
    }

    /// <summary>
    /// 中文注释：统一的非模态子窗口展示入口；窗口已可见时再调 Show(owner) 会抛
    /// “已经可见的窗体不能显示为模式对话框”，此处改为激活并前置。
    /// </summary>
    private void ShowOwnedWindow(Form window)
    {
        if (!window.Visible)
        {
            window.Show(this);
        }
        else if (window.WindowState == FormWindowState.Minimized)
        {
            window.WindowState = FormWindowState.Normal;
        }

        window.BringToFront();
        window.Activate();
    }

    // 中文注释：构建插件下拉菜单（工具栏“插件”按钮与标题栏插件图标共用）：
    // 列出已安装插件（勾选态=已启用，点击切换），附固定到工具栏/重载/管理入口。
    private void BuildPluginDropDown(ToolStripDropDownButton target)
    {
        target.DropDownItems.Clear();
        var packages = _pluginManager.DiscoverPackages();
        if (packages.Count == 0)
        {
            target.DropDownItems.Add(new ToolStripMenuItem("（未安装插件）")
            {
                Enabled = false
            });
        }

        foreach (var package in packages)
        {
            var statusSuffix = package.Enabled ? string.Empty : "（已禁用）";
            if (package.Enabled && !package.IsLoadable)
            {
                statusSuffix = $"（{package.LoadStatus}）";
            }

            var item = new ToolStripMenuItem($"{package.Name}{statusSuffix}")
            {
                Checked = package.Enabled,
                ToolTipText = string.IsNullOrWhiteSpace(package.Description)
                    ? $"id: {package.Id}"
                    : $"{package.Description}\nid: {package.Id}"
            };
            var pluginId = package.Id;
            var pluginName = package.Name;
            var targetEnabled = !package.Enabled;
            // 中文注释：每个插件展开为子菜单：“在当前页面运行”是主动作（一键填充这类插件靠它触发），启停是次动作。
            var runItem = new ToolStripMenuItem("▶ 在当前页面运行")
            {
                Enabled = package.Enabled && package.IsLoadable,
                ToolTipText = "立即在当前标签页执行该插件脚本",
                // 中文注释：展示插件自定义快捷键（如 Ctrl+Shift+V），按键由页面桥/窗体双路径触发。
                ShortcutKeyDisplayString = string.IsNullOrWhiteSpace(package.RunShortcut) ? null : package.RunShortcut
            };
            runItem.Click += (_, _) => RunPluginOnCurrentPage(pluginId, pluginName);
            item.DropDownItems.Add(runItem);

            // 中文注释：插件声明了选项页（options_ui/options_page 转换而来）时提供“插件选项”入口。
            if (!string.IsNullOrWhiteSpace(package.OptionsPage))
            {
                var pluginDirectory = package.DirectoryPath;
                var optionsPage = package.OptionsPage;
                var optionsItem = new ToolStripMenuItem("⚙ 插件选项")
                {
                    ToolTipText = "打开该插件的设置页面（自定义规则/个人资料等）"
                };
                optionsItem.Click += (_, _) => OpenPluginOptionsPage(pluginDirectory, optionsPage, pluginName);
                item.DropDownItems.Add(optionsItem);
            }

            var toggleItem = new ToolStripMenuItem(package.Enabled ? "禁用" : "启用");
            toggleItem.Click += (_, _) => TogglePluginEnabled(pluginId, targetEnabled);
            item.DropDownItems.Add(toggleItem);
            target.DropDownItems.Add(item);
        }

        target.DropDownItems.Add(new ToolStripSeparator());
        // 中文注释：勾选即把插件固定为工具栏快捷按钮，取消勾选即移除。
        var pinMenu = new ToolStripMenuItem("固定到工具栏")
        {
            Enabled = packages.Count > 0,
            ToolTipText = "把常用插件固定在工具栏上，一键启停"
        };
        foreach (var package in packages)
        {
            var pluginId = package.Id;
            var pinItem = new ToolStripMenuItem(package.Name)
            {
                Checked = _config.PinnedPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase)
            };
            pinItem.Click += (_, _) => TogglePluginPinned(pluginId);
            pinMenu.DropDownItems.Add(pinItem);
        }

        target.DropDownItems.Add(pinMenu);

        var reloadItem = new ToolStripMenuItem("重载插件");
        reloadItem.Click += (_, _) =>
        {
            _pluginManager.Reload();
            ShowToast("插件已重载");
        };
        target.DropDownItems.Add(reloadItem);

        var manageItem = new ToolStripMenuItem("管理插件...");
        manageItem.Click += (_, _) => ShowPluginSettings();
        target.DropDownItems.Add(manageItem);
    }

    // 中文注释：打开设置窗口并直达“插件”分类页。
    private void ShowPluginSettings()
    {
        ShowSettings();
        _settingsWindow?.NavigateToPluginSection();
    }

    // 中文注释：在当前活动标签页上手动执行插件脚本，结果用 Toast 反馈。
    private async void RunPluginOnCurrentPage(string pluginId, string pluginName)
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null)
        {
            ShowToast("没有可运行的标签页");
            return;
        }

        try
        {
            var (ok, error) = await tab.RunPluginAsync(pluginId);
            ShowToast(ok ? $"已运行插件：{pluginName}" : $"运行失败：{error}");
            if (!ok)
            {
                Log($"[插件:{pluginId}] 运行失败: {error}");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"运行失败：{ex.Message}");
            Log($"[插件:{pluginId}] 运行异常: {ex.Message}");
        }
    }

    // 中文注释：新标签页打开插件选项页（file:// 地址，桥脚本会按目录前缀自动挂载全局 chrome.storage）。
    private async void OpenPluginOptionsPage(string pluginDirectory, string optionsPage, string pluginName)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(pluginDirectory, optionsPage));
            if (!fullPath.StartsWith(Path.GetFullPath(pluginDirectory), StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullPath))
            {
                ShowToast($"未找到插件选项页：{optionsPage}");
                return;
            }

            await _tabManager.CreateTabAsync(new Uri(fullPath));
            RefreshToolbar();
            RefreshMetrics();
        }
        catch (Exception ex)
        {
            ShowToast($"打开选项页失败：{ex.Message}");
            Log($"[插件:{pluginName}] 打开选项页异常: {ex.Message}");
        }
    }

    private void TogglePluginEnabled(string pluginId, bool enabled)
    {
        if (!_pluginManager.SetPluginEnabled(pluginId, enabled, out var error))
        {
            ShowToast($"插件操作失败：{error}");
            return;
        }

        _pluginManager.Reload();
        ShowToast(enabled ? "插件已启用，刷新页面后生效" : "插件已禁用，刷新页面后生效");
        Log($"[插件:{pluginId}] 已{(enabled ? "启用" : "禁用")}");
        // 中文注释：同步刷新固定按钮的勾选/背景态。
        RebuildPinnedPluginButtons();
    }

    // 中文注释：切换插件的工具栏固定状态并持久化。
    private void TogglePluginPinned(string pluginId)
    {
        var existing = _config.PinnedPluginIds
            .FirstOrDefault(id => string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _config.PinnedPluginIds.Add(pluginId);
            ShowToast("已固定到工具栏");
        }
        else
        {
            _config.PinnedPluginIds.Remove(existing);
            ShowToast("已取消固定");
        }

        SaveConfigSilently();
        RebuildPinnedPluginButtons();
    }

    // 中文注释：按配置重建固定插件按钮并刷新工具栏；插件被卸载后自动从配置清理。
    private void RebuildPinnedPluginButtons()
    {
        _pinnedPluginButtons.Clear();
        if (_config.PinnedPluginIds.Count > 0)
        {
            var packages = _pluginManager.DiscoverPackages();
            var validIds = new List<string>();
            foreach (var pinnedId in _config.PinnedPluginIds)
            {
                var package = packages.FirstOrDefault(p =>
                    string.Equals(p.Id, pinnedId, StringComparison.OrdinalIgnoreCase));
                if (package is null)
                {
                    continue;
                }

                validIds.Add(package.Id);
                var stateText = package.Enabled ? "已启用" : "已禁用";
                var button = new ToolStripButton(package.Name)
                {
                    AutoSize = true,
                    Margin = new Padding(3, 1, 3, 1),
                    BackColor = package.Enabled ? AppTheme.AccentHover : AppTheme.AccentMuted,
                    ForeColor = AppTheme.TextPrimary,
                    Font = AppTheme.CreateFont(9F, FontStyle.Regular),
                    Checked = package.Enabled,
                    ToolTipText = $"{package.Name}（{stateText}）\n点击：在当前页面运行该插件\n启停/取消固定请在“插件”下拉中操作"
                };
                var pluginId = package.Id;
                var pluginName = package.Name;
                var pluginEnabled = package.Enabled;
                // 中文注释：固定按钮点击即“在当前页面运行”，未启用时提示先启用；启停改到插件下拉里。
                button.Click += (_, _) =>
                {
                    if (!pluginEnabled)
                    {
                        ShowToast($"插件“{pluginName}”已禁用，请在“插件”下拉中启用后再运行");
                        return;
                    }

                    RunPluginOnCurrentPage(pluginId, pluginName);
                };
                _pinnedPluginButtons.Add(button);
            }

            // 中文注释：配置里残留的已卸载插件 id 顺手清掉。
            if (validIds.Count != _config.PinnedPluginIds.Count)
            {
                _config.PinnedPluginIds = validIds;
                SaveConfigSilently();
            }
        }

        RebuildTopBarItems();
    }

    private void BuildSearchEngineMenu()
    {
        _searchEngineButton.DropDownItems.Clear();
        var allOptions = SearchEngineCatalog.GetAll(_config);
        var matchedPreset = allOptions.FirstOrDefault(option =>
            string.Equals(option.Template, _config.DefaultSearchEngineUrlTemplate, StringComparison.OrdinalIgnoreCase));
        // 中文注释：菜单包含内置 + 自定义引擎，有关键字的展示地址栏前缀提示。
        foreach (var option in allOptions)
        {
            var displayName = string.IsNullOrWhiteSpace(option.Keyword)
                ? option.Name
                : $"{option.Name}（{option.Keyword} 关键词）";
            var item = new ToolStripMenuItem(displayName)
            {
                Checked = string.Equals(option.Name, _config.DefaultSearchEngineName, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(option.Template, _config.DefaultSearchEngineUrlTemplate, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => ApplySearchEngine(option.Name, option.Template);
            _searchEngineButton.DropDownItems.Add(item);
        }

        _searchEngineButton.DropDownItems.Add(new ToolStripSeparator());
        var customChecked = matchedPreset is null;
        var customCurrent = new ToolStripMenuItem(customChecked ? "自定义（当前）" : "自定义")
        {
            Checked = customChecked
        };
        customCurrent.Click += (_, _) => ShowSettings();
        _searchEngineButton.DropDownItems.Add(customCurrent);

        var customItem = new ToolStripMenuItem("在设置中自定义...");
        customItem.Click += (_, _) => ShowSettings();
        _searchEngineButton.DropDownItems.Add(customItem);

        UpdateSearchEngineButtonText();
    }

    private void ApplySearchEngine(string name, string template)
    {
        _config.DefaultSearchEngineName = name;
        _config.DefaultSearchEngineUrlTemplate = template;
        BuildSearchEngineMenu();
        SaveConfigSilently();
        Log($"默认搜索引擎已切换为 {name}");
    }

    private void UpdateSearchEngineButtonText()
    {
        var name = string.IsNullOrWhiteSpace(_config.DefaultSearchEngineName)
            ? "默认"
            : _config.DefaultSearchEngineName.Trim();
        _searchEngineButton.Text = "🔍";
        _searchEngineButton.Width = AppTheme.IsCompact ? 30 : 34;
        _searchEngineButton.ToolTipText = $"默认搜索引擎: {name}\n点击切换或自定义搜索引擎";
    }

    private void BuildAppearanceMenu()
    {
        _appearanceButton.DropDownItems.Clear();

        var lightItem = new ToolStripMenuItem("浅色主题")
        {
            Checked = string.Equals(_config.UiTheme, "Light", StringComparison.OrdinalIgnoreCase)
        };
        lightItem.Click += (_, _) => ApplyUiThemeFromMenu("Light");
        _appearanceButton.DropDownItems.Add(lightItem);

        var darkItem = new ToolStripMenuItem("深色主题")
        {
            Checked = string.Equals(_config.UiTheme, "Dark", StringComparison.OrdinalIgnoreCase)
        };
        darkItem.Click += (_, _) => ApplyUiThemeFromMenu("Dark");
        _appearanceButton.DropDownItems.Add(darkItem);

        var systemThemeItem = new ToolStripMenuItem("跟随系统")
        {
            Checked = string.Equals(_config.UiTheme, ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase),
            ToolTipText = "随 Windows 深浅色模式自动切换"
        };
        systemThemeItem.Click += (_, _) => ApplyUiThemeFromMenu(ThemeOptionNames.ThemeSystem);
        _appearanceButton.DropDownItems.Add(systemThemeItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        var normalDensityItem = new ToolStripMenuItem("标准密度")
        {
            Checked = string.Equals(_config.UiDensity, "Normal", StringComparison.OrdinalIgnoreCase)
        };
        normalDensityItem.Click += (_, _) => ApplyUiDensityFromMenu("Normal");
        _appearanceButton.DropDownItems.Add(normalDensityItem);

        var compactDensityItem = new ToolStripMenuItem("紧凑密度")
        {
            Checked = string.Equals(_config.UiDensity, "Compact", StringComparison.OrdinalIgnoreCase)
        };
        compactDensityItem.Click += (_, _) => ApplyUiDensityFromMenu("Compact");
        _appearanceButton.DropDownItems.Add(compactDensityItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        foreach (var preset in UiAppearancePresets)
        {
            var item = new ToolStripMenuItem($"预设: {preset.DisplayName}")
            {
                Checked = string.Equals(_config.UiTheme, preset.Theme, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(_config.UiDensity, preset.Density, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(_config.UiAccent, preset.Accent, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(_config.UiAccentStrength, preset.AccentStrength, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(_config.UiFont, preset.Font, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => ApplyUiAppearancePreset(preset);
            _appearanceButton.DropDownItems.Add(item);
        }

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        var fontYaHeiItem = new ToolStripMenuItem("字体: 雅黑")
        {
            Checked = string.Equals(_config.UiFont, "YaHei", StringComparison.OrdinalIgnoreCase)
        };
        fontYaHeiItem.Click += (_, _) => ApplyUiFontFromMenu("YaHei");
        _appearanceButton.DropDownItems.Add(fontYaHeiItem);

        var fontDengXianItem = new ToolStripMenuItem("字体: 等线")
        {
            Checked = string.Equals(_config.UiFont, "DengXian", StringComparison.OrdinalIgnoreCase)
        };
        fontDengXianItem.Click += (_, _) => ApplyUiFontFromMenu("DengXian");
        _appearanceButton.DropDownItems.Add(fontDengXianItem);

        var fontSongItem = new ToolStripMenuItem("字体: 宋体")
        {
            Checked = string.Equals(_config.UiFont, "Song", StringComparison.OrdinalIgnoreCase)
        };
        fontSongItem.Click += (_, _) => ApplyUiFontFromMenu("Song");
        _appearanceButton.DropDownItems.Add(fontSongItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        var accentBlueItem = new ToolStripMenuItem("强调色: 蓝")
        {
            Checked = string.Equals(_config.UiAccent, "Blue", StringComparison.OrdinalIgnoreCase)
        };
        accentBlueItem.Click += (_, _) => ApplyUiAccentFromMenu("Blue");
        _appearanceButton.DropDownItems.Add(accentBlueItem);

        var accentGreenItem = new ToolStripMenuItem("强调色: 青绿")
        {
            Checked = string.Equals(_config.UiAccent, "Green", StringComparison.OrdinalIgnoreCase)
        };
        accentGreenItem.Click += (_, _) => ApplyUiAccentFromMenu("Green");
        _appearanceButton.DropDownItems.Add(accentGreenItem);

        var accentOrangeItem = new ToolStripMenuItem("强调色: 琥珀")
        {
            Checked = string.Equals(_config.UiAccent, "Orange", StringComparison.OrdinalIgnoreCase)
        };
        accentOrangeItem.Click += (_, _) => ApplyUiAccentFromMenu("Orange");
        _appearanceButton.DropDownItems.Add(accentOrangeItem);

        var accentRoseItem = new ToolStripMenuItem("强调色: 玫红")
        {
            Checked = string.Equals(_config.UiAccent, "Rose", StringComparison.OrdinalIgnoreCase)
        };
        accentRoseItem.Click += (_, _) => ApplyUiAccentFromMenu("Rose");
        _appearanceButton.DropDownItems.Add(accentRoseItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        var accentSoftItem = new ToolStripMenuItem("强调强度: 柔和")
        {
            Checked = string.Equals(_config.UiAccentStrength, "Soft", StringComparison.OrdinalIgnoreCase)
        };
        accentSoftItem.Click += (_, _) => ApplyUiAccentStrengthFromMenu("Soft");
        _appearanceButton.DropDownItems.Add(accentSoftItem);

        var accentStandardItem = new ToolStripMenuItem("强调强度: 标准")
        {
            Checked = string.Equals(_config.UiAccentStrength, "Standard", StringComparison.OrdinalIgnoreCase)
        };
        accentStandardItem.Click += (_, _) => ApplyUiAccentStrengthFromMenu("Standard");
        _appearanceButton.DropDownItems.Add(accentStandardItem);

        var accentVividItem = new ToolStripMenuItem("强调强度: 鲜明")
        {
            Checked = string.Equals(_config.UiAccentStrength, "Vivid", StringComparison.OrdinalIgnoreCase)
        };
        accentVividItem.Click += (_, _) => ApplyUiAccentStrengthFromMenu("Vivid");
        _appearanceButton.DropDownItems.Add(accentVividItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());

        var statusDetailItem = new ToolStripMenuItem("状态栏: 详细")
        {
            Checked = _config.DetailedStatusBar
        };
        statusDetailItem.Click += (_, _) => ApplyStatusBarDetailFromMenu(detailed: true);
        _appearanceButton.DropDownItems.Add(statusDetailItem);

        var statusCompactItem = new ToolStripMenuItem("状态栏: 简洁")
        {
            Checked = !_config.DetailedStatusBar
        };
        statusCompactItem.Click += (_, _) => ApplyStatusBarDetailFromMenu(detailed: false);
        _appearanceButton.DropDownItems.Add(statusCompactItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());
        var zenModeItem = new ToolStripMenuItem("沉浸模式")
        {
            Checked = _isZenMode
        };
        zenModeItem.Click += (_, _) => ApplyZenMode(!_isZenMode, persist: true, showToast: true);
        _appearanceButton.DropDownItems.Add(zenModeItem);

        _appearanceButton.DropDownItems.Add(new ToolStripSeparator());
        var settingsItem = new ToolStripMenuItem("打开设置...");
        settingsItem.Click += (_, _) => ShowSettings();
        _appearanceButton.DropDownItems.Add(settingsItem);

        UpdateAppearanceButtonText();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowBackdrop.Apply(this, AppTheme.IsDark);
    }

    private void OnSystemUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General || !AppTheme.IsFollowingSystem)
        {
            return;
        }

        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        // 中文注释：SystemEvents 可能在非 UI 线程回调，先切回 UI 线程再处理。
        if (InvokeRequired)
        {
            BeginInvoke(() => OnSystemUserPreferenceChanged(sender, e));
            return;
        }

        if (AppTheme.IsSystemDarkTheme() == AppTheme.IsDark)
        {
            return;
        }

        ApplyRuntimeConfig();
        ShowToast(AppTheme.IsDark ? "已跟随系统切换到深色主题" : "已跟随系统切换到浅色主题");
    }

    private void ApplyUiThemeFromMenu(string theme)
    {
        var normalized = string.Equals(theme, ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase)
            ? ThemeOptionNames.ThemeSystem
            : string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        if (string.Equals(_config.UiTheme, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.UiTheme = normalized;
        ApplyRuntimeConfig();
        SaveConfigSilently();
        ShowToast(normalized switch
        {
            ThemeOptionNames.ThemeSystem => AppTheme.IsDark ? "已跟随系统主题（当前深色）" : "已跟随系统主题（当前浅色）",
            "Dark" => "已切换到深色主题",
            _ => "已切换到浅色主题"
        });
    }

    private void ApplyUiDensityFromMenu(string density)
    {
        var normalized = string.Equals(density, "Compact", StringComparison.OrdinalIgnoreCase) ? "Compact" : "Normal";
        if (string.Equals(_config.UiDensity, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.UiDensity = normalized;
        ApplyRuntimeConfig();
        SaveConfigSilently();
        ShowToast(normalized == "Compact" ? "已切换到紧凑密度" : "已切换到标准密度");
    }

    private void ApplyUiAccentFromMenu(string accent)
    {
        var normalized = accent switch
        {
            "Green" => "Green",
            "Orange" => "Orange",
            "Rose" => "Rose",
            _ => "Blue"
        };
        if (string.Equals(_config.UiAccent, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.UiAccent = normalized;
        ApplyRuntimeConfig();
        SaveConfigSilently();
        var accentText = normalized switch
        {
            "Green" => "青绿",
            "Orange" => "琥珀",
            "Rose" => "玫红",
            _ => "蓝"
        };
        ShowToast($"已切换强调色: {accentText}");
    }

    private void ApplyUiFontFromMenu(string fontPreset)
    {
        var normalized = fontPreset switch
        {
            "DengXian" => "DengXian",
            "Song" => "Song",
            _ => "YaHei"
        };
        if (string.Equals(_config.UiFont, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.UiFont = normalized;
        ApplyRuntimeConfig();
        SaveConfigSilently();
        var fontText = normalized switch
        {
            "DengXian" => "等线",
            "Song" => "宋体",
            _ => "雅黑"
        };
        ShowToast($"已切换界面字体: {fontText}");
    }

    private void ApplyUiAccentStrengthFromMenu(string strength)
    {
        var normalized = strength switch
        {
            "Soft" => "Soft",
            "Vivid" => "Vivid",
            _ => "Standard"
        };
        if (string.Equals(_config.UiAccentStrength, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _config.UiAccentStrength = normalized;
        ApplyRuntimeConfig();
        SaveConfigSilently();
        var strengthText = normalized switch
        {
            "Soft" => "柔和",
            "Vivid" => "鲜明",
            _ => "标准"
        };
        ShowToast($"已切换强调强度: {strengthText}");
    }

    private void ApplyStatusBarDetailFromMenu(bool detailed)
    {
        if (_config.DetailedStatusBar == detailed)
        {
            return;
        }

        _config.DetailedStatusBar = detailed;
        BuildAppearanceMenu();
        RefreshMetrics();
        UpdateModeLabelText();
        SaveConfigSilently();
        ShowToast(detailed ? "状态栏已切换为详细模式" : "状态栏已切换为简洁模式");
    }

    private void UpdateAppearanceButtonText()
    {
        var themeText = string.Equals(_config.UiTheme, "Dark", StringComparison.OrdinalIgnoreCase) ? "深色" : "浅色";
        var densityText = string.Equals(_config.UiDensity, "Compact", StringComparison.OrdinalIgnoreCase) ? "紧凑" : "标准";
        var fontText = _config.UiFont switch
        {
            "DengXian" => "等线",
            "Song" => "宋体",
            _ => "微软雅黑"
        };
        var accentText = _config.UiAccent switch
        {
            "Green" => "绿色",
            "Orange" => "橙色",
            "Rose" => "玫瑰红",
            _ => "经典蓝"
        };
        var strengthText = _config.UiAccentStrength switch
        {
            "Soft" => "柔和",
            "Vivid" => "鲜艳",
            _ => "标准"
        };
        var zenText = _isZenMode ? "沉浸模式" : "常规模式";
        _appearanceButton.Text = "🎨";
        _appearanceButton.Width = AppTheme.IsCompact ? 30 : 34;
        _appearanceButton.ToolTipText = $"外观主题\n当前: {themeText} · {densityText} · {fontText} · {accentText} · {strengthText} · {zenText}\n点击快速切换外观主题";
        UpdateModeLabelText();
    }

    private void UpdateModeLabelText()
    {
        var themeText = string.Equals(_config.UiTheme, "Dark", StringComparison.OrdinalIgnoreCase) ? "深色" : "浅色";
        var densityText = string.Equals(_config.UiDensity, "Compact", StringComparison.OrdinalIgnoreCase) ? "紧凑" : "标准";
        var accentText = _config.UiAccent switch
        {
            "Green" => "青绿",
            "Orange" => "琥珀",
            "Rose" => "玫红",
            _ => "蓝色"
        };
        var strengthText = _config.UiAccentStrength switch
        {
            "Soft" => "柔和",
            "Vivid" => "鲜明",
            _ => "标准"
        };
        var metricsMode = _config.DetailedStatusBar ? "详细" : "简洁";
        var zenText = _isZenMode ? "沉浸" : "常规";
        var activeTabColor = ResolveTabColorText(_tabManager.ActiveTab?.ColorTagKey);
        var hasCustomTabTitle = _tabManager.ActiveTab?.HasCustomTitle == true;
        _statusBarPanel.ModeLabel.Text = $"🎨 {themeText} · {accentText}";
        _statusBarPanel.ModeLabel.ToolTipText = $"外观主题: {themeText} | 强调色: {accentText} ({strengthText}) | 密度: {densityText} | 视图: {zenText} | 窗口: {_launchOptions.WindowModeText}" +
                                 (string.IsNullOrWhiteSpace(activeTabColor) ? string.Empty : $" | 标签色: {activeTabColor}") +
                                 (hasCustomTabTitle ? " | 标签名: 自定义" : string.Empty) +
                                 "\n点击可展开外观与个性化设置菜单";
    }

    private void UpdateAppTitleLabel()
    {
        _appTitleLabel.Text = "Lfsun浏览器";
        _appTitleLabel.ToolTipText = "双击打开工作区中心，右键查看更多快捷操作";
        _appTitleLabel.ForeColor = AppTheme.TextSecondary;
        UpdateTitleStatusLabels();
    }

    private void UpdateTitleStatusLabels()
    {
        var memoryMb = Process.GetCurrentProcess().PrivateMemorySize64 / 1024d / 1024d;
        var tabCount = _tabManager.Tabs.Count;
        var liveCount = _tabManager.LiveTabCount;
        var pinnedCount = _tabManager.Tabs.Count(tab => tab.IsPinned);
        var suspendedCount = _tabManager.Tabs.Count(tab => tab.IsSuspended);

        _titleTabStatsLabel.Text = $"Tab {tabCount}/{liveCount}";
        _titleTabStatsLabel.ToolTipText = $"标签总数 {tabCount}\n活跃标签 {liveCount}\n固定标签 {pinnedCount}\n休眠标签 {suspendedCount}\n点击查看标签统计";
        _titleMemoryStatsLabel.Text = $"Mem {memoryMb:F0}MB";
        _titleMemoryStatsLabel.ToolTipText = "当前进程内存占用\n点击查看标签统计";

        if (memoryMb >= 1800)
        {
            _titleMemoryStatsLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.64F)
                : AppTheme.Mix(AppTheme.Danger, Color.White, 0.82F);
        }
        else if (memoryMb >= 1200)
        {
            _titleMemoryStatsLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.64F)
                : AppTheme.Mix(AppTheme.Warning, Color.White, 0.82F);
        }
        else
        {
            _titleMemoryStatsLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.64F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.82F);
        }

        _titleTabStatsLabel.Visible = false;
        _titleMemoryStatsLabel.Visible = false;
    }

    private void UpdateTabQuickButtonText()
    {
        var tabs = _tabManager.Tabs;
        var active = _tabManager.ActiveTab;
        var isNarrow = ClientSize.Width < 1160;
        var isUltraNarrow = ClientSize.Width < 980;
        if (active is null || tabs.Count == 0)
        {
            _tabQuickButton.Text = isUltraNarrow ? "0" : "标签 0";
            _tabQuickButton.ToolTipText = "当前没有可用标签";
            _tabQuickButton.Width = isUltraNarrow
                ? (AppTheme.IsCompact ? 46 : 54)
                : (AppTheme.IsCompact ? 90 : 104);
            return;
        }

        var activeIndex = tabs
            .Select((tab, index) => new { tab, index })
            .FirstOrDefault(item => ReferenceEquals(item.tab, active))
            ?.index ?? 0;
        var displayTitle = string.IsNullOrWhiteSpace(active.DisplayTitle) ? "新标签页" : active.DisplayTitle.Trim();
        var maxTitleLength = isUltraNarrow ? 8 : isNarrow ? 12 : 18;
        var shortTitle = displayTitle.Length > maxTitleLength ? $"{displayTitle[..maxTitleLength]}..." : displayTitle;
        _tabQuickButton.Text = isUltraNarrow
            ? $"{activeIndex + 1}/{tabs.Count}"
            : $"标签[{activeIndex + 1}/{tabs.Count}] {shortTitle}";
        _tabQuickButton.ToolTipText = $"切换标签 ({activeIndex + 1}/{tabs.Count})\n{displayTitle}\n快捷键: Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+1..9 / Ctrl+Shift+T / Ctrl+Shift+PgUp/PgDn 或 Ctrl+Shift+←/→(移动) / Ctrl+Shift+W(关其他) / Ctrl+Shift+L(关左侧) / Ctrl+Shift+R(关右侧) / Ctrl+Shift+D(去重)";
        var measured = TextRenderer.MeasureText(_tabQuickButton.Text, _topBar.Font).Width + (AppTheme.IsCompact ? 20 : 24);
        _tabQuickButton.Width = isUltraNarrow
            ? Math.Clamp(measured, AppTheme.IsCompact ? 50 : 58, AppTheme.IsCompact ? 82 : 92)
            : isNarrow
                ? Math.Clamp(measured, AppTheme.IsCompact ? 112 : 126, AppTheme.IsCompact ? 218 : 242)
                : Math.Clamp(measured, AppTheme.IsCompact ? 166 : 186, AppTheme.IsCompact ? 312 : 360);
    }

    private void UpdateWorkspaceButtonBadge(int? preloadedCount = null)
    {
        var workspaceCount = preloadedCount ?? _namedSessionStore.ListSessions()
            .Count(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase));
        _cachedWorkspaceCount = workspaceCount;
        var policyText = GetWorkspaceOpenPolicyDisplayText(ResolveWorkspaceOpenPolicy());
        var isNarrow = ClientSize.Width < 1160;
        var isUltraNarrow = ClientSize.Width < 980;
        var workspaceText = isUltraNarrow
            ? (workspaceCount > 0 ? workspaceCount.ToString() : null)
            : isNarrow
                ? (workspaceCount > 0 ? $"工作区{workspaceCount}" : "工作区")
                : (workspaceCount > 0 ? $"工作区 {workspaceCount}" : "工作区");
        _workspaceButton.Text = ComposeIconButtonText(_workspaceButton, "▦", workspaceText);
        _workspaceButton.ToolTipText = workspaceCount > 0
            ? $"工作区中心：已保存 {workspaceCount} 个工作区，打开策略 {policyText}，重复风险 {_cachedWorkspaceDuplicateCount}，高负载 {_cachedWorkspaceHeavyCount}，待整理 {_cachedWorkspaceStaleCount}（Ctrl+Shift+S 保存 / Ctrl+Shift+O 打开）"
            : $"工作区中心：当前还没有保存的工作区（打开策略 {policyText}，Ctrl+Shift+S 保存 / Ctrl+Shift+O 打开）";
        var measuredWidth = TextRenderer.MeasureText(_workspaceButton.Text, _titleBar.Font).Width + (AppTheme.IsCompact ? 24 : 28) + (_workspaceButton.Image?.Width ?? 0);
        _workspaceButton.Width = isUltraNarrow
            ? Math.Clamp(measuredWidth, AppTheme.IsCompact ? 48 : 56, AppTheme.IsCompact ? 94 : 104)
            : isNarrow
                ? Math.Clamp(measuredWidth, AppTheme.IsCompact ? 86 : 98, AppTheme.IsCompact ? 146 : 166)
                : Math.Clamp(measuredWidth, AppTheme.IsCompact ? 100 : 116, AppTheme.IsCompact ? 164 : 188);
        UpdateAppTitleLabel();
    }

    private void UpdateResponsiveTopBarVisuals()
    {
        var isNarrow = ClientSize.Width < 1160;
        var isUltraNarrow = ClientSize.Width < 980;
        var isCompactMode = ClientSize.Width < 900;

        ApplyAdaptiveTopToolbarLabels(isNarrow, isUltraNarrow);
        _commandPaletteButton.Visible = !isCompactMode;
        _tabSwitchButton.Visible = !isCompactMode;
        _searchEngineButton.Visible = !isCompactMode;
        _pluginButton.Visible = !isUltraNarrow;

        _siteBadgeLabel.Visible = !isCompactMode;
        _siteBadgeLabel.Margin = AppTheme.IsCompact ? new Padding(3, 1, 1, 1) : new Padding(4, 2, 1, 2);

        UpdateTabQuickButtonText();
        UpdateWorkspaceButtonBadge();
        UpdateAppearanceButtonText();
        UpdateSiteBadge();
    }

    private void ApplyAdaptiveTopToolbarLabels(bool isNarrow, bool isUltraNarrow)
    {
        var compact = AppTheme.IsCompact;
        var iconWidth = compact ? 32 : 36;
        var iconHeight = compact ? 26 : 30;

        void FormatIconButton(ToolStripButton button, string icon, string tooltip, string? shortcut = null)
        {
            button.Text = icon;
            button.AutoSize = false;
            button.Width = iconWidth;
            button.Height = iconHeight;
            button.Margin = new Padding(2, 1, 2, 1);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.ToolTipText = BuildToolbarToolTip(tooltip, shortcut);
        }

        FormatIconButton(_backButton, "←", "后退", "Alt+←");
        FormatIconButton(_forwardButton, "→", "前进", "Alt+→");
        FormatIconButton(_refreshButton, "↻", "刷新", "F5 / Ctrl+R");
        FormatIconButton(_newTabButton, "+", "新建空白标签", "Ctrl+T");
        FormatIconButton(_closeTabButton, "×", "关闭当前标签", "Ctrl+W");
        FormatIconButton(_downloadButton, "↓", "下载管理", "Ctrl+J");
        FormatIconButton(_historyButton, "⏱", "历史记录", "Ctrl+H");
        FormatIconButton(_addBookmarkButton, "★", "收藏当前页", "Ctrl+D");
        FormatIconButton(_settingsButton, "⚙", "设置", "Ctrl+,");

        _multiWindowButton.Text = "❐";
        _multiWindowButton.AutoSize = false;
        _multiWindowButton.Width = iconWidth;
        _multiWindowButton.Height = iconHeight;
        _multiWindowButton.Margin = new Padding(1, 1, 1, 1);
        _multiWindowButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _multiWindowButton.ToolTipText = "多开 / 双开窗口 (独立/共享/无痕)";

        _pluginButton.Text = "✇";
        _pluginButton.AutoSize = false;
        _pluginButton.Width = iconWidth;
        _pluginButton.Height = iconHeight;
        _pluginButton.Margin = new Padding(1, 1, 1, 1);
        _pluginButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _pluginButton.ToolTipText = "插件与扩展\n查看/启停已安装插件，支持重载与管理";

        _recentTabsButton.Text = "⟲";
        _recentTabsButton.AutoSize = false;
        _recentTabsButton.Width = compact ? 30 : 34;
        _recentTabsButton.Height = iconHeight;
        _recentTabsButton.Margin = new Padding(1, 1, 1, 1);
        _recentTabsButton.DisplayStyle = ToolStripItemDisplayStyle.Text;

        _sessionButton.Text = "☰";
        _sessionButton.AutoSize = false;
        _sessionButton.Width = compact ? 30 : 34;
        _sessionButton.Height = iconHeight;
        _sessionButton.Margin = new Padding(1, 1, 1, 1);
        _sessionButton.DisplayStyle = ToolStripItemDisplayStyle.Text;

        _tabQuickButton.Text = "❖";
        _tabQuickButton.AutoSize = false;
        _tabQuickButton.Width = compact ? 30 : 34;
        _tabQuickButton.Height = iconHeight;
        _tabQuickButton.Margin = new Padding(1, 1, 1, 1);
        _tabQuickButton.DisplayStyle = ToolStripItemDisplayStyle.Text;

        _commandPaletteButton.Text = "⌘";
        _commandPaletteButton.AutoSize = false;
        _commandPaletteButton.Width = compact ? 28 : 32;
        _commandPaletteButton.Height = iconHeight;
        _commandPaletteButton.Margin = new Padding(1, 1, 1, 1);

        _tabSwitchButton.Text = "@";
        _tabSwitchButton.AutoSize = false;
        _tabSwitchButton.Width = compact ? 28 : 32;
        _tabSwitchButton.Height = iconHeight;
        _tabSwitchButton.Margin = new Padding(1, 1, 1, 1);
    }

    private void ApplyAdaptiveToolbarButton(
        ToolStripButton button,
        string iconText,
        string fullText,
        string compactText,
        bool verboseMode,
        bool compactMode,
        bool iconOnlyMode,
        int minWidth)
    {
        // 中文注释：根据窗口宽度在“图标+文字 / 简写 / 仅图标”间切换，保证信息密度和可读性平衡。
        var adaptiveText = iconOnlyMode
            ? null
            : verboseMode
                ? fullText
                : compactMode
                    ? compactText
                    : null;
        button.Text = ComposeIconButtonText(button, iconText, adaptiveText);
        SetToolbarButtonWidth(button, minWidth);
    }

    private void BuildTabQuickDropDown()
    {
        _tabQuickButton.DropDownItems.Clear();
        var tabs = _tabManager.Tabs;
        if (tabs.Count == 0)
        {
            _tabQuickButton.DropDownItems.Add(new ToolStripMenuItem("暂无标签")
            {
                Enabled = false
            });
            return;
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var tabIndex = i;
            var title = string.IsNullOrWhiteSpace(tab.DisplayTitle) ? "新标签页" : tab.DisplayTitle.Trim();
            var shortTitle = title.Length > 30 ? $"{title[..30]}..." : title;
            var isActive = ReferenceEquals(tab, _tabManager.ActiveTab);
            var prefix = isActive ? "◆" : tab.IsPinned ? "●" : " ";
            var item = new ToolStripMenuItem($"{prefix} #{tabIndex + 1} {shortTitle}")
            {
                ToolTipText = tab.CurrentUri?.AbsoluteUri ?? "about:blank",
                Checked = isActive
            };
            item.Click += (_, _) =>
            {
                if (tabIndex < 0 || tabIndex >= _tabControl.TabCount)
                {
                    return;
                }

                _tabControl.SelectedIndex = tabIndex;
            };
            _tabQuickButton.DropDownItems.Add(item);
            if (tabIndex >= 14)
            {
                break;
            }
        }

        _tabQuickButton.DropDownItems.Add(new ToolStripSeparator());
        var saveWorkspaceItem = new ToolStripMenuItem("保存当前为工作区...");
        saveWorkspaceItem.Click += (_, _) => SaveWorkspaceSnapshot();
        _tabQuickButton.DropDownItems.Add(saveWorkspaceItem);

        var workspaces = _namedSessionStore.ListSessions()
            .Where(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();
        if (workspaces.Count == 0)
        {
            _tabQuickButton.DropDownItems.Add(new ToolStripMenuItem("暂无工作区")
            {
                Enabled = false
            });
        }
        else
        {
            foreach (var workspace in workspaces)
            {
                var item = new ToolStripMenuItem($"打开工作区：{GetWorkspaceDisplayName(workspace.Name)}")
                {
                    ToolTipText = $"保存于 {workspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {workspace.TabCount} 个标签"
                };
                item.Click += async (_, _) => await RestoreNamedSessionIntoCurrentWindowAsync(workspace.Name);
                _tabQuickButton.DropDownItems.Add(item);
            }
        }

        _tabQuickButton.DropDownItems.Add(new ToolStripSeparator());
        var manageWorkspaceItem = new ToolStripMenuItem("管理工作区...");
        manageWorkspaceItem.Click += (_, _) => ShowSessionsWindow(workspaceOnlyMode: true);
        _tabQuickButton.DropDownItems.Add(manageWorkspaceItem);
    }

    private void BuildRecentTabsDropDown()
    {
        _recentTabsButton.DropDownItems.Clear();
        if (_tabManager.RecentlyClosedTabs.Count == 0)
        {
            _recentTabsButton.DropDownItems.Add(new ToolStripMenuItem("暂无最近关闭标签")
            {
                Enabled = false
            });
            return;
        }

        foreach (var entry in _tabManager.RecentlyClosedTabs.Take(12))
        {
            var title = string.IsNullOrWhiteSpace(entry.Title) ? "未命名标签页" : entry.Title.Trim();
            var item = new ToolStripMenuItem(title)
            {
                ToolTipText = entry.Url
            };
            item.Click += async (_, _) =>
            {
                if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) || uri is null)
                {
                    return;
                }

                await _tabManager.CreateTabAsync(uri);
                RefreshToolbar();
                RefreshMetrics();
            };
            _recentTabsButton.DropDownItems.Add(item);
        }
    }

    private void BuildSessionDropDown()
    {
        _sessionButton.DropDownItems.Clear();

        var session = _sessionStore.Load();
        var namedSessions = _namedSessionStore.ListSessions();
        var hasSavedSession = session is not null && session.Tabs.Count > 0;
        var restoreItem = new ToolStripMenuItem("恢复已保存会话")
        {
            Enabled = hasSavedSession
        };
        restoreItem.Click += async (_, _) => await RestoreSavedSessionIntoCurrentWindowAsync();
        _sessionButton.DropDownItems.Add(restoreItem);

        var saveItem = new ToolStripMenuItem("立即保存当前会话");
        saveItem.Click += (_, _) => SaveCurrentSessionSnapshot();
        _sessionButton.DropDownItems.Add(saveItem);

        var saveAsNamedItem = new ToolStripMenuItem("另存为命名会话");
        saveAsNamedItem.Click += (_, _) => SaveNamedSessionSnapshot();
        _sessionButton.DropDownItems.Add(saveAsNamedItem);

        var clearItem = new ToolStripMenuItem("清除已保存会话")
        {
            Enabled = hasSavedSession
        };
        clearItem.Click += (_, _) => ClearSavedSessionSnapshot();
        _sessionButton.DropDownItems.Add(clearItem);

        var openNamedMenu = new ToolStripMenuItem("打开命名会话")
        {
            Enabled = namedSessions.Count > 0
        };
        foreach (var namedSession in namedSessions.Take(12))
        {
            var item = new ToolStripMenuItem(namedSession.Name)
            {
                ToolTipText = $"保存于 {namedSession.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {namedSession.TabCount} 个标签"
            };
            item.Click += async (_, _) => await RestoreNamedSessionIntoCurrentWindowAsync(namedSession.Name);
            openNamedMenu.DropDownItems.Add(item);
        }
        _sessionButton.DropDownItems.Add(openNamedMenu);

        var openNamedInSharedMenu = new ToolStripMenuItem("在新共享窗口打开命名会话")
        {
            Enabled = namedSessions.Count > 0
        };
        foreach (var namedSession in namedSessions.Take(12))
        {
            var item = new ToolStripMenuItem(namedSession.Name)
            {
                ToolTipText = $"保存于 {namedSession.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {namedSession.TabCount} 个标签"
            };
            item.Click += (_, _) => OpenNamedSessionInSharedWindow(namedSession.Name);
            openNamedInSharedMenu.DropDownItems.Add(item);
        }
        _sessionButton.DropDownItems.Add(openNamedInSharedMenu);

        var openNamedInIsolatedMenu = new ToolStripMenuItem("在新独立窗口打开命名会话")
        {
            Enabled = namedSessions.Count > 0
        };
        foreach (var namedSession in namedSessions.Take(12))
        {
            var item = new ToolStripMenuItem(namedSession.Name)
            {
                ToolTipText = $"保存于 {namedSession.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {namedSession.TabCount} 个标签"
            };
            item.Click += (_, _) => OpenNamedSessionInIsolatedWindow(namedSession.Name);
            openNamedInIsolatedMenu.DropDownItems.Add(item);
        }
        _sessionButton.DropDownItems.Add(openNamedInIsolatedMenu);

        var openNamedInPrivateMenu = new ToolStripMenuItem("在新无痕窗口打开命名会话")
        {
            Enabled = namedSessions.Count > 0
        };
        foreach (var namedSession in namedSessions.Take(12))
        {
            var item = new ToolStripMenuItem(namedSession.Name)
            {
                ToolTipText = $"保存于 {namedSession.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {namedSession.TabCount} 个标签"
            };
            item.Click += (_, _) => OpenNamedSessionInPrivateWindow(namedSession.Name);
            openNamedInPrivateMenu.DropDownItems.Add(item);
        }
        _sessionButton.DropDownItems.Add(openNamedInPrivateMenu);

        var deleteNamedMenu = new ToolStripMenuItem("删除命名会话")
        {
            Enabled = namedSessions.Count > 0
        };
        foreach (var namedSession in namedSessions.Take(12))
        {
            var item = new ToolStripMenuItem(namedSession.Name)
            {
                ToolTipText = $"保存于 {namedSession.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            };
            item.Click += (_, _) => DeleteNamedSessionSnapshot(namedSession.Name);
            deleteNamedMenu.DropDownItems.Add(item);
        }
        _sessionButton.DropDownItems.Add(deleteNamedMenu);

        _sessionButton.DropDownItems.Add(new ToolStripSeparator());
        var manageItem = new ToolStripMenuItem("管理命名会话...");
        manageItem.Click += (_, _) => ShowSessionsWindow(workspaceOnlyMode: false);
        _sessionButton.DropDownItems.Add(manageItem);

        if (session is not null)
        {
            _sessionButton.DropDownItems.Add(new ToolStripSeparator());
            var summary = session.ExitedCleanly
                ? $"已保存 {session.Tabs.Count} 个标签"
                : $"已保存快照 {session.Tabs.Count} 个标签";
            _sessionButton.DropDownItems.Add(new ToolStripMenuItem(summary)
            {
                Enabled = false
            });
        }

        if (namedSessions.Count > 0)
        {
            var latestNamed = namedSessions[0];
            _sessionButton.DropDownItems.Add(new ToolStripMenuItem(
                $"最近命名会话：{latestNamed.Name} ({latestNamed.TabCount} 标签)")
            {
                Enabled = false
            });
        }
    }

    private void BuildWorkspaceDropDown()
    {
        _workspaceButton.DropDownItems.Clear();
        var namedSessions = _namedSessionStore.ListSessions();
        var workspaces = namedSessions
            .Where(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
            .Take(24)
            .ToList();
        UpdateWorkspaceButtonBadge(workspaces.Count);
        var hasActiveTab = _tabManager.ActiveTab?.CurrentUri is not null;
        var overview = CalculateWorkspaceOverview(workspaces);
        _cachedWorkspaceDuplicateCount = overview.DuplicateCount;
        _cachedWorkspaceHeavyCount = overview.HeavyWorkspaceCount;
        _cachedWorkspaceStaleCount = overview.StaleWorkspaceCount;

        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem("▦ 工作区中心")
        {
            Enabled = false
        });
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem(
            workspaces.Count == 0
                ? "当前暂无工作区"
                : $"已保存 {workspaces.Count} 个工作区")
        {
            Enabled = false
        });
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem(
            workspaces.Count == 0
                ? "总览: 标签 0 / 重复风险 0"
                : $"总览: 标签 {overview.TotalTabs} / 重复风险 {overview.DuplicateCount} / 高负载 {overview.HeavyWorkspaceCount}")
        {
            Enabled = false
        });
        var healthCheckItem = new ToolStripMenuItem("◎ 查看工作区健康检查...");
        healthCheckItem.Click += (_, _) => ShowWorkspaceOverviewDialog(overview, workspaces.Count);
        _workspaceButton.DropDownItems.Add(healthCheckItem);
        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem("快速动作")
        {
            Enabled = false
        });
        _workspaceButton.DropDownItems.Add(BuildWindowTabOpsQuickMenu());
        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem("打开策略")
        {
            Enabled = false
        });
        var currentPolicy = ResolveWorkspaceOpenPolicy();
        var openPolicyMenu = new ToolStripMenuItem("◉ 工作区打开前自动整理");
        var policyKeepAllItem = new ToolStripMenuItem("完整打开（不整理）")
        {
            Checked = string.Equals(currentPolicy, WorkspaceOpenPolicyKeepAll, StringComparison.OrdinalIgnoreCase),
            ToolTipText = "保留工作区里的原始标签顺序和内容"
        };
        policyKeepAllItem.Click += (_, _) => UpdateWorkspaceOpenPolicy(WorkspaceOpenPolicyKeepAll);
        openPolicyMenu.DropDownItems.Add(policyKeepAllItem);
        var policyDedupeItem = new ToolStripMenuItem("自动去重（推荐）")
        {
            Checked = string.Equals(currentPolicy, WorkspaceOpenPolicyDedupe, StringComparison.OrdinalIgnoreCase),
            ToolTipText = "打开前自动清理重复 URL，减少重复页"
        };
        policyDedupeItem.Click += (_, _) => UpdateWorkspaceOpenPolicy(WorkspaceOpenPolicyDedupe);
        openPolicyMenu.DropDownItems.Add(policyDedupeItem);
        var policyCleanItem = new ToolStripMenuItem("深度整理（去重+清空白）")
        {
            Checked = string.Equals(currentPolicy, WorkspaceOpenPolicyClean, StringComparison.OrdinalIgnoreCase),
            ToolTipText = "在去重基础上移除 about:blank 空白标签"
        };
        policyCleanItem.Click += (_, _) => UpdateWorkspaceOpenPolicy(WorkspaceOpenPolicyClean);
        openPolicyMenu.DropDownItems.Add(policyCleanItem);
        _workspaceButton.DropDownItems.Add(openPolicyMenu);
        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());

        if (workspaces.Count > 0)
        {
            var latestWorkspace = workspaces[0];
            var latestDisplayName = GetWorkspaceDisplayName(latestWorkspace.Name);
            var latestOpenItem = new ToolStripMenuItem($"★ 打开最近工作区：{latestDisplayName}")
            {
                ToolTipText = $"覆盖当前窗口打开，最近保存于 {latestWorkspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            };
            latestOpenItem.Click += async (_, _) => await RestoreNamedSessionIntoCurrentWindowAsync(latestWorkspace.Name);
            _workspaceButton.DropDownItems.Add(latestOpenItem);

            var latestOpenSharedItem = new ToolStripMenuItem($"⧉ 最近工作区到新共享窗口：{latestDisplayName}")
            {
                ToolTipText = $"在新共享窗口打开 {latestDisplayName}"
            };
            latestOpenSharedItem.Click += (_, _) => OpenNamedSessionInSharedWindow(latestWorkspace.Name);
            _workspaceButton.DropDownItems.Add(latestOpenSharedItem);

            var latestAppendItem = new ToolStripMenuItem($"＋ 当前标签加入最近工作区：{latestDisplayName}")
            {
                Enabled = hasActiveTab,
                ToolTipText = hasActiveTab
                    ? $"将当前标签加入 {latestDisplayName} 并自动去重"
                    : "当前没有可加入的标签页"
            };
            latestAppendItem.Click += (_, _) => AppendActiveTabToWorkspace(latestWorkspace.Name);
            _workspaceButton.DropDownItems.Add(latestAppendItem);
            _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        }

        var saveItem = new ToolStripMenuItem("▣ 保存当前标签组为工作区...")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+S"
        };
        saveItem.Click += (_, _) => SaveWorkspaceSnapshot();
        _workspaceButton.DropDownItems.Add(saveItem);
        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem("工作区列表")
        {
            Enabled = false
        });

        var appendCurrentTabMenu = new ToolStripMenuItem("＋ 加入当前标签（自动去重）")
        {
            Enabled = workspaces.Count > 0 && hasActiveTab
        };
        foreach (var workspace in workspaces.Take(10))
        {
            var displayName = GetWorkspaceDisplayName(workspace.Name);
            var item = new ToolStripMenuItem($"{displayName}  ({workspace.TabCount} 标签)")
            {
                ToolTipText = $"当前标签加入该工作区并自动去重；保存于 {workspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            };
            item.Click += (_, _) => AppendActiveTabToWorkspace(workspace.Name);
            appendCurrentTabMenu.DropDownItems.Add(item);
        }
        _workspaceButton.DropDownItems.Add(appendCurrentTabMenu);

        var openMenu = new ToolStripMenuItem("↪ 快速打开（覆盖当前窗口）")
        {
            Enabled = workspaces.Count > 0
        };
        foreach (var workspace in workspaces.Take(10))
        {
            var displayName = GetWorkspaceDisplayName(workspace.Name);
            var item = new ToolStripMenuItem($"{displayName}  ({workspace.TabCount} 标签)")
            {
                ToolTipText = $"保存于 {workspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {workspace.TabCount} 个标签"
            };
            item.Click += async (_, _) => await RestoreNamedSessionIntoCurrentWindowAsync(workspace.Name);
            openMenu.DropDownItems.Add(item);
        }
        _workspaceButton.DropDownItems.Add(openMenu);

        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        var openSharedMenu = new ToolStripMenuItem("⧉ 在新共享窗口打开工作区")
        {
            Enabled = workspaces.Count > 0
        };
        foreach (var workspace in workspaces.Take(10))
        {
            var displayName = GetWorkspaceDisplayName(workspace.Name);
            var item = new ToolStripMenuItem($"{displayName}  ({workspace.TabCount} 标签)")
            {
                ToolTipText = $"保存于 {workspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}，共 {workspace.TabCount} 个标签"
            };
            item.Click += (_, _) => OpenNamedSessionInSharedWindow(workspace.Name);
            openSharedMenu.DropDownItems.Add(item);
        }
        _workspaceButton.DropDownItems.Add(openSharedMenu);

        var deleteMenu = new ToolStripMenuItem("✕ 删除工作区")
        {
            Enabled = workspaces.Count > 0
        };
        foreach (var workspace in workspaces.Take(10))
        {
            var displayName = GetWorkspaceDisplayName(workspace.Name);
            var item = new ToolStripMenuItem($"{displayName}  ({workspace.TabCount} 标签)")
            {
                ToolTipText = $"保存于 {workspace.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            };
            item.Click += (_, _) => DeleteNamedSessionSnapshot(workspace.Name);
            deleteMenu.DropDownItems.Add(item);
        }
        _workspaceButton.DropDownItems.Add(deleteMenu);

        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        var dedupeAllItem = new ToolStripMenuItem("⊙ 批量去重维护全部工作区")
        {
            Enabled = workspaces.Count > 0,
            ToolTipText = "遍历所有工作区，自动清理重复页面并保存"
        };
        dedupeAllItem.Click += (_, _) => DedupeAllWorkspaces();
        _workspaceButton.DropDownItems.Add(dedupeAllItem);

        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        var manageItem = new ToolStripMenuItem("☰ 管理工作区...")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+O"
        };
        manageItem.Click += (_, _) => ShowSessionsWindow(workspaceOnlyMode: true);
        _workspaceButton.DropDownItems.Add(manageItem);

        _workspaceButton.DropDownItems.Add(new ToolStripSeparator());
        if (workspaces.Count == 0)
        {
            _workspaceButton.DropDownItems.Add(new ToolStripMenuItem("暂无工作区，可先保存当前标签组合")
            {
                Enabled = false
            });
            return;
        }

        var latest = workspaces[0];
        var latestSaved = latest.SavedAtUtc.ToLocalTime().ToString("MM-dd HH:mm");
        _workspaceButton.DropDownItems.Add(new ToolStripMenuItem(
            $"最近：{GetWorkspaceDisplayName(latest.Name)} / {latest.TabCount} 标签 / {latestSaved}")
        {
            Enabled = false
        });
    }

    private ToolStripMenuItem BuildWindowTabOpsQuickMenu()
    {
        var hasTabs = _tabManager.Tabs.Count > 0;
        var hasActiveWebTab = _tabManager.ActiveTab?.CurrentUri is not null;
        var menu = new ToolStripMenuItem("⚡ 当前窗口整理")
        {
            Enabled = hasTabs
        };

        var dedupeItem = new ToolStripMenuItem("去重标签（保留当前/固定）");
        dedupeItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("dedupe");
        menu.DropDownItems.Add(dedupeItem);

        var closeBlankItem = new ToolStripMenuItem("关闭空白标签")
        {
            Enabled = hasActiveWebTab
        };
        closeBlankItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("closeblank");
        menu.DropDownItems.Add(closeBlankItem);

        var focusHostItem = new ToolStripMenuItem("仅保留同站标签")
        {
            Enabled = hasActiveWebTab
        };
        focusHostItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("focushost");
        menu.DropDownItems.Add(focusHostItem);

        var suspendOthersItem = new ToolStripMenuItem("休眠其他标签")
        {
            Enabled = hasActiveWebTab
        };
        suspendOthersItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("suspendothers");
        menu.DropDownItems.Add(suspendOthersItem);

        menu.DropDownItems.Add(new ToolStripSeparator());
        var resumeAllItem = new ToolStripMenuItem("恢复全部休眠标签");
        resumeAllItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("resumeall");
        menu.DropDownItems.Add(resumeAllItem);

        var hostStatsItem = new ToolStripMenuItem("查看站点分布统计");
        hostStatsItem.Click += async (_, _) => await ApplyWindowQuickTabOpsAsync("hoststats");
        menu.DropDownItems.Add(hostStatsItem);

        // 中文注释：把标签整理动作收口到工作区入口，减少用户在右键菜单和命令面板之间来回切换。
        return menu;
    }

    private async Task ApplyWindowQuickTabOpsAsync(string action)
    {
        if (_tabManager.Tabs.Count == 0)
        {
            ShowToast("当前窗口没有可整理的标签");
            return;
        }

        await ApplyTabOpsByCommandAsync([action]);
    }

    private WorkspaceOverview CalculateWorkspaceOverview(IReadOnlyCollection<NamedSessionDescriptor> workspaces)
    {
        if (workspaces.Count == 0)
        {
            return new WorkspaceOverview(0, 0, 0, 0);
        }

        var totalTabs = 0;
        var duplicateCount = 0;
        var heavyWorkspaceCount = 0;
        var staleWorkspaceCount = 0;
        var staleSince = DateTime.UtcNow.AddDays(-7);

        foreach (var workspace in workspaces)
        {
            totalTabs += workspace.TabCount;
            if (workspace.TabCount >= 18)
            {
                heavyWorkspaceCount++;
            }

            if (workspace.SavedAtUtc < staleSince)
            {
                staleWorkspaceCount++;
            }

            var loaded = _namedSessionStore.Load(workspace.Name);
            if (loaded is null || loaded.Tabs.Count <= 1)
            {
                continue;
            }

            // 中文注释：统一复用工作区去重逻辑，仅统计可清理数量，不直接修改数据。
            _ = DeduplicateWorkspaceTabs(loaded.Tabs, out var removedCount);
            duplicateCount += removedCount;
        }

        return new WorkspaceOverview(totalTabs, duplicateCount, heavyWorkspaceCount, staleWorkspaceCount);
    }

    private void ShowWorkspaceOverviewDialog(WorkspaceOverview overview, int workspaceCount)
    {
        var averageTabs = workspaceCount == 0 ? 0D : overview.TotalTabs / (double)workspaceCount;
        var summary = workspaceCount == 0
            ? "当前没有工作区，可先保存当前标签组合。"
            : $"工作区总数: {workspaceCount}\n总标签数: {overview.TotalTabs}\n平均每个工作区: {averageTabs:F1} 标签\n重复风险: {overview.DuplicateCount}\n高负载工作区(≥18标签): {overview.HeavyWorkspaceCount}\n超过7天未更新: {overview.StaleWorkspaceCount}";
        MessageBox.Show(this, summary, "工作区健康检查", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Log($"已查看工作区健康检查：工作区 {workspaceCount}，重复风险 {overview.DuplicateCount}");
    }

    private void ShowHistoryWindow()
    {
        if (_historyWindow is null || _historyWindow.IsDisposed)
        {
            _historyWindow = new HistoryWindow(
                _historyManager,
                () => _tabManager.RecentlyClosedTabs,
                OpenUrlFromHistoryAsync,
                ReopenLastClosedTabFromHistoryAsync,
                Log);
        }

        _historyWindow.RefreshData();
        ShowOwnedWindow(_historyWindow);
    }

    private async Task OpenUrlFromHistoryAsync(string url, bool inNewTab)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri is null)
        {
            Log("历史记录地址无效");
            return;
        }

        if (inNewTab || _tabManager.ActiveTab is null)
        {
            await _tabManager.CreateTabAsync(uri);
            return;
        }

        await _tabManager.ActiveTab.NavigateAsync(uri);
    }

    private async Task<bool> ReopenLastClosedTabFromHistoryAsync()
    {
        var reopened = await _tabManager.ReopenLastClosedTabAsync();
        if (reopened)
        {
            RefreshToolbar();
            RefreshMetrics();
        }

        return reopened;
    }

    private void RefreshToolbar()
    {
        var active = _tabManager.ActiveTab;
        _backButton.Enabled = active?.CanGoBack == true;
        _forwardButton.Enabled = active?.CanGoForward == true;
        _refreshButton.Enabled = active is not null;
        _closeTabButton.Enabled = active is not null;
        _addBookmarkButton.Enabled = TryGetActiveUri(out _);
        _splitViewButton.Checked = _splitPanel.IsSplitActive;
        _verticalTabStrip?.RebuildTabs();
        _verticalTabsToggleButton.Checked = _verticalTabStrip?.Visible == true;
        _readerModeButton.Checked = active?.IsReaderModeActive ?? false;
        if (active is not null && Math.Abs(active.ZoomFactor - 1.0) > 0.02)
        {
            _zoomPillButton.Visible = true;
            _zoomPillButton.Text = $"{(int)Math.Round(active.ZoomFactor * 100)}% 🔍";
        }
        else
        {
            _zoomPillButton.Visible = false;
        }
        UpdateSiteBadge();
        UpdateTabQuickButtonText();
        UpdateWorkspaceButtonBadge();
        UpdateModeLabelText();
        RequestTitleBarRebuild();
        UpdateWindowCaption();
        _sidePanel?.RefreshActiveContent();
    }

    private void RequestTitleBarRebuild()
    {
        if (_workspaceButton.DropDown.Visible)
        {
            _pendingTitleBarRebuild = true;
            return;
        }

        RebuildTitleBarItems();
    }

    private async Task CloseTabByIndexAsync(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabControl.TabCount)
        {
            return;
        }

        var targetPage = _tabControl.TabPages[tabIndex];
        var targetTab = _tabManager.Tabs.FirstOrDefault(tab => ReferenceEquals(tab.TabPage, targetPage));
        if (targetTab is null)
        {
            return;
        }

        await _tabManager.CloseTabAsync(targetTab);
        RefreshToolbar();
        RefreshMetrics();
    }

    private async Task DuplicateTabByIndexAsync(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabControl.TabCount)
        {
            return;
        }

        var targetPage = _tabControl.TabPages[tabIndex];
        var targetTab = _tabManager.Tabs.FirstOrDefault(tab => ReferenceEquals(tab.TabPage, targetPage));
        if (targetTab is null)
        {
            return;
        }

        var uri = targetTab.CurrentUri ?? new Uri("about:blank");
        await _tabManager.CreateTabAsync(uri);
        RefreshToolbar();
        RefreshMetrics();
        Log("已复制标签");
    }

    private async Task CloseOtherTabsByIndexAsync(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabControl.TabCount)
        {
            return;
        }

        var targetPage = _tabControl.TabPages[tabIndex];
        var targetTab = _tabManager.Tabs.FirstOrDefault(tab => ReferenceEquals(tab.TabPage, targetPage));
        if (targetTab is null)
        {
            return;
        }

        var closingTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, targetTab) && !tab.IsPinned)
            .ToArray();
        foreach (var tab in closingTabs)
        {
            await _tabManager.CloseTabAsync(tab);
        }

        if (targetTab.TabPage.Parent is not null)
        {
            _tabControl.SelectedTab = targetTab.TabPage;
        }

        RefreshToolbar();
        RefreshMetrics();
        Log("已关闭其他非固定标签");
    }

    private async Task CloseOtherTabsKeepActiveAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is null)
        {
            return;
        }

        var closingTabs = _tabManager.Tabs
            .Where(tab => !ReferenceEquals(tab, activeTab) && !tab.IsPinned)
            .ToArray();
        foreach (var tab in closingTabs)
        {
            await _tabManager.CloseTabAsync(tab);
        }

        if (activeTab.TabPage.Parent is not null)
        {
            _tabControl.SelectedTab = activeTab.TabPage;
        }

        RefreshToolbar();
        RefreshMetrics();
        Log("已关闭其他非固定标签");
    }

    private bool TryMoveTabById(Guid sourceTabId, Guid targetTabId)
    {
        if (sourceTabId == targetTabId)
        {
            return false;
        }

        var tabs = _tabManager.Tabs;
        var sourceIndex = tabs
            .Select((tab, index) => new { tab, index })
            .FirstOrDefault(item => item.tab.Id == sourceTabId)
            ?.index ?? -1;
        var targetIndex = tabs
            .Select((tab, index) => new { tab, index })
            .FirstOrDefault(item => item.tab.Id == targetTabId)
            ?.index ?? -1;
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return false;
        }

        _tabManager.MoveTab(sourceIndex, targetIndex);
        return true;
    }

    private void ResetTitleTabDragState(bool logIfMoved = false)
    {
        if (logIfMoved && _titleTabDragMoved)
        {
            Log("已调整标签顺序");
        }

        _titleTabDragSourceId = null;
        _titleTabDragStartPoint = Point.Empty;
        _titleTabDragging = false;
        _titleTabDragMoved = false;
    }

    private bool TrySelectTabByIndex(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabControl.TabCount)
        {
            return false;
        }

        _tabControl.SelectedIndex = tabIndex;
        return true;
    }

    private bool TryCycleTabSelection(bool forward)
    {
        var tabCount = _tabControl.TabCount;
        if (tabCount <= 1)
        {
            return false;
        }

        var currentIndex = _tabControl.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = forward
            ? (currentIndex + 1) % tabCount
            : (currentIndex - 1 + tabCount) % tabCount;
        _tabControl.SelectedIndex = nextIndex;
        return true;
    }

    private bool TryMoveActiveTabByDelta(int delta)
    {
        if (_tabControl.TabCount <= 1)
        {
            return false;
        }

        var currentIndex = _tabControl.SelectedIndex;
        if (currentIndex < 0)
        {
            return false;
        }

        var targetIndex = Math.Clamp(currentIndex + delta, 0, _tabControl.TabCount - 1);
        if (targetIndex == currentIndex)
        {
            return false;
        }

        _tabManager.MoveTab(currentIndex, targetIndex);
        RefreshToolbar();
        return true;
    }

    private void RefreshMetrics()
    {
        var memoryMb = Process.GetCurrentProcess().PrivateMemorySize64 / 1024d / 1024d;
        var colorMetrics = BuildTabColorMetricsSuffix();
        var tabCount = _tabManager.Tabs.Count;
        var liveCount = _tabManager.LiveTabCount;
        var downloadCount = _downloadManager.Items.Count;
        var pluginCount = _pluginManager.PluginCount;
        var historyCount = _historyManager.Entries.Count;

        _statusBarPanel.MemoryLabel.Text = _config.DetailedStatusBar
            ? $"⚡ {memoryMb:F0} MB · {tabCount} 标签 (活跃 {liveCount})"
            : $"⚡ {memoryMb:F0} MB · {tabCount} 标签";
        _statusBarPanel.SetMemoryUsageLevel(memoryMb);

        _statusBarPanel.MemoryLabel.ToolTipText =
            $"系统资源与运行统计:\n" +
            $"• 内存占用: {memoryMb:F1} MB (健康阈值: <1200MB 正常, 1200-1800MB 关注, >=1800MB 预警)\n" +
            $"• 标签页总数: {tabCount} (活跃就绪: {liveCount}, 休眠节省: {tabCount - liveCount})\n" +
            $"• 已安装插件: {pluginCount} 个\n" +
            $"• 下载任务: {downloadCount} 项\n" +
            $"• 浏览历史: {historyCount} 条{colorMetrics}\n" +
            $"点击可查看标签统计详情与资源诊断卡片";
        UpdateResponsiveStatusBarVisuals();
        UpdateAppTitleLabel();
    }

    private void UpdateResponsiveStatusBarVisuals()
    {
        if (!_statusBarPanel.Strip.Visible)
        {
            return;
        }

        var isNarrow = ClientSize.Width < 1280;
        _statusBarPanel.ApplyResponsiveLayout(ClientSize.Width);
        if (isNarrow && _statusBarPanel.ModeLabel.Visible)
        {
            var shortTheme = string.Equals(_config.UiTheme, "Dark", StringComparison.OrdinalIgnoreCase) ? "深色" : "浅色";
            _statusBarPanel.ModeLabel.Text = $"🎨 {shortTheme}";
        }
        else
        {
            UpdateModeLabelText();
        }
    }

    private void ShowBrandQuickMenu(Point location)
    {
        DismissTransientMenus();
        var menu = new ContextMenuStrip();
        AppTheme.ApplyContextMenu(menu);
        // 中文注释：Closed 里同步 Dispose 会让 ModalMenuFilter 收尾时访问已释放菜单而崩溃，延迟到消息循环空闲后再释放。
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);

        menu.Items.Add(new ToolStripMenuItem("浏览器概览")
        {
            Enabled = false
        });
        menu.Items.Add(new ToolStripMenuItem(
            $"标签 {_tabManager.Tabs.Count} / 活跃 {_tabManager.LiveTabCount} / 工作区 {_cachedWorkspaceCount}")
        {
            Enabled = false
        });
        menu.Items.Add(new ToolStripSeparator());

        var workspaceCenter = new ToolStripMenuItem("打开工作区中心...");
        workspaceCenter.Click += (_, _) =>
        {
            ShowSessionsWindow(workspaceOnlyMode: true);
            Log("已通过顶部品牌菜单打开工作区中心");
        };
        menu.Items.Add(workspaceCenter);

        var sessionCenter = new ToolStripMenuItem("打开会话中心...");
        sessionCenter.Click += (_, _) =>
        {
            ShowSessionsWindow(workspaceOnlyMode: false);
            Log("已通过顶部品牌菜单打开会话中心");
        };
        menu.Items.Add(sessionCenter);

        var statsItem = new ToolStripMenuItem("查看标签统计...");
        statsItem.Click += (_, _) => ShowTabStatsDialog();
        menu.Items.Add(statsItem);

        var newTabItem = new ToolStripMenuItem("新建空白标签");
        newTabItem.Click += async (_, _) =>
        {
            await _tabManager.CreateTabAsync(new Uri("about:blank"));
            RefreshToolbar();
            RefreshMetrics();
            Log("已通过顶部品牌菜单新建空白标签");
        };
        menu.Items.Add(newTabItem);

        // 中文注释：品牌区右键菜单聚合“状态+入口”，减少在多处按钮间来回切换。
        menu.Show(Cursor.Position);
    }

    private string BuildTabColorMetricsSuffix()
    {
        var grouped = _tabManager.Tabs
            .Select(tab => BrowserTab.NormalizeColorTagKey(tab.ColorTagKey))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(group =>
            {
                var label = ResolveTabColorText(group.Key);
                return string.IsNullOrWhiteSpace(label) ? null : $"{label[..1]}{group.Count()}";
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
        var renamedCount = _tabManager.Tabs.Count(tab => tab.HasCustomTitle);
        if (grouped.Length == 0 && renamedCount == 0)
        {
            return string.Empty;
        }

        var metrics = new List<string>();
        if (grouped.Length > 0)
        {
            metrics.Add($"色标:{string.Join("/", grouped)}");
        }

        if (renamedCount > 0)
        {
            metrics.Add($"命名:{renamedCount}");
        }

        var pinnedCount = _tabManager.Tabs.Count(tab => tab.IsPinned);
        if (pinnedCount > 0)
        {
            metrics.Add($"固定:{pinnedCount}");
        }

        var suspendedCount = _tabManager.Tabs.Count(tab => tab.IsSuspended);
        if (suspendedCount > 0)
        {
            metrics.Add($"休眠:{suspendedCount}");
        }

        return " " + string.Join(" ", metrics);
    }

    private void ShowDownloadManager()
    {
        if (_downloadsWindow is null || _downloadsWindow.IsDisposed)
        {
            _downloadsWindow = new DownloadsWindow(_downloadManager);
        }

        ShowOwnedWindow(_downloadsWindow);
    }

    private void ShowSessionsWindow()
    {
        ShowSessionsWindow(workspaceOnlyMode: false);
    }

    private void ShowSessionsWindow(bool workspaceOnlyMode)
    {
        if (_sessionsWindow is null || _sessionsWindow.IsDisposed)
        {
            _sessionsWindow = new SessionsWindow(
                _namedSessionStore,
                RestoreNamedSessionIntoCurrentWindowAsync,
                OpenNamedSessionInSharedWindow,
                OpenNamedSessionInIsolatedWindow,
                OpenNamedSessionInPrivateWindow,
                Log,
                workspaceOnlyMode);
        }
        else
        {
            _sessionsWindow.SetWorkspaceOnlyMode(workspaceOnlyMode, refresh: false);
        }

        _sessionsWindow.RefreshData();
        ShowOwnedWindow(_sessionsWindow);
    }

    private bool TryGetActiveUri(out Uri uri)
    {
        var active = _tabManager.ActiveTab;
        if (active?.CurrentUri is not null &&
            (active.CurrentUri.Scheme == Uri.UriSchemeHttp || active.CurrentUri.Scheme == Uri.UriSchemeHttps))
        {
            uri = active.CurrentUri;
            return true;
        }

        uri = new Uri("about:blank");
        return false;
    }

    private void Log(string message)
    {
        _statusBarPanel.StatusLabel.Text = message;
        AppLogger.Info(message);
    }

    private void UpdateSiteBadge()
    {
        if (!TryGetActiveUri(out var uri) || uri is null || string.IsNullOrWhiteSpace(uri.Host) || uri.AbsoluteUri == "about:blank")
        {
            _siteBadgeLabel.Text = "🌐";
            _siteBadgeLabel.Width = 34;
            _siteBadgeLabel.ToolTipText = "新标签页 / 空白页\n输入网址或搜索内容后按回车访问";
            _siteBadgeLabel.BackColor = AppTheme.IsDark ? Color.FromArgb(50, 65, 85) : Color.FromArgb(226, 232, 240);
            _siteBadgeLabel.ForeColor = AppTheme.TextSecondary;
            return;
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (isHttps)
        {
            _siteBadgeLabel.Text = "🔒";
            _siteBadgeLabel.Width = 34;
            _siteBadgeLabel.ToolTipText = $"安全连接 (HTTPS)\n{uri.AbsoluteUri}\n已通过安全加密传输，点击查看站点属性与证书详情";
            _siteBadgeLabel.BackColor = AppTheme.IsDark ? Color.FromArgb(32, 74, 58) : Color.FromArgb(220, 245, 235);
            _siteBadgeLabel.ForeColor = AppTheme.IsDark ? Color.FromArgb(140, 225, 195) : Color.FromArgb(16, 120, 85);
        }
        else if (isHttp)
        {
            _siteBadgeLabel.Text = "⚠ 不安全";
            _siteBadgeLabel.Width = 72;
            _siteBadgeLabel.ToolTipText = $"不安全连接 (HTTP)\n{uri.AbsoluteUri}\n此网站未使用加密传输，请勿输入密码等敏感信息";
            _siteBadgeLabel.BackColor = AppTheme.IsDark ? Color.FromArgb(88, 65, 25) : Color.FromArgb(254, 238, 215);
            _siteBadgeLabel.ForeColor = AppTheme.IsDark ? Color.FromArgb(245, 195, 130) : Color.FromArgb(155, 80, 5);
        }
        else
        {
            _siteBadgeLabel.Text = "ℹ";
            _siteBadgeLabel.Width = 34;
            _siteBadgeLabel.ToolTipText = $"内部专有协议 ({uri.Scheme})\n{uri.AbsoluteUri}";
            _siteBadgeLabel.BackColor = AppTheme.IsDark ? Color.FromArgb(50, 65, 85) : Color.FromArgb(226, 232, 240);
            _siteBadgeLabel.ForeColor = AppTheme.TextPrimary;
        }
    }

    private void UpdateWindowCaption()
    {
        Text = " ";
        var maximized = WindowState == FormWindowState.Maximized;
        ApplyFluentIcon(
            _titleWindowMaxButton,
            maximized ? AppIconNames.WindowRestore : AppIconNames.WindowMaximize,
            12,
            AppTheme.TextPrimary);
        _titleWindowMaxButton.Text = ComposeIconButtonText(_titleWindowMaxButton, maximized ? "❐" : "□");
    }

    private void UpdateMaximizedBoundsForCurrentScreen()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        // 中文注释：无边框窗口最大化时使用 WorkingArea，避免遮挡系统任务栏。
        var screen = Screen.FromHandle(Handle);
        MaximizedBounds = screen.WorkingArea;
    }

    private void BeginWindowDragFromTitleArea(MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        _ = SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
    }

    private void HandleTitleStripDoubleClick(ToolStrip strip, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        var item = strip.GetItemAt(eventArgs.Location);
        if (item is null || ReferenceEquals(item, _titleDragSpacer))
        {
            ToggleWindowMaximizeState();
        }
    }

    private void ToggleWindowMaximizeState()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateWindowCaption();
        _sidePanel?.RefreshActiveContent();
    }

    private void HandleWindowStateTransition()
    {
        var currentState = WindowState;
        var previousState = _lastWindowState;
        if (_animateNextRestore &&
            previousState == FormWindowState.Minimized &&
            currentState != FormWindowState.Minimized)
        {
            BeginRestoreTransition();
        }

        _lastWindowState = currentState;
    }

    private void BeginMinimizeTransition()
    {
        if (WindowState == FormWindowState.Minimized || _isWindowTransitionRunning)
        {
            return;
        }

        _windowTransitionTimer.Stop();
        _isWindowTransitionRunning = true;
        _isWindowFadeOutPhase = true;
        _windowTransitionStep = 0;
        _windowTransitionTimer.Start();
    }

    private void BeginRestoreTransition()
    {
        _windowTransitionTimer.Stop();
        _isWindowTransitionRunning = true;
        _isWindowFadeOutPhase = false;
        _windowTransitionStep = 0;
        Opacity = WindowFadeInStartOpacity;
        _windowTransitionTimer.Start();
    }

    private void OnWindowTransitionTick()
    {
        if (!_isWindowTransitionRunning)
        {
            _windowTransitionTimer.Stop();
            return;
        }

        _windowTransitionStep = Math.Min(WindowTransitionSteps, _windowTransitionStep + 1);
        var progress = (double)_windowTransitionStep / WindowTransitionSteps;

        if (_isWindowFadeOutPhase)
        {
            var nextOpacity = 1D - ((1D - WindowFadeOutMinimumOpacity) * progress);
            Opacity = Math.Clamp(nextOpacity, WindowFadeOutMinimumOpacity, 1D);
            if (_windowTransitionStep < WindowTransitionSteps)
            {
                return;
            }

            _windowTransitionTimer.Stop();
            _isWindowTransitionRunning = false;
            _isWindowFadeOutPhase = false;
            _animateNextRestore = true;
            Opacity = 1D;
            WindowState = FormWindowState.Minimized;
            return;
        }

        var fadeInOpacity = WindowFadeInStartOpacity + ((1D - WindowFadeInStartOpacity) * progress);
        Opacity = Math.Clamp(fadeInOpacity, WindowFadeInStartOpacity, 1D);
        if (_windowTransitionStep < WindowTransitionSteps)
        {
            return;
        }

        _windowTransitionTimer.Stop();
        _isWindowTransitionRunning = false;
        _animateNextRestore = false;
        Opacity = 1D;
    }

    private void ApplyWindowIconFromConfig()
    {
        var icon = TryLoadTaskbarIconFromConfig(_config.TaskbarIconPath) ?? CreateDefaultWindowIcon();
        var previousIcon = _windowIcon;
        _windowIcon = icon;
        Icon = icon;
        if (IsHandleCreated)
        {
            _ = SetClassLongPtr(Handle, GclHIcon, _windowIcon.Handle);
            _ = SetClassLongPtr(Handle, GclHIconSm, _windowIcon.Handle);
            _ = SendMessage(Handle, WmSetIcon, (IntPtr)IconSmall, _windowIcon.Handle);
            _ = SendMessage(Handle, WmSetIcon, (IntPtr)IconBig, _windowIcon.Handle);
        }

        previousIcon?.Dispose();
    }

    private Icon? TryLoadTaskbarIconFromConfig(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var filePath = ResolveTaskbarIconPath(configuredPath);
        if (!File.Exists(filePath))
        {
            Log($"任务栏图标文件不存在，已回退到内置图标：{filePath}");
            return null;
        }

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".ico")
            {
                return new Icon(filePath);
            }

            return TryCreateIconFromImageFile(filePath);
        }
        catch (Exception ex)
        {
            Log($"任务栏图标加载失败：{ex.Message}");
            return null;
        }
    }

    private static string ResolveTaskbarIconPath(string configuredPath)
    {
        var path = configuredPath.Trim();
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    private static Icon? TryCreateIconFromImageFile(string filePath)
    {
        using var sourceImage = Image.FromFile(filePath);
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(sourceImage, new Rectangle(0, 0, 64, 64));
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(iconHandle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static Icon CreateDefaultWindowIcon()
    {
        // 中文注释：品牌 Logo（窗口/任务栏图标），与 scripts/gen-app-icon.ps1 的 exe 图标保持同一几何设计。
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            // 底座：45° 对角渐变圆角方块，跟随当前强调色，无描边（扁平）。
            var baseRect = new Rectangle(4, 4, 56, 56);
            var topColor = AppTheme.Mix(AppTheme.Accent, Color.White, 0.22F);
            var bottomColor = AppTheme.Mix(AppTheme.Accent, Color.Black, 0.26F);
            using var bodyBrush = new LinearGradientBrush(baseRect, topColor, bottomColor, 45F);
            using var shape = BuildRoundedRectanglePath(baseRect, 16);
            graphics.FillPath(bodyBrush, shape);

            // 字标：几何 "L"（竖笔 + 横笔两个圆角矩形拼合），白色实心。
            using var glyphBrush = new SolidBrush(Color.White);
            using var stemShape = BuildRoundedRectanglePath(new Rectangle(20, 15, 10, 26), 5);
            using var footShape = BuildRoundedRectanglePath(new Rectangle(20, 31, 25, 10), 5);
            graphics.FillPath(glyphBrush, stemShape);
            graphics.FillPath(glyphBrush, footShape);

            // 轨道圆点：右上角半透明白点，呼应"浏览/环游"的意象。
            using var dotBrush = new SolidBrush(Color.FromArgb(235, Color.White));
            graphics.FillEllipse(dotBrush, 39F, 15F, 9F, 9F);
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(iconHandle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private async Task LoadPublicIpInfoAsync()
    {
        try
        {
            var ipText = await _publicIpInfoService.GetDisplayTextAsync();
            _statusBarPanel.IpLabel.Text = ipText.StartsWith("🌐") ? ipText : $"🌐 {ipText}";
            _statusBarPanel.IpLabel.ToolTipText = $"{ipText}\n点击可复制IP信息，右键可刷新";
        }
        catch
        {
            _statusBarPanel.IpLabel.Text = "🌐 IP: 获取失败";
        }
    }

    private async Task HardRefreshActiveTabAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is null)
        {
            return;
        }

        await activeTab.ClearCacheAndReloadAsync();
        ShowToast("已清除缓存并强制刷新");
    }

    /// <summary>
    /// 中文注释：在新标签以 view-source 打开活动标签页面源码（Ctrl+U / viewsource 指令共用）。
    /// </summary>
    private async Task OpenActiveTabSourceViewAsync()
    {
        var sourceUri = _tabManager.ActiveTab?.CurrentUri;
        if (sourceUri is null ||
            (!string.Equals(sourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            ShowToast("当前页面不支持查看源码");
            return;
        }

        try
        {
            await _tabManager.CreateTabAsync(new Uri("view-source:" + sourceUri.AbsoluteUri));
            RefreshToolbar();
            RefreshMetrics();
        }
        catch (Exception ex)
        {
            Log($"打开页面源码失败：{ex.Message}");
        }
    }

    private void ShowToast(string message)
    {
        _toastLabel.Text = message;
        var measure = TextRenderer.MeasureText(
            message,
            _toastLabel.Font,
            new Size(460, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        var width = Math.Clamp(measure.Width + 52, 260, 540);
        var height = Math.Clamp(Math.Max(44, measure.Height + 24), 44, 94);
        _toastPanel.Size = new Size(width, height);
        _toastRemainingMs = ToastDurationMs;
        _toastPanel.Visible = true;
        _toastPanel.BringToFront();
        _toastPanel.Invalidate();
        UpdateToastLocation();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private bool LaunchBrowserProcess(bool isPrivateWindow, bool useCurrentProfile, BrowserSessionState? startupSession = null)
    {
        var profileId = useCurrentProfile
            ? _launchOptions.ProfileId
            : AppLaunchOptions.CreateIsolatedProfileId();
        var arguments = isPrivateWindow
            ? $"--private --profile-id={AppLaunchOptions.CreateIsolatedProfileId()}"
            : useCurrentProfile
                ? $"--profile-id={profileId}"
                : $"--isolated --profile-id={profileId}";

        if (startupSession is not null)
        {
            var payloadPath = _launchPayloadStore.Save(startupSession);
            if (string.IsNullOrWhiteSpace(payloadPath))
            {
                return false;
            }

            arguments = $"{arguments} --restore-session-payload={Uri.EscapeDataString(payloadPath)}";
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = arguments,
            UseShellExecute = true
        });
        return true;
    }

    private BrowserTab? ResolveTabByContextIndex()
    {
        if (_tabContextIndex < 0 || _tabContextIndex >= _tabControl.TabCount)
        {
            return null;
        }

        var tabPage = _tabControl.TabPages[_tabContextIndex];
        return _tabManager.Tabs.FirstOrDefault(tab => ReferenceEquals(tab.TabPage, tabPage));
    }

    private void BuildTabColorMenu(BrowserTab? tab)
    {
        _tabCtxColor.DropDownItems.Clear();
        var normalizedCurrent = BrowserTab.NormalizeColorTagKey(tab?.ColorTagKey);
        foreach (var option in TabColorOptions)
        {
            var item = new ToolStripMenuItem(option.DisplayName)
            {
                Checked = string.Equals(normalizedCurrent, option.Key, StringComparison.OrdinalIgnoreCase),
                Enabled = tab is not null
            };
            item.Click += (_, _) => ApplyTabColorByContext(option.Key);
            _tabCtxColor.DropDownItems.Add(item);
        }

        _tabCtxColor.DropDownItems.Add(new ToolStripSeparator());
        var clearItem = new ToolStripMenuItem("清除色标")
        {
            Checked = string.IsNullOrWhiteSpace(normalizedCurrent),
            Enabled = tab is not null
        };
        clearItem.Click += (_, _) => ApplyTabColorByContext(string.Empty);
        _tabCtxColor.DropDownItems.Add(clearItem);
    }

    private void ApplyTabColorByContext(string colorKey)
    {
        var tab = ResolveTabByContextIndex();
        if (tab is null)
        {
            return;
        }

        _tabManager.SetColorTag(tab, colorKey);
        RefreshToolbar();
        RefreshMetrics();
        var label = ResolveTabColorText(tab.ColorTagKey);
        Log(string.IsNullOrWhiteSpace(label) ? "已清除标签色标" : $"已设置标签色标: {label}");
    }

    private void RenameTabByContext()
    {
        var tab = ResolveTabByContextIndex();
        if (tab is null)
        {
            return;
        }

        var initial = tab.HasCustomTitle ? tab.CustomTitle : tab.Title;
        if (!TryPromptForTabCustomTitle(initial, out var customTitle))
        {
            return;
        }

        tab.SetCustomTitle(customTitle);
        RefreshToolbar();
        RefreshMetrics();
        var display = string.IsNullOrWhiteSpace(tab.CustomTitle) ? "已恢复网页标题" : $"已重命名标签: {tab.DisplayTitle}";
        ShowToast(display);
        Log(display);
    }

    private bool TryPromptForTabCustomTitle(string initialValue, out string customTitle)
    {
        customTitle = string.Empty;

        using var dialog = new Form
        {
            Text = "重命名标签",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(420, 142),
            ShowInTaskbar = false
        };

        var promptLabel = new Label
        {
            Left = 16,
            Top = 14,
            Width = 386,
            Height = 20,
            Text = "输入新的标签名称，留空表示恢复网页标题："
        };
        var textBox = new TextBox
        {
            Left = 16,
            Top = 42,
            Width = 386,
            Text = initialValue ?? string.Empty
        };
        var okButton = new Button
        {
            Text = "确定",
            Left = 234,
            Top = 88,
            Width = 80,
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button
        {
            Text = "取消",
            Left = 322,
            Top = 88,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(promptLabel);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        dialog.BackColor = AppTheme.Surface;
        promptLabel.ForeColor = AppTheme.TextPrimary;
        promptLabel.Font = AppTheme.CreateFont(9F);
        textBox.BackColor = AppTheme.InputBackground;
        textBox.ForeColor = AppTheme.TextPrimary;
        textBox.Font = AppTheme.CreateFont(9.5F);
        okButton.BackColor = AppTheme.Accent;
        okButton.ForeColor = Color.White;
        okButton.FlatStyle = FlatStyle.Flat;
        okButton.FlatAppearance.BorderColor = AppTheme.Accent;
        okButton.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
        cancelButton.BackColor = AppTheme.AccentMuted;
        cancelButton.ForeColor = AppTheme.TextPrimary;
        cancelButton.FlatStyle = FlatStyle.Flat;
        cancelButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        cancelButton.FlatAppearance.MouseOverBackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.AccentMuted, Color.White, 0.10F)
            : AppTheme.Mix(AppTheme.AccentMuted, Color.Black, 0.08F);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        customTitle = textBox.Text.Trim();
        return true;
    }

    private async Task ApplyTabOpsFromContextAsync(string action)
    {
        var tab = ResolveTabByContextIndex();
        if (tab is null)
        {
            return;
        }

        _tabControl.SelectedTab = tab.TabPage;
        await _tabManager.ActivateSelectedTabAsync();
        await ApplyTabOpsByCommandAsync([action]);
    }

    private void ToggleTabPinByContext()
    {
        var tab = ResolveTabByContextIndex();
        if (tab is null)
        {
            return;
        }

        _tabManager.SetPinned(tab, !tab.IsPinned);
        RefreshToolbar();
        RefreshMetrics();
        Log(tab.IsPinned ? "已固定标签" : "已取消固定标签");
    }

    private async Task DuplicateTabByContextAsync()
    {
        var tab = ResolveTabByContextIndex();
        if (tab?.CurrentUri is null)
        {
            return;
        }

        await _tabManager.CreateTabAsync(tab.CurrentUri);
        RefreshToolbar();
        RefreshMetrics();
        Log("已复制标签");
    }

    private async Task ReopenClosedTabFromTabMenuAsync()
    {
        var reopened = await _tabManager.ReopenLastClosedTabAsync();
        if (!reopened)
        {
            Log("没有可恢复的关闭标签");
            return;
        }

        RefreshToolbar();
        RefreshMetrics();
        Log("已恢复最近关闭标签");
    }

    private async Task CloseTabMenuSelectionAsync(bool closeCurrent, bool closeOthers, bool closeLeft, bool closeRight, bool closeAll)
    {
        var target = ResolveTabByContextIndex();
        if (target is null)
        {
            return;
        }

        var tabs = _tabManager.Tabs.ToArray();

        IEnumerable<BrowserTab> closingTargets = [];
        if (closeAll)
        {
            closingTargets = tabs.Where(tab => !tab.IsPinned);
        }
        else if (closeCurrent)
        {
            closingTargets = [target];
        }
        else if (closeOthers)
        {
            closingTargets = tabs.Where(tab => !ReferenceEquals(tab, target) && !tab.IsPinned);
        }
        else if (closeLeft)
        {
            closingTargets = tabs.Where(tab => _tabControl.TabPages.IndexOf(tab.TabPage) < _tabContextIndex && !tab.IsPinned);
        }
        else if (closeRight)
        {
            closingTargets = tabs.Where(tab => _tabControl.TabPages.IndexOf(tab.TabPage) > _tabContextIndex && !tab.IsPinned);
        }

        foreach (var tab in closingTargets.ToArray())
        {
            await _tabManager.CloseTabAsync(tab);
        }

        RefreshToolbar();
        RefreshMetrics();
    }

    private ToolStripButton CreateBookmarkBarTitleButton()
    {
        var button = new ToolStripButton("书签")
        {
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent,
            Font = AppTheme.CreateFont(8.9F, FontStyle.Bold),
            Padding = AppTheme.IsCompact ? new Padding(3, 2, 4, 2) : new Padding(4, 2, 5, 2),
            Margin = new Padding(0, 1, 1, 1),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = true,
            ToolTipText = "书签栏 (Ctrl+Shift+B) | 点击管理书签"
        };
        button.Click += (_, _) => ShowBookmarksSettingsWindow();
        return button;
    }

    private static ToolStripLabel CreateBookmarkBarEmptyLabel()
    {
        return new ToolStripLabel("☆ 右键空白处可添加书签")
        {
            ForeColor = AppTheme.TextSecondary,
            Font = AppTheme.CreateFont(8.8F),
            Margin = new Padding(2, 2, 2, 2)
        };
    }

    private static void StyleBookmarkBarItem(ToolStripItem item, bool isFolder)
    {
        item.ForeColor = AppTheme.TextPrimary;
        item.BackColor = Color.Transparent;
        item.Padding = AppTheme.IsCompact ? new Padding(3, 2, 4, 2) : new Padding(4, 2, 5, 2);
        item.Margin = new Padding(1, 1, 1, 1);
        item.Font = AppTheme.CreateFont(8.9F, isFolder ? FontStyle.Bold : FontStyle.Regular);
        item.AutoSize = true;
    }

    private static void StyleBookmarkMenuItem(ToolStripMenuItem item, bool isFolder)
    {
        item.BackColor = AppTheme.InputBackground;
        item.ForeColor = AppTheme.TextPrimary;
        item.Font = AppTheme.CreateFont(9F, isFolder ? FontStyle.Bold : FontStyle.Regular);
        item.Padding = AppTheme.IsCompact ? new Padding(10, 4, 10, 4) : new Padding(12, 5, 12, 5);
    }

    private void SetToolbarButtonWidth(ToolStripItem button, int minWidth)
    {
        var measured = TextRenderer.MeasureText(button.Text, _topBar.Font).Width + (AppTheme.IsCompact ? 24 : 32);
        if (button.Image is not null)
        {
            measured += button.Image.Width + (string.IsNullOrEmpty(button.Text) ? 0 : 4);
        }
        var targetMinWidth = AppTheme.IsCompact ? Math.Max(72, minWidth - 14) : minWidth;
        var maxWidth = ClientSize.Width switch
        {
            < 980 => AppTheme.IsCompact ? 84 : 92,
            < 1180 => AppTheme.IsCompact ? 116 : 128,
            _ => AppTheme.IsCompact ? 148 : 166
        };
        button.Width = Math.Clamp(Math.Max(targetMinWidth, measured), targetMinWidth, maxWidth);
    }

    private static string BuildToolbarToolTip(string title, string? shortcutText = null, string? extraText = null)
    {
        if (string.IsNullOrWhiteSpace(shortcutText) && string.IsNullOrWhiteSpace(extraText))
        {
            return title;
        }

        if (string.IsNullOrWhiteSpace(extraText))
        {
            return $"{title}\n快捷键: {shortcutText}";
        }

        if (string.IsNullOrWhiteSpace(shortcutText))
        {
            return $"{title}\n{extraText}";
        }

        return $"{title}\n快捷键: {shortcutText}\n{extraText}";
    }

    private static string ResolveTitleTabSiteBadge(Uri? uri)
    {
        if (uri is null)
        {
            return "□";
        }

        if (uri.IsFile)
        {
            return "文";
        }

        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "本";
        }

        if (IPAddress.TryParse(uri.Host, out _))
        {
            return "I";
        }

        var host = uri.Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return "□";
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var core = parts.Length >= 2 ? parts[^2] : parts[0];
        var token = core.FirstOrDefault(char.IsLetterOrDigit);
        if (token == default)
        {
            token = host.FirstOrDefault(char.IsLetterOrDigit);
        }

        return token == default ? "□" : char.ToUpperInvariant(token).ToString();
    }

    private static string ResolveTitleTabColorBadge(string? colorKey)
    {
        var normalized = BrowserTab.NormalizeColorTagKey(colorKey);
        return normalized switch
        {
            "Blue" => "〔蓝〕",
            "Green" => "〔绿〕",
            "Orange" => "〔橙〕",
            "Rose" => "〔红〕",
            _ => string.Empty
        };
    }

    private static string ResolveTitleTabStateBadge(BrowserTab tab, bool isActive)
    {
        if (isActive)
        {
            return "▍";
        }

        if (tab.IsPinned)
        {
            return "●";
        }

        if (tab.IsSuspended)
        {
            return "⏾";
        }

        if (tab.HasCustomTitle)
        {
            return "◆";
        }

        return " ";
    }

    private static void ConfigureToolbarIconButton(
        ToolStripButton button,
        string iconName,
        string iconText,
        string toolTip,
        string? shortcutText = null,
        string? extraText = null,
        bool emphasized = false)
    {
        ApplyFluentIcon(button, iconName, AppTheme.IsCompact ? 16 : 18, AppTheme.TextPrimary);
        button.Text = ComposeIconButtonText(button, iconText);
        button.ToolTipText = BuildToolbarToolTip(toolTip, shortcutText, extraText);
        button.Tag = emphasized ? "primary" : null;
        button.BackColor = emphasized ? AppTheme.AccentMuted : Color.Transparent;
    }

    private static void ApplyFluentIcon(ToolStripItem item, string iconName, int size, Color color)
    {
        var icon = AppTheme.GetIcon(iconName, size, color);
        if (icon is null)
        {
            return;
        }

        // 中文注释：字体图标以位图形式与文字混排；系统缺少图标字体时保持原字符方案。
        item.Image = icon;
        item.ImageScaling = ToolStripItemImageScaling.None;
        item.ImageAlign = ContentAlignment.MiddleCenter;
        item.TextImageRelation = TextImageRelation.ImageBeforeText;
        item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
    }

    private static string ComposeIconButtonText(ToolStripItem item, string fallbackGlyph, string? text = null)
    {
        if (item.Image is not null)
        {
            return text ?? string.Empty;
        }

        return string.IsNullOrEmpty(text) ? fallbackGlyph : $"{fallbackGlyph} {text}";
    }

    private void UpdateBookmarkBarToggleButtonText()
    {
        ApplyFluentIcon(
            _toggleBookmarksBarButton,
            _config.ShowBookmarksBar ? AppIconNames.BookmarksBarCollapse : AppIconNames.BookmarksBarExpand,
            AppTheme.IsCompact ? 14 : 16,
            AppTheme.TextPrimary);
        _toggleBookmarksBarButton.Text = ComposeIconButtonText(_toggleBookmarksBarButton, _config.ShowBookmarksBar ? "▾" : "▸");
        _toggleBookmarksBarButton.ToolTipText = _config.ShowBookmarksBar ? "收起书签栏" : "展开书签栏";
    }

    private void ToggleBookmarksBarVisibility()
    {
        _config.ShowBookmarksBar = !_config.ShowBookmarksBar;
        _bookmarksBar.Visible = _config.ShowBookmarksBar;
        UpdateBookmarkBarToggleButtonText();
        SaveConfigSilently();
        Log(_config.ShowBookmarksBar ? "已显示书签栏" : "已隐藏书签栏");
    }

    private void SaveConfigSilently()
    {
        if (!BrowserConfig.SaveDefault(_config, out var error))
        {
            Log($"配置保存失败: {error}");
        }
    }

    private static string ResolveUserDataFilePath(string configuredFileName, string fallbackFileName)
    {
        var safeFileName = string.IsNullOrWhiteSpace(configuredFileName)
            ? fallbackFileName
            : configuredFileName.Trim();
        if (Path.IsPathRooted(safeFileName))
        {
            return safeFileName;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfsunBrowser",
            "data",
            safeFileName);
    }

    private void TryMigrateLegacyDataFile(string configuredFileName, string targetPath, string dataLabel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuredFileName))
            {
                return;
            }

            var safeFileName = configuredFileName.Trim();
            if (Path.IsPathRooted(safeFileName))
            {
                return;
            }

            var legacyPath = Path.Combine(AppContext.BaseDirectory, safeFileName);
            if (!File.Exists(legacyPath) || File.Exists(targetPath))
            {
                return;
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(legacyPath, targetPath, overwrite: false);
            Log($"已迁移{dataLabel}数据到用户目录");
        }
        catch (Exception ex)
        {
            Log($"{dataLabel}迁移失败: {ex.Message}");
        }
    }

    // 中文注释：把程序目录 Plugins 下的插件（内置示例或旧版本安装的）迁移到用户数据目录；
    // 只补齐用户目录里不存在的插件，已存在的不覆盖，避免回退用户的启用/修改状态。
    private void TryMigrateLegacyPlugins(string legacyRoot, string targetRoot)
    {
        try
        {
            if (!Directory.Exists(legacyRoot) ||
                string.Equals(Path.GetFullPath(legacyRoot), Path.GetFullPath(targetRoot), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var migrated = 0;
            foreach (var sourceDirectory in Directory.GetDirectories(legacyRoot))
            {
                if (!File.Exists(Path.Combine(sourceDirectory, "manifest.json")))
                {
                    continue;
                }

                var targetDirectory = Path.Combine(targetRoot, Path.GetFileName(sourceDirectory));
                if (Directory.Exists(targetDirectory))
                {
                    continue;
                }

                PluginManager.CopyDirectoryRecursive(sourceDirectory, targetDirectory);
                migrated++;
            }

            if (migrated > 0)
            {
                Log($"已迁移 {migrated} 个插件到用户目录: {targetRoot}");
            }
        }
        catch (Exception ex)
        {
            Log($"插件迁移失败: {ex.Message}");
        }
    }

    private async Task<bool> HandleShortcutAsync(KeyEventArgs eventArgs)
    {
                if (eventArgs.Control && !eventArgs.Shift && !eventArgs.Alt && eventArgs.KeyCode == Keys.M)
        {
            var activeTab = _tabManager.ActiveTab;
            if (activeTab is not null)
            {
                activeTab.ToggleMute();
                ShowToast(activeTab.IsMuted ? "已静音当前标签 🔇" : "已恢复当前标签声音 🔊");
                RefreshToolbar();
                return true;
            }
        }

        if ((eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.E) || (eventArgs.Alt && eventArgs.KeyCode == Keys.S))
        {
            ToggleSidePanel();
            return true;
        }

        if ((eventArgs.Alt && eventArgs.KeyCode == Keys.D) || (eventArgs.Control && eventArgs.Alt && eventArgs.KeyCode == Keys.D))
        {
            await ToggleSplitViewAsync();
            return true;
        }

        if ((eventArgs.Alt && eventArgs.KeyCode == Keys.R) || (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.R))
        {
            await ToggleReaderModeForActiveTabAsync();
            return true;
        }

        if (eventArgs.Control && eventArgs.Alt && eventArgs.KeyCode == Keys.S)
        {
            await CaptureScreenshotForActiveTabAsync();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.V)
        {
            ToggleVerticalTabs();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.P)
        {
            OpenCommandPalette();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.K)
        {
            OpenTabQuickSwitchPalette();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.Z)
        {
            ApplyZenMode(!_isZenMode, persist: true, showToast: true);
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.S)
        {
            SaveWorkspaceSnapshot();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.O)
        {
            ShowSessionsWindow(workspaceOnlyMode: true);
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.T)
        {
            var reopened = await _tabManager.ReopenLastClosedTabAsync();
            if (reopened)
            {
                RefreshToolbar();
                RefreshMetrics();
                Log("已恢复最近关闭标签");
            }
            else
            {
                Log("没有可恢复的关闭标签");
            }

            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.T)
        {
            await _tabManager.CreateTabAsync(new Uri("about:blank"));
            RefreshToolbar();
            RefreshMetrics();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.W)
        {
            await _tabManager.CloseActiveTabAsync();
            RefreshToolbar();
            RefreshMetrics();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.W)
        {
            await CloseOtherTabsKeepActiveAsync();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.L)
        {
            await ApplyTabOpsByCommandAsync(["closeleft"]);
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.R)
        {
            await ApplyTabOpsByCommandAsync(["closeright"]);
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.D)
        {
            await ApplyTabOpsByCommandAsync(["dedupe"]);
            return true;
        }

        if ((eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.R) || eventArgs.KeyCode == Keys.F5)
        {
            _tabManager.ActiveTab?.Reload();
            return true;
        }

        if (eventArgs.Alt && eventArgs.KeyCode == Keys.Left)
        {
            if (_tabManager.ActiveTab?.CanGoBack == true)
            {
                _tabManager.ActiveTab.GoBack();
                RefreshToolbar();
            }

            return true;
        }

        if (eventArgs.Alt && eventArgs.KeyCode == Keys.Right)
        {
            if (_tabManager.ActiveTab?.CanGoForward == true)
            {
                _tabManager.ActiveTab.GoForward();
                RefreshToolbar();
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.PageUp)
        {
            if (TryMoveActiveTabByDelta(-1))
            {
                Log("已将当前标签向左移动");
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.PageDown)
        {
            if (TryMoveActiveTabByDelta(1))
            {
                Log("已将当前标签向右移动");
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.Left)
        {
            if (TryMoveActiveTabByDelta(-1))
            {
                Log("已将当前标签向左移动");
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.Right)
        {
            if (TryMoveActiveTabByDelta(1))
            {
                Log("已将当前标签向右移动");
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.KeyCode == Keys.Tab)
        {
            if (TryCycleTabSelection(forward: !eventArgs.Shift))
            {
                return true;
            }
        }

        if (eventArgs.Control && !eventArgs.Shift)
        {
            var tabIndex = eventArgs.KeyCode switch
            {
                Keys.D1 or Keys.NumPad1 => 0,
                Keys.D2 or Keys.NumPad2 => 1,
                Keys.D3 or Keys.NumPad3 => 2,
                Keys.D4 or Keys.NumPad4 => 3,
                Keys.D5 or Keys.NumPad5 => 4,
                Keys.D6 or Keys.NumPad6 => 5,
                Keys.D7 or Keys.NumPad7 => 6,
                Keys.D8 or Keys.NumPad8 => 7,
                Keys.D9 or Keys.NumPad9 => 8,
                _ => -1
            };
            if (tabIndex >= 0 && TrySelectTabByIndex(tabIndex))
            {
                return true;
            }
        }

        if ((eventArgs.Control && eventArgs.KeyCode == Keys.L) || (eventArgs.Alt && eventArgs.KeyCode == Keys.D))
        {
            if (_isZenMode)
            {
                ApplyZenMode(false, persist: true, showToast: false);
            }

            _addressTextBox.Focus();
            _addressTextBox.SelectAll();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutAddBookmarkEnabled, _config.ShortcutAddBookmark, eventArgs))
        {
            AddCurrentPageBookmark();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutNewTabEnabled, _config.ShortcutNewTab, eventArgs))
        {
            await _tabManager.CreateTabAsync(new Uri(_config.StartPage));
            RefreshToolbar();
            RefreshMetrics();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutCloseTabEnabled, _config.ShortcutCloseTab, eventArgs))
        {
            await _tabManager.CloseActiveTabAsync();
            RefreshToolbar();
            RefreshMetrics();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutFocusAddressEnabled, _config.ShortcutFocusAddress, eventArgs))
        {
            _addressTextBox.Focus();
            _addressTextBox.SelectAll();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutRefreshEnabled, _config.ShortcutRefresh, eventArgs))
        {
            _tabManager.ActiveTab?.Reload();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutDevToolsEnabled, _config.ShortcutDevTools, eventArgs))
        {
            _tabManager.ActiveTab?.OpenDevTools();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutToggleBookmarksBarEnabled, _config.ShortcutToggleBookmarksBar, eventArgs))
        {
            ToggleBookmarksBarVisibility();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutRestoreClosedTabEnabled, _config.ShortcutRestoreClosedTab, eventArgs))
        {
            var reopened = await _tabManager.ReopenLastClosedTabAsync();
            if (!reopened)
            {
                Log("没有可恢复的关闭标签");
            }
            else
            {
                Log("已恢复最近关闭标签");
                RefreshToolbar();
                RefreshMetrics();
            }

            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutOpenHistoryEnabled, _config.ShortcutOpenHistory, eventArgs))
        {
            ShowHistoryWindow();
            return true;
        }

        if (IsShortcutTriggered(_config.ShortcutFindInPageEnabled, _config.ShortcutFindInPage, eventArgs))
        {
            ShowFindBar();
            return true;
        }

        // 中文注释：无痕窗口固定快捷键 Ctrl+Shift+N，不参与自定义快捷键体系。
        if (eventArgs.Control && eventArgs.Shift && !eventArgs.Alt && eventArgs.KeyCode == Keys.N)
        {
            LaunchBrowserProcess(isPrivateWindow: true, useCurrentProfile: false);
            return true;
        }

        // 中文注释：独立双开窗口固定快捷键 Ctrl+Shift+M（全新独立 profile，数据不共享），与无痕的 Ctrl+Shift+N 并列。
        if (eventArgs.Control && eventArgs.Shift && !eventArgs.Alt && eventArgs.KeyCode == Keys.M)
        {
            LaunchBrowserProcess(isPrivateWindow: false, useCurrentProfile: false);
            return true;
        }

        // 中文注释：查看源码固定快捷键 Ctrl+U（开发者向），新标签打开 view-source。
        if (eventArgs.Control && !eventArgs.Shift && !eventArgs.Alt && eventArgs.KeyCode == Keys.U)
        {
            await OpenActiveTabSourceViewAsync();
            return true;
        }

        // 中文注释：页面缩放固定快捷键 Ctrl+加号/减号/0（含小键盘），不参与自定义快捷键体系。
        if (eventArgs.Control && !eventArgs.Alt && !eventArgs.Shift)
        {
            switch (eventArgs.KeyCode)
            {
                case Keys.Oemplus or Keys.Add:
                    AdjustActiveTabZoom(1);
                    return true;
                case Keys.OemMinus or Keys.Subtract:
                    AdjustActiveTabZoom(-1);
                    return true;
                case Keys.D0 or Keys.NumPad0:
                    _tabManager.ActiveTab?.SetZoomFactor(1.0);
                    return true;
            }
        }

        // 中文注释：插件自定义快捷键兜底：WebView 未聚焦（工具栏/地址栏）时由窗体触发“在当前页面运行”；
        // 页面聚焦时由插件桥脚本在页面内直接派发运行事件，两路互补。
        foreach (var (pluginId, pluginName, shortcut, _) in _pluginManager.GetRunShortcutBindings())
        {
            if (ShortcutParser.TryParse(shortcut, out var pluginKeys) && eventArgs.KeyData == pluginKeys)
            {
                RunPluginOnCurrentPage(pluginId, pluginName);
                return true;
            }
        }

        return false;
    }

    private static bool IsShortcutTriggered(bool enabled, string configuredShortcut, KeyEventArgs eventArgs)
    {
        if (!enabled)
        {
            return false;
        }

        if (!ShortcutParser.TryParse(configuredShortcut, out var expected))
        {
            return false;
        }

        return eventArgs.KeyData == expected;
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x0084;
        const int wmNcRButtonUp = 0x00A5;
        const int wmNcLButtonDown = 0x00A1;
        const int wmNcRButtonDown = 0x00A4;
        const int wmNcMButtonDown = 0x00A7;
        const int wmSysCommand = 0x0112;
        const int scRestore = 0xF120;
        const int scMinimize = 0xF020;
        const int htCaption = 2;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        // 无边框模式下支持边缘与四角原生平滑拖拽调整窗口大小
        if (m.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            var screenPoint = new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
            var clientPoint = PointToClient(screenPoint);
            const int resizeBorder = 6;

            var onLeft = clientPoint.X <= resizeBorder;
            var onRight = clientPoint.X >= ClientSize.Width - resizeBorder;
            var onTop = clientPoint.Y <= resizeBorder;
            var onBottom = clientPoint.Y >= ClientSize.Height - resizeBorder;

            if (onTop && onLeft) { m.Result = (IntPtr)htTopLeft; return; }
            if (onTop && onRight) { m.Result = (IntPtr)htTopRight; return; }
            if (onBottom && onLeft) { m.Result = (IntPtr)htBottomLeft; return; }
            if (onBottom && onRight) { m.Result = (IntPtr)htBottomRight; return; }
            if (onLeft) { m.Result = (IntPtr)htLeft; return; }
            if (onRight) { m.Result = (IntPtr)htRight; return; }
            if (onTop) { m.Result = (IntPtr)htTop; return; }
            if (onBottom) { m.Result = (IntPtr)htBottom; return; }
        }

        if (m.Msg == wmNcLButtonDown || m.Msg == wmNcRButtonDown || m.Msg == wmNcMButtonDown)
        {
            DismissTransientMenus();
        }

        if (m.Msg == wmSysCommand)
        {
            var command = (int)((long)m.WParam & 0xFFF0);
            if ((command == scRestore || command == scMinimize) &&
                _startupCompleted &&
                WindowState != FormWindowState.Minimized &&
                GetForegroundWindow() == Handle)
            {
                BeginMinimizeTransition();
                return;
            }
        }

        if (m.Msg == wmNcRButtonUp && m.WParam.ToInt32() == htCaption)
        {
            DismissTransientMenus();
            var x = unchecked((short)(long)m.LParam);
            var y = unchecked((short)((long)m.LParam >> 16));
            _windowModeContextMenu.Show(new Point(x, y));
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        DrawAddressFocusChrome(eventArgs.Graphics);
        DrawFloatingPanelShadow(eventArgs.Graphics, _addressSuggestionPanel, 12);
        DrawFloatingPanelShadow(eventArgs.Graphics, _toastPanel, 16);
        DrawFloatingPanelShadow(eventArgs.Graphics, _findBar, 12);

        // 窗口处于普通大小时，在最外层勾勒 1px 精致边框，避免与浅色或深色桌面背景混淆
        if (WindowState == FormWindowState.Normal && Width > 2 && Height > 2)
        {
            using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
            eventArgs.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        // 中文注释：窗口最小化或布局早期，ClientRectangle 的宽或高可能为 0，
        // 此时 LinearGradientBrush / HatchBrush 会抛 ArgumentException（Rectangle ... cannot have a width or height equal to 0），
        // 被全局异常处理捕获后弹出“浏览器异常”对话框。这里直接回退到基类纯色填充并返回，避免误报。
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            base.OnPaintBackground(eventArgs);
            return;
        }

        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.16F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.08F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.22F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.05F);
        using var gradient = new LinearGradientBrush(
            ClientRectangle,
            startColor,
            endColor,
            90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glowPrimary = Color.FromArgb(AppTheme.IsDark ? 56 : 40, AppTheme.Accent);
        var glowSecondaryBase = AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.40F);
        var glowSecondary = Color.FromArgb(AppTheme.IsDark ? 40 : 30, glowSecondaryBase);
        var glowTertiaryBase = AppTheme.Mix(AppTheme.Accent, AppTheme.Warning, 0.34F);
        var glowTertiary = Color.FromArgb(AppTheme.IsDark ? 30 : 24, glowTertiaryBase);

        var motionFactor = _isZenMode ? 0F : 1F;
        var phase = _ambientMotionPhase;
        var driftAx = (int)Math.Round(Math.Sin(phase * 0.78F) * 24F * motionFactor);
        var driftAy = (int)Math.Round(Math.Cos(phase * 0.53F) * 16F * motionFactor);
        var driftBx = (int)Math.Round(Math.Cos((phase * 0.67F) + 0.9F) * 18F * motionFactor);
        var driftBy = (int)Math.Round(Math.Sin((phase * 0.61F) + 0.3F) * 13F * motionFactor);
        var driftCx = (int)Math.Round(Math.Sin((phase * 0.83F) + 2.2F) * 20F * motionFactor);
        var driftCy = (int)Math.Round(Math.Cos((phase * 0.72F) + 1.1F) * 14F * motionFactor);

        var blobA = new Rectangle((int)(ClientSize.Width * 0.67F) + driftAx, -120 + driftAy, 360, 270);
        var blobB = new Rectangle(-130 + driftBx, (int)(ClientSize.Height * 0.34F) + driftBy, 280, 260);
        var blobC = new Rectangle((int)(ClientSize.Width * 0.34F) + driftCx, ClientSize.Height - 170 + driftCy, 320, 220);

        using var brushA = new SolidBrush(glowPrimary);
        using var brushB = new SolidBrush(glowSecondary);
        using var brushC = new SolidBrush(glowTertiary);
        eventArgs.Graphics.FillEllipse(brushA, blobA);
        eventArgs.Graphics.FillEllipse(brushB, blobB);
        eventArgs.Graphics.FillEllipse(brushC, blobC);

        var textureStyle = AppTheme.IsDark ? HatchStyle.WideDownwardDiagonal : HatchStyle.LargeGrid;
        using var texture = new HatchBrush(
            textureStyle,
            Color.FromArgb(AppTheme.IsDark ? 11 : 8, AppTheme.Accent),
            Color.Transparent);
        eventArgs.Graphics.FillRectangle(texture, ClientRectangle);

        if (!_isZenMode)
        {
            using var dividerPen = new Pen(AppTheme.IsDark
                ? Color.FromArgb(108, 126, 154)
                : Color.FromArgb(174, 199, 232));
            if (_titleBar.Visible)
            {
                eventArgs.Graphics.DrawLine(dividerPen, 0, _titleBar.Bottom, ClientSize.Width, _titleBar.Bottom);
            }

            if (_topBar.Visible)
            {
                eventArgs.Graphics.DrawLine(dividerPen, 0, _topBar.Bottom, ClientSize.Width, _topBar.Bottom);
            }

            if (_addressBar.Visible)
            {
                eventArgs.Graphics.DrawLine(dividerPen, 0, _addressBar.Bottom, ClientSize.Width, _addressBar.Bottom);
            }

            if (_bookmarksBar.Visible)
            {
                eventArgs.Graphics.DrawLine(dividerPen, 0, _bookmarksBar.Bottom, ClientSize.Width, _bookmarksBar.Bottom);
            }

            if (_statusBarPanel.Strip.Visible)
            {
                eventArgs.Graphics.DrawLine(dividerPen, 0, _statusBarPanel.Strip.Top - 1, ClientSize.Width, _statusBarPanel.Strip.Top - 1);
            }
        }
    }

    private void DrawAddressFocusChrome(Graphics graphics)
    {
        var textBox = _addressTextBox.TextBox;
        if (!_addressTextBoxFocused ||
            !_topBar.Visible ||
            !textBox.Visible ||
            textBox.IsDisposed ||
            textBox.Parent is null ||
            !textBox.IsHandleCreated)
        {
            return;
        }

        var screenRect = textBox.RectangleToScreen(textBox.ClientRectangle);
        var focusRect = RectangleToClient(screenRect);
        if (focusRect.Width <= 0 || focusRect.Height <= 0)
        {
            return;
        }

        focusRect.Inflate(6, 5);
        using var path = BuildRoundedRectanglePath(focusRect, radius: 12);
        using var glowBrush = new SolidBrush(Color.FromArgb(AppTheme.IsDark ? 36 : 28, AppTheme.Accent));
        using var outerPen = new Pen(Color.FromArgb(AppTheme.IsDark ? 150 : 126, AppTheme.Accent), 1.9F);
        using var innerPen = new Pen(
            AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.30F)
                : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.14F),
            1F);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(glowBrush, path);
        graphics.DrawPath(outerPen, path);

        var innerRect = Rectangle.Inflate(focusRect, -2, -2);
        using var innerPath = BuildRoundedRectanglePath(innerRect, radius: 10);
        graphics.DrawPath(innerPen, innerPath);
    }

    private void DrawFloatingPanelShadow(Graphics graphics, Control panel, int radius)
    {
        if (!panel.Visible || !panel.IsHandleCreated || panel.Width <= 0 || panel.Height <= 0)
        {
            return;
        }

        var panelRect = RectangleToClient(panel.RectangleToScreen(panel.ClientRectangle));
        if (panelRect.Width <= 0 || panelRect.Height <= 0)
        {
            return;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < 4; i++)
        {
            var shadowRect = Rectangle.Inflate(panelRect, i + 1, i + 1);
            var alpha = AppTheme.IsDark ? Math.Max(6, 34 - (i * 6)) : Math.Max(6, 26 - (i * 5));
            using var shadowPen = new Pen(Color.FromArgb(alpha, Color.Black), 1F);
            using var shadowPath = BuildRoundedRectanglePath(shadowRect, radius + i + 1);
            graphics.DrawPath(shadowPen, shadowPath);
        }
    }

    private void DrawAddressSuggestionSurface(Graphics graphics)
    {
        var bounds = _addressSuggestionPanel.ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var cardRect = Rectangle.Inflate(bounds, -1, -1);
        var topColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.18F)
            : AppTheme.Mix(AppTheme.SurfaceAlt, AppTheme.Accent, 0.10F);
        var bottomColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.12F)
            : AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.03F);
        var borderColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.PanelBorder, Color.White, 0.18F)
            : AppTheme.Mix(AppTheme.PanelBorder, Color.Black, 0.08F);
        var highlightColor = AppTheme.IsDark
            ? Color.FromArgb(86, 255, 255, 255)
            : Color.FromArgb(112, 255, 255, 255);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var cardPath = BuildRoundedRectanglePath(cardRect, radius: 12);
        using var fillBrush = new LinearGradientBrush(cardRect, topColor, bottomColor, 90F);
        using var borderPen = new Pen(borderColor, 1.1F);
        using var highlightPen = new Pen(highlightColor, 1F);
        graphics.FillPath(fillBrush, cardPath);
        graphics.DrawPath(borderPen, cardPath);
        graphics.DrawLine(highlightPen, cardRect.Left + 10, cardRect.Top + 3, cardRect.Right - 10, cardRect.Top + 3);
    }

    private void DrawToastSurface(Graphics graphics)
    {
        var bounds = _toastPanel.ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var cardRect = Rectangle.Inflate(bounds, -1, -1);
        var topColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.10F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.24F);
        var bottomColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.Black, 0.20F)
            : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.06F);
        var borderColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.30F)
            : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.18F);
        var highlightColor = AppTheme.IsDark
            ? Color.FromArgb(96, 255, 255, 255)
            : Color.FromArgb(130, 255, 255, 255);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var cardPath = BuildRoundedRectanglePath(cardRect, radius: 16);
        using var fillBrush = new LinearGradientBrush(cardRect, topColor, bottomColor, 90F);
        using var borderPen = new Pen(borderColor, 1.2F);
        using var highlightPen = new Pen(highlightColor, 1F);
        graphics.FillPath(fillBrush, cardPath);
        graphics.DrawPath(borderPen, cardPath);
        graphics.DrawLine(highlightPen, cardRect.Left + 12, cardRect.Top + 3, cardRect.Right - 12, cardRect.Top + 3);

        var progress = Math.Clamp((float)_toastRemainingMs / ToastDurationMs, 0F, 1F);
        if (progress <= 0F)
        {
            return;
        }

        var progressBounds = new Rectangle(cardRect.Left + 4, cardRect.Bottom - 5, Math.Max(0, cardRect.Width - 8), 3);
        var width = (int)Math.Round(progressBounds.Width * progress);
        if (width <= 0)
        {
            return;
        }

        var progressRect = new Rectangle(progressBounds.Left, progressBounds.Top, width, progressBounds.Height);
        var progressColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Success, AppTheme.Accent, 0.25F)
            : AppTheme.Mix(AppTheme.Success, AppTheme.Accent, 0.35F);
        using var progressBrush = new SolidBrush(progressColor);
        graphics.FillRectangle(progressBrush, progressRect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 中文注释：停止所有 6 个定时器，避免窗口释放后仍触发 Tick 回调访问已释放对象。
            try { _maintenanceTimer.Stop(); } catch { }
            try { _refreshClickTimer.Stop(); } catch { }
            try { _toastTimer.Stop(); } catch { }
            try { _addressSuggestionRefreshTimer.Stop(); } catch { }
            try { _ambientMotionTimer.Stop(); } catch { }
            try { _windowTransitionTimer.Stop(); } catch { }

            // 中文注释：取消正在进行的 ToolStripItem 背景色渐变动画，释放静态 Timer。
            try { UI.Chrome.ToolStripItemColorAnimator.CancelAll(); } catch { }

            _windowIcon?.Dispose();
            _windowIcon = null;
        }

        base.Dispose(disposing);
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLongW", SetLastError = true)]
    private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static void SetRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        var regionHandle = CreateRoundRectRgn(0, 0, control.Width, control.Height, radius, radius);
        if (regionHandle == IntPtr.Zero)
        {
            return;
        }

        control.Region?.Dispose();
        control.Region = Region.FromHrgn(regionHandle);
        DeleteObject(regionHandle);
    }

    private static GraphicsPath BuildRoundedRectanglePath(Rectangle bounds, int radius) =>
        AppTheme.BuildRoundedRect(bounds, radius);

    private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetClassLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private readonly record struct WorkspaceOverview(
        int TotalTabs,
        int DuplicateCount,
        int HeavyWorkspaceCount,
        int StaleWorkspaceCount);

    private sealed record AddressSuggestionEntry(
        string Category,
        string PrimaryText,
        string SecondaryText,
        string Value,
        bool IsHeader,
        int MatchScore,
        string CommandId = "",
        string BadgeText = "",
        Guid? TargetTabId = null);

    private sealed record AddressCommandDefinition(
        string CommandId,
        string Command,
        string DisplayName,
        string Description,
        string[] Aliases);

    private sealed record AddressCommandArgumentOption(
        string Token,
        string Description,
        string[] Aliases);

    private sealed record UiAppearancePreset(
        string Id,
        string DisplayName,
        string Theme,
        string Density,
        string Accent,
        string AccentStrength,
        string Font);

    private sealed record RecentAddressCommandEntry(
        string CommandId,
        string ArgumentToken,
        DateTime UsedAtUtc);

    private sealed record TabColorOption(
        string Key,
        string DisplayName);

    private sealed record BookmarkBarTag(Guid NodeId, Guid? ParentFolderId);

    private void ToggleSidePanel()
    {
        _sidePanel.Visible = !_sidePanel.Visible;
        if (_sidePanel.Visible)
        {
            _sidePanel.BringToFront();
            _sidePanel.RefreshActiveContent();
        }
    }

    private async Task CaptureScreenshotForActiveTabAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is not null)
        {
            var bmp = await activeTab.CaptureScreenshotAsync();
            if (bmp is not null)
            {
                Clipboard.SetImage(bmp);
                ShowToast("网页截图已复制到剪贴板！📸");
            }
            else
            {
                ShowToast("截图失败，请确保页面已加载完毕");
            }
        }
    }

    private void ToggleVerticalTabs()
    {
        _verticalTabStrip.Visible = !_verticalTabStrip.Visible;
        if (_verticalTabStrip.Visible)
        {
            _verticalTabStrip.BringToFront();
            _verticalTabStrip.RebuildTabs();
            ShowToast("已开启垂直标签栏 (Ctrl+Shift+V 切换)");
        }
        else
        {
            ShowToast("已恢复常规水平标签栏");
        }
        RefreshToolbar();
    }

    private async Task ToggleSplitViewAsync()
    {
        if (_splitPanel.IsSplitActive)
        {
            _splitPanel.DeactivateSplit(_tabControl, this);
            ShowToast("已退出双屏分屏浏览");
        }
        else
        {
            var activeTab = _tabManager.ActiveTab;
            var targetUri = activeTab?.CurrentUri ?? new Uri("https://cn.bing.com");
            await _splitPanel.ActivateSplitAsync(
                _tabControl,
                targetUri,
                () => _tabManager.ActiveTab?.CurrentUri,
                uri => _ = NavigateActiveTabAsync(uri));
            ShowToast("已开启双屏分屏并行浏览 (可拖拽中缝，Alt+D 切换)");
        }
        RefreshToolbar();
    }

    private async Task ToggleReaderModeForActiveTabAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is null)
        {
            ShowToast("当前无活跃标签页");
            return;
        }

        var active = await activeTab.ToggleReaderModeAsync();
        ShowToast(active ? "已开启沉浸式阅读模式 (Esc 退出) 📖" : "已退出沉浸阅读模式");
        RefreshToolbar();
    }
    private void ShowQrCodeForActiveTab()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.CurrentUri is null)
        {
            ShowToast("当前页面无可用的网址");
            return;
        }

        using var qrDialog = new QrCodeCardDialog(tab.CurrentUri.AbsoluteUri, tab.Title);
        qrDialog.ShowDialog(this);
    }

    private async Task NavigateActiveTabAsync(Uri uri)
    {
        if (TryHandleInternalBrowserUrl(uri.AbsoluteUri, userInitiated: true))
        {
            return;
        }

        if (_tabManager.ActiveTab is not null)
        {
            await _tabManager.ActiveTab.NavigateAsync(uri);
        }
        else
        {
            await _tabManager.CreateTabAsync(uri);
        }
        RefreshToolbar();
    }

    private async Task ClearCookiesAndDataForCurrentHostAsync()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.CurrentUri is null)
        {
            return;
        }

        try
        {
            await tab.ClearCacheAndReloadAsync();
            ShowToast("已清理当前站点缓存并强刷");
        }
        catch (Exception ex)
        {
            ShowToast($"清理失败: {ex.Message}");
        }
    }

    private async Task ViewActiveTabSourceAsync()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.CurrentUri is null)
        {
            return;
        }

        var sourceUrl = $"view-source:{tab.CurrentUri.AbsoluteUri}";
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            await _tabManager.CreateTabAsync(sourceUri);
            RefreshToolbar();
        }
    }
}