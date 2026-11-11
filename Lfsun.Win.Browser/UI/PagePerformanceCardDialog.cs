using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 网页加载性能与诊断分析卡片弹窗。
/// 毫秒级展示 DNS 查询、TCP 握手、TTFB 首包到达、DOM 就绪及完全加载耗时阶梯，
/// 并统计网络传输体积、资源类型分布与 DOM 节点数，支持一键复制 Markdown 报告与强刷测试。
/// </summary>
public sealed class PagePerformanceCardDialog : Form
{
    private readonly BrowserTab _tab;
    private readonly Action<string> _toastCallback;
    private PagePerformanceReport? _report;

    private readonly Label _urlLabel = new();
    private readonly Label _protocolBadge = new();
    private readonly Panel _metricsPanel = new();
    private readonly FlowLayoutPanel _actionsPanel = new();

    public PagePerformanceCardDialog(BrowserTab tab, Action<string> toastCallback)
    {
        _tab = tab;
        _toastCallback = toastCallback;

        Text = "网页性能与诊断卡片";
        Width = 680;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.WindowBackground;

        BuildLayout();
        Shown += async (_, _) => await LoadPerformanceDataAsync();
    }

    private void BuildLayout()
    {
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = AppTheme.WindowBackground
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. 顶部标题栏与协议芯片
        var headerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var titleLabel = new Label
        {
            Text = "⚡ 网页性能与诊断",
            Font = AppTheme.CreateFont(13F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 12, 0)
        };
        headerPanel.Controls.Add(titleLabel);

        _protocolBadge.Text = "HTTP/2";
        _protocolBadge.Font = AppTheme.CreateMonoFont(8.5F, FontStyle.Bold);
        _protocolBadge.ForeColor = AppTheme.Accent;
        _protocolBadge.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.25F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.15F);
        _protocolBadge.Padding = new Padding(8, 3, 8, 3);
        _protocolBadge.AutoSize = true;
        AppTheme.ApplyControlRegion(_protocolBadge, 8);
        headerPanel.Controls.Add(_protocolBadge);

        _urlLabel.Text = _tab.CurrentUri?.AbsoluteUri ?? "加载中...";
        _urlLabel.Font = AppTheme.CreateMonoFont(8.5F);
        _urlLabel.ForeColor = AppTheme.TextSecondary;
        _urlLabel.AutoEllipsis = true;
        _urlLabel.Width = 620;
        _urlLabel.Margin = new Padding(0, 6, 0, 0);
        headerPanel.Controls.Add(_urlLabel);

        mainContainer.Controls.Add(headerPanel, 0, 0);

        // 2. 中间性能指标面板
        _metricsPanel.Dock = DockStyle.Fill;
        _metricsPanel.AutoScroll = true;
        _metricsPanel.BackColor = AppTheme.Surface;
        _metricsPanel.Padding = new Padding(14);

        void UpdateMetricsPanelRegion()
        {
            if (_metricsPanel.Width > 8 && _metricsPanel.Height > 8)
            {
                using var path = AppTheme.BuildRoundedRect(_metricsPanel.ClientRectangle, 8);
                _metricsPanel.Region = new Region(path);
            }
        }
        _metricsPanel.SizeChanged += (_, _) => UpdateMetricsPanelRegion();
        _metricsPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _metricsPanel.Width - 1, _metricsPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        mainContainer.Controls.Add(_metricsPanel, 0, 1);

        // 3. 底部操作栏
        _actionsPanel.Dock = DockStyle.Bottom;
        _actionsPanel.AutoSize = true;
        _actionsPanel.FlowDirection = FlowDirection.RightToLeft;
        _actionsPanel.Padding = new Padding(0, 12, 0, 0);

        var closeButton = CreateButton("关闭", 80, isPrimary: false, () => Close());
        var copyReportBtn = CreateButton("📋 复制 Markdown 报告", 160, isPrimary: true, () =>
        {
            if (_report is null)
            {
                _toastCallback("数据采集中，请稍候");
                return;
            }

            Clipboard.SetText(_report.ToMarkdown());
            _toastCallback("性能诊断报告已复制到剪贴板！");
        });
        var refreshBtn = CreateButton("🔄 强刷页面并重测", 140, isPrimary: false, async () =>
        {
            _toastCallback("正在清理缓存并重新测试...");
            await _tab.ClearCacheAndReloadAsync();
            await Task.Delay(1200);
            await LoadPerformanceDataAsync();
        });
        var devToolsBtn = CreateButton("🛠️ DevTools", 100, isPrimary: false, () =>
        {
            _tab.OpenDevTools();
        });

        _actionsPanel.Controls.Add(closeButton);
        _actionsPanel.Controls.Add(copyReportBtn);
        _actionsPanel.Controls.Add(refreshBtn);
        _actionsPanel.Controls.Add(devToolsBtn);

