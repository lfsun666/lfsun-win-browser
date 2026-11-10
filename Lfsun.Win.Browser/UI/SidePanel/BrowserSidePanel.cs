using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.History;

namespace Lfsun.Win.Browser.UI.SidePanel;

public enum SidePanelMode
{
    Bookmarks,
    History,
    Downloads,
    Scratchpad,
    Toolbox
}

/// <summary>
/// 中文注释：主窗口现代化侧边多功能抽屉栏。
/// 停靠于页面右侧，可在书签、历史记录、下载、随手便签与网页工具箱之间丝滑切换与实时联动。
/// </summary>
public sealed class BrowserSidePanel : Panel
{
    private readonly BookmarkManager _bookmarkManager;
    private readonly HistoryManager _historyManager;
    private readonly DownloadManager _downloadManager;
    private readonly Func<BrowserTab?> _getActiveTab;
    private readonly Func<Uri, bool, Task> _navigateCallback;
    private readonly Action<string> _toastCallback;
    private readonly Action _openBookmarkManagerCallback;
    private readonly Action _openHistoryWindowCallback;
    private readonly Action _openDownloadsWindowCallback;
    private readonly Action? _toggleSplitCallback;
    private readonly Func<IReadOnlyList<BrowserTab>>? _getAllTabs;
    private readonly Func<Task<int>>? _suspendBackgroundTabsCallback;
    private readonly Func<bool>? _toggleMuteAllCallback;
    private readonly Action? _openPerformanceCallback;

    private readonly Panel _headerBar = new();
    private readonly Panel _contentContainer = new();
    private readonly Button _closeButton = new();

    private readonly Dictionary<SidePanelMode, Button> _navButtons = new();
    private readonly Dictionary<SidePanelMode, Control> _views = new();
    private SidePanelMode _currentMode = SidePanelMode.Toolbox;

    public event Action? CloseRequested;

    public BrowserSidePanel(
        BookmarkManager bookmarkManager,
        HistoryManager historyManager,
        DownloadManager downloadManager,
        Func<BrowserTab?> getActiveTab,
        Func<Uri, bool, Task> navigateCallback,
        Action<string> toastCallback,
        Action openBookmarkManagerCallback,
        Action openHistoryWindowCallback,
        Action openDownloadsWindowCallback,
        Action? toggleSplitCallback = null,
        Func<IReadOnlyList<BrowserTab>>? getAllTabs = null,
        Func<Task<int>>? suspendBackgroundTabsCallback = null,
        Func<bool>? toggleMuteAllCallback = null,
        Action? openPerformanceCallback = null)
    {
        _bookmarkManager = bookmarkManager;
        _historyManager = historyManager;
        _downloadManager = downloadManager;
        _getActiveTab = getActiveTab;
        _navigateCallback = navigateCallback;
        _toastCallback = toastCallback;
        _openBookmarkManagerCallback = openBookmarkManagerCallback;
        _openHistoryWindowCallback = openHistoryWindowCallback;
        _openDownloadsWindowCallback = openDownloadsWindowCallback;
        _toggleSplitCallback = toggleSplitCallback;
        _getAllTabs = getAllTabs;
        _suspendBackgroundTabsCallback = suspendBackgroundTabsCallback;
        _toggleMuteAllCallback = toggleMuteAllCallback;
        _openPerformanceCallback = openPerformanceCallback;

        Width = 340;
        Dock = DockStyle.Right;
        Visible = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        ApplyTheme();
        SwitchTo(SidePanelMode.Toolbox);
    }

    public SidePanelMode CurrentMode => _currentMode;

