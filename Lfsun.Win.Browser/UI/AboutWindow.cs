using System.Diagnostics;
using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 现代化「关于与版本更新日志」窗口。
/// 呈现应用信息、运行环境，以及按版本记录的详细更新内容。
/// </summary>
public sealed class AboutWindow : Form
{
    private readonly Panel _headerPanel = new();
    private readonly Label _appTitleLabel = new();
    private readonly Label _versionBadgeLabel = new();
    private readonly Label _sloganLabel = new();
    private readonly Label _envInfoLabel = new();
    private readonly RichTextBox _changelogBox = new();
    private readonly FlowLayoutPanel _actionsPanel = new();
    private readonly Button _openReleaseFolderButton = new();
    private readonly Button _openRepoButton = new();
    private readonly Button _closeButton = new();

    public AboutWindow()
    {
        Text = $"关于 {AppVersionInfo.ProductName}";
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 680;
        Height = 580;
        // 中文注释：低分辨率/高缩放下把窗口收敛到工作区，保证底部操作按钮可见。
        WindowLayout.FitToWorkingArea(this);
        Font = AppTheme.CreateFont(9.2F);
        // 中文注释：与设置窗口一致的高 DPI 自适应，高缩放屏上控件与字体等比例放大。
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        BuildLayout();
        ApplyTheme();
        PopulateContent();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 顶部品牌区 (Fluent 卡片质感)
        _headerPanel.Dock = DockStyle.Fill;
        _headerPanel.AutoSize = true;
        _headerPanel.Padding = new Padding(14, 12, 14, 12);
        _headerPanel.BackColor = AppTheme.SurfaceAlt;
        _headerPanel.Margin = new Padding(0, 0, 0, 8);
        AppTheme.ApplyControlRegion(_headerPanel, 8);
        _headerPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _headerPanel.Width - 1, _headerPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };

        var titleRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4)
        };

        _appTitleLabel.Text = AppVersionInfo.ProductName;
        _appTitleLabel.Font = AppTheme.CreateFont(16F, FontStyle.Bold);
        _appTitleLabel.AutoSize = true;
        _appTitleLabel.Margin = new Padding(0, 0, 10, 0);

        _versionBadgeLabel.Text = $"v{AppVersionInfo.CurrentVersion}";
        _versionBadgeLabel.Font = AppTheme.CreateMonoFont(9.5F, FontStyle.Bold);
        _versionBadgeLabel.AutoSize = true;
        _versionBadgeLabel.Padding = new Padding(8, 3, 8, 3);
        _versionBadgeLabel.Margin = new Padding(0, 3, 0, 0);

        titleRow.Controls.Add(_appTitleLabel);
        titleRow.Controls.Add(_versionBadgeLabel);

        _sloganLabel.Text = AppVersionInfo.Slogan;
        _sloganLabel.Font = AppTheme.CreateFont(9.6F);
        _sloganLabel.AutoSize = true;
        _sloganLabel.Margin = new Padding(0, 2, 0, 6);

        _envInfoLabel.Text = $"环境: .NET {Environment.Version.Major}.{Environment.Version.Minor} · " +
                             $"架构: {(Environment.Is64BitProcess ? "x64" : "x86")} · " +
                             $"系统: {Environment.OSVersion.VersionString}";
        _envInfoLabel.Font = AppTheme.CreateMonoFont(8.5F);
        _envInfoLabel.AutoSize = true;

        var headerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        headerFlow.Controls.Add(titleRow);
        headerFlow.Controls.Add(_sloganLabel);
        headerFlow.Controls.Add(_envInfoLabel);
        _headerPanel.Controls.Add(headerFlow);

        // 标签：版本更新日志
        var changelogTitle = new Label
        {
            Text = "版本更新记录与特性演进：",
            Font = AppTheme.CreateFont(10F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(4, 10, 4, 6)
        };

        // 更新日志内容区
        _changelogBox.Dock = DockStyle.Fill;
        _changelogBox.ReadOnly = true;
        _changelogBox.BorderStyle = BorderStyle.None;
        _changelogBox.Font = AppTheme.CreateFont(9.2F);

        var changelogContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            Padding = new Padding(6),
            Margin = new Padding(0, 2, 0, 10)
        };

        void UpdateChangelogRegion()
        {
            if (changelogContainer.Width > 8 && changelogContainer.Height > 8)
            {
                using var path = AppTheme.BuildRoundedRect(changelogContainer.ClientRectangle, 6);
                changelogContainer.Region = new Region(path);
            }
        }
        changelogContainer.SizeChanged += (_, _) => UpdateChangelogRegion();
        changelogContainer.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, changelogContainer.Width - 1, changelogContainer.Height - 1), 6);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        changelogContainer.Controls.Add(_changelogBox);

        // 底部操作按钮
        _actionsPanel.Dock = DockStyle.Fill;
        _actionsPanel.AutoSize = true;
        _actionsPanel.FlowDirection = FlowDirection.RightToLeft;
        _actionsPanel.Padding = new Padding(0, 8, 0, 0);

        _closeButton.Text = "关闭";
        _closeButton.Click += (_, _) => Close();
        AppTheme.ApplyPrimaryButton(_closeButton, emphasize: true);

        _openReleaseFolderButton.Text = "打开发布目录 (release/)";
        _openReleaseFolderButton.Click += (_, _) => OpenReleaseFolder();
        AppTheme.ApplyPrimaryButton(_openReleaseFolderButton, emphasize: false);

        _openRepoButton.Text = "访问项目仓库";
        _openRepoButton.Click += (_, _) => OpenRepository();
        AppTheme.ApplyPrimaryButton(_openRepoButton, emphasize: false);

        _actionsPanel.Controls.Add(_closeButton);
        _actionsPanel.Controls.Add(_openReleaseFolderButton);
        _actionsPanel.Controls.Add(_openRepoButton);

        root.Controls.Add(_headerPanel, 0, 0);
        root.Controls.Add(changelogTitle, 0, 1);
        root.Controls.Add(changelogContainer, 0, 2);
        root.Controls.Add(_actionsPanel, 0, 3);

        Controls.Add(root);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        ForeColor = AppTheme.TextPrimary;

        _appTitleLabel.ForeColor = AppTheme.TextPrimary;
        _sloganLabel.ForeColor = AppTheme.TextSecondary;
        _envInfoLabel.ForeColor = AppTheme.TextSecondary;

        _versionBadgeLabel.BackColor = AppTheme.AccentMuted;
        _versionBadgeLabel.ForeColor = AppTheme.Accent;
        AppTheme.ApplyControlRegion(_versionBadgeLabel, 10);

        _changelogBox.BackColor = AppTheme.Surface;
        _changelogBox.ForeColor = AppTheme.TextPrimary;

        AppTheme.ApplyControlRegion(_closeButton, 6);
        AppTheme.ApplyControlRegion(_openReleaseFolderButton, 6);
        AppTheme.ApplyControlRegion(_openRepoButton, 6);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.11F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.05F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.15F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glow = Color.FromArgb(AppTheme.IsDark ? 22 : 16, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle(-60, -40, 200, 160));
    }

    private void PopulateContent()
    {
        _changelogBox.Clear();

        foreach (var release in AppVersionInfo.GetRecentReleases(10))
        {
            var isCurrent = string.Equals(release.Version, AppVersionInfo.CurrentVersion, StringComparison.OrdinalIgnoreCase);

            // 版本标题行
            _changelogBox.SelectionFont = AppTheme.CreateFont(10.5F, FontStyle.Bold);
            _changelogBox.SelectionColor = isCurrent ? AppTheme.Accent : AppTheme.TextPrimary;
            _changelogBox.AppendText($"v{release.Version} ({release.ReleaseDate}) - {release.Title}");
            if (isCurrent)
            {
                _changelogBox.SelectionFont = AppTheme.CreateFont(8.5F, FontStyle.Bold);
                _changelogBox.SelectionColor = AppTheme.Success;
                _changelogBox.AppendText("  [当前版本]");
            }
            _changelogBox.AppendText(Environment.NewLine);

            // 亮点内容
            _changelogBox.SelectionFont = AppTheme.CreateFont(9.2F);
            _changelogBox.SelectionColor = AppTheme.TextPrimary;
            foreach (var highlight in release.Highlights)
            {
                _changelogBox.AppendText($"  • {highlight}{Environment.NewLine}");
            }

            _changelogBox.AppendText(Environment.NewLine);
        }

        _changelogBox.Select(0, 0);
    }

    private void OpenReleaseFolder()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "..", "..", "..", "..", "release"),
                Path.Combine(baseDir, "release"),
                Path.Combine(Environment.CurrentDirectory, "release")
            };

            var target = candidates.FirstOrDefault(Directory.Exists);
            if (target is null)
            {
                target = Path.Combine(Environment.CurrentDirectory, "release");
                Directory.CreateDirectory(target);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开发布目录: {ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppVersionInfo.RepositoryUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }
}