        mainContainer.Controls.Add(_actionsPanel, 0, 2);
        Controls.Add(mainContainer);
    }

    private async Task LoadPerformanceDataAsync()
    {
        _report = await _tab.GetPagePerformanceReportAsync();
        if (_report is null)
        {
            _metricsPanel.Controls.Clear();
            _metricsPanel.Controls.Add(new Label
            {
                Text = "无法采集当前页面的性能数据（页面可能仍在加载或属于内部空白页）。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AppTheme.TextSecondary
            });
            return;
        }

        _protocolBadge.Text = _report.Protocol.ToUpperInvariant();
        _urlLabel.Text = _report.Url;
        RenderMetrics();
    }

    private void RenderMetrics()
    {
        if (_report is null) return;

        _metricsPanel.Controls.Clear();
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = _metricsPanel.ClientSize.Width - 30
        };

        // 阶段耗时分析
        flow.Controls.Add(CreateSubheader("⏱ 耗时阶段瀑布分析 (Navigation Timing)"));

        var maxDuration = Math.Max(1.0, _report.LoadCompleteMs > 0 ? _report.LoadCompleteMs : _report.TotalDurationMs);
        flow.Controls.Add(CreateTimingRow("🌐 DNS 查询解析", _report.DnsDurationMs, maxDuration, AppTheme.Accent));
        flow.Controls.Add(CreateTimingRow("🤝 TCP/TLS 握手连接", _report.TcpDurationMs, maxDuration, Color.FromArgb(168, 85, 247)));
        flow.Controls.Add(CreateTimingRow("🚀 首包到达 (TTFB)", _report.TtfbMs, maxDuration, Color.FromArgb(234, 88, 12)));
        flow.Controls.Add(CreateTimingRow("📄 DOMContentLoaded 就绪", _report.DomReadyMs, maxDuration, AppTheme.Success));
        flow.Controls.Add(CreateTimingRow("🏁 页面完全加载 (Load)", _report.LoadCompleteMs, maxDuration, Color.FromArgb(37, 99, 235)));

        // 资源与体积概览
        flow.Controls.Add(CreateSubheader("📊 资源开销与体积分布 (Resource Timing)"));

        var resGrid = new TableLayoutPanel
        {
            Width = flow.Width,
            Height = 110,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 10)
        };
        resGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
        resGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
        resGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));
        resGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        resGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        resGrid.Controls.Add(CreateStatBadge("网络传输体积", PagePerformanceReport.FormatBytes(_report.TransferSizeBytes)), 0, 0);
        resGrid.Controls.Add(CreateStatBadge("资源请求总数", $"{_report.ResourceCount} 个"), 1, 0);
        resGrid.Controls.Add(CreateStatBadge("DOM 节点总数", $"{_report.DomElementCount} 个"), 2, 0);

        resGrid.Controls.Add(CreateStatBadge("JS 脚本数量", $"{_report.ScriptCount} 个"), 0, 1);
        resGrid.Controls.Add(CreateStatBadge("CSS 样式数量", $"{_report.StyleCount} 个"), 1, 1);
        resGrid.Controls.Add(CreateStatBadge("图片与媒体", $"{_report.ImageCount} 个"), 2, 1);

        flow.Controls.Add(resGrid);
        _metricsPanel.Controls.Add(flow);
    }

    private Control CreateTimingRow(string label, double valueMs, double maxMs, Color barColor)
    {
        var row = new Panel
        {
            Width = 590,
            Height = 28,
            Margin = new Padding(0, 3, 0, 3)
        };

        var nameLabel = new Label
        {
            Text = label,
            Location = new Point(0, 4),
            Size = new Size(180, 20),
            Font = AppTheme.CreateFont(8.8F),
            ForeColor = AppTheme.TextPrimary
        };
        row.Controls.Add(nameLabel);

        var valText = $"{valueMs:F0} ms";
        var valLabel = new Label
        {
            Text = valText,
            Location = new Point(185, 4),
            Size = new Size(70, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Font = AppTheme.CreateMonoFont(8.5F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary
        };
        row.Controls.Add(valLabel);

        var barContainer = new Panel
        {
            Location = new Point(265, 7),
            Size = new Size(310, 14),
            BackColor = AppTheme.SurfaceAlt
        };
        AppTheme.ApplyControlRegion(barContainer, 4);

        var pct = Math.Clamp(valueMs / maxMs, 0.02, 1.0);
        var fillWidth = (int)Math.Round(barContainer.Width * pct);
        var fillBar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(fillWidth, 14),
            BackColor = barColor
        };
        AppTheme.ApplyControlRegion(fillBar, 4);
        barContainer.Controls.Add(fillBar);
        row.Controls.Add(barContainer);

        return row;
    }

    private Control CreateStatBadge(string title, string value)
    {
        var p = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Margin = new Padding(3),
            Padding = new Padding(6)
        };
        AppTheme.ApplyControlRegion(p, 6);
        p.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 6);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 18,
            Font = AppTheme.CreateFont(7.8F),
            ForeColor = AppTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var valLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = AppTheme.CreateMonoFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter
        };

        p.Controls.Add(valLabel);
        p.Controls.Add(titleLabel);
        return p;
    }

    private Control CreateSubheader(string text)
    {
        return new Label
        {
            Text = text,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 6)
        };
    }

    private Button CreateButton(string text, int width, bool isPrimary, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Font = AppTheme.CreateFont(8.8F),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        btn.FlatAppearance.BorderSize = 1;

        if (isPrimary)
        {
            btn.BackColor = AppTheme.Accent;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = AppTheme.Accent;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.AccentHover;
            btn.FlatAppearance.MouseDownBackColor = AppTheme.AccentPressed;
        }
        else
        {
            btn.BackColor = AppTheme.Surface;
            btn.ForeColor = AppTheme.TextPrimary;
            btn.FlatAppearance.BorderColor = AppTheme.PanelBorder;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.AccentHover, AppTheme.Surface, 0.25F);
            btn.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.AccentPressed, AppTheme.Surface, 0.40F);
        }

        AppTheme.ApplyControlRegion(btn, 6);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.10F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.05F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.14F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glow = Color.FromArgb(AppTheme.IsDark ? 22 : 16, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle(-60, -40, 200, 160));
    }
}
