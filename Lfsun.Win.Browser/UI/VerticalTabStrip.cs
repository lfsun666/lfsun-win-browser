using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：现代化垂直标签栏 (Vertical Tab Strip)。
/// 停靠于主窗口左侧，适合宽屏显示器浏览海量标签页。
/// 支持完整模式（220px）与极简紧凑收缩模式（48px）、固定标签置顶、音频动态徽标、
/// 单击切换、中键/双击关闭、快速新建标签及展开/折叠切换。
/// </summary>
public sealed class VerticalTabStrip : Panel
{
    private const int ExpandedWidth = 220;
    private const int CollapsedWidth = 48;
    private const int ItemHeight = 36;

    private readonly Func<IReadOnlyList<BrowserTab>> _getTabs;
    private readonly Func<BrowserTab?> _getActiveTab;
    private readonly Action<BrowserTab> _activateTab;
    private readonly Action<BrowserTab> _closeTab;
    private readonly Action _newTab;
    private readonly Action<BrowserTab, Point> _showTabContextMenu;

    private readonly Panel _headerPanel = new();
    private readonly Button _toggleCollapseButton = new();
    private readonly Button _newTabButton = new();
    private readonly Panel _tabsScrollContainer = new();
    private readonly ToolTip _toolTip = new();

    private bool _isCollapsed;
    private int _hoverIndex = -1;
    private int _hoverCloseIndex = -1;

    public event Action<bool>? CollapseChanged;

