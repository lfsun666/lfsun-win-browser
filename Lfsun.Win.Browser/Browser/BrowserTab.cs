using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    private CoreWebView2Environment? _environment;
    private CoreWebView2? _core;
    private readonly List<CoreWebView2DevToolsProtocolEventReceiver> _cdpReceivers = new();
    private string _lastKnownUrl;
    private readonly NetworkCapture _networkCapture = new();
    private string _customTitle = string.Empty;
    private bool _findEventsHooked;
    private bool _coreEventsHooked;

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
    public bool IsPlayingAudio => _core?.IsDocumentPlayingAudio ?? false;
    public bool IsReaderModeActive { get; private set; }
    public event Action<bool>? ReaderModeChanged;
    public bool IsMuted
    {
        get => _core?.IsMuted ?? false;
        set
        {
            if (_core is not null)
            {
                _core.IsMuted = value;
                AudioStateChanged?.Invoke(this);
                StateChanged?.Invoke(this);
            }
        }
    }

        /// <summary>
    /// 中文注释：切换当前页面的沉浸式阅读模式。
    /// 自动提取正文内容、排版成无干扰高品质阅读视图，支持白天/羊皮纸/深色主题及字号缩放。
    /// </summary>
    public async Task<bool> ToggleReaderModeAsync()
    {
        if (_core is null)
        {
            return false;
        }

        try
        {
            if (IsReaderModeActive)
            {
                await _core.ExecuteScriptAsync(ReaderModeHelper.ExitReaderScript);
                IsReaderModeActive = false;
                ReaderModeChanged?.Invoke(false);
                return false;
            }

            var resJson = await _core.ExecuteScriptAsync(ReaderModeHelper.ToggleReaderScript);
            IsReaderModeActive = resJson != null && resJson.Contains("\"active\":true");
            ReaderModeChanged?.Invoke(IsReaderModeActive);
            return IsReaderModeActive;
        }
        catch (Exception ex)
        {
            _log($"切换阅读模式失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 中文注释：触发页面视频画中画（Picture-in-Picture）独立浮窗模式。
    /// </summary>
    public async Task<(bool ok, bool active, string message)> TogglePictureInPictureAsync()
    {
        if (_core is null)
        {
            return (false, false, "内核未就绪");
        }

        try
        {
            var json = await _core.ExecuteScriptAsync(ReaderModeHelper.PictureInPictureScript);
            if (!string.IsNullOrEmpty(json) && json.StartsWith("\"") && json.EndsWith("\""))
            {
                json = System.Text.RegularExpressions.Regex.Unescape(json[1..^1]);
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.GetProperty("ok").GetBoolean();
            var active = root.GetProperty("active").GetBoolean();
            var msg = root.GetProperty("message").GetString() ?? string.Empty;
            return (ok, active, msg);
        }
        catch (Exception ex)
        {
            return (false, false, $"调用失败: {ex.Message}");
        }
    }

    public void ToggleMute() => IsMuted = !IsMuted;

    public event Action<BrowserTab>? StateChanged;
    public event Action<BrowserTab>? AudioStateChanged;
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
    public double ZoomFactor
    {
        get => _webView?.ZoomFactor ?? 1.0;
        set => SetZoomFactor(value);
    }

    /// <summary>
    /// 中文注释：整页/可视区域快照截图并返回 Bitmap。
    /// </summary>
    public async Task<Bitmap?> CaptureScreenshotAsync()
    {
        if (_core is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream();
            await _core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            _log($"网页快照截图失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 中文注释：为网页动态应用 CSS 视觉滤镜（如护眼暖色、夜间反色、高对比度等）。
    /// </summary>
    public async Task ApplyPageFilterAsync(string filterCss)
    {
        if (_core is null)
        {
            return;
        }

        try
        {
            var script = string.IsNullOrWhiteSpace(filterCss)
                ? "document.documentElement.style.filter = '';"
                : $"document.documentElement.style.filter = '{filterCss}';";
            await _core.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _log($"应用页面视觉滤镜失败: {ex.Message}");
        }
    }

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

        // 中文注释：先取消所有 CDP 事件订阅，避免 Edge 进程释放后仍收到回调产生空引用异常。
        UnsubscribeCdpEvents();
        UnsubscribeCoreEvents();

        _lastKnownUrl = _webView.Source?.AbsoluteUri ?? _lastKnownUrl;
        TabPage.Controls.Remove(_webView);
        _webView.Dispose();
        _webView = null;
        _core = null;
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

    // 中文注释：拼装 WebView2 启动参数：默认磁盘缓存上限 + 可选 CORS 绕过(--disable-web-security)
    // + 用户自定义参数。--disable-web-security 需配合独立 userDataFolder（本浏览器每个标签独立 profile，已满足）。
    private static string BuildBrowserArgs(BrowserConfig config)
    {
        var args = new List<string> { "--disk-cache-size=104857600" };
        if (config.DisableWebSecurity)
        {
            args.Add("--disable-web-security");
            args.Add("--disable-features=CrossOriginOpenerPolicy");
        }

        if (!string.IsNullOrWhiteSpace(config.AdditionalBrowserArguments))
        {
            foreach (var part in config.AdditionalBrowserArguments
                         .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                {
                    args.Add(part);
                }
            }
        }

        return string.Join(" ", args);
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

        _environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: _profileStoragePath,
            options: new CoreWebView2EnvironmentOptions(BuildBrowserArgs(_config)));
        await _webView.EnsureCoreWebView2Async(_environment);
        _core = _webView.CoreWebView2;

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
        _webView.CoreWebView2.WebMessageReceived += async (_, args) =>
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

            // 中文注释：插件 chrome.* 桥消息（storage 读写/打开选项页/新标签）处理；
            // Network 抓包消息由当前标签页自己处理（它持有 WebView2 网络事件）。
            try
            {
                var bridgeJson = args.WebMessageAsJson;
                if (!string.IsNullOrEmpty(bridgeJson) &&
                    _webView?.CoreWebView2 is { } coreForBridge)
                {
                    if (await TryHandleNetworkBridgeMessageAsync(coreForBridge, bridgeJson))
                    {
                        return;
                    }

                    if (bridgeJson.Contains("lfsun-plugin-", StringComparison.Ordinal))
                    {
                        _ = _pluginManager.HandleBridgeMessageAsync(coreForBridge, bridgeJson, NewTabRequested);
                    }
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
                var requestUri = args.Request.Uri;
                var method = args.Request.Method;
                var resourceContext = args.ResourceContext.ToString();
                var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in args.Request.Headers)
                {
                    requestHeaders[header.Key] = header.Value;
                }

                if (AdBlockService is { IsEnabled: true } adBlock &&
                    Uri.TryCreate(requestUri, UriKind.Absolute, out var uri) &&
                    adBlock.ShouldBlock(uri))
                {
                    args.Response = _environment!.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked", "Content-Type: text/plain");
                    AdBlockedCount++;
                    AdBlockedCountChanged?.Invoke(this, AdBlockedCount);
                    return;
                }
            }
            catch
            {
                // 中文注释：拦截失败时放行请求，不影响页面加载。
            }
        };
        // 中文注释：改用 CDP Network 域监控请求（含响应体），见 SetupNetworkMonitoringAsync。
        if (_core is not null)
        {
            await SetupNetworkMonitoringAsync(_core);
        }
        _core!.NewWindowRequested += async (_, args) =>
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

        // 中文注释：订阅导航/标题/音频变化事件（CoreWebView2 事件）；统一使用可取消的具名方法，
        // Suspend 时自动取否则 Resume 多次会叠加订阅。
        // 注意：ZoomFactorChanged 是 WebView2 控件事件，仅从 _webView 订阅无需取销（随控件 Dispose 释放）。
        _core!.NavigationStarting += OnNavigationStarting;
        _core.NavigationCompleted += OnNavigationCompleted;
        _core.DocumentTitleChanged += OnDocumentTitleChanged;
        _core.IsDocumentPlayingAudioChanged += OnIsDocumentPlayingAudioChanged;
        _core.IsMutedChanged += OnIsMutedChanged;
        _webView.ZoomFactorChanged += OnZoomFactorChanged;
        _coreEventsHooked = true;

        if (Uri.TryCreate(_lastKnownUrl, UriKind.Absolute, out var currentUri))
        {
            _webView.Source = currentUri;
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (IsReaderModeActive)
        {
            IsReaderModeActive = false;
            ReaderModeChanged?.Invoke(false);
        }
        _lastKnownUrl = args.Uri;
        // 中文注释：新导航开始时拦截计数归零，并记录耗时起点。
        _loadStartTimestamp = Environment.TickCount64;
        LastLoadMilliseconds = -1;
        AdBlockedCount = 0;
        AdBlockedCountChanged?.Invoke(this, AdBlockedCount);
        StateChanged?.Invoke(this);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        => _ = HandleNavigationCompletedAsync();

    private void OnZoomFactorChanged(object? sender, object args)
    {
        if (_webView is not null)
        {
            ZoomChanged?.Invoke(this, _webView.ZoomFactor);
        }
    }

    private void OnDocumentTitleChanged(object? sender, object args)
    {
        if (_core is not null)
        {
            Title = string.IsNullOrWhiteSpace(_core.DocumentTitle)
                ? "新标签页"
                : _core.DocumentTitle.Trim();
            UpdateCaption();
            StateChanged?.Invoke(this);
        }
    }

    private void OnIsDocumentPlayingAudioChanged(object? sender, object args)
    {
        AudioStateChanged?.Invoke(this);
        StateChanged?.Invoke(this);
    }

    private void OnIsMutedChanged(object? sender, object args)
    {
        AudioStateChanged?.Invoke(this);
        StateChanged?.Invoke(this);
    }

    private void UnsubscribeCoreEvents()
    {
        if (!_coreEventsHooked || _core is null)
        {
            return;
        }

        try
        {
            _core.NavigationStarting -= OnNavigationStarting;
            _core.NavigationCompleted -= OnNavigationCompleted;
            _core.DocumentTitleChanged -= OnDocumentTitleChanged;
            _core.IsDocumentPlayingAudioChanged -= OnIsDocumentPlayingAudioChanged;
            _core.IsMutedChanged -= OnIsMutedChanged;
            if (_webView is not null)
            {
                _webView.ZoomFactorChanged -= OnZoomFactorChanged;
            }
        }
        catch
        {
            // 中文注释：WebView2 正在 Dispose 时忽略取销异常。
        }

        _coreEventsHooked = false;
    }

    private void UnsubscribeCdpEvents()
    {
        foreach (var receiver in _cdpReceivers)
        {
            try
            {
                receiver.DevToolsProtocolEventReceived -= OnNetworkCdpEvent;
            }
            catch
            {
                // 中文注释：接收器已释放时忽略取销异常。
            }
        }
        _cdpReceivers.Clear();
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

    /// <summary>
    /// 中文注释：启用 CDP Network 域并订阅请求/响应事件，供 Network 抓包（含响应体）。
    /// 注意：SubscribeNetworkEvent 会保存 receiver 引用，必须通过 UnsubscribeCdpEvents() 清理，
    /// 否则 Suspend WebView2 后 Edge 进程退出仍会触发回调导致空引用异常。
    /// </summary>
    private async Task SetupNetworkMonitoringAsync(CoreWebView2 core)
    {
        try
        {
            // 中文注释：启用前先清理旧接收器，避免 Resume 时叠加。
            UnsubscribeCdpEvents();
            await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            SubscribeNetworkEvent(core, "Network.requestWillBeSent", 0);
            SubscribeNetworkEvent(core, "Network.responseReceived", 1);
            SubscribeNetworkEvent(core, "Network.loadingFinished", 2);
            SubscribeNetworkEvent(core, "Network.loadingFailed", 3);
        }
        catch
        {
            // 中文注释：CDP Network 不可用时静默降级，Network 抓包将不可用。
        }
    }

    private void SubscribeNetworkEvent(CoreWebView2 core, string eventName, int eventIndex)
    {
        try
        {
            var receiver = core.GetDevToolsProtocolEventReceiver(eventName);
            // 中文注释：用 IndexTag 携带事件名索引，便于统一 Handler 根据序号区分事件。
            receiver.DevToolsProtocolEventReceived += OnNetworkCdpEvent;
            _cdpReceivers.Add(receiver);
        }
        catch
        {
            // 中文注释：事件接收器订阅失败忽略。
        }
    }

    // 中文注释：CDP Network 事件处理器；四个事件共用，按接收者在列表中的索引区分。
    private void OnNetworkCdpEvent(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (sender is not CoreWebView2DevToolsProtocolEventReceiver receiver)
        {
            return;
        }

        var index = _cdpReceivers.IndexOf(receiver);
        var eventName = index switch
        {
            0 => "Network.requestWillBeSent",
            1 => "Network.responseReceived",
            2 => "Network.loadingFinished",
            3 => "Network.loadingFailed",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(eventName))
        {
            _networkCapture.HandleCdpEvent(eventName, e.ParameterObjectAsJson);
        }
    }

    private async Task<bool> TryHandleNetworkBridgeMessageAsync(CoreWebView2 sender, string bridgeJson)
    {
        JsonObject? message;
        try
        {
            message = JsonSerializer.Deserialize<JsonObject>(bridgeJson);
        }
        catch
        {
            return false;
        }

        if (message is null)
        {
            return false;
        }

        var type = message["type"]?.GetValue<string>();
        switch (type)
        {
            case "lfsun-plugin-network-start":
            {
                var filters = ParseNetworkFilters(message["filters"]);
                _networkCapture.Start(filters);
                return true;
            }
            case "lfsun-plugin-network-stop":
            {
                _networkCapture.Stop();
                return true;
            }
            case "lfsun-plugin-network-get":
            {
                var clear = message["clear"]?.GetValue<bool>() ?? false;
                var entries = _networkCapture.GetEntries(clear);
                var reqId = message["reqId"]?.GetValue<string>() ?? string.Empty;
                var response = new JsonObject
                {
                    ["type"] = "lfsun-plugin-network-entries-response",
                    ["reqId"] = reqId,
                    // 中文注释：显式 camelCase，与插件前端（inject.js 读 e.requestId / e.method 等）保持一致，
                    // 否则 System.Text.Json 默认 PascalCase 会导致前端字段全为 undefined，Network 列表空白。
                    ["entries"] = JsonSerializer.SerializeToNode(entries, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                };
                sender.PostWebMessageAsJson(response.ToJsonString());
                return true;
            }
            case "lfsun-plugin-network-body":
            {
                var requestId = message["requestId"]?.GetValue<string>() ?? string.Empty;
                var reqId = message["reqId"]?.GetValue<string>() ?? string.Empty;
                var (body, base64Encoded, error) = await _networkCapture.GetResponseBodyAsync(sender, requestId);
                var response = new JsonObject
                {
                    ["type"] = "lfsun-plugin-network-body-response",
                    ["reqId"] = reqId,
                    ["body"] = body,
                    ["base64Encoded"] = base64Encoded,
                    ["error"] = error
                };
                sender.PostWebMessageAsJson(response.ToJsonString());
                return true;
            }
        }

        return false;
    }

    private static NetworkFilters ParseNetworkFilters(JsonNode? node)
    {
        var filters = new NetworkFilters();
        if (node is not JsonObject obj)
        {
            return filters;
        }

        filters.Xhr = obj["xhr"]?.GetValue<bool>() ?? true;
        filters.Js = obj["js"]?.GetValue<bool>() ?? true;
        filters.Css = obj["css"]?.GetValue<bool>() ?? true;
        filters.Img = obj["img"]?.GetValue<bool>() ?? true;
        filters.Media = obj["media"]?.GetValue<bool>() ?? false;
        filters.Doc = obj["doc"]?.GetValue<bool>() ?? true;
        filters.Other = obj["other"]?.GetValue<bool>() ?? false;
        filters.UrlPattern = obj["urlPattern"]?.GetValue<string>() ?? string.Empty;
        filters.Methods = obj["methods"]?.GetValue<string>() ?? string.Empty;
        return filters;
    }

    // 中文注释：Network 抓包数据模型与过滤器。
    private sealed class NetworkFilters
    {
        public bool Xhr { get; set; }
        public bool Js { get; set; }
        public bool Css { get; set; }
        public bool Img { get; set; }
        public bool Media { get; set; }
        public bool Doc { get; set; }
        public bool Other { get; set; }
        public string UrlPattern { get; set; } = string.Empty;
        public string Methods { get; set; } = string.Empty;
    }

    private sealed class NetworkEntry
    {
        public string RequestId { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public int Status { get; set; }
        public long TimeMs { get; set; }
        public long Size { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string? PostData { get; set; }
        public string ErrorText { get; set; } = string.Empty;
        public Dictionary<string, string> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 中文注释：Network 抓包器：基于 WebView2 的 DevTools Protocol（CDP）Network 域，
    /// 监听 requestWillBeSent / responseReceived / loadingFinished / loadingFailed，
    /// 按资源类型、URL 通配符、HTTP 方法过滤，并支持按需读取响应体（Network.getResponseBody）。
    /// </summary>
    private sealed class NetworkCapture
    {
        private readonly object _lock = new();
        private readonly List<NetworkEntry> _entries = new();
        private readonly Dictionary<string, NetworkEntry> _byId = new(StringComparer.Ordinal);
        private NetworkFilters _filters = new();
        private bool _capturing;
        private const int MaxEntries = 5000;

        public void Start(NetworkFilters filters)
        {
            lock (_lock)
            {
                _filters = filters;
                _capturing = true;
                _entries.Clear();
                _byId.Clear();
            }
        }

        public void Stop()
        {
            lock (_lock) { _capturing = false; }
        }

        public NetworkEntry[] GetEntries(bool clear)
        {
            lock (_lock)
            {
                var result = _entries.ToArray();
                if (clear)
                {
                    _entries.Clear();
                    _byId.Clear();
                }

                return result;
            }
        }

        // 中文注释：处理 CDP Network 事件 JSON；仅在捕获开启时记录匹配请求。
        public void HandleCdpEvent(string eventName, string json)
        {
            if (!_capturing)
            {
                return;
            }

            try
            {
                var node = JsonSerializer.Deserialize<JsonObject>(json);
                if (node is null)
                {
                    return;
                }

                var rid = node["requestId"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(rid))
                {
                    return;
                }

                lock (_lock)
                {
                    switch (eventName)
                    {
                        case "Network.requestWillBeSent":
                        {
                            if (_byId.ContainsKey(rid))
                            {
                                return;
                            }

                            var req = node["request"] as JsonObject;
                            var hintType = node["type"]?.GetValue<string>()
                                ?? (req?["type"]?.GetValue<string>()) ?? "Other";
                            var url = req?["url"]?.GetValue<string>() ?? string.Empty;
                            var method = req?["method"]?.GetValue<string>() ?? "GET";
                            if (!ShouldCapture(hintType, url, method))
                            {
                                return;
                            }

                            var entry = new NetworkEntry
                            {
                                RequestId = rid,
                                Method = method.ToUpperInvariant(),
                                Url = url,
                                ResourceType = NormalizeResourceType(hintType),
                                TimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                RequestHeaders = HeadersToDict(req?["headers"]),
                                PostData = req?["postData"]?.GetValue<string>()
                            };
                            _entries.Add(entry);
                            _byId[rid] = entry;
                            CapLocked();
                            break;
                        }
                        case "Network.responseReceived":
                        {
                            if (!_byId.TryGetValue(rid, out var entry))
                            {
                                return;
                            }

                            var resp = node["response"] as JsonObject;
                            entry.Status = resp?["status"]?.GetValue<int>() ?? entry.Status;
                            entry.ResourceType = NormalizeResourceType(
                                node["type"]?.GetValue<string>() ?? entry.ResourceType);
                            entry.MimeType = resp?["mimeType"]?.GetValue<string>() ?? entry.MimeType;
                            entry.ResponseHeaders = HeadersToDict(resp?["headers"]);
                            break;
                        }
                        case "Network.loadingFinished":
                        {
                            if (_byId.TryGetValue(rid, out var entry))
                            {
                                entry.Size = node["encodedDataLength"]?.GetValue<long>() ?? entry.Size;
                            }

                            break;
                        }
                        case "Network.loadingFailed":
                        {
                            if (_byId.TryGetValue(rid, out var entry))
                            {
                                entry.ErrorText = node["errorText"]?.GetValue<string>() ?? "failed";
                            }

                            break;
                        }
                    }
                }
            }
            catch
            {
                // 中文注释：CDP 事件解析失败忽略，不影响页面。
            }
        }

        // 中文注释：按需读取响应体；返回 (body, base64Encoded, error)。
        public async Task<(string Body, bool Base64Encoded, string Error)> GetResponseBodyAsync(
            CoreWebView2 core, string requestId)
        {
            if (string.IsNullOrEmpty(requestId) || core is null)
            {
                return (string.Empty, false, "缺少 requestId");
            }

            try
            {
                var param = new JsonObject { ["requestId"] = requestId }.ToJsonString();
                var result = await core.CallDevToolsProtocolMethodAsync("Network.getResponseBody", param);
                var node = JsonSerializer.Deserialize<JsonObject>(result);
                var body = node?["body"]?.GetValue<string>() ?? string.Empty;
                var base64 = node?["base64Encoded"]?.GetValue<bool>() ?? false;
                return (body, base64, string.Empty);
            }
            catch (Exception exception)
            {
                return (string.Empty, false, exception.Message);
            }
        }

        private void CapLocked()
        {
            while (_entries.Count > MaxEntries)
            {
                var oldest = _entries[0];
                _entries.RemoveAt(0);
                _byId.Remove(oldest.RequestId);
            }
        }

        private static Dictionary<string, string> HeadersToDict(JsonNode? node)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (node is JsonObject obj)
            {
                foreach (var kv in obj)
                {
                    var value = kv.Value;
                    if (value is JsonValue jv && jv.TryGetValue<string>(out var s))
                    {
                        dict[kv.Key] = s;
                    }
                    else if (value is JsonArray arr)
                    {
                        dict[kv.Key] = string.Join(", ", arr.Select(x => x?.ToString() ?? string.Empty));
                    }
                    else if (value is not null)
                    {
                        dict[kv.Key] = value.ToString();
                    }
                }
            }

            return dict;
        }

        private bool ShouldCapture(string resourceType, string url, string method)
        {
            var normalizedType = NormalizeResourceType(resourceType);
            bool typeMatch = normalizedType switch
            {
                "xhr" or "fetch" => _filters.Xhr,
                "script" => _filters.Js,
                "stylesheet" => _filters.Css,
                "image" => _filters.Img,
                "media" => _filters.Media,
                "document" => _filters.Doc,
                _ => _filters.Other
            };

            if (!typeMatch)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_filters.UrlPattern) && !WildcardMatch(url, _filters.UrlPattern))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_filters.Methods))
            {
                var allowed = _filters.Methods
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim().ToUpperInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!allowed.Contains(method))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeResourceType(string resourceType)
        {
            return resourceType.ToLowerInvariant() switch
            {
                "xmlhttprequest" => "xhr",
                "fetch" => "xhr",
                "script" => "script",
                "stylesheet" => "stylesheet",
                "image" => "image",
                "media" => "media",
                "document" => "document",
                "other" => "other",
                _ => resourceType.ToLowerInvariant()
            };
        }

        private static bool WildcardMatch(string text, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return true;
            }

            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(text, regex, RegexOptions.IgnoreCase);
        }
    }
}

