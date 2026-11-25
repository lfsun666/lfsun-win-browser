using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.Security;
using System.Runtime.InteropServices;

namespace Lfsun.Win.Browser.Browser;

public sealed class TabManager
{
    private readonly List<BrowserTab> _tabs = new();
    private readonly TabControl _tabControl;
    private readonly BrowserConfig _config;
    private readonly DownloadManager _downloadManager;
    private readonly PluginManager _pluginManager;
    private readonly CredentialAutofillService _credentialAutofillService;
    private readonly string _profileStoragePath;
    private readonly Action<string> _log;
    private readonly List<ClosedTabEntry> _recentlyClosedTabs = [];

    public TabManager(
        TabControl tabControl,
        BrowserConfig config,
        DownloadManager downloadManager,
        PluginManager pluginManager,
        CredentialAutofillService credentialAutofillService,
        string profileStoragePath,
        Action<string> log)
    {
        _tabControl = tabControl;
        _config = config;
        _downloadManager = downloadManager;
        _pluginManager = pluginManager;
        _credentialAutofillService = credentialAutofillService;
        _profileStoragePath = profileStoragePath;
        _log = log;
    }

    public IReadOnlyList<BrowserTab> Tabs => _tabs;

    public BrowserTab? ActiveTab
    {
        get
        {
            var selected = _tabControl.SelectedTab;
            if (selected is null)
            {
                return null;
            }

            return _tabs.FirstOrDefault(tab => ReferenceEquals(tab.TabPage, selected));
        }
    }

    public event Action<BrowserTab?>? ActiveTabChanged;
    public event Action? TabsChanged;
    public event Action<Uri, string>? PageVisited;
    public event Action? TabSurfaceInteracted;

    /// <summary>
    /// 中文注释：页内查找匹配变化（标签、当前序号、总匹配数），由各标签转发。
    /// </summary>
    public event Action<BrowserTab, int, int>? TabFindMatchesChanged;

    /// <summary>
    /// 中文注释：标签缩放倍率变化（标签、新倍率），由各标签转发。
    /// </summary>
    public event Action<BrowserTab, double>? TabZoomChanged;

    /// <summary>
    /// 中文注释：按主机名解析记忆缩放的委托，由宿主窗口注入并透传给每个标签。
    /// </summary>
    public Func<string, double?>? ZoomOverrideResolver { get; set; }

    /// <summary>
    /// 中文注释：广告过滤服务，由宿主窗口注入并透传给每个标签。
    /// </summary>
    public AdBlockService? AdBlockService { get; set; }

    /// <summary>
    /// 中文注释：标签广告拦截计数变化（标签、本页累计拦截数），由各标签转发。
    /// </summary>
    public event Action<BrowserTab, int>? TabAdBlockedCountChanged;

    /// <summary>
    /// 中文注释：访问专有浏览器内部协议事件（如 edge://、chrome:// 等）。
    /// </summary>
    public event Action<BrowserTab, string>? InternalUrlNavigated;

    /// <summary>
    /// 中文注释：内部路由页面交互事件（如点击了外观、书签、历史按钮）。
    /// </summary>
    public event Action<BrowserTab, string>? InternalUrlActionRequested;

    /// <summary>
    /// 中文注释：标签音频播放或静音状态变化事件，由各标签转发。
    /// </summary>
    public event Action<BrowserTab>? TabAudioStateChanged;

    /// <summary>
    /// 中文注释：标签导航加载状态变化（标签, 是否加载中），由各标签转发给主窗口驱动顶部进度条。
    /// </summary>
    public event Action<BrowserTab, bool>? TabLoadingStateChanged;

    public IReadOnlyList<ClosedTabEntry> RecentlyClosedTabs => _recentlyClosedTabs;

    /// <summary>
    /// 中文注释：新标签页速拨面板 HTML 提供者，由宿主窗口注入并透传给每个标签。
    /// </summary>
    public Func<string?>? NewTabPageHtmlProvider { get; set; }

