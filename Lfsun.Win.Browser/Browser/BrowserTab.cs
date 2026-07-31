using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.Security;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Browser;

public sealed class BrowserTab : IAsyncDisposable
{
    private const string SuspendedHintName = "__suspended_hint__";
    private const string PagePointerBridgeMessage = "__lfsun_page_pointerdown__";
    private const string PagePointerBridgeScript = """
        (() => {
            if (window.__lfsunMenuDismissBridgeInstalled) {
                return;
            }
            window.__lfsunMenuDismissBridgeInstalled = true;
            const notifyHost = () => {
                try {
                    window.chrome?.webview?.postMessage('__lfsun_page_pointerdown__');
                } catch {
                    // Ignore bridge failures; this is only used to dismiss host UI menus.
                }
            };
            window.addEventListener('pointerdown', notifyHost, true);
            window.addEventListener('contextmenu', notifyHost, true);
        })();
        """;
    private readonly BrowserConfig _config;
    private readonly DownloadManager _downloadManager;
    private readonly PluginManager _pluginManager;
    private readonly CredentialAutofillService _credentialAutofillService;
    private readonly string _profileStoragePath;
    private readonly Action<string> _log;
    private WebView2? _webView;
    private string _lastKnownUrl;
    private string _customTitle = string.Empty;
    private bool _findEventsHooked;

    public BrowserTab(
        TabPage tabPage,
        BrowserConfig config,
        DownloadManager downloadManager,
        PluginManager pluginManager,
        CredentialAutofillService credentialAutofillService,
        string profileStoragePath,
        Action<string> log)
    {
        Id = Guid.NewGuid();
        TabPage = tabPage;
        TabPage.Tag = this;
        _config = config;
        _downloadManager = downloadManager;
        _pluginManager = pluginManager;
        _credentialAutofillService = credentialAutofillService;
        _profileStoragePath = profileStoragePath;
        _log = log;
        _lastKnownUrl = _config.StartPage;
        LastActivatedUtc = DateTime.UtcNow;
        TabPage.Text = "新标签页";
    }

    public Guid Id { get; }
    public TabPage TabPage { get; }
    public bool IsSuspended { get; private set; }
    public bool IsPinned { get; private set; }
    public string ColorTagKey { get; private set; } = string.Empty;
    public DateTime LastActivatedUtc { get; private set; }
    public string Title { get; private set; } = "新标签页";
    public string CustomTitle => _customTitle;
    public bool HasCustomTitle => !string.IsNullOrWhiteSpace(_customTitle);
    public string DisplayTitle => HasCustomTitle ? _customTitle.Trim() : Title;
    public Uri? CurrentUri => Uri.TryCreate(_lastKnownUrl, UriKind.Absolute, out var uri) ? uri : null;
    public bool CanGoBack => _webView?.CoreWebView2?.CanGoBack ?? false;
    public bool CanGoForward => _webView?.CoreWebView2?.CanGoForward ?? false;
    public bool IsLive => !IsSuspended && _webView is not null;

    public event Action<BrowserTab>? StateChanged;
    public event Action<BrowserTab, Uri, string>? PageVisited;
    public event Func<Uri, Task>? NewTabRequested;
    public event Action<BrowserTab>? SurfaceInteracted;

    /// <summary>
    /// 中文注释：新标签页速拨面板 HTML 提供者；导航到 about:blank 时若返回非空内容则本地渲染。
    /// </summary>
    public Func<string?>? NewTabHtmlProvider { get; set; }

    /// <summary>
    /// 中文注释：页内查找匹配变化事件（当前序号、总匹配数）。
    /// </summary>
    public event Action<BrowserTab, int, int>? FindMatchesChanged;

    /// <summary>
    /// 中文注释：缩放倍率变化事件（新倍率），含用户 Ctrl+滚轮与快捷键两种来源。
    /// </summary>
    public event Action<BrowserTab, double>? ZoomChanged;

    /// <summary>
    /// 中文注释：按主机名解析记忆的缩放倍率，导航完成后自动应用；无记忆返回 null。
    /// </summary>
    public Func<string, double?>? ZoomOverrideResolver { get; set; }

    /// <summary>
    /// 中文注释：广告过滤服务（全局共享实例），未注入或开关关闭时不拦截。
    /// </summary>
    public AdBlockService? AdBlockService { get; set; }

    /// <summary>
    /// 中文注释：本页已拦截请求数变化事件（导航开始时归零）。
    /// </summary>
    public event Action<BrowserTab, int>? AdBlockedCountChanged;

    /// <summary>
    /// 中文注释：当前页面已拦截的广告/统计请求数。
    /// </summary>
    public int AdBlockedCount { get; private set; }

