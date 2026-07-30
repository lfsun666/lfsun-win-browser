namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 底部状态栏组件：四段状态标签（主状态/模式/公网IP/内存统计）。
/// 负责构建、主题样式、响应式布局与点击事件转发；文本内容由宿主窗口填充。
/// </summary>
public sealed class StatusBarPanel
{
    private readonly ToolStripRenderer _renderer = new ModernStatusStripRenderer();

    public StatusStrip Strip { get; } = new();
    public ToolStripStatusLabel StatusLabel { get; } = new();
    public ToolStripStatusLabel AdBlockLabel { get; } = new();
    public ToolStripStatusLabel LoadTimeLabel { get; } = new();
    public ToolStripStatusLabel ZoomLabel { get; } = new();
    public ToolStripStatusLabel ModeLabel { get; } = new();
    public ToolStripStatusLabel IpLabel { get; } = new();
    public ToolStripStatusLabel MemoryLabel { get; } = new();

    /// <summary>点击内存统计标签（宿主弹出标签统计详情）。</summary>
    public event EventHandler? MemoryClicked;
    /// <summary>点击公网 IP 标签（宿主执行复制）。</summary>
    public event EventHandler? IpClicked;
    /// <summary>点击模式标签（宿主展开外观菜单）。</summary>
    public event EventHandler? ModeClicked;
    /// <summary>点击缩放标签（宿主重置当前标签缩放为 100%）。</summary>
    public event EventHandler? ZoomResetClicked;
    /// <summary>点击拦截计数标签（宿主打开设置隐私页）。</summary>
    public event EventHandler? AdBlockClicked;
    /// <summary>状态栏右键（宿主弹出上下文菜单，坐标为状态栏客户区坐标）。</summary>
    public event EventHandler<Point>? ContextMenuRequested;

