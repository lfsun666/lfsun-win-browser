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
        Font = AppTheme.CreateFont(9.2F);

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

        // 顶部品牌区
        _headerPanel.Dock = DockStyle.Fill;
        _headerPanel.AutoSize = true;
        _headerPanel.Padding = new Padding(8, 6, 8, 12);

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
        _changelogBox.Margin = new Padding(0, 2, 0, 10);

        // 底部操作按钮
        _actionsPanel.Dock = DockStyle.Fill;
        _actionsPanel.AutoSize = true;
        _actionsPanel.FlowDirection = FlowDirection.RightToLeft;
        _actionsPanel.Padding = new Padding(0, 8, 0, 0);

        _closeButton.Text = "关闭";
        _closeButton.Click += (_, _) => Close();
        AppTheme.ApplyPrimaryButton(_closeButton);

        _openReleaseFolderButton.Text = "打开发布目录 (release/)";
        _openReleaseFolderButton.Click += (_, _) => OpenReleaseFolder();
        AppTheme.ApplyPrimaryButton(_openReleaseFolderButton);

        _openRepoButton.Text = "访问项目仓库";
        _openRepoButton.Click += (_, _) => OpenRepository();
        AppTheme.ApplyPrimaryButton(_openRepoButton);

        _actionsPanel.Controls.Add(_closeButton);
        _actionsPanel.Controls.Add(_openReleaseFolderButton);
        _actionsPanel.Controls.Add(_openRepoButton);

        root.Controls.Add(_headerPanel, 0, 0);
        root.Controls.Add(changelogTitle, 0, 1);
        root.Controls.Add(_changelogBox, 0, 2);
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

        _changelogBox.BackColor = AppTheme.Surface;
        _changelogBox.ForeColor = AppTheme.TextPrimary;
    }

    private void PopulateContent()
    {
        _changelogBox.Clear();

        foreach (var release in AppVersionInfo.ReleaseHistory)
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
