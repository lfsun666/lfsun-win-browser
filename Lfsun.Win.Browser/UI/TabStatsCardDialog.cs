using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：标签与内存资源诊断卡片弹窗。
/// 采用 Windows 11 Fluent 浮动圆角立体卡片质感，展示标签生命周期（总数、活跃、休眠、固定、色标、重复）、
/// 内存负载状态、站点域名分布，并提供一键清理重复、休眠后台、关闭其他等高频运维操作。
/// </summary>
public sealed class TabStatsCardDialog : Form
{
    private readonly int _totalTabs;
    private readonly int _activeTabs;
    private readonly int _pinnedTabs;
    private readonly int _suspendedTabs;
    private readonly int _customNamedTabs;
    private readonly int _coloredTabs;
    private readonly int _duplicateTabs;
    private readonly double _memoryMb;
    private readonly IReadOnlyList<(string Host, int Count)> _hostDistributions;
    private readonly Func<Task>? _cleanDuplicatesAction;
    private readonly Func<Task>? _suspendOthersAction;
    private readonly Func<Task>? _closeOthersAction;

    public TabStatsCardDialog(
        int totalTabs,
        int activeTabs,
        int pinnedTabs,
        int suspendedTabs,
        int customNamedTabs,
        int coloredTabs,
        int duplicateTabs,
        double memoryMb,
        IReadOnlyList<(string Host, int Count)> hostDistributions,
        Func<Task>? cleanDuplicatesAction = null,
        Func<Task>? suspendOthersAction = null,
        Func<Task>? closeOthersAction = null)
    {
        _totalTabs = totalTabs;
        _activeTabs = activeTabs;
        _pinnedTabs = pinnedTabs;
        _suspendedTabs = suspendedTabs;
        _customNamedTabs = customNamedTabs;
        _coloredTabs = coloredTabs;
        _duplicateTabs = duplicateTabs;
        _memoryMb = memoryMb;
        _hostDistributions = hostDistributions;
        _cleanDuplicatesAction = cleanDuplicatesAction;
        _suspendOthersAction = suspendOthersAction;
        _closeOthersAction = closeOthersAction;

        Text = "标签与资源诊断";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(530, 520);
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Color.Transparent
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // 顶部卡片
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 164)); // 6个指标卡片网格
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 域名分布卡片
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // 底部按钮栏

        // 1. 顶部卡片：标题与内存芯片
        var headerCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        headerCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AppTheme.ApplyControlRegion(headerCard, 8);
        headerCard.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, headerCard.Width - 1, headerCard.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var titleLabel = new Label
        {
            Text = "🏷️ 标签与内存诊断",
            Font = AppTheme.CreateFont(11.5F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var subtitleLabel = new Label
        {
            Text = "当前窗口标签生命周期、内存负载与域名分布",
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true
        };
        headerCard.Controls.Add(titleLabel, 0, 0);
        headerCard.Controls.Add(subtitleLabel, 0, 1);

        var memoryColor = _memoryMb >= 1800
            ? AppTheme.Danger
            : (_memoryMb >= 1200 ? AppTheme.Warning : AppTheme.Success);
        var memoryBadge = new Label
        {
            Text = $"🧠 内存: {_memoryMb:F1} MB",
            Font = AppTheme.CreateMonoFont(8.8F, FontStyle.Bold),
            ForeColor = memoryColor,
            BackColor = AppTheme.IsDark
                ? AppTheme.Mix(memoryColor, AppTheme.Surface, 0.76F)
                : AppTheme.Mix(memoryColor, Color.White, 0.86F),
            Padding = new Padding(10, 4, 10, 4),
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        AppTheme.ApplyControlRegion(memoryBadge, 8);
        headerCard.Controls.Add(memoryBadge, 1, 0);
        headerCard.SetRowSpan(memoryBadge, 2);

        mainContainer.Controls.Add(headerCard, 0, 0);

        // 2. 指标网格（3列 x 2行）
        var metricsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        metricsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metricsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        metricsGrid.Controls.Add(CreateMetricCard("总标签数", _totalTabs.ToString(), AppTheme.TextPrimary, "当前窗口总数"), 0, 0);
        metricsGrid.Controls.Add(CreateMetricCard("活跃标签", _activeTabs.ToString(), AppTheme.Success, "前台与渲染进程活跃"), 1, 0);
        metricsGrid.Controls.Add(CreateMetricCard("休眠标签", _suspendedTabs.ToString(), AppTheme.Accent, "内存挂起省电模式"), 2, 0);
        metricsGrid.Controls.Add(CreateMetricCard("固定标签", _pinnedTabs.ToString(), AppTheme.Warning, "常驻左侧防误关"), 0, 1);
        metricsGrid.Controls.Add(CreateMetricCard("色标 / 命名", $"{_coloredTabs} / {_customNamedTabs}", AppTheme.AccentHover, "个性化分组标记"), 1, 1);

        var dupColor = _duplicateTabs > 0 ? AppTheme.Danger : AppTheme.TextSecondary;
        metricsGrid.Controls.Add(CreateMetricCard("重复 URL", _duplicateTabs.ToString(), dupColor, _duplicateTabs > 0 ? "可一键去重清理" : "无冗余重复标签"), 2, 1);

        mainContainer.Controls.Add(metricsGrid, 0, 1);

        // 3. 域名分布卡片
        var hostCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 8)
        };
        AppTheme.ApplyControlRegion(hostCard, 8);
        hostCard.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, hostCard.Width - 1, hostCard.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var hostTitle = new Label
        {
            Dock = DockStyle.Top,
            Text = "🌐 主要站点分布 (Top 域名)",
            Font = AppTheme.CreateFont(9.2F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            Height = 22
        };
        hostCard.Controls.Add(hostTitle);

        var hostFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 0)
        };

        if (_hostDistributions.Count == 0)
        {
            hostFlow.Controls.Add(new Label
            {
                Text = "暂无活跃站点域名数据",
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(2, 6, 0, 0)
            });
        }
        else
        {
            foreach (var item in _hostDistributions)
            {
                var pill = new Label
                {
                    Text = $"{item.Host}  ·  {item.Count}",
                    Font = AppTheme.CreateMonoFont(8.5F, FontStyle.Bold),
                    ForeColor = AppTheme.TextPrimary,
                    BackColor = AppTheme.Surface,
                    Padding = new Padding(8, 4, 8, 4),
                    Margin = new Padding(0, 0, 6, 6),
                    AutoSize = true
                };
                AppTheme.ApplyControlRegion(pill, 6);
                pill.Paint += (_, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, pill.Width - 1, pill.Height - 1), 6);
                    using var pen = new Pen(AppTheme.PanelBorder, 1F);
                    e.Graphics.DrawPath(pen, path);
                };
                hostFlow.Controls.Add(pill);
            }
        }

        hostCard.Controls.Add(hostFlow);
        hostTitle.BringToFront();
        mainContainer.Controls.Add(hostCard, 0, 2);

        // 4. 底部快捷操作栏
        var actionBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        var closeButton = CreateActionButton("关闭", 78, isPrimary: false, () => Close());
        actionBar.Controls.Add(closeButton);

        if (_closeOthersAction is not null && _totalTabs > 1)
        {
            var btnCloseOthers = CreateActionButton("关闭其他标签", 108, isPrimary: false, async () =>
            {
                Close();
                await _closeOthersAction();
            });
            actionBar.Controls.Add(btnCloseOthers);
        }

        if (_suspendOthersAction is not null && _activeTabs > 1)
        {
            var btnSuspend = CreateActionButton("💤 休眠后台标签", 118, isPrimary: false, async () =>
            {
                Close();
                await _suspendOthersAction();
            });
            actionBar.Controls.Add(btnSuspend);
        }

        if (_cleanDuplicatesAction is not null && _duplicateTabs > 0)
        {
            var btnDedupe = CreateActionButton($"◎ 清理重复 ({_duplicateTabs})", 126, isPrimary: true, async () =>
            {
                Close();
                await _cleanDuplicatesAction();
            });
            actionBar.Controls.Add(btnDedupe);
        }

        mainContainer.Controls.Add(actionBar, 0, 3);
        Controls.Add(mainContainer);
    }

    private static Panel CreateMetricCard(string title, string value, Color accentColor, string subtext)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(3)
        };
        AppTheme.ApplyControlRegion(card, 8);
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(10, 7)
        };

        var valueLabel = new Label
        {
            Text = value,
            Font = AppTheme.CreateFont(13.5F, FontStyle.Bold),
            ForeColor = accentColor,
            AutoSize = true,
            Location = new Point(9, 26)
        };

        var subLabel = new Label
        {
            Text = subtext,
            Font = AppTheme.CreateFont(7.8F),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(10, 52)
        };

        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(subLabel);
        return card;
    }

    private Button CreateActionButton(string text, int width, bool isPrimary, Action action)
    {
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            Margin = new Padding(6, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        AppTheme.ApplyPrimaryButton(btn, emphasize: isPrimary);
        AppTheme.ApplyControlRegion(btn, 6);
        btn.Click += (_, _) => action();
        return btn;
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.12F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.06F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.16F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glow = Color.FromArgb(AppTheme.IsDark ? 28 : 20, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle((int)(ClientSize.Width * 0.72F), -80, 240, 180));
    }
}
