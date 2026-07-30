using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 顶部三条 ToolStrip（标题栏/工具栏/书签栏）共享的现代化渲染器。
/// 从 MainWindow 嵌套类提取，供 Chrome 组件复用。
/// </summary>
public sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
{
    public ModernToolStripRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs eventArgs)
    {
        if (eventArgs.ToolStrip is StatusStrip || eventArgs.ToolStrip is ToolStripDropDownMenu)
        {
            base.OnRenderToolStripBackground(eventArgs);
            return;
        }

        var bounds = new Rectangle(0, 0, eventArgs.ToolStrip.Width, eventArgs.ToolStrip.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var surface = eventArgs.ToolStrip.BackColor.IsEmpty ? AppTheme.Surface : eventArgs.ToolStrip.BackColor;
        // 中文注释：Fluent 风扁平背景：纯色表面 + 底部 1px 分隔线，不再叠渐变与高光。
        using var fillBrush = new SolidBrush(surface);
        using var bottomLinePen = new Pen(AppTheme.PanelBorder);
        eventArgs.Graphics.FillRectangle(fillBrush, bounds);
        eventArgs.Graphics.DrawLine(bottomLinePen, 0, bounds.Bottom - 1, bounds.Width, bounds.Bottom - 1);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // 中文注释：去掉四周描边，分隔感由背景底线提供。
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var center = e.Item.Bounds.Left + (e.Item.Bounds.Width / 2);
        using var pen = new Pen(AppTheme.PanelBorder);
        e.Graphics.DrawLine(pen, center, 8, center, e.Item.Bounds.Height - 8);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripButton button)
        {
            base.OnRenderButtonBackground(e);
            return;
        }

        if (button.Tag is string tag &&
            tag.StartsWith("title-tab", StringComparison.Ordinal))
        {
            RenderTitleTabBackground(
                e.Graphics,
                button.Size,
                button.BackColor,
                button.Pressed,
                button.Selected,
                button.Checked);
            return;
        }

        RenderRoundedBackground(e.Graphics, button.Size, button.BackColor, button.Pressed, button.Selected);
    }

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripDropDownButton button)
        {
            base.OnRenderDropDownButtonBackground(e);
            return;
        }

        RenderRoundedBackground(e.Graphics, button.Size, button.BackColor, button.Pressed, button.Selected);
    }

    private static void RenderRoundedBackground(
        Graphics graphics,
        Size size,
        Color baseColor,
        bool isPressed,
        bool isSelected)
    {
        var bounds = new Rectangle(Point.Empty, size);
        bounds.Inflate(-1, -1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        // 中文注释：Fluent 风静默按钮：平时透明，悬停/按下才显示圆角纯色底，无描边无阴影。
        if (!isPressed && !isSelected)
        {
            return;
        }

        var fillColor = isPressed
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.10F)
                : AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.06F))
            : (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.05F)
                : AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.025F));

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRectangle(bounds, 6);
        using var brush = new SolidBrush(fillColor);
        graphics.FillPath(brush, path);
    }

    private static void RenderTitleTabBackground(
        Graphics graphics,
        Size size,
        Color baseColor,
        bool isPressed,
        bool isSelected,
        bool isActive)
    {
        var bounds = new Rectangle(Point.Empty, size);
        bounds.Inflate(-1, -1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        // 中文注释：标题栏标签扁平化：活动标签纯色卡片 + 底部强调条，非活动标签悬停才显底色。
        var fillColor = baseColor;
        if (isPressed)
        {
            fillColor = AppTheme.IsDark
                ? AppTheme.Mix(baseColor, Color.White, 0.08F)
                : AppTheme.Mix(baseColor, Color.Black, 0.05F);
        }
        else if (isSelected && !isActive)
        {
            fillColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.06F)
                : AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.03F);
        }
        else if (!isActive)
        {
            // 非活动且未悬停：透明，融入标题栏背景。
            fillColor = Color.Transparent;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRectangle(bounds, 7);
        if (fillColor != Color.Transparent)
        {
            using var fillBrush = new SolidBrush(fillColor);
            graphics.FillPath(fillBrush, path);
        }

        if (isActive)
        {
            using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
            graphics.DrawPath(borderPen, path);
            using var activePen = new Pen(AppTheme.Accent, 2.4F);
            var y = bounds.Bottom - 2;
            graphics.DrawLine(activePen, bounds.Left + 10, y, bounds.Right - 10, y);
        }
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
