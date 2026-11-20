namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：窗口级布局约束工具。
/// 统一把主窗口与大型管理窗口限制在当前显示器工作区内，避免 1366x768 低分辨率、
/// 高 DPI 缩放或副屏工作区较小时，窗口底部操作按钮被挤出屏幕而无法点击。
/// </summary>
public static class WindowLayout
{
    private const int DefaultMargin = 24;

    /// <summary>
    /// 中文注释：按显示器工作区收敛窗口尺寸；窗口显示后（Load）再校正一次，
    /// 以覆盖 AutoScaleMode.Dpi 在创建句柄时放大尺寸的情况。
    /// </summary>
    public static void FitToWorkingArea(Form form, int margin = DefaultMargin)
    {
        if (form is null || form.IsDisposed)
        {
            return;
        }

        ApplyFit(form, margin);
        form.Load += (_, _) => ApplyFit(form, margin);
    }

    private static void ApplyFit(Form form, int margin)
    {
        if (form.IsDisposed)
        {
            return;
        }

        var area = ResolveWorkingArea(form);
        var scaledMargin = Math.Max(8, (int)Math.Round(margin * (form.DeviceDpi / 96.0)));
        var maxWidth = Math.Max(320, area.Width - scaledMargin * 2);
        var maxHeight = Math.Max(240, area.Height - scaledMargin * 2);

        // 中文注释：若窗口最小尺寸本身大于工作区，先放宽最小尺寸，否则 Size 会被 MinimumSize 顶回去。
        var minimum = form.MinimumSize;
        if (minimum.Width > maxWidth || minimum.Height > maxHeight)
        {
            form.MinimumSize = new Size(
                minimum.Width > 0 ? Math.Min(minimum.Width, maxWidth) : 0,
                minimum.Height > 0 ? Math.Min(minimum.Height, maxHeight) : 0);
        }

        // 中文注释：最大化/最小化状态下 WinForms 会忽略 Size/Location 赋值，直接跳过避免干扰窗口状态。
        if (form.WindowState != FormWindowState.Normal)
        {
            return;
        }

        var width = Math.Min(form.Width, maxWidth);
        var height = Math.Min(form.Height, maxHeight);
        if (width != form.Width || height != form.Height)
        {
            form.Size = new Size(width, height);
        }

        // 中文注释：尺寸变化后把窗口整体推回工作区内，保证标题栏与底部按钮都可触达。
        var left = Math.Clamp(form.Left, area.Left, Math.Max(area.Left, area.Right - form.Width));
        var top = Math.Clamp(form.Top, area.Top, Math.Max(area.Top, area.Bottom - form.Height));
        if (left != form.Left || top != form.Top)
        {
            form.Location = new Point(left, top);
        }
    }

    private static Rectangle ResolveWorkingArea(Form form)
    {
        try
        {
            // 中文注释：优先跟随 Owner 所在屏幕（CenterParent 对话框），主窗口则使用主屏工作区。
            if (form.Owner is { IsDisposed: false } owner && owner.IsHandleCreated)
            {
                return Screen.FromControl(owner).WorkingArea;
            }

            if (form.IsHandleCreated)
            {
                return Screen.FromControl(form).WorkingArea;
            }
        }
        catch
        {
            // 中文注释：屏幕信息读取失败时回退到主屏，不影响窗口正常显示。
        }

        return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
    }
}