    public async Task<BrowserTab> CreateTabAsync(Uri initialUri)
    {
        var page = new TabPage("新标签页");

        var tab = new BrowserTab(
            page,
            _config,
            _downloadManager,
            _pluginManager,
            _credentialAutofillService,
            _profileStoragePath,
            _log);
        tab.StateChanged += HandleTabStateChanged;
        tab.PageVisited += HandlePageVisited;
        tab.NewTabRequested += HandleTabNewTabRequestedAsync;
        tab.SurfaceInteracted += HandleTabSurfaceInteracted;
        tab.FindMatchesChanged += HandleTabFindMatchesChanged;
        tab.ZoomChanged += HandleTabZoomChanged;
        tab.AdBlockedCountChanged += HandleTabAdBlockedCountChanged;
        tab.AudioStateChanged += HandleTabAudioStateChanged;
        tab.LoadingStateChanged += HandleTabLoadingStateChanged;
        tab.InternalUrlNavigated += (t, u) => InternalUrlNavigated?.Invoke(t, u);
        tab.InternalUrlActionRequested += (t, a) => InternalUrlActionRequested?.Invoke(t, a);
        tab.NewTabHtmlProvider = () => NewTabPageHtmlProvider?.Invoke();
        tab.ZoomOverrideResolver = host => ZoomOverrideResolver?.Invoke(host);
        tab.AdBlockService = AdBlockService;
        _tabs.Add(tab);
        _tabControl.TabPages.Add(page);

        _tabControl.SelectedTab = page;
        await tab.NavigateAsync(initialUri);
        tab.MarkActivated();

        await EnforceMemoryBudgetAsync();
        PublishTabEvents();
        return tab;
    }

    public async Task CloseActiveTabAsync()
    {
        await CloseTabAsync(ActiveTab);
    }