    /// <summary>
    /// 中文注释：最近一次导航的耗时（毫秒），尚无完成的导航时为 -1；供状态栏展示开发者信息。
    /// </summary>
    public int LastLoadMilliseconds { get; private set; } = -1;
    private long _loadStartTimestamp;

    /// <summary>
    /// 中文注释：当前缩放倍率（WebView 未就绪时视为 100%）。
    /// </summary>
    public double ZoomFactor => _webView?.ZoomFactor ?? 1.0;

    /// <summary>
    /// 中文注释：设置缩放倍率（自动限制在 0.25~5.0）。
    /// </summary>
    public void SetZoomFactor(double factor)
    {
        if (_webView is null)
        {
            return;
        }

        _webView.ZoomFactor = Math.Clamp(factor, 0.25, 5.0);
    }

    /// <summary>
    /// 中文注释：启动页内查找（WebView2 原生 Find API）；WebView 未就绪或调用失败返回 false。
    /// </summary>
    public async Task<bool> StartFindAsync(string term, bool caseSensitive)
    {
        if (_webView?.CoreWebView2 is not { } core || string.IsNullOrEmpty(term))
        {
            return false;
        }

        try
        {
            var options = core.Environment.CreateFindOptions();
            options.FindTerm = term;
            options.IsCaseSensitive = caseSensitive;
            options.ShouldHighlightAllMatches = true;
            options.SuppressDefaultFindDialog = true;
            if (!_findEventsHooked)
            {
                _findEventsHooked = true;
                core.Find.MatchCountChanged += (_, _) => RaiseFindMatches(core);
                core.Find.ActiveMatchIndexChanged += (_, _) => RaiseFindMatches(core);
            }

            await core.Find.StartAsync(options);
            RaiseFindMatches(core);
            return true;
        }
        catch (Exception exception)
        {
            _log($"页内查找失败: {exception.Message}");
            return false;
        }
    }

    public void FindNext()
    {
        try
        {
            _webView?.CoreWebView2?.Find.FindNext();
        }
        catch
        {
            // 中文注释：查找会话未就绪时忽略。
        }
    }

    public void FindPrevious()
    {
        try
        {
            _webView?.CoreWebView2?.Find.FindPrevious();
        }
        catch
        {
            // 中文注释：查找会话未就绪时忽略。
        }
    }

    public void StopFind()
    {
        try
        {
            _webView?.CoreWebView2?.Find.Stop();
        }
        catch
        {
            // 中文注释：查找会话未就绪时忽略。
        }
    }

    public void FocusPage()
    {
        _webView?.Focus();
    }

    private void RaiseFindMatches(CoreWebView2 core)
    {
        try
        {
            FindMatchesChanged?.Invoke(this, core.Find.ActiveMatchIndex, core.Find.MatchCount);
        }
        catch
        {
            // 中文注释：页面销毁竞态时忽略。
        }
    }

    public void MarkActivated()
    {
        LastActivatedUtc = DateTime.UtcNow;
        StateChanged?.Invoke(this);
    }

    public async Task NavigateAsync(Uri uri)
    {
        await ResumeIfNeededAsync();
        _lastKnownUrl = uri.AbsoluteUri;
        // 中文注释：about:blank 且存在速拨页内容时，用 NavigateToString 展示本地新标签页。
        if (string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase) &&
            _webView?.CoreWebView2 is not null &&
            NewTabHtmlProvider?.Invoke() is { Length: > 0 } newTabHtml)
        {
            _webView.CoreWebView2.NavigateToString(newTabHtml);
            StateChanged?.Invoke(this);
            return;
        }

