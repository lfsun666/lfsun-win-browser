using Microsoft.Web.WebView2.WinForms;
using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.UI.SplitView;

/// <summary>
/// 中文注释：主窗口双屏分屏并行浏览容器。
/// 支持左右两侧同时浏览不同页面，中间带有可拖拽分屏调节条、顶部状态与快捷控制栏、
/// 左右页面一键对调（Swap）、快捷关闭及共享 Cookie/会话能力。
/// </summary>
public sealed class SplitBrowserPanel : Panel
{
    private readonly SplitContainer _splitContainer = new();
    private readonly Panel _secondaryHeader = new();
    private readonly Label _secondaryTitleLabel = new();
    private readonly TextBox _secondaryUrlBox = new();
    private readonly Button _btnBack = new();
    private readonly Button _btnForward = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnSwap = new();
    private readonly Button _btnClose = new();
    private WebView2? _secondaryWebView;

    private Func<Uri?>? _getPrimaryUri;
    private Action<Uri>? _navigatePrimary;
    private string _userDataDir = string.Empty;
    private bool _isSplitActive;

    public event Action? SplitViewClosed;

    public SplitBrowserPanel()
    {
        Dock = DockStyle.Fill;
        Visible = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        ApplyTheme();
    }

    public bool IsSplitActive => _isSplitActive;

    public Uri? SecondaryUri => _secondaryWebView?.Source;

