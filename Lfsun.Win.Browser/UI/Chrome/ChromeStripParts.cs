namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 共享的主题化颜色表，供 ToolStrip/StatusStrip 渲染器使用。
/// </summary>
public sealed class ModernColorTable : ProfessionalColorTable
{
    public override Color ToolStripGradientBegin => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Surface, AppTheme.Accent, 0.10F)
        : AppTheme.Mix(AppTheme.Surface, AppTheme.Accent, 0.06F);
    public override Color ToolStripGradientMiddle => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Surface, AppTheme.Accent, 0.08F)
        : AppTheme.Mix(AppTheme.Surface, AppTheme.Accent, 0.04F);
    public override Color ToolStripGradientEnd => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Surface, Color.Black, 0.12F)
        : AppTheme.Mix(AppTheme.Surface, Color.White, 0.03F);
    public override Color MenuItemSelected => AppTheme.AccentHover;
    public override Color MenuItemBorder => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.16F)
        : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.16F);
    public override Color ButtonSelectedBorder => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.16F)
        : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.16F);
    public override Color ButtonPressedBorder => AppTheme.IsDark
        ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.10F)
        : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.22F);
}

/// <summary>
/// 状态栏渐变背景渲染器。
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

        // 中文注释：状态栏改纯色表面，与顶部工具条风格统一。
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