    public async Task CloseTabAsync(BrowserTab? tab)
    {
        if (tab is null)
        {
            return;
        }

        if (_tabs.All(item => !ReferenceEquals(item, tab)))
        {
            return;
        }

        if (!ReferenceEquals(ActiveTab, tab))
        {
            _tabControl.SelectedTab = tab.TabPage;
        }

        var current = tab;
        current.StateChanged -= HandleTabStateChanged;
        current.PageVisited -= HandlePageVisited;
        current.NewTabRequested -= HandleTabNewTabRequestedAsync;
        current.SurfaceInteracted -= HandleTabSurfaceInteracted;
        current.FindMatchesChanged -= HandleTabFindMatchesChanged;
        current.ZoomChanged -= HandleTabZoomChanged;
        current.AdBlockedCountChanged -= HandleTabAdBlockedCountChanged;
        current.AudioStateChanged -= HandleTabAudioStateChanged;
        current.LoadingStateChanged -= HandleTabLoadingStateChanged;

        if (current.CurrentUri is not null &&
            (string.Equals(current.CurrentUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current.CurrentUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            _recentlyClosedTabs.Insert(0, new ClosedTabEntry(
                string.IsNullOrWhiteSpace(current.Title) ? "未命名标签页" : current.Title,
                current.CurrentUri.AbsoluteUri,
                DateTime.UtcNow));
            if (_recentlyClosedTabs.Count > 30)
            {
                _recentlyClosedTabs.RemoveRange(30, _recentlyClosedTabs.Count - 30);
            }
        }

        _tabs.Remove(current);
        _tabControl.TabPages.Remove(current.TabPage);
        await current.DisposeAsync();

        if (_tabs.Count == 0)
        {
            await CreateTabAsync(new Uri(_config.StartPage));
        }

        await ActivateSelectedTabAsync();
        PublishTabEvents();
    }

    public async Task<bool> ReopenLastClosedTabAsync()
    {
        if (_recentlyClosedTabs.Count == 0)
        {
            return false;
        }

        var last = _recentlyClosedTabs[0];
        _recentlyClosedTabs.RemoveAt(0);
        if (!Uri.TryCreate(last.Url, UriKind.Absolute, out var uri) || uri is null)
        {
            return false;
        }

        await CreateTabAsync(uri);
        PublishTabEvents();
        return true;
    }

    public int IndexOf(BrowserTab tab) => _tabs.IndexOf(tab);

    public async Task SelectTabAsync(BrowserTab? tab)
    {
        if (tab is null || !_tabs.Contains(tab))
        {
            return;
        }

        _tabControl.SelectedTab = tab.TabPage;
        await ActivateSelectedTabAsync();
    }

    public async Task ActivateSelectedTabAsync()
    {
        var active = ActiveTab;
        if (active is null)
        {
            return;
        }

        try
        {
            await active.ResumeIfNeededAsync();
            active.MarkActivated();
            await EnforceMemoryBudgetAsync();
            PublishTabEvents();
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80004004))
        {
            _log($"WebView2 控件初始化被中止，已跳过本次激活：{ex.Message}");
        }
        catch (ObjectDisposedException ex)
        {
            _log($"WebView2 控件已释放，已跳过本次激活：{ex.Message}");
        }
    }

    public async Task SuspendIdleTabsAsync()
    {
        if (_config.SuspendAfterMinutes <= 0)
        {
            return;
        }

        var deadline = DateTime.UtcNow.AddMinutes(-_config.SuspendAfterMinutes);
        foreach (var tab in _tabs
                     .Where(tab => !ReferenceEquals(tab, ActiveTab))
                     .Where(tab => !tab.IsSuspended)
                     .Where(tab => tab.LastActivatedUtc <= deadline)
                     .OrderBy(tab => tab.LastActivatedUtc))
        {
            await tab.SuspendAsync();
        }

        PublishTabEvents();
    }

    /// <summary>
    /// 中文注释：一键休眠所有后台空闲标签页，立即释放内存。
    /// </summary>
    public async Task<int> SuspendAllBackgroundTabsAsync()
    {
        var count = 0;
        foreach (var tab in _tabs
                     .Where(tab => !ReferenceEquals(tab, ActiveTab))
                     .Where(tab => !tab.IsSuspended)
                     .ToList())
        {
            await tab.SuspendAsync();
            count++;
        }

        PublishTabEvents();
        return count;
    }

    /// <summary>
    /// 中文注释：一键切换所有标签页的全局静音状态。
    /// </summary>
    public bool ToggleMuteAllTabs()
    {
        var anyUnmuted = _tabs.Any(tab => !tab.IsMuted);
        var targetMute = anyUnmuted;
        foreach (var tab in _tabs)
        {
            tab.IsMuted = targetMute;
        }

        PublishTabEvents();
        return targetMute;
    }

    public int LiveTabCount => _tabs.Count(tab => tab.IsLive);

    /// <summary>
    /// 中文注释：设置保存后批量刷新所有标签页的广告过滤过滤器注册状态
    /// （开关开时注册 "*" 全量过滤器、关时移除，详见 BrowserTab.UpdateWebResourceFilterRegistration）。
    /// </summary>
    public void RefreshWebResourceFilters()
    {
        foreach (var tab in _tabs)
        {
            tab.UpdateWebResourceFilterRegistration();
        }
    }

    /// <summary>
    /// 中文注释：设置保存后把 WebView2 内核的自动填充/密码保存开关同步到所有已初始化的标签页。
    /// </summary>
    public void RefreshWebViewSettings()
    {
        foreach (var tab in _tabs)
        {
            tab.UpdateWebViewSettings();
        }
    }

    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count || toIndex < 0 || toIndex >= _tabs.Count || fromIndex == toIndex)
        {
            return;
        }

        var movingTab = _tabs[fromIndex];
        var requestedTarget = toIndex;

