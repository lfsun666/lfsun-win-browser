using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.Browser;

namespace Lfsun.Win.Browser.UI;

public sealed class ModernTabControl : TabControl, IDisposable
{
    private static readonly Size CompactTabSize = new(220, 36);
    private static readonly Size DefaultTabSize = new(248, 42);
    private static readonly Point CompactTabPadding = new(18, 8);
    private static readonly Point DefaultTabPadding = new(22, 10);
    private int _hoverTabIndex = -1;
    private int _hoverCloseIndex = -1;
    private int _dragTabIndex = -1;
    private readonly Dictionary<int, Rectangle> _closeButtonRects = new();
    private readonly ToolTip _tabToolTip = new();
    private string _lastTooltipText = string.Empty;
    private bool _isCompact;
    private bool _hideTabHeader;
    private bool _disposed;

    // 中文注释：缓存 GDI 资源，避免 OnDrawItem 每帧 new Font/Brush/Pen 导致 GDI 泄漏。
    private Font? _pinFont;
    private Font? _tagFont;
    private Font? _titleBoldFont;

    public ModernTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = DefaultTabSize;
        Padding = DefaultTabPadding;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        _tabToolTip.InitialDelay = 180;
        _tabToolTip.ReshowDelay = 80;
        _tabToolTip.AutoPopDelay = 5000;
        _tabToolTip.ShowAlways = true;
    }

    public event Action<int>? TabCloseRequested;
    public event Action<int, int>? TabOrderChangeRequested;
    public event Action? EmptyAreaDoubleClicked;

    public bool HideTabHeader
    {
        get => _hideTabHeader;
        set
        {
            if (_hideTabHeader == value)
            {
                return;
            }

            _hideTabHeader = value;
            ApplyTabHeaderLayout();
        }
    }

    public void ApplyDensity(bool compact)
    {
        _isCompact = compact;
        RecreateCachedFonts();
        ApplyTabHeaderLayout();
    }

    private void RecreateCachedFonts()
    {
        _pinFont?.Dispose();
        _tagFont?.Dispose();
        _titleBoldFont?.Dispose();
        _pinFont = AppTheme.CreateFont(_isCompact ? 7.4F : 8F, FontStyle.Bold);
        _tagFont = AppTheme.CreateFont(_isCompact ? 6.5F : 7.1F, FontStyle.Bold);
        _titleBoldFont = null; // 按需基于 Font 创建
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RecreateCachedFonts();
    }

    protected override void OnDrawItem(DrawItemEventArgs eventArgs)
    {
        if (_hideTabHeader)
        {
            return;
        }

        if (eventArgs.Index < 0 || eventArgs.Index >= TabPages.Count)
        {
            return;
        }

        var bounds = GetTabRect(eventArgs.Index);
        var selected = eventArgs.Index == SelectedIndex;
        var hovered = eventArgs.Index == _hoverTabIndex;
        var hoveredClose = eventArgs.Index == _hoverCloseIndex;
        var browserTab = TabPages[eventArgs.Index].Tag as BrowserTab;
        var pinned = browserTab?.IsPinned == true;
        var renamed = browserTab?.HasCustomTitle == true;
        var tabColor = ResolveTabColor(browserTab?.ColorTagKey);

        var fill = selected
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.04F)
                : Color.White)
            : hovered
                ? (AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.05F)
                    : AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.025F))
                : AppTheme.Surface;
        var border = selected ? AppTheme.PanelBorder : AppTheme.Surface;

        var tabRect = Rectangle.Inflate(bounds, -3, selected ? -1 : (_isCompact ? -2 : -4));
        if (selected)
        {
            tabRect.Y -= 2;
            tabRect.Height += 4;
        }

        var closeSize = _isCompact ? 16 : 18;
        var closeRect = new Rectangle(tabRect.Right - (closeSize + 8), tabRect.Top + (tabRect.Height - closeSize) / 2, closeSize, closeSize);
        _closeButtonRects[eventArgs.Index] = closeRect;

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = BuildRoundedRect(tabRect, 8))
        using (var fillBrush = new SolidBrush(fill))
        using (var borderPen = new Pen(border))
        {
            eventArgs.Graphics.FillPath(fillBrush, path);
            eventArgs.Graphics.DrawPath(borderPen, path);
        }

        if (selected)
        {
            var activeBarColor = tabColor.HasValue
                ? AppTheme.Mix(AppTheme.Accent, tabColor.Value, 0.56F)
                : AppTheme.Accent;
            using var activeBarBrush = new SolidBrush(activeBarColor);
            eventArgs.Graphics.FillRectangle(activeBarBrush, tabRect.Left + 8, tabRect.Top + 1, Math.Max(18, tabRect.Width - 16), 3);
        }

        if (hoveredClose)
        {
            using var closeHoverBrush = new SolidBrush(AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Danger, AppTheme.Surface, 0.70F)
                : AppTheme.Mix(AppTheme.Danger, Color.White, 0.85F));
            eventArgs.Graphics.FillEllipse(closeHoverBrush, closeRect);
        }

        var contentLeft = tabRect.Left + 12;
        if (pinned)
        {
            var pinRect = new Rectangle(contentLeft, tabRect.Top + (tabRect.Height - (_isCompact ? 16 : 18)) / 2, _isCompact ? 26 : 30, _isCompact ? 16 : 18);
            using var pinBrush = new SolidBrush(selected
                ? AppTheme.Accent
                : (AppTheme.IsDark
                    ? AppTheme.Mix(AppTheme.AccentMuted, Color.White, 0.08F)
                    : AppTheme.Mix(AppTheme.AccentMuted, Color.Black, 0.08F)));
            using var pinPath = BuildRoundedRect(pinRect, 7);
            eventArgs.Graphics.FillPath(pinBrush, pinPath);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                "固定",
                _pinFont ?? Font,
                pinRect,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            contentLeft = pinRect.Right + 6;
        }

        if (tabColor.HasValue)
        {
            var dotSize = _isCompact ? 10 : 12;
            var dotRect = new Rectangle(
                contentLeft + 1,
                tabRect.Top + (tabRect.Height - dotSize) / 2,
                dotSize,
                dotSize);
            using var dotBrush = new SolidBrush(tabColor.Value);
            using var dotPen = new Pen(AppTheme.IsDark
                ? AppTheme.Mix(tabColor.Value, Color.White, 0.45F)
                : AppTheme.Mix(tabColor.Value, Color.Black, 0.20F));
            eventArgs.Graphics.FillEllipse(dotBrush, dotRect);
            eventArgs.Graphics.DrawEllipse(dotPen, dotRect);
            contentLeft = dotRect.Right + 6;
        }

        if (renamed)
        {
            var tagRect = new Rectangle(contentLeft, tabRect.Top + (tabRect.Height - (_isCompact ? 15 : 17)) / 2, _isCompact ? 28 : 34, _isCompact ? 15 : 17);
            using var tagBrush = new SolidBrush(AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.Surface, 0.76F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.86F));
            using var tagPath = BuildRoundedRect(tagRect, 6);
            eventArgs.Graphics.FillPath(tagBrush, tagPath);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                "NAME",
                _tagFont ?? Font,
                tagRect,
                AppTheme.Success,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            contentLeft = tagRect.Right + 6;
        }

        var titleRect = new Rectangle(
            contentLeft,
            tabRect.Top + (_isCompact ? 5 : 7),
            Math.Max(32, closeRect.Left - contentLeft - 8),
            tabRect.Height - (_isCompact ? 10 : 14));
        var title = TabPages[eventArgs.Index].Text ?? "新标签页";
        using var titleBrush = new SolidBrush(AppTheme.TextPrimary);
        var drawFont = selected ? (_titleBoldFont ??= new Font(Font, FontStyle.Bold)) : Font;
        TextRenderer.DrawText(
            eventArgs.Graphics,
            title,
            drawFont,
            titleRect,
            titleBrush.Color,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var closeColor = hoveredClose ? AppTheme.Danger : AppTheme.TextSecondary;
        using var closePen = new Pen(closeColor, 1.8F);
        eventArgs.Graphics.DrawLine(closePen, closeRect.Left + 4, closeRect.Top + 4, closeRect.Right - 4, closeRect.Bottom - 4);
        eventArgs.Graphics.DrawLine(closePen, closeRect.Right - 4, closeRect.Top + 4, closeRect.Left + 4, closeRect.Bottom - 4);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        base.OnMouseLeave(eventArgs);
        if (_hoverTabIndex == -1 && _hoverCloseIndex == -1)
        {
            return;
        }

        _hoverTabIndex = -1;
        _hoverCloseIndex = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        if (_hideTabHeader)
        {
            return;
        }

        if (eventArgs.Button == MouseButtons.Middle)
        {
            var tabIndex = HitTestTab(eventArgs.Location);
            if (tabIndex >= 0)
            {
                TabCloseRequested?.Invoke(tabIndex);
            }

            return;
        }

        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        var closeTabIndex = HitTestClose(eventArgs.Location);
        if (closeTabIndex >= 0)
        {
            TabCloseRequested?.Invoke(closeTabIndex);
            return;
        }

        _dragTabIndex = HitTestTab(eventArgs.Location);
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (_hideTabHeader)
        {
            if (_hoverTabIndex != -1 || _hoverCloseIndex != -1)
            {
                _hoverTabIndex = -1;
                _hoverCloseIndex = -1;
                Invalidate();
            }

            _tabToolTip.Hide(this);
            _lastTooltipText = string.Empty;
            return;
        }

        var newHoverTab = HitTestTab(eventArgs.Location);
        var newHoverClose = HitTestClose(eventArgs.Location);
        if (newHoverTab != _hoverTabIndex || newHoverClose != _hoverCloseIndex)
        {
            _hoverTabIndex = newHoverTab;
            _hoverCloseIndex = newHoverClose;
            Invalidate();
        }

        UpdateTooltip(newHoverTab, eventArgs.Location);

        if (eventArgs.Button == MouseButtons.Left && _dragTabIndex >= 0)
        {
            var hoverIndex = HitTestTab(eventArgs.Location);
            if (hoverIndex >= 0 && hoverIndex != _dragTabIndex)
            {
                TabOrderChangeRequested?.Invoke(_dragTabIndex, hoverIndex);
                _dragTabIndex = hoverIndex;
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        _dragTabIndex = -1;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs eventArgs)
    {
        base.OnMouseDoubleClick(eventArgs);
        if (_hideTabHeader)
        {
            return;
        }

        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        if (HitTestTab(eventArgs.Location) >= 0)
        {
            return;
        }

        EmptyAreaDoubleClicked?.Invoke();
    }

    public int GetTabIndexAt(Point location)
    {
        if (_hideTabHeader)
        {
            return -1;
        }

        return HitTestTab(location);
    }

    private void ApplyTabHeaderLayout()
    {
        if (_hideTabHeader)
        {
            Multiline = true;
            SizeMode = TabSizeMode.Fixed;
            Padding = Point.Empty;
            ItemSize = new Size(0, 1);
            _closeButtonRects.Clear();
            _hoverTabIndex = -1;
            _hoverCloseIndex = -1;
            _dragTabIndex = -1;
            _tabToolTip.Hide(this);
            _lastTooltipText = string.Empty;
        }
        else
        {
            Multiline = false;
            SizeMode = TabSizeMode.Fixed;
            Padding = _isCompact ? CompactTabPadding : DefaultTabPadding;
            ItemSize = _isCompact ? CompactTabSize : DefaultTabSize;
        }

        Invalidate();
    }

    private int HitTestTab(Point location)
    {
        for (var i = 0; i < TabCount; i++)
        {
            if (GetTabRect(i).Contains(location))
            {
                return i;
            }
        }

        return -1;
    }

    private int HitTestClose(Point location)
    {
        foreach (var pair in _closeButtonRects)
        {
            if (pair.Value.Contains(location))
            {
                return pair.Key;
            }
        }

        return -1;
    }

    private static Color? ResolveTabColor(string? colorKey)
    {
        var normalized = BrowserTab.NormalizeColorTagKey(colorKey);
        return normalized switch
        {
            "Blue" => AppTheme.IsDark ? Color.FromArgb(119, 178, 252) : Color.FromArgb(45, 118, 227),
            "Green" => AppTheme.IsDark ? Color.FromArgb(107, 215, 172) : Color.FromArgb(41, 155, 103),
            "Orange" => AppTheme.IsDark ? Color.FromArgb(247, 189, 108) : Color.FromArgb(222, 138, 34),
            "Rose" => AppTheme.IsDark ? Color.FromArgb(244, 151, 194) : Color.FromArgb(204, 94, 145),
            _ => null
        };
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

    private void UpdateTooltip(int tabIndex, Point location)
    {
        string tooltipText;
        if (tabIndex >= 0 && tabIndex < TabPages.Count && TabPages[tabIndex].Tag is BrowserTab browserTab)
        {
            var title = string.IsNullOrWhiteSpace(browserTab.DisplayTitle) ? "新标签页" : browserTab.DisplayTitle;
            var url = browserTab.CurrentUri?.AbsoluteUri ?? "about:blank";
            var pinText = browserTab.IsPinned ? " | 固定" : string.Empty;
            var colorText = ResolveTabColorText(browserTab.ColorTagKey);
            var colorSegment = string.IsNullOrWhiteSpace(colorText) ? string.Empty : $" | 色标:{colorText}";
            var customSegment = browserTab.HasCustomTitle ? " | 自定义名" : string.Empty;
            tooltipText = $"{title}{pinText}{colorSegment}{customSegment}\n{url}";
        }
        else
        {
            tooltipText = string.Empty;
        }

        if (string.Equals(tooltipText, _lastTooltipText, StringComparison.Ordinal))
        {
            return;
        }

        _tabToolTip.Hide(this);
        _lastTooltipText = tooltipText;
        if (!string.IsNullOrWhiteSpace(tooltipText))
        {
            _tabToolTip.Show(tooltipText, this, location.X + 14, location.Y + 22, 3000);
        }
    }

    private static GraphicsPath BuildRoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    // 中文注释：释放 GDI 缓存资源（new 显式隐藏基类 Component.Dispose，由 Controls 集合自动调用）。
    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _pinFont?.Dispose();
        _tagFont?.Dispose();
        _titleBoldFont?.Dispose();
        _pinFont = null;
        _tagFont = null;
        _titleBoldFont = null;
    }
}