    private void BuildLayout()
    {
        _splitContainer.Dock = DockStyle.Fill;
        _splitContainer.Orientation = Orientation.Vertical;
        _splitContainer.SplitterWidth = 6;
        _splitContainer.Panel1MinSize = 0;
        _splitContainer.Panel2MinSize = 0;
        _splitContainer.BackColor = AppTheme.PanelBorder;
        _splitContainer.SizeChanged += (_, _) => SafeApplySplitterLayout();
        _splitContainer.SplitterMoving += (_, e) =>
        {
            var totalWidth = _splitContainer.ClientSize.Width;
            var splitterWidth = _splitContainer.SplitterWidth;
            const int minSize = 180;
            if (totalWidth >= minSize * 2 + splitterWidth)
            {
                e.SplitX = Math.Clamp(e.SplitX, minSize, totalWidth - minSize - splitterWidth);
            }
        };

        // 右侧副屏顶部工具条
        _secondaryHeader.Dock = DockStyle.Top;
        _secondaryHeader.Height = 36;
        _secondaryHeader.Padding = new Padding(6, 4, 6, 4);
        _secondaryHeader.BackColor = AppTheme.SurfaceAlt;

        var headerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _btnBack.Text = "‹";
        _btnBack.Size = new Size(26, 26);
        _btnBack.FlatStyle = FlatStyle.Flat;
        _btnBack.FlatAppearance.BorderSize = 0;
        _btnBack.Font = AppTheme.CreateFont(11F, FontStyle.Bold);
        _btnBack.Cursor = Cursors.Hand;
        _btnBack.Click += (_, _) => { if (_secondaryWebView?.CanGoBack == true) _secondaryWebView.GoBack(); };

        _btnForward.Text = "›";
        _btnForward.Size = new Size(26, 26);
        _btnForward.FlatStyle = FlatStyle.Flat;
        _btnForward.FlatAppearance.BorderSize = 0;
        _btnForward.Font = AppTheme.CreateFont(11F, FontStyle.Bold);
        _btnForward.Cursor = Cursors.Hand;
        _btnForward.Click += (_, _) => { if (_secondaryWebView?.CanGoForward == true) _secondaryWebView.GoForward(); };

        _btnRefresh.Text = "↻";
        _btnRefresh.Size = new Size(26, 26);
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Font = AppTheme.CreateFont(10.5F, FontStyle.Bold);
        _btnRefresh.Cursor = Cursors.Hand;
        _btnRefresh.Click += (_, _) => _secondaryWebView?.Reload();

        _secondaryUrlBox.Size = new Size(200, 26);
        _secondaryUrlBox.Font = AppTheme.CreateFont(8.8F);
        _secondaryUrlBox.BorderStyle = BorderStyle.FixedSingle;
        _secondaryUrlBox.PlaceholderText = "输入网址回车访问...";
        _secondaryUrlBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateSecondary(_secondaryUrlBox.Text.Trim());
            }
        };

        _btnSwap.Text = "⇄ 对调";
        _btnSwap.Size = new Size(62, 26);
        _btnSwap.FlatStyle = FlatStyle.Flat;
        _btnSwap.FlatAppearance.BorderSize = 0;
        _btnSwap.Font = AppTheme.CreateFont(8.5F);
        _btnSwap.Cursor = Cursors.Hand;
        _btnSwap.Click += (_, _) => SwapPages();

        _btnClose.Text = "✕ 关闭分屏";
        _btnClose.Size = new Size(80, 26);
        _btnClose.FlatStyle = FlatStyle.Flat;
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Font = AppTheme.CreateFont(8.5F);
        _btnClose.ForeColor = Color.FromArgb(230, 80, 80);
        _btnClose.Cursor = Cursors.Hand;
        _btnClose.Click += (_, _) => SplitViewClosed?.Invoke();

        headerFlow.Controls.Add(_btnBack);
        headerFlow.Controls.Add(_btnForward);
        headerFlow.Controls.Add(_btnRefresh);
        headerFlow.Controls.Add(_secondaryUrlBox);
        headerFlow.Controls.Add(_btnSwap);
        headerFlow.Controls.Add(_btnClose);

        _secondaryHeader.SizeChanged += (_, _) =>
        {
            var navWidth = _btnBack.Width + _btnForward.Width + _btnRefresh.Width + 24;
            var actionWidth = _btnSwap.Width + _btnClose.Width + 24;
            var avail = _secondaryHeader.ClientSize.Width - navWidth - actionWidth;
            _secondaryUrlBox.Width = Math.Clamp(avail, 140, 600);
        };

        _secondaryHeader.Controls.Add(headerFlow);

        _splitContainer.Panel2.Controls.Add(_secondaryHeader);

        Controls.Add(_splitContainer);
    }

    public void InitializeUserDataDir(string userDataDir)
    {
        _userDataDir = userDataDir;
    }

    public async Task ActivateSplitAsync(
        Control primaryControl,
        Uri? initialSecondaryUri,
        Func<Uri?> getPrimaryUri,
        Action<Uri> navigatePrimary)
    {
        _getPrimaryUri = getPrimaryUri;
        _navigatePrimary = navigatePrimary;

        // 将原主容器挂载到 Panel1
        if (primaryControl.Parent != _splitContainer.Panel1)
        {
            _splitContainer.Panel1.Controls.Add(primaryControl);
            primaryControl.Dock = DockStyle.Fill;
            primaryControl.BringToFront();
        }

        // 初始化或准备副屏 WebView
        if (_secondaryWebView is null)
        {
            _secondaryWebView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            _splitContainer.Panel2.Controls.Add(_secondaryWebView);
            _secondaryWebView.BringToFront();

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: string.IsNullOrWhiteSpace(_userDataDir) ? null : _userDataDir);
            await _secondaryWebView.EnsureCoreWebView2Async(env);

            _secondaryWebView.NavigationCompleted += (_, _) =>
            {
                if (_secondaryWebView.Source is not null)
                {
                    _secondaryUrlBox.Text = _secondaryWebView.Source.AbsoluteUri;
                }
            };
        }

        var targetUri = initialSecondaryUri ?? new Uri("https://cn.bing.com");
        _secondaryWebView.Source = targetUri;
        _secondaryUrlBox.Text = targetUri.AbsoluteUri;

        _isSplitActive = true;
        Visible = true;
        BringToFront();

        SafeApplySplitterLayout();
    }

    private void SafeApplySplitterLayout()
    {
        if (!_isSplitActive) return;

        try
        {
            var totalWidth = _splitContainer.ClientSize.Width;
            var splitterWidth = _splitContainer.SplitterWidth;
            const int minSize = 180;

            _splitContainer.Panel1MinSize = 0;
            _splitContainer.Panel2MinSize = 0;

            if (totalWidth >= minSize * 2 + splitterWidth)
            {
                var safeDistance = Math.Clamp(totalWidth / 2, minSize, totalWidth - minSize - splitterWidth);
                _splitContainer.SplitterDistance = safeDistance;
            }
            else if (totalWidth > splitterWidth)
            {
                _splitContainer.SplitterDistance = Math.Max(0, (totalWidth - splitterWidth) / 2);
            }
        }
        catch
        {
            // 防御性静默，防止任何 DPI 动态缩放或尺寸突变抛出异常
        }
    }

    public void DeactivateSplit(Control primaryControl, Control originalParent)
    {
        if (!_isSplitActive) return;

        // 将主窗口 tabControl 还原回到原主容器
        if (_splitContainer.Panel1.Controls.Contains(primaryControl))
        {
            _splitContainer.Panel1.Controls.Remove(primaryControl);
        }

        if (originalParent != null && !originalParent.Controls.Contains(primaryControl))
        {
            originalParent.Controls.Add(primaryControl);
            primaryControl.Dock = DockStyle.Fill;
            primaryControl.BringToFront();
        }

        _isSplitActive = false;
        Visible = false;
    }

    private void SwapPages()
    {
        var primaryUri = _getPrimaryUri?.Invoke();
        var secondaryUri = _secondaryWebView?.Source;

        if (secondaryUri is not null && _navigatePrimary is not null)
        {
            _navigatePrimary(secondaryUri);
        }

        if (primaryUri is not null && _secondaryWebView is not null)
        {
            _secondaryWebView.Source = primaryUri;
            _secondaryUrlBox.Text = primaryUri.AbsoluteUri;
        }
    }

    private void NavigateSecondary(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return;

        var url = rawUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (url.Contains('.') && !url.Contains(' '))
            {
                url = "https://" + url;
            }
            else
            {
                url = $"https://cn.bing.com/search?q={Uri.EscapeDataString(url)}";
            }
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _secondaryWebView?.CoreWebView2?.Navigate(uri.AbsoluteUri);
        }
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _splitContainer.BackColor = AppTheme.PanelBorder;
        _secondaryHeader.BackColor = AppTheme.SurfaceAlt;
        _secondaryUrlBox.BackColor = AppTheme.InputBackground;
        _secondaryUrlBox.ForeColor = AppTheme.TextPrimary;

        _btnBack.ForeColor = AppTheme.TextSecondary;
        _btnForward.ForeColor = AppTheme.TextSecondary;
        _btnRefresh.ForeColor = AppTheme.TextSecondary;
        _btnSwap.ForeColor = AppTheme.Accent;
        _btnClose.ForeColor = Color.FromArgb(235, 87, 87);

        var hoverBg = AppTheme.IsDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);
        var pressBg = AppTheme.IsDark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(36, 0, 0, 0);
        foreach (var btn in new[] { _btnBack, _btnForward, _btnRefresh, _btnSwap })
        {
            btn.FlatAppearance.MouseOverBackColor = hoverBg;
            btn.FlatAppearance.MouseDownBackColor = pressBg;
        }
        _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        _btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(199, 15, 29);
    }
}
