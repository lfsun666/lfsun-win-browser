using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 内容区顶部细横线进度条（Chrome 风格）。
/// 设计取舍：控件常驻可见（高度 1~4px），空闲时只画一条极淡分隔线，
/// 避免显隐造成 Dock 布局抖动与 WebView2 反复 resize；导航开始时进入"不确定进度"，
/// 缓慢逼近 90%，导航结束后冲满并淡出。
/// </summary>
public sealed class LoadingProgressStrip : Control
{
    private const int TickIntervalMs = 33;
    private const float LoadingCeiling = 0.9F;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = TickIntervalMs };
    private float _progress;
    private float _fade = 1F;
    private bool _isLoading;
    private bool _isCompleting;
    private bool _isDisposedTimer;

    public LoadingProgressStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        Dock = DockStyle.Top;
        Height = 3;
        TabStop = false;
        AccessibleName = "页面加载进度";
        _timer.Tick += (_, _) => AdvanceFrame();
        ApplyTheme();
    }

    /// <summary>当前是否处于导航加载中。</summary>
    public bool IsLoading => _isLoading;

    /// <summary>当前绘制进度（0~1），供布局诊断与回归测试读取。</summary>
    internal float CurrentProgress => _progress;

    /// <summary>应用当前主题配色（背景取窗口表面色，进度取强调色）。</summary>
    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        Invalidate();
    }

    /// <summary>导航开始：进入加载态并从当前进度继续爬升。</summary>
    public void StartLoading()
    {
        if (_isDisposedTimer)
        {
            return;
        }

        _isLoading = true;
        _isCompleting = false;
        _fade = 1F;
        // 中文注释：淡出中开始新导航时不能沿用 100%，否则渐近曲线会先倒退到 90%。
        if (_progress <= 0F || _progress >= LoadingCeiling)
        {
            _progress = 0.04F;
        }

        if (!_timer.Enabled)
        {
            _timer.Start();
        }

        Invalidate();
    }

    /// <summary>导航结束：进度冲满后淡出，回到常量分隔线形态。</summary>
    public void StopLoading()
    {
        if (_isDisposedTimer)
        {
            return;
        }

        _isLoading = false;
        _isCompleting = true;
        _progress = 1F;
        if (!_timer.Enabled)
        {
            _timer.Start();
        }

        Invalidate();
    }

    /// <summary>立即复位（如切换标签时目标标签并无加载任务）。</summary>
    public void ResetProgress()
    {
        _isLoading = false;
        _isCompleting = false;
        _progress = 0F;
        _fade = 1F;
        if (_isDisposedTimer)
        {
            return;
        }

        _timer.Stop();
        Invalidate();
    }

    internal void AdvanceFrame()
    {
        if (_isLoading)
        {
            // 中文注释：不确定进度用渐近曲线逼近上限，接近但永不到 100%，避免"假完成"。
            _progress += (LoadingCeiling - _progress) * 0.075F;
            if (_progress > LoadingCeiling)
            {
                _progress = LoadingCeiling;
            }

            Invalidate();
            return;
        }

        if (_isCompleting)
        {
            _fade -= 0.14F;
            if (_fade <= 0F)
            {
                ResetProgress();
                return;
            }

            Invalidate();
            return;
        }

        _timer.Stop();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.Clear(BackColor);

        if (!_isLoading && !_isCompleting)
        {
            // 中文注释：空闲态只画一条极淡底线充当内容区分隔，不占用视觉注意力。
            using var separatorPen = new Pen(AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.SurfaceAlt, Color.White, 0.08F)
                : AppTheme.Mix(AppTheme.SurfaceAlt, Color.Black, 0.06F));
            var separatorY = Height - 1;
            graphics.DrawLine(separatorPen, 0, separatorY, Width, separatorY);
            return;
        }

        var trackWidth = (int)Math.Round(Width * Math.Clamp(_progress, 0F, 1F));
        if (trackWidth <= 0)
        {
            return;
        }

        var barHeight = Math.Max(2, Height - 1);
        var barRect = new Rectangle(0, 0, trackWidth, barHeight);
        var startColor = Color.FromArgb((int)(255 * Math.Clamp(_fade, 0F, 1F)), AppTheme.Accent);
        var endColor = Color.FromArgb((int)(255 * Math.Clamp(_fade, 0F, 1F)), AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.32F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.18F));
        using var brush = new LinearGradientBrush(barRect, startColor, endColor, LinearGradientMode.Horizontal);
        graphics.FillRectangle(brush, barRect);

        // 中文注释：行进方向加一段高亮拖尾，让"加载中"有方向感而不抢页面注意力。
        if (_isLoading && trackWidth > 12)
        {
            var glowWidth = Math.Min(90, Math.Max(24, trackWidth / 4));
            var glowRect = new Rectangle(Math.Max(0, trackWidth - glowWidth), 0, glowWidth, barHeight);
            using var glowBrush = new LinearGradientBrush(
                glowRect,
                Color.FromArgb(0, Color.White),
                Color.FromArgb((int)(90 * Math.Clamp(_fade, 0F, 1F)), Color.White),
                LinearGradientMode.Horizontal);
            graphics.FillRectangle(glowBrush, glowRect);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isDisposedTimer = true;
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
