namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：SplitContainer 的统一边界保护。
/// 所有分隔条赋值都先挂起布局并放宽最小尺寸，再按真实客户区计算合法距离，避免 DPI/Resize 递归布局越界。
/// </summary>
internal static class SplitLayoutHelper
{
    internal static int CalculateDistance(int availableWidth, int splitterWidth, int panel1Min, int panel2Min, int desired)
    {
        var available = Math.Max(0, availableWidth - Math.Max(0, splitterWidth));
        if (available == 0)
        {
            return 0;
        }

        var leftMinimum = Math.Max(0, panel1Min);
        var rightMinimum = Math.Max(0, panel2Min);
        var requestedMinimum = leftMinimum + rightMinimum;
        if (requestedMinimum > available && requestedMinimum > 0)
        {
            // 中文注释：空间不足时等比缩小两侧约束，不能只满足左侧而反向挤压右侧到不可用。
            leftMinimum = (int)((long)leftMinimum * available / requestedMinimum);
            rightMinimum = available - leftMinimum;
        }

        var minimum = Math.Clamp(leftMinimum, 0, available);
        var maximum = Math.Clamp(available - rightMinimum, minimum, available);
        return Math.Clamp(desired, minimum, maximum);
    }

    internal static void ApplyDistance(
        SplitContainer split,
        int panel1Min,
        int panel2Min,
        int desired,
        bool preserveDesiredDistance = false)
    {
        if (split.IsDisposed || split.ClientSize.Width <= 0)
        {
            return;
        }

        var target = preserveDesiredDistance ? split.SplitterDistance : desired;
        target = CalculateDistance(split.ClientSize.Width, split.SplitterWidth, panel1Min, panel2Min, target);

        split.SuspendLayout();
        try
        {
            // 中文注释：先放宽约束再赋值，避免旧 PanelMinSize 与新窗口宽度在过渡态互相矛盾。
            split.Panel1MinSize = 0;
            split.Panel2MinSize = 0;
            if (split.SplitterDistance != target)
            {
                split.SplitterDistance = target;
            }
        }
        catch (InvalidOperationException)
        {
            // 中文注释：极端 DPI 切换时控件可能再次重入布局；保留当前位置，等待下一次 SizeChanged 校准。
        }
        finally
        {
            split.ResumeLayout(performLayout: true);
        }
    }
}
