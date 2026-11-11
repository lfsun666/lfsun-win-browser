using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：站点安全与属性卡片弹窗。
/// 包含 HTTPS 加密状态、当前主机、广告过滤拦截次数、耗时统计、快速清除 Cookie/缓存以及扫码等实用快捷交互。
/// </summary>
public sealed class SiteInfoCardDialog : Form
{
    private readonly Uri? _currentUri;
    private readonly int _blockedCount;
    private readonly long _loadMs;
    private readonly Action? _clearCookiesAction;
    private readonly Action? _hardRefreshAction;
    private readonly Action? _viewSourceAction;
    private readonly Action? _showQrAction;

    public SiteInfoCardDialog(
        Uri? currentUri,
        int blockedCount,
        long loadMs,
        Action? clearCookiesAction,
        Action? hardRefreshAction,
        Action? viewSourceAction,
        Action? showQrAction)
    {
        _currentUri = currentUri;
        _blockedCount = blockedCount;
        _loadMs = loadMs;
        _clearCookiesAction = clearCookiesAction;
        _hardRefreshAction = hardRefreshAction;
        _viewSourceAction = viewSourceAction;
        _showQrAction = showQrAction;

        Text = "站点信息";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 360);
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var isHttps = string.Equals(_currentUri?.Scheme, "https", StringComparison.OrdinalIgnoreCase);
        var isLocal = _currentUri is null ||
                      string.Equals(_currentUri.Scheme, "about", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(_currentUri.Scheme, "file", StringComparison.OrdinalIgnoreCase);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(22, 18, 22, 16),
            BackColor = AppTheme.Surface
        };

        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Security header card
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Hostname
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Stats (Ad block + Load time)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Action buttons
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. Security header card
        var cardBg = isLocal
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.15F)
            : (isHttps
                ? (AppTheme.IsDark ? Color.FromArgb(28, 54, 42) : Color.FromArgb(232, 248, 238))
                : (AppTheme.IsDark ? Color.FromArgb(64, 44, 24) : Color.FromArgb(254, 244, 230)));
        var borderColor = isLocal
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.40F)
            : (isHttps
                ? (AppTheme.IsDark ? Color.FromArgb(45, 90, 65) : Color.FromArgb(170, 225, 190))
                : (AppTheme.IsDark ? Color.FromArgb(120, 80, 40) : Color.FromArgb(240, 195, 150)));

        var securityCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = cardBg,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8)
        };

        void UpdateSecurityCardRegion()
        {
            if (securityCard.Width > 8 && securityCard.Height > 8)
            {
                using var path = AppTheme.BuildRoundedRect(securityCard.ClientRectangle, 8);
                securityCard.Region = new Region(path);
            }
        }
        securityCard.SizeChanged += (_, _) => UpdateSecurityCardRegion();
        securityCard.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, securityCard.Width - 1, securityCard.Height - 1), 8);
            using var pen = new Pen(borderColor, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var securityLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = isLocal
                ? "⚙️ 内置或本地页面 (系统环境安全)"
                : (isHttps
                    ? "🔒 连接已加密 (有效且受信的 HTTPS 连接)"
                    : "⚠️ 连接未加密 (敏感信息存在被窥探风险)"),
            Font = AppTheme.CreateFont(9.3F, FontStyle.Bold),
            ForeColor = isLocal
                ? AppTheme.TextPrimary
                : (isHttps
                    ? (AppTheme.IsDark ? Color.FromArgb(120, 220, 160) : Color.FromArgb(22, 128, 60))
                    : (AppTheme.IsDark ? Color.FromArgb(250, 180, 90) : Color.FromArgb(190, 90, 10))),
            TextAlign = ContentAlignment.MiddleLeft
        };
        securityCard.Controls.Add(securityLabel);
        mainPanel.Controls.Add(securityCard, 0, 0);

        // 2. Hostname
        var host = _currentUri?.Host ?? "about:blank";
        var hostLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"当前域名: {host}",
            Font = AppTheme.CreateFont(10F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainPanel.Controls.Add(hostLabel, 0, 1);

        // 3. Stats
        var statsText = $"🛡️ 本页拦截广告/追踪: {_blockedCount} 个\n⚡ 加载耗时: {(_loadMs > 0 ? $"{_loadMs} ms" : "即时响应")}";
        var statsLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = statsText,
            Font = AppTheme.CreateFont(8.8F),
            ForeColor = AppTheme.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainPanel.Controls.Add(statsLabel, 0, 2);

        // 4. Action buttons grid
        var actionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 8, 0, 0)
        };
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        actionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var clearCookiesBtn = CreateActionButton("🍪 清除本站 Cookie", () =>
        {
            _clearCookiesAction?.Invoke();
            Close();
        });
        var hardRefreshBtn = CreateActionButton("🔄 清缓存强刷", () =>
        {
            _hardRefreshAction?.Invoke();
            Close();
        });
        var qrBtn = CreateActionButton("📱 手机扫码打开", () =>
        {
            Close();
            _showQrAction?.Invoke();
        });
        var viewSourceBtn = CreateActionButton("🔍 查看网页源码", () =>
        {
            _viewSourceAction?.Invoke();
            Close();
        });

        actionsPanel.Controls.Add(clearCookiesBtn, 0, 0);
        actionsPanel.Controls.Add(hardRefreshBtn, 1, 0);
        actionsPanel.Controls.Add(qrBtn, 0, 1);
        actionsPanel.Controls.Add(viewSourceBtn, 1, 1);
        mainPanel.Controls.Add(actionsPanel, 0, 3);

        Controls.Add(mainPanel);
    }

    private Button CreateActionButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = AppTheme.CreateFont(8.8F),
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(3),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        btn.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.35F);
        btn.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.AccentPressed, AppTheme.SurfaceAlt, 0.45F);
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