    public VerticalTabStrip(
        Func<IReadOnlyList<BrowserTab>> getTabs,
        Func<BrowserTab?> getActiveTab,
        Action<BrowserTab> activateTab,
        Action<BrowserTab> closeTab,
        Action newTab,
        Action<BrowserTab, Point> showTabContextMenu)
    {
        _getTabs = getTabs;
        _getActiveTab = getActiveTab;
        _activateTab = activateTab;
        _closeTab = closeTab;
        _newTab = newTab;
        _showTabContextMenu = showTabContextMenu;

        Dock = DockStyle.Left;
        Width = ExpandedWidth;
        Visible = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        ApplyTheme();
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            Width = _isCollapsed ? CollapsedWidth : ExpandedWidth;
            _toggleCollapseButton.Text = _isCollapsed ? "▶" : "◀";
            _newTabButton.Text = _isCollapsed ? "➕" : "➕ 新建标签";
            _toolTip.SetToolTip(_toggleCollapseButton, _isCollapsed ? "展开垂直标签栏" : "折叠垂直标签栏 (仅显示图标)");
            CollapseChanged?.Invoke(_isCollapsed);
            _tabsScrollContainer.Invalidate();
        }
    }

    private void BuildLayout()
    {
        // 顶部操作栏（新建标签 + 折叠按钮）
        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Height = 40;
        _headerPanel.Padding = new Padding(6, 6, 6, 6);
        _headerPanel.BackColor = AppTheme.SurfaceAlt;

        _toggleCollapseButton.Text = "◀";
        _toggleCollapseButton.Dock = DockStyle.Right;
        _toggleCollapseButton.Width = 30;
        _toggleCollapseButton.FlatStyle = FlatStyle.Flat;
        _toggleCollapseButton.FlatAppearance.BorderSize = 0;
        _toggleCollapseButton.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        _toggleCollapseButton.ForeColor = AppTheme.TextSecondary;
        _toggleCollapseButton.Cursor = Cursors.Hand;
        _toolTip.SetToolTip(_toggleCollapseButton, "折叠垂直标签栏 (仅显示图标)");
        _toggleCollapseButton.Click += (_, _) => IsCollapsed = !IsCollapsed;

        _newTabButton.Text = "➕ 新建标签";
        _newTabButton.Dock = DockStyle.Fill;
        _newTabButton.FlatStyle = FlatStyle.Flat;
        _newTabButton.FlatAppearance.BorderSize = 0;
        _newTabButton.Font = AppTheme.CreateFont(9F, FontStyle.Bold);
        _newTabButton.BackColor = AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.15F);
        _newTabButton.ForeColor = AppTheme.Accent;
        _newTabButton.Cursor = Cursors.Hand;
        _toolTip.SetToolTip(_newTabButton, "新建标签页");
        _newTabButton.Click += (_, _) => _newTab();

        _headerPanel.Controls.Add(_newTabButton);
        _headerPanel.Controls.Add(_toggleCollapseButton);
        _toggleCollapseButton.BringToFront();

        // 标签列表自绘滚动区
        _tabsScrollContainer.Dock = DockStyle.Fill;
        _tabsScrollContainer.AutoScroll = true;
        _tabsScrollContainer.DoubleBuffered(true);
        _tabsScrollContainer.BackColor = AppTheme.Surface;
        _tabsScrollContainer.Paint += OnTabsPaint;
        _tabsScrollContainer.MouseMove += OnTabsMouseMove;
        _tabsScrollContainer.MouseLeave += OnTabsMouseLeave;
        _tabsScrollContainer.MouseDown += OnTabsMouseDown;
        _tabsScrollContainer.DoubleClick += OnTabsDoubleClick;

        Controls.Add(_tabsScrollContainer);
        Controls.Add(_headerPanel);
        _headerPanel.BringToFront();
    }

    public void RebuildTabs()
    {
        var tabs = _getTabs();
        var totalHeight = Math.Max(_tabsScrollContainer.Height, tabs.Count * ItemHeight + 10);
        _tabsScrollContainer.AutoScrollMinSize = new Size(0, totalHeight);
        _tabsScrollContainer.Invalidate();
    }

    private void OnTabsPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var tabs = _getTabs();
        var activeTab = _getActiveTab();
        var scrollY = _tabsScrollContainer.AutoScrollPosition.Y;
        var width = _tabsScrollContainer.ClientSize.Width;

        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var isActive = ReferenceEquals(tab, activeTab);
            var isHover = i == _hoverIndex;
            var itemY = scrollY + i * ItemHeight;
            var itemRect = new Rectangle(4, itemY + 2, width - 8, ItemHeight - 4);

            if (itemRect.Bottom < 0 || itemRect.Top > _tabsScrollContainer.ClientSize.Height)
            {
                continue;
            }

            // 背景绘制
            if (isActive)
            {
                using var bgBrush = new SolidBrush(AppTheme.Mix(AppTheme.Accent, AppTheme.Surface, 0.18F));
                using var path = CreateRoundedPath(itemRect, 6);
                g.FillPath(bgBrush, path);

                // 左侧指示条
                using var accentPen = new Pen(AppTheme.Accent, 3);
                g.DrawLine(accentPen, itemRect.Left + 2, itemRect.Top + 4, itemRect.Left + 2, itemRect.Bottom - 4);
            }
            else if (isHover)
            {
                using var hoverBrush = new SolidBrush(AppTheme.SurfaceAlt);
                using var path = CreateRoundedPath(itemRect, 6);
                g.FillPath(hoverBrush, path);
            }

            // 图标徽标 (首字母或固定/静音/声音标志)
            var iconRect = new Rectangle(itemRect.Left + 8, itemRect.Top + (itemRect.Height - 20) / 2, 20, 20);
            var badgeText = tab.IsPinned ? "📌" : (tab.IsPlayingAudio ? (tab.IsMuted ? "🔇" : "🔊") : ResolveIconLetter(tab.DisplayTitle));
            using var font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
            using var textBrush = new SolidBrush(isActive ? AppTheme.Accent : AppTheme.TextPrimary);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(badgeText, font, textBrush, iconRect, sf);

            // 文本模式 (未折叠时绘制标题和关闭按钮)
            if (!_isCollapsed)
            {
                var textLeft = iconRect.Right + 6;
                var textWidth = itemRect.Right - textLeft - 28;
                if (textWidth > 10)
                {
                    var textRect = new Rectangle(textLeft, itemRect.Top, textWidth, itemRect.Height);
                    using var titleFormat = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    using var titleBrush = new SolidBrush(isActive ? AppTheme.TextPrimary : AppTheme.TextSecondary);
                    g.DrawString(tab.DisplayTitle, Font, titleBrush, textRect, titleFormat);
                }

                // 关闭按钮
                if (!tab.IsPinned && (isHover || isActive))
                {
                    var closeRect = GetCloseButtonRect(itemRect);
                    var isCloseHover = i == _hoverCloseIndex;
                    if (isCloseHover)
                    {
                        using var closeBg = new SolidBrush(Color.FromArgb(235, 87, 87));
                        using var cp = CreateRoundedPath(closeRect, 4);
                        g.FillPath(closeBg, cp);
                    }
                    using var closeBrush = new SolidBrush(isCloseHover ? Color.White : AppTheme.TextSecondary);
                    using var closeFont = AppTheme.CreateFont(7.5F, FontStyle.Bold);
                    g.DrawString("✕", closeFont, closeBrush, closeRect, sf);
                }
            }
        }
    }

    private static string ResolveIconLetter(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "🌐";
        var first = title.Trim()[0];
        return char.IsAsciiLetter(first) ? char.ToUpperInvariant(first).ToString() : first.ToString();
    }

    private Rectangle GetCloseButtonRect(Rectangle itemRect)
    {
        return new Rectangle(itemRect.Right - 22, itemRect.Top + (itemRect.Height - 18) / 2, 18, 18);
    }

    private void OnTabsMouseMove(object? sender, MouseEventArgs e)
    {
        var tabs = _getTabs();
        var scrollY = _tabsScrollContainer.AutoScrollPosition.Y;
        var newHoverIndex = -1;
        var newCloseHover = -1;

        for (var i = 0; i < tabs.Count; i++)
        {
            var itemY = scrollY + i * ItemHeight;
            var itemRect = new Rectangle(4, itemY + 2, _tabsScrollContainer.ClientSize.Width - 8, ItemHeight - 4);
            if (itemRect.Contains(e.Location))
            {
                newHoverIndex = i;
                if (!_isCollapsed && !tabs[i].IsPinned)
                {
                    var closeRect = GetCloseButtonRect(itemRect);
                    if (closeRect.Contains(e.Location))
                    {
                        newCloseHover = i;
                    }
                }
                break;
            }
        }

        if (newHoverIndex != _hoverIndex || newCloseHover != _hoverCloseIndex)
        {
            _hoverIndex = newHoverIndex;
            _hoverCloseIndex = newCloseHover;
            _tabsScrollContainer.Invalidate();

            if (_hoverIndex >= 0 && _hoverIndex < tabs.Count)
            {
                var tab = tabs[_hoverIndex];
                var stateDesc = tab.IsSuspended ? "💤 已休眠 (点击唤醒)" : "⚡ 活跃中";
                var audioDesc = tab.IsPlayingAudio ? (tab.IsMuted ? " [🔇 已静音]" : " [🔊 正在播放]") : "";
                var pinDesc = tab.IsPinned ? " [📌 已固定]" : "";
                _toolTip.SetToolTip(_tabsScrollContainer, $"{tab.DisplayTitle}{pinDesc}{audioDesc}\n🔗 {tab.CurrentUri?.AbsoluteUri ?? "about:blank"}\n状态: {stateDesc}");
            }
            else
            {
                _toolTip.SetToolTip(_tabsScrollContainer, null);
            }
        }
    }

    private void OnTabsMouseLeave(object? sender, EventArgs e)
    {
        _hoverIndex = -1;
        _hoverCloseIndex = -1;
        _tabsScrollContainer.Invalidate();
        _toolTip.SetToolTip(_tabsScrollContainer, null);
    }

    private void OnTabsMouseDown(object? sender, MouseEventArgs e)
    {
        var tabs = _getTabs();
        var scrollY = _tabsScrollContainer.AutoScrollPosition.Y;

        for (var i = 0; i < tabs.Count; i++)
        {
            var itemY = scrollY + i * ItemHeight;
            var itemRect = new Rectangle(4, itemY + 2, _tabsScrollContainer.ClientSize.Width - 8, ItemHeight - 4);
            if (itemRect.Contains(e.Location))
            {
                var tab = tabs[i];

                // 点击关闭按钮
                if (!_isCollapsed && !tab.IsPinned && GetCloseButtonRect(itemRect).Contains(e.Location))
                {
                    _closeTab(tab);
                    return;
                }

                // 鼠标中键直接关闭标签
                if (e.Button == MouseButtons.Middle)
                {
                    if (!tab.IsPinned)
                    {
                        _closeTab(tab);
                    }
                    return;
                }

                // 右键上下文菜单
                if (e.Button == MouseButtons.Right)
                {
                    _activateTab(tab);
                    _showTabContextMenu(tab, _tabsScrollContainer.PointToScreen(e.Location));
                    return;
                }

                // 左键激活标签
                if (e.Button == MouseButtons.Left)
                {
                    _activateTab(tab);
                    return;
                }
            }
        }
    }

    private void OnTabsDoubleClick(object? sender, EventArgs e)
    {
        if (_hoverIndex >= 0)
        {
            var tabs = _getTabs();
            if (_hoverIndex < tabs.Count)
            {
                var tab = tabs[_hoverIndex];
                if (!tab.IsPinned)
                {
                    _closeTab(tab);
                }
            }
        }
        else
        {
            _newTab();
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _headerPanel.BackColor = AppTheme.SurfaceAlt;
        _tabsScrollContainer.BackColor = AppTheme.Surface;
        _toggleCollapseButton.ForeColor = AppTheme.TextSecondary;
        _newTabButton.BackColor = AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.15F);
        _newTabButton.ForeColor = AppTheme.Accent;
        _tabsScrollContainer.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 右边框分割线
        using var pen = new Pen(AppTheme.PanelBorder, 1);
        e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
    }
}

internal static class ControlDoubleBufferExtensions
{
    public static void DoubleBuffered(this Control control, bool enable)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        prop?.SetValue(control, enable, null);
    }
}