    private void BuildLayout()
    {
        // 顶部导航选项卡栏
        _headerBar.Dock = DockStyle.Top;
        _headerBar.Height = 40;
        _headerBar.Padding = new Padding(6, 4, 6, 4);
        _headerBar.BackColor = AppTheme.SurfaceAlt;

        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var bookmarksBtn = CreateNavButton("🔖", "书签", SidePanelMode.Bookmarks);
        var historyBtn = CreateNavButton("🕒", "历史", SidePanelMode.History);
        var downloadsBtn = CreateNavButton("⬇️", "下载", SidePanelMode.Downloads);
        var scratchpadBtn = CreateNavButton("📝", "便签", SidePanelMode.Scratchpad);
        var toolboxBtn = CreateNavButton("🛠️", "工具", SidePanelMode.Toolbox);

        navFlow.Controls.Add(toolboxBtn);
        navFlow.Controls.Add(bookmarksBtn);
        navFlow.Controls.Add(historyBtn);
        navFlow.Controls.Add(downloadsBtn);
        navFlow.Controls.Add(scratchpadBtn);

        _closeButton.Text = "✕";
        _closeButton.Dock = DockStyle.Right;
        _closeButton.Width = 32;
        _closeButton.FlatStyle = FlatStyle.Flat;
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Font = AppTheme.CreateFont(9.5F, FontStyle.Bold);
        _closeButton.ForeColor = AppTheme.TextSecondary;
        _closeButton.Cursor = Cursors.Hand;
        _closeButton.Click += (_, _) => CloseRequested?.Invoke();

        _headerBar.Controls.Add(_closeButton);
        _headerBar.Controls.Add(navFlow);
        _closeButton.BringToFront();

        // 内容展示区
        _contentContainer.Dock = DockStyle.Fill;
        _contentContainer.BackColor = AppTheme.Surface;

        // 初始化子视图
        var bookmarksView = new QuickBookmarksView(
            _bookmarkManager,
            _navigateCallback,
            _toastCallback,
            _openBookmarkManagerCallback);

        var historyView = new QuickHistoryView(
            _historyManager,
            _navigateCallback,
            _toastCallback,
            _openHistoryWindowCallback);

        var downloadsView = new QuickDownloadsView(
            _downloadManager,
            _toastCallback,
            _openDownloadsWindowCallback);

        var scratchpadView = new ScratchpadView(
            () =>
            {
                var tab = _getActiveTab();
                return tab?.CurrentUri is null ? null : $"[{tab.DisplayTitle}]({tab.CurrentUri.AbsoluteUri})";
            },
            _toastCallback);

        var toolboxView = new ToolboxView(
            _getActiveTab,
            _toastCallback,
            _toggleSplitCallback,
            _getAllTabs,
            _suspendBackgroundTabsCallback,
            _toggleMuteAllCallback,
            _openPerformanceCallback);

        _views[SidePanelMode.Bookmarks] = bookmarksView;
        _views[SidePanelMode.History] = historyView;
        _views[SidePanelMode.Downloads] = downloadsView;
        _views[SidePanelMode.Scratchpad] = scratchpadView;
        _views[SidePanelMode.Toolbox] = toolboxView;

        foreach (var view in _views.Values)
        {
            view.Dock = DockStyle.Fill;
            view.Visible = false;
            _contentContainer.Controls.Add(view);
        }

        Controls.Add(_contentContainer);
        Controls.Add(_headerBar);
        _headerBar.BringToFront();
    }

    private Button CreateNavButton(string icon, string label, SidePanelMode mode)
    {
        var btn = new Button
        {
            Text = $"{icon} {label}",
            Width = 56,
            Height = 30,
            Font = AppTheme.CreateFont(8.2F),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1, 1, 1, 1),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => SwitchTo(mode);
        _navButtons[mode] = btn;
        return btn;
    }

    public void SwitchTo(SidePanelMode mode)
    {
        _currentMode = mode;

        foreach (var (m, btn) in _navButtons)
        {
            var isSelected = m == mode;
            btn.BackColor = isSelected ? AppTheme.Accent : Color.Transparent;
            btn.ForeColor = isSelected ? Color.White : AppTheme.TextSecondary;
            btn.Font = AppTheme.CreateFont(8.2F, isSelected ? FontStyle.Bold : FontStyle.Regular);
        }

        foreach (var (m, view) in _views)
        {
            var isTarget = m == mode;
            view.Visible = isTarget;
            if (isTarget)
            {
                view.BringToFront();
                if (view is QuickBookmarksView bv) bv.ReloadBookmarks();
                else if (view is QuickHistoryView hv) hv.ReloadHistory();
                else if (view is QuickDownloadsView dv) dv.ReloadDownloads();
                else if (view is ToolboxView tv) tv.RefreshContent();
            }
        }
    }

    public void ToggleMode(SidePanelMode mode)
    {
        if (Visible && _currentMode == mode)
        {
            Visible = false;
        }
        else
        {
            SwitchTo(mode);
            Visible = true;
            BringToFront();
        }
    }

    public void RefreshActiveContent()
    {
        if (!Visible) return;

        if (_views.TryGetValue(_currentMode, out var view))
        {
            if (view is ToolboxView tv)
            {
                tv.RefreshContent();
            }
        }
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _headerBar.BackColor = AppTheme.SurfaceAlt;
        _contentContainer.BackColor = AppTheme.Surface;
        _closeButton.ForeColor = AppTheme.TextSecondary;

        foreach (var (m, btn) in _navButtons)
        {
            var isSelected = m == _currentMode;
            btn.BackColor = isSelected ? AppTheme.Accent : Color.Transparent;
            btn.ForeColor = isSelected ? Color.White : AppTheme.TextSecondary;
        }

        foreach (var view in _views.Values)
        {
            if (view is QuickBookmarksView bv) bv.ApplyTheme();
            else if (view is QuickHistoryView hv) hv.ApplyTheme();
            else if (view is QuickDownloadsView dv) dv.ApplyTheme();
            else if (view is ScratchpadView sv) sv.ApplyTheme();
            else if (view is ToolboxView tv) tv.ApplyTheme();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 在左侧绘制一条细腻的分隔边框，明确划分网页内容与侧边栏
        using var pen = new Pen(AppTheme.PanelBorder, 1);
        e.Graphics.DrawLine(pen, 0, 0, 0, Height);
    }
}