    public StatusBarPanel()
    {
        Strip.Dock = DockStyle.Bottom;
        Strip.SizingGrip = false;
        StatusLabel.Spring = true;
        StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        StatusLabel.Text = "就绪";
        ModeLabel.Text = string.Empty;
        ZoomLabel.Text = string.Empty;
        ZoomLabel.Visible = false;
        AdBlockLabel.Text = string.Empty;
        AdBlockLabel.Visible = false;
        LoadTimeLabel.Text = string.Empty;
        LoadTimeLabel.Visible = false;
        LoadTimeLabel.ToolTipText = "本页导航耗时（开发者信息）";
        IpLabel.Text = "IP: 获取中...";
        MemoryLabel.Text = "标签:0 活跃:0 内存:0MB";
        Strip.Items.AddRange([StatusLabel, AdBlockLabel, LoadTimeLabel, ZoomLabel, ModeLabel, IpLabel, MemoryLabel]);

        MemoryLabel.IsLink = true;
        MemoryLabel.ToolTipText = $"{MemoryLabel.ToolTipText}\n点击可查看标签统计详情";
        MemoryLabel.Click += (_, _) => MemoryClicked?.Invoke(this, EventArgs.Empty);

        IpLabel.IsLink = true;
        IpLabel.ToolTipText = "点击可复制当前IP信息";
        IpLabel.Click += (_, _) => IpClicked?.Invoke(this, EventArgs.Empty);

        ModeLabel.IsLink = true;
        ModeLabel.ToolTipText = "点击展开外观菜单";
        ModeLabel.Click += (_, _) => ModeClicked?.Invoke(this, EventArgs.Empty);

        ZoomLabel.IsLink = true;
        ZoomLabel.ToolTipText = "当前页面缩放，点击恢复 100%";
        ZoomLabel.Click += (_, _) => ZoomResetClicked?.Invoke(this, EventArgs.Empty);

        AdBlockLabel.IsLink = true;
        AdBlockLabel.ToolTipText = "本页已拦截的广告/统计请求数，点击打开过滤设置";
        AdBlockLabel.Click += (_, _) => AdBlockClicked?.Invoke(this, EventArgs.Empty);

        Strip.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            ContextMenuRequested?.Invoke(this, eventArgs.Location);
        };
    }

    /// <summary>应用当前 AppTheme 的全部状态栏样式（背景/字体/标签芯片配色/渲染器）。</summary>
    public void ApplyTheme(bool compact)
    {
        Strip.BackColor = AppTheme.Surface;
        Strip.Padding = compact ? new Padding(6, 2, 6, 2) : new Padding(8, 3, 8, 3);
        StatusLabel.ForeColor = AppTheme.TextPrimary;
        AdBlockLabel.ForeColor = AppTheme.TextPrimary;
        LoadTimeLabel.ForeColor = AppTheme.TextPrimary;
        ZoomLabel.ForeColor = AppTheme.TextPrimary;
        ModeLabel.ForeColor = AppTheme.TextPrimary;
        IpLabel.ForeColor = AppTheme.TextPrimary;
        MemoryLabel.ForeColor = AppTheme.TextPrimary;
        ApplyStatusLabelStyles();
    }

    /// <summary>按窗口宽度调整标签可见性与紧凑布局；模式标签文字由宿主随后填充。</summary>
    public void ApplyResponsiveLayout(int clientWidth)
    {
        if (!Strip.Visible)
        {
            return;
        }

        var isNarrow = clientWidth < 1280;
        var isUltraNarrow = clientWidth < 1020;
        var isCompact = clientWidth < 900;

        ModeLabel.Visible = !isUltraNarrow;
        IpLabel.Visible = !isNarrow;
        MemoryLabel.Visible = true;
        // 中文注释：超窄屏隐藏耗时芯片，优先保主状态。
        if (isUltraNarrow)
        {
            LoadTimeLabel.Visible = false;
        }

        // 中文注释：窄屏时让状态栏优先展示运行状态和内存，弱化次级信息，避免信息挤压。
        StatusLabel.Spring = isCompact;
        StatusLabel.TextAlign = isCompact ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
        StatusLabel.Padding = isUltraNarrow ? new Padding(2, 1, 2, 1) : new Padding(8, 2, 8, 2);
        var compactPadding = isUltraNarrow ? new Padding(4, 1, 4, 1) : new Padding(7, 2, 7, 2);
        AdBlockLabel.Padding = compactPadding;
        LoadTimeLabel.Padding = compactPadding;
        ZoomLabel.Padding = compactPadding;
        ModeLabel.Padding = compactPadding;
        IpLabel.Padding = compactPadding;
        MemoryLabel.Padding = compactPadding;
    }

    /// <summary>按内存占用量切换内存标签的警示配色（正常/关注/危险）。</summary>
    public void SetMemoryUsageLevel(double memoryMb)
    {
        if (memoryMb >= 1800)
        {
            MemoryLabel.ForeColor = AppTheme.Danger;
            MemoryLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.66F)
                : AppTheme.Mix(AppTheme.Danger, Color.White, 0.84F);
        }
        else if (memoryMb >= 1200)
        {
            MemoryLabel.ForeColor = AppTheme.Warning;
            MemoryLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.70F)
                : AppTheme.Mix(AppTheme.Warning, Color.White, 0.86F);
        }
        else
        {
            MemoryLabel.ForeColor = AppTheme.Success;
            MemoryLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.70F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.86F);
        }
    }

    /// <summary>中文注释：更新缩放标签；100% 时自动隐藏。</summary>
    public void SetZoomFactor(double zoomFactor)
    {
        var percent = (int)Math.Round(zoomFactor * 100);
        var deviates = Math.Abs(zoomFactor - 1.0) >= 0.005;
        ZoomLabel.Text = deviates ? $"缩放 {percent}%" : string.Empty;
        ZoomLabel.Visible = deviates;
    }

    /// <summary>中文注释：更新本页广告拦截计数；功能关闭或计数为 0 时隐藏。</summary>
    public void SetAdBlockedCount(bool enabled, int blockedCount)
    {
        var visible = enabled && blockedCount > 0;
        AdBlockLabel.Text = visible ? $"拦截 {blockedCount}" : string.Empty;
        AdBlockLabel.Visible = visible;
    }

    /// <summary>中文注释：更新页面导航耗时芯片；负值隐藏，按快慢分级着色（开发者向）。</summary>
    public void SetLoadTime(int milliseconds)
    {
        if (milliseconds < 0)
        {
            LoadTimeLabel.Text = string.Empty;
            LoadTimeLabel.Visible = false;
            return;
        }

        LoadTimeLabel.Text = milliseconds >= 1000
            ? $"⏱ {milliseconds / 1000.0:0.0}s"
            : $"⏱ {milliseconds}ms";
        LoadTimeLabel.Visible = true;

        var semantic = milliseconds < 1000
            ? AppTheme.Success
            : milliseconds < 3000 ? AppTheme.Warning : AppTheme.Danger;
        LoadTimeLabel.ForeColor = semantic;
        LoadTimeLabel.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(semantic, AppTheme.Surface, 0.80F)
            : AppTheme.Mix(semantic, Color.White, 0.88F);
    }

    private void ApplyStatusLabelStyles()
    {
        var statusFont = AppTheme.CreateFont(8.8F);
        // 中文注释：数值类芯片（耗时/缩放/IP/内存）用等宽字体，更贴近开发者工具观感。
        var monoFont = AppTheme.CreateMonoFont(8.4F);
        // 中文注释：扁平芯片风：去 3D 边框，仅用淡色背景区分信息块。
        var normalChipBack = AppTheme.SurfaceAlt;
        var metaChipBack = AppTheme.SurfaceAlt;
        StatusLabel.Font = statusFont;
        StatusLabel.BackColor = normalChipBack;
        StatusLabel.BorderSides = ToolStripStatusLabelBorderSides.None;
        StatusLabel.Padding = new Padding(8, 2, 8, 2);
        StatusLabel.Margin = new Padding(4, 2, 4, 2);

        foreach (var label in new[] { AdBlockLabel, LoadTimeLabel, ZoomLabel, ModeLabel, IpLabel, MemoryLabel })
        {
            label.Font = statusFont;
            label.BackColor = metaChipBack;
            label.ForeColor = AppTheme.TextPrimary;
            label.BorderSides = ToolStripStatusLabelBorderSides.None;
            label.Padding = new Padding(8, 2, 8, 2);
            label.Margin = new Padding(4, 2, 4, 2);
        }

        // 中文注释：功能芯片用极淡的语义色底，文字用对应语义色，去饱和大色块。
        ModeLabel.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.Surface, 0.82F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.90F);
        ModeLabel.ForeColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.30F)
            : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.12F);
        ZoomLabel.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.Surface, 0.78F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.86F);
        ZoomLabel.ForeColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.30F)
            : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.12F);
        AdBlockLabel.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Success, AppTheme.Surface, 0.80F)
            : AppTheme.Mix(AppTheme.Success, Color.White, 0.88F);
        AdBlockLabel.ForeColor = AppTheme.Success;
        IpLabel.BackColor = AppTheme.SurfaceAlt;
        IpLabel.ForeColor = AppTheme.TextSecondary;

        // 中文注释：数值芯片改等宽字体（覆盖上面循环里的默认字体）。
        LoadTimeLabel.Font = monoFont;
        ZoomLabel.Font = monoFont;
        IpLabel.Font = monoFont;
        MemoryLabel.Font = monoFont;

        Strip.BackColor = AppTheme.Surface;
        Strip.ForeColor = AppTheme.TextPrimary;
        Strip.RenderMode = ToolStripRenderMode.Professional;
        Strip.Renderer = _renderer;
    }
}
