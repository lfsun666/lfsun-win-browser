using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 共享的主题化颜色表，供 ToolStrip/StatusStrip 渲染器使用。
/// </summary>
public sealed class ModernColorTable : ProfessionalColorTable
{
    public override Color ToolStripGradientBegin => AppTheme.Surface;
    public override Color ToolStripGradientMiddle => AppTheme.Surface;
    public override Color ToolStripGradientEnd => AppTheme.Surface;

    public override Color ToolStripDropDownBackground => AppTheme.IsDark
        ? AppTheme.SurfaceAlt
        : Color.FromArgb(252, 253, 255);

    public override Color ImageMarginGradientBegin => AppTheme.IsDark
        ? AppTheme.SurfaceAlt
        : Color.FromArgb(252, 253, 255);
    public override Color ImageMarginGradientMiddle => ImageMarginGradientBegin;
    public override Color ImageMarginGradientEnd => ImageMarginGradientBegin;

    public override Color MenuBorder => AppTheme.PanelBorder;
    public override Color MenuItemBorder => Color.Transparent;

    public override Color MenuItemSelected => AppTheme.IsDark
        ? Color.FromArgb(48, 255, 255, 255)
        : Color.FromArgb(22, 0, 0, 0);

    public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
    public override Color MenuItemSelectedGradientEnd => MenuItemSelected;

    public override Color MenuItemPressedGradientBegin => AppTheme.IsDark
        ? Color.FromArgb(64, 255, 255, 255)
        : Color.FromArgb(36, 0, 0, 0);
    public override Color MenuItemPressedGradientMiddle => MenuItemPressedGradientBegin;
    public override Color MenuItemPressedGradientEnd => MenuItemPressedGradientBegin;

    public override Color SeparatorDark => AppTheme.PanelBorder;
    public override Color SeparatorLight => Color.Transparent;

    public override Color ButtonSelectedBorder => Color.Transparent;
    public override Color ButtonPressedBorder => Color.Transparent;
    public override Color ButtonSelectedHighlight => AppTheme.AccentHover;
    public override Color ButtonPressedHighlight => AppTheme.Accent;
}

/// <summary>
/// 状态栏现代扁平化胶囊芯片渲染器。
/// </summary>
public sealed class ModernStatusStripRenderer : ToolStripProfessionalRenderer
{
    public ModernStatusStripRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs eventArgs)
    {
        if (eventArgs.ToolStrip is not StatusStrip)
        {
            base.OnRenderToolStripBackground(eventArgs);
            return;
        }

        var bounds = new Rectangle(0, 0, eventArgs.ToolStrip.Width, eventArgs.ToolStrip.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var brush = new SolidBrush(AppTheme.Surface);
        eventArgs.Graphics.FillRectangle(brush, bounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
    {
        if (eventArgs.ToolStrip is not StatusStrip)
        {
            base.OnRenderToolStripBorder(eventArgs);
            return;
        }

        var width = eventArgs.ToolStrip.Width;
        if (width <= 0)
        {
            return;
        }

        using var topPen = new Pen(AppTheme.PanelBorder);
        eventArgs.Graphics.DrawLine(topPen, 0, 0, width, 0);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripStatusLabel statusLabel ||
            statusLabel.BackColor.IsEmpty ||
            statusLabel.BackColor == Color.Transparent ||
            statusLabel.BackColor == AppTheme.Surface)
        {
            base.OnRenderLabelBackground(e);
            return;
        }

        var bounds = new Rectangle(Point.Empty, statusLabel.Size);
        bounds.Inflate(-1, -1);
        if (bounds.Width <= 6 || bounds.Height <= 6)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRectangle(bounds, 5);
        using var brush = new SolidBrush(statusLabel.BackColor);
        using var pen = new Pen(AppTheme.PanelBorder, 1F);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath BuildRoundedRectangle(Rectangle bounds, int radius)
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
}

/// <summary>
/// 占满剩余宽度的 ToolStrip 标签（标题栏拖动区）。
/// </summary>
public sealed class ToolStripSpringLabel : ToolStripLabel
{
    public override Size GetPreferredSize(Size constrainingSize)
    {
        if (IsOnOverflow || Owner is null)
        {
            return base.GetPreferredSize(constrainingSize);
        }

        var width = Owner.DisplayRectangle.Width;
        if (Owner.OverflowButton.Visible)
        {
            width -= Owner.OverflowButton.Width + Owner.OverflowButton.Margin.Horizontal;
        }

        foreach (ToolStripItem item in Owner.Items)
        {
            if (item == this || item.IsOnOverflow)
            {
                continue;
            }

            width -= item.Width + item.Margin.Horizontal;
        }

        var preferred = base.GetPreferredSize(constrainingSize);
        preferred.Width = Math.Max(AppTheme.IsCompact ? 36 : 48, width);
        return preferred;
    }
}

/// <summary>
/// 占满剩余宽度的 ToolStrip 文本框（地址栏）。
/// </summary>
public sealed class ToolStripSpringTextBox : ToolStripTextBox
{
    public override Size GetPreferredSize(Size constrainingSize)
    {
        if (IsOnOverflow || Owner is null)
        {
            return DefaultSize;
        }

        var width = Owner.DisplayRectangle.Width;
        if (Owner.OverflowButton.Visible)
        {
            width -= Owner.OverflowButton.Width + Owner.OverflowButton.Margin.Horizontal;
        }

        foreach (ToolStripItem item in Owner.Items)
        {
            if (item == this || item.IsOnOverflow)
            {
                continue;
            }

            width -= item.Width + item.Margin.Horizontal;
        }

        var minWidth = AppTheme.IsCompact ? 150 : 180;
        var threshold = AppTheme.IsCompact ? 200 : 240;
        if (width < threshold)
        {
            width = minWidth;
        }

        var preferred = base.GetPreferredSize(constrainingSize);
        preferred.Width = width;
        return preferred;
    }
}