        _webView!.Source = uri;
        StateChanged?.Invoke(this);
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            _webView!.CoreWebView2.GoBack();
        }
    }

    public void GoForward()
    {
        if (CanGoForward)
        {
            _webView!.CoreWebView2.GoForward();
        }
    }

    public void Reload()
    {
        _webView?.Reload();
    }

    public async Task ClearCacheAndReloadAsync()
    {
        if (_webView?.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.clearBrowserCache", "{}");
        }
        catch
        {
            // Ignore cache clearing failures and still attempt a normal reload.
        }

        _webView.Reload();
    }

    public void OpenDevTools()
    {
        _webView?.CoreWebView2?.OpenDevToolsWindow();
    }

    // 中文注释：在当前页面手动执行指定插件（插件下拉“在当前页面运行”/固定按钮点击）。
    public async Task<(bool Ok, string Error)> RunPluginAsync(string pluginId)
    {
        await ResumeIfNeededAsync();
        if (_webView?.CoreWebView2 is null)
        {
            return (false, "页面尚未就绪，请稍后重试");
        }

        return await _pluginManager.RunPluginOnPageAsync(_webView, pluginId);
    }

    public void SetPinned(bool pinned)
    {
        if (IsPinned == pinned)
        {
            return;
        }

        IsPinned = pinned;
        UpdateCaption();
        StateChanged?.Invoke(this);
    }

    public void SetColorTag(string? colorTagKey)
    {
        var normalized = NormalizeColorTagKey(colorTagKey);
        if (string.Equals(ColorTagKey, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ColorTagKey = normalized;
        StateChanged?.Invoke(this);
    }

    public void SetCustomTitle(string? customTitle)
    {
        var normalized = string.IsNullOrWhiteSpace(customTitle) ? string.Empty : customTitle.Trim();
        if (string.Equals(_customTitle, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _customTitle = normalized;
        UpdateCaption();
        StateChanged?.Invoke(this);
    }

    public static string NormalizeColorTagKey(string? rawColorTagKey)
    {
        if (string.IsNullOrWhiteSpace(rawColorTagKey))
        {
            return string.Empty;
        }

        var normalized = rawColorTagKey.Trim();
        if (normalized.Equals("Blue", StringComparison.OrdinalIgnoreCase))
        {
            return "Blue";
        }

        if (normalized.Equals("Green", StringComparison.OrdinalIgnoreCase))
        {
            return "Green";
        }

        if (normalized.Equals("Orange", StringComparison.OrdinalIgnoreCase))
        {
            return "Orange";
        }

        if (normalized.Equals("Rose", StringComparison.OrdinalIgnoreCase))
        {
            return "Rose";
        }

        return string.Empty;
    }

    public Task SuspendAsync()
    {
        if (_webView is null || IsSuspended)
        {
            return Task.CompletedTask;
        }

        _lastKnownUrl = _webView.Source?.AbsoluteUri ?? _lastKnownUrl;
        TabPage.Controls.Remove(_webView);
        _webView.Dispose();
        _webView = null;
        IsSuspended = true;
        RenderSuspendedHint();
        UpdateCaption();
        StateChanged?.Invoke(this);
        return Task.CompletedTask;
    }

    public async Task ResumeIfNeededAsync()
    {
        if (!IsSuspended && _webView is not null)
        {
            return;
        }

        RemoveSuspendedHint();
        await EnsureWebViewAsync();
        IsSuspended = false;
        UpdateCaption();
        StateChanged?.Invoke(this);
    }

    public async ValueTask DisposeAsync()
    {
        await SuspendAsync();
        RemoveSuspendedHint();
    }

    private async Task EnsureWebViewAsync()
    {
        if (_webView is not null)
        {
            return;
        }

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        _webView.MouseDown += (_, _) => SurfaceInteracted?.Invoke(this);
        TabPage.Controls.Add(_webView);

        Directory.CreateDirectory(_profileStoragePath);

        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: _profileStoragePath,
            options: new CoreWebView2EnvironmentOptions("--disk-cache-size=104857600"));
        await _webView.EnsureCoreWebView2Async(environment);

        _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = _config.IsGeneralAutofillEnabled;
        _webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = _config.IsPasswordAutosaveEnabled;
        _webView.CoreWebView2.Profile.IsGeneralAutofillEnabled = _config.IsGeneralAutofillEnabled;
        _webView.CoreWebView2.Profile.IsPasswordAutosaveEnabled = _config.IsPasswordAutosaveEnabled;
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PagePointerBridgeScript);
        // 中文注释：注册插件内容脚本（文档创建时按匹配域名执行）。
        await _pluginManager.RegisterContentScriptsAsync(_webView.CoreWebView2);
        _webView.CoreWebView2.WebMessageReceived += (_, args) =>
        {
            string? message = null;
            try
            {
                message = args.TryGetWebMessageAsString();
            }
            catch
            {
                // 中文注释：非字符串消息（插件桥 JSON）继续走下方分支处理。
            }

            if (string.Equals(message, PagePointerBridgeMessage, StringComparison.Ordinal))
            {
                SurfaceInteracted?.Invoke(this);
                return;
            }

            // 中文注释：插件 chrome.* 桥消息（storage 读写/打开选项页/新标签）转交插件管理器。
            try
            {
                var bridgeJson = args.WebMessageAsJson;
                if (!string.IsNullOrEmpty(bridgeJson) &&
                    bridgeJson.Contains("lfsun-plugin-", StringComparison.Ordinal) &&
                    _webView?.CoreWebView2 is { } coreForBridge)
                {
                    _ = _pluginManager.HandleBridgeMessageAsync(coreForBridge, bridgeJson, NewTabRequested);
                }
            }
            catch
            {
                // 中文注释：桥消息解析失败忽略，不影响页面。
            }
        };
        _downloadManager.AttachTo(_webView.CoreWebView2);
        // 中文注释：简版广告过滤：按域名拦截广告/统计请求，返回 403 空响应。
        _webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        _webView.CoreWebView2.WebResourceRequested += (_, args) =>
        {
            try
            {
                if (AdBlockService is not { IsEnabled: true } adBlock ||
                    !Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var requestUri) ||
                    !adBlock.ShouldBlock(requestUri))
                {
                    return;
                }

                args.Response = environment.CreateWebResourceResponse(null, 403, "Blocked", string.Empty);
                AdBlockedCount++;
                AdBlockedCountChanged?.Invoke(this, AdBlockedCount);
            }
            catch
            {
                // 中文注释：拦截失败时放行请求，不影响页面加载。
            }
        };
        _webView.CoreWebView2.NewWindowRequested += async (_, args) =>
        {
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var targetUri) || targetUri is null)
            {
                return;
            }

            args.Handled = true;
            var openInNewTab = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (openInNewTab && NewTabRequested is not null)
            {
                await NewTabRequested.Invoke(targetUri);
                return;
            }

            await NavigateAsync(targetUri);
        };

        _webView.NavigationStarting += (_, args) =>
        {
            _lastKnownUrl = args.Uri;
            // 中文注释：新导航开始时拦截计数归零，并记录耗时起点。
            _loadStartTimestamp = Environment.TickCount64;
            LastLoadMilliseconds = -1;
            AdBlockedCount = 0;
            AdBlockedCountChanged?.Invoke(this, AdBlockedCount);
            StateChanged?.Invoke(this);
        };

        _webView.NavigationCompleted += (_, _) => _ = HandleNavigationCompletedAsync();
        _webView.ZoomFactorChanged += (_, _) =>
        {
            if (_webView is not null)
            {
                ZoomChanged?.Invoke(this, _webView.ZoomFactor);
            }
        };
        _webView.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            Title = string.IsNullOrWhiteSpace(_webView.CoreWebView2.DocumentTitle)
                ? "新标签页"
                : _webView.CoreWebView2.DocumentTitle.Trim();
            UpdateCaption();
            StateChanged?.Invoke(this);
        };

        if (Uri.TryCreate(_lastKnownUrl, UriKind.Absolute, out var currentUri))
        {
            _webView.Source = currentUri;
        }
    }

    private async Task HandleNavigationCompletedAsync()
    {
        if (_webView is null || _webView.Source is null)
        {
            return;
        }

        _lastKnownUrl = _webView.Source.AbsoluteUri;
        // 中文注释：导航完成时结算耗时，供状态栏展示。
        if (_loadStartTimestamp > 0)
        {
            LastLoadMilliseconds = (int)Math.Max(0, Environment.TickCount64 - _loadStartTimestamp);
        }

        ApplyZoomOverrideForCurrentHost();
        await _pluginManager.InjectForPageAsync(_webView, _webView.Source);
        await _credentialAutofillService.TryAutofillAsync(_webView, _webView.Source);
        UpdateCaption();
        PageVisited?.Invoke(this, _webView.Source, Title);
        StateChanged?.Invoke(this);
    }

    /// <summary>
    /// 中文注释：导航完成后应用当前主机名的记忆缩放；无记忆时回退 100%，避免跨站残留上个站点的缩放。
    /// </summary>
    private void ApplyZoomOverrideForCurrentHost()
    {
        if (_webView?.Source is not { } source)
        {
            return;
        }

        var host = source.Host;
        var target = string.IsNullOrEmpty(host) ? 1.0 : ZoomOverrideResolver?.Invoke(host) ?? 1.0;
        if (Math.Abs(_webView.ZoomFactor - target) > 0.001)
        {
            _webView.ZoomFactor = Math.Clamp(target, 0.25, 5.0);
        }
    }

    private void RenderSuspendedHint()
    {
        if (TabPage.Controls.ContainsKey(SuspendedHintName))
        {
            return;
        }

        var label = new Label
        {
            Name = SuspendedHintName,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "标签页已休眠以降低内存占用。\r\n切回该标签页会自动恢复。",
            ForeColor = Color.DimGray
        };
        TabPage.Controls.Add(label);
    }

    private void RemoveSuspendedHint()
    {
        if (!TabPage.Controls.ContainsKey(SuspendedHintName))
        {
            return;
        }

        var hint = TabPage.Controls[SuspendedHintName];
        if (hint is null)
        {
            return;
        }

        TabPage.Controls.Remove(hint);
        hint.Dispose();
    }

    private void UpdateCaption()
    {
        var title = string.IsNullOrWhiteSpace(DisplayTitle) ? "新标签页" : DisplayTitle.Trim();
        TabPage.Text = IsSuspended ? $"[休眠] {title}" : title;
    }
}

