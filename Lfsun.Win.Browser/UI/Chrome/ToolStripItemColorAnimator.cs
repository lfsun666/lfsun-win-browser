namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 中文注释：ToolStripItem 背景色渐变动画器。
/// 单个共享 Timer（约 60fps）驱动所有条目的悬停淡入淡出，避免每个按钮各建定时器。
/// </summary>
public static class ToolStripItemColorAnimator
{
    private sealed class AnimationState
    {
        public Color From;
        public Color To;
        public float Progress;
        public int DurationMs;
    }

    private const int TickIntervalMs = 16;
    private static readonly Dictionary<ToolStripItem, AnimationState> States = new();
    private static readonly System.Windows.Forms.Timer Timer = new() { Interval = TickIntervalMs };

    static ToolStripItemColorAnimator()
    {
        Timer.Tick += (_, _) => AdvanceAll();
    }

    public static void AnimateBackColor(ToolStripItem item, Color target, int durationMs = 140)
    {
        if (item.BackColor.ToArgb() == target.ToArgb())
        {
            States.Remove(item);
            return;
        }

        States[item] = new AnimationState
        {
            From = item.BackColor,
            To = target,
            Progress = 0F,
            DurationMs = Math.Max(TickIntervalMs, durationMs)
        };

        if (!Timer.Enabled)
        {
            Timer.Start();
        }
    }

    public static void CancelAll()
    {
        States.Clear();
        Timer.Stop();
    }

    private static void AdvanceAll()
    {
        if (States.Count == 0)
        {
            Timer.Stop();
            return;
        }

        var finished = new List<ToolStripItem>();
        foreach (var pair in States)
        {
            var item = pair.Key;
            var state = pair.Value;
            if (item.Owner is null || item.Owner.IsDisposed)
            {
                finished.Add(item);
                continue;
            }

            state.Progress = Math.Min(1F, state.Progress + ((float)TickIntervalMs / state.DurationMs));
            // 中文注释：ease-out 缓动，起始快结尾缓，观感接近 Fluent 悬停反馈。
            var eased = 1F - ((1F - state.Progress) * (1F - state.Progress));
            item.BackColor = AppTheme.Mix(state.From, state.To, eased);
            if (state.Progress >= 1F)
            {
                finished.Add(item);
            }
        }

        foreach (var item in finished)
        {
            States.Remove(item);
        }

        if (States.Count == 0)
        {
            Timer.Stop();
        }
    }
}