        if (movingTab.IsPinned)
        {
            requestedTarget = Math.Min(requestedTarget, Math.Max(0, _tabs.Count(tab => tab.IsPinned) - 1));
        }
        else
        {
            requestedTarget = Math.Max(requestedTarget, _tabs.Count(tab => tab.IsPinned));
        }

        if (requestedTarget == fromIndex)
        {
            return;
        }

        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(requestedTarget, movingTab);

        _tabControl.TabPages.Remove(movingTab.TabPage);
        _tabControl.TabPages.Insert(requestedTarget, movingTab.TabPage);
        _tabControl.SelectedTab = movingTab.TabPage;
        PublishTabEvents();
    }

    public void SetPinned(BrowserTab? tab, bool pinned)
    {
        if (tab is null)
        {
            return;
        }

        var index = _tabs.FindIndex(item => ReferenceEquals(item, tab));
        if (index < 0)
        {
            return;
        }

        tab.SetPinned(pinned);

        _tabs.RemoveAt(index);
        _tabControl.TabPages.Remove(tab.TabPage);

        // 中文注释：固定标签始终位于普通标签之前；取消固定后同样插入固定分组末尾。
        var targetIndex = _tabs.TakeWhile(item => item.IsPinned).Count();
        _tabs.Insert(targetIndex, tab);
        _tabControl.TabPages.Insert(targetIndex, tab.TabPage);
        _tabControl.SelectedTab = tab.TabPage;
        PublishTabEvents();
    }

    public void SetColorTag(BrowserTab? tab, string? colorTagKey)
    {
        if (tab is null)
        {
            return;
        }

        if (_tabs.All(item => !ReferenceEquals(item, tab)))
        {
            return;
        }

        tab.SetColorTag(colorTagKey);
        PublishTabEvents();
    }

    private async Task EnforceMemoryBudgetAsync()
    {
        if (_config.MaxLiveTabs <= 0)
        {
            return;
        }

        while (LiveTabCount > _config.MaxLiveTabs)
        {
            var tabToSuspend = _tabs
                .Where(tab => !ReferenceEquals(tab, ActiveTab))
                .Where(tab => tab.IsLive)
                .OrderBy(tab => tab.LastActivatedUtc)
                .FirstOrDefault();

            if (tabToSuspend is null)
            {
                break;
            }

            await tabToSuspend.SuspendAsync();
        }
    }

    private void HandleTabStateChanged(BrowserTab _)
    {
        PublishTabEvents();
    }

    private void HandlePageVisited(BrowserTab _, Uri uri, string title)
    {
        PageVisited?.Invoke(uri, title);
    }

    private async Task HandleTabNewTabRequestedAsync(Uri uri)
    {
        await CreateTabAsync(uri);
    }

    private void HandleTabSurfaceInteracted(BrowserTab _)
    {
        TabSurfaceInteracted?.Invoke();
    }

    private void HandleTabFindMatchesChanged(BrowserTab tab, int activeIndex, int matchCount)
    {
        TabFindMatchesChanged?.Invoke(tab, activeIndex, matchCount);
    }

    private void HandleTabZoomChanged(BrowserTab tab, double zoomFactor)
    {
        TabZoomChanged?.Invoke(tab, zoomFactor);
    }

    private void HandleTabAdBlockedCountChanged(BrowserTab tab, int blockedCount)
    {
        TabAdBlockedCountChanged?.Invoke(tab, blockedCount);
    }

    private void HandleTabAudioStateChanged(BrowserTab tab)
    {
        TabAudioStateChanged?.Invoke(tab);
        PublishTabEvents();
    }

    private void HandleTabLoadingStateChanged(BrowserTab tab, bool isLoading)
        => TabLoadingStateChanged?.Invoke(tab, isLoading);

    private void PublishTabEvents()
    {
        ActiveTabChanged?.Invoke(ActiveTab);
        TabsChanged?.Invoke();
    }
}

