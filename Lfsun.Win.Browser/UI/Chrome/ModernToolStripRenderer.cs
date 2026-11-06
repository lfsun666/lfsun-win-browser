using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 顶部三条 ToolStrip（标题栏/工具栏/书签栏）共享的现代化渲染器。
/// 遵循 Windows 11 Fluent 扁平设计准则，提供圆角卡片、平滑悬停微动与无缝边框体系。
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
        using var fillBrush = new SolidBrush(surface);
        using var bottomLinePen = new Pen(AppTheme.PanelBorder);
        eventArgs.Graphics.FillRectangle(fillBrush, bounds);
        // 底部细致的 1px 分隔线，区分工具栏与页面内容
        eventArgs.Graphics.DrawLine(bottomLinePen, 0, bounds.Bottom - 1, bounds.Width, bounds.Bottom - 1);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // 扁平化：边框由背景底线呈现，无需冗余外边框
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var center = e.Item.Bounds.Left + (e.Item.Bounds.Width / 2);
        var top = Math.Max(4, (e.Item.Bounds.Height - 16) / 2);
        var bottom = Math.Min(e.Item.Bounds.Height - 4, top + 16);
        using var pen = new Pen(AppTheme.PanelBorder);
        e.Graphics.DrawLine(pen, center, top, center, bottom);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 窗口关闭按钮悬停/按下时，强制使用亮白色文本/字形
        if (e.Item.Tag is "window-close" && (e.Item.Pressed || e.Item.Selected))
        {
            e.TextColor = Color.White;
        }

        base.OnRenderItemText(e);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripButton button)
        {
            base.OnRenderButtonBackground(e);
            return;
        }

        var tag = button.Tag as string;

        // 1. 窗口关闭按钮
        if (tag is "window-close")
        {
            RenderWindowCloseBackground(e.Graphics, button.Size, button.Pressed, button.Selected);
            return;
        }

        // 2. 窗口最小化/最大化按钮
        if (tag is "window-min" or "window-max")
        {
            RenderWindowControlBackground(e.Graphics, button.Size, button.Pressed, button.Selected);
            return;
        }

        // 3. 标题栏标签页按钮
        if (tag is not null && tag.StartsWith("title-tab", StringComparison.Ordinal))
        {
            RenderTitleTabBackground(
                e.Graphics,
                button.Size,
                button.BackColor,
                button.Pressed,
                button.Selected,
                button.Checked || tag.Contains("active"));
            return;
        }

        // 4. 普通工具栏按钮
        var isEmphasized = tag is "primary";
        RenderToolbarButtonBackground(e.Graphics, button.Size, button.Pressed, button.Selected, isEmphasized);
    }

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripDropDownButton button)
        {
            base.OnRenderDropDownButtonBackground(e);
            return;
        }

        var isEmphasized = button.Tag is "primary";
        RenderToolbarButtonBackground(e.Graphics, button.Size, button.Pressed, button.Selected, isEmphasized);
    }

    private static void RenderWindowCloseBackground(Graphics graphics, Size size, bool isPressed, bool isSelected)
    {
        if (!isPressed && !isSelected)
        {
            return;
        }

        var bounds = new Rectangle(Point.Empty, size);
        var closeColor = isPressed ? Color.FromArgb(199, 15, 29) : Color.FromArgb(232, 17, 35);
        using var brush = new SolidBrush(closeColor);
        graphics.FillRectangle(brush, bounds);
    }

    private static void RenderWindowControlBackground(Graphics graphics, Size size, bool isPressed, bool isSelected)
    {
        if (!isPressed && !isSelected)
        {
            return;
        }

        var bounds = new Rectangle(Point.Empty, size);
        bounds.Inflate(-1, -1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        var color = isPressed
            ? (AppTheme.IsDark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(35, 0, 0, 0))
            : (AppTheme.IsDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0));

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRectangle(bounds, 4);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static void RenderToolbarButtonBackground(
        Graphics graphics,
        Size size,
        bool isPressed,
        bool isSelected,
        bool isEmphasized)
    {
        var bounds = new Rectangle(Point.Empty, size);
        bounds.Inflate(-1, -1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (isEmphasized)
        {
            var baseFill = isPressed
                ? (AppTheme.IsDark ? AppTheme.Mix(AppTheme.Accent, Color.Black, 0.20F) : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.12F))
                : isSelected
                    ? AppTheme.AccentHover
                    : AppTheme.AccentMuted;

            using var emphasizedPath = BuildRoundedRectangle(bounds, 6);
            using var emphasizedBrush = new SolidBrush(baseFill);
            using var emphasizedBorderPen = new Pen(AppTheme.PanelBorder);
            graphics.FillPath(emphasizedBrush, emphasizedPath);
            graphics.DrawPath(emphasizedBorderPen, emphasizedPath);
            return;
        }

        // 普通按钮平时透明，仅在悬停与按下时展现圆角底色反馈
        if (!isPressed && !isSelected)
        {
            return;
        }

        var fillColor = isPressed
            ? (AppTheme.IsDark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0))
            : (AppTheme.IsDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0));

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

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (isActive)
        {
            // 活动标签：纯色卡片，连接下方工具栏
            var activeCardColor = AppTheme.IsDark ? AppTheme.SurfaceAlt : Color.White;
            using var path = BuildRoundedRectangle(bounds, 6);
            using var cardBrush = new SolidBrush(activeCardColor);
            using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
            graphics.FillPath(cardBrush, path);
            graphics.DrawPath(borderPen, path);

            // 底部强调指示条
            using var activeIndicatorPen = new Pen(AppTheme.Accent, 2.5F);
            var y = bounds.Bottom - 1;
            graphics.DrawLine(activeIndicatorPen, bounds.Left + 10, y, bounds.Right - 10, y);
        }
        else if (isPressed || isSelected)
        {
            // 非活动标签悬停/按下
            var hoverColor = isPressed
                ? (AppTheme.IsDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0))
                : (AppTheme.IsDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0));

            using var path = BuildRoundedRectangle(bounds, 6);
            using var brush = new SolidBrush(hoverColor);
            graphics.FillPath(brush, path);
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
