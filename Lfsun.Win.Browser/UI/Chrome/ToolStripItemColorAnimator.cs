namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 中文注释：ToolStripItem 背景色渐变动画器。
/// 单个共享 Timer（约 60fps）驱动所有条目的悬停淡入淡出，避免每个按钮各建定时器。
/// 使用弱引用持有 ToolStripItem，避免被动画静态字典长期持有而无法 GC。
/// </summary>
public static class ToolStripItemColorAnimator
{
    private sealed class AnimationState
    {
        public WeakReference<ToolStripItem> ItemRef = null!;
        public Color From;
        public Color To;
        public float Progress;
        public int DurationMs;
    }

    private const int TickIntervalMs = 16;
    private static readonly List<AnimationState> States = new();
    private static readonly System.Windows.Forms.Timer Timer = new() { Interval = TickIntervalMs };

    static ToolStripItemColorAnimator()
    {
        Timer.Tick += (_, _) => AdvanceAll();
    }

    public static void AnimateBackColor(ToolStripItem item, Color target, int durationMs = 140)
    {
        if (item.BackColor.ToArgb() == target.ToArgb())
        {
            States.RemoveAll(s => !s.ItemRef.TryGetTarget(out _) || ReferenceEquals(s.ItemRef.GetTargetOrDefault(), item));
            return;
        }

        // 同一 item 已在动画时覆盖其状态，避免叠加。
        var existing = States.FirstOrDefault(s =>
        {
            s.ItemRef.TryGetTarget(out var targetItem);
            return ReferenceEquals(targetItem, item);
        });
        if (existing is not null)
        {
            existing.From = item.BackColor;
            existing.To = target;
            existing.Progress = 0F;
            existing.DurationMs = Math.Max(TickIntervalMs, durationMs);
            EnsureTimerRunning();
            return;
        }

        States.Add(new AnimationState
        {
            ItemRef = new WeakReference<ToolStripItem>(item),
            From = item.BackColor,
            To = target,
            Progress = 0F,
            DurationMs = Math.Max(TickIntervalMs, durationMs)
        });

        EnsureTimerRunning();
    }

    public static void CancelAll()
    {
        States.Clear();
        Timer.Stop();
    }

    public static void CancelFor(ToolStripItem item)
    {
        var changed = States.RemoveAll(s =>
        {
            s.ItemRef.TryGetTarget(out var targetItem);
            return ReferenceEquals(targetItem, item);
        }) > 0;
        if (changed && States.Count == 0)
        {
            Timer.Stop();
        }
    }

    private static void EnsureTimerRunning()
    {
        if (!Timer.Enabled)
        {
            Timer.Start();
        }
    }

    private static void AdvanceAll()
    {
        if (States.Count == 0)
        {
            Timer.Stop();
            return;
        }

        var toRemove = new List<AnimationState>();
        foreach (var state in States)
        {
            if (!state.ItemRef.TryGetTarget(out var item) ||
                item is null ||
                item.Owner is null ||
                item.Owner.IsDisposed)
            {
                toRemove.Add(state);
                continue;
            }

            state.Progress = Math.Min(1F, state.Progress + ((float)TickIntervalMs / state.DurationMs));
            // 中文注释：ease-out 缓动，起始快结尾缓，观感接近 Fluent 悬停反馈。
            var eased = 1F - ((1F - state.Progress) * (1F - state.Progress));
            item.BackColor = AppTheme.Mix(state.From, state.To, eased);
            if (state.Progress >= 1F)
            {
                toRemove.Add(state);
            }
        }

        foreach (var state in toRemove)
        {
            States.Remove(state);
        }

        if (States.Count == 0)
        {
            Timer.Stop();
        }
    }
}

internal static class WeakReferenceExtensions
{
    public static T? GetTargetOrDefault<T>(this WeakReference<T> reference) where T : class
    {
        return reference.TryGetTarget(out var target) ? target : null;
    }
}
