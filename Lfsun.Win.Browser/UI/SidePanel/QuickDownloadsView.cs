using System.Diagnostics;
using Lfsun.Win.Browser.Downloads;

namespace Lfsun.Win.Browser.UI.SidePanel;

/// <summary>
/// 中文注释：侧边栏快速下载视图。
/// 实时展示下载进度、状态、一键打开文件与打开下载目录。
/// </summary>
public sealed class QuickDownloadsView : UserControl
{
    private readonly DownloadManager _downloadManager;
    private readonly Action<string> _toastCallback;
    private readonly Action _openDownloadsWindowCallback;

    private readonly ListBox _downloadsListBox = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _openFullButton = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _ctxOpenFile = new("打开文件");
    private readonly ToolStripMenuItem _ctxOpenFolder = new("在文件夹中显示");
    private readonly ToolStripMenuItem _ctxClear = new("清空已完成记录");

    public QuickDownloadsView(
        DownloadManager downloadManager,
        Action<string> toastCallback,
        Action openDownloadsWindowCallback)
    {
        _downloadManager = downloadManager;
        _toastCallback = toastCallback;
        _openDownloadsWindowCallback = openDownloadsWindowCallback;

        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        BuildContextMenu();
        ApplyTheme();
        ReloadDownloads();

        _downloadManager.Items.ListChanged += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(ReloadDownloads);
            }
        };
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 8, 8, 8),
            BackColor = AppTheme.Surface
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _downloadsListBox.Dock = DockStyle.Fill;
        _downloadsListBox.BorderStyle = BorderStyle.None;
        _downloadsListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _downloadsListBox.ItemHeight = 48;
        _downloadsListBox.DrawItem += OnDrawDownloadItem;
        _downloadsListBox.DoubleClick += (_, _) => OpenSelectedDownload();
        mainLayout.Controls.Add(_downloadsListBox, 0, 0);

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };

        _openFolderButton.Text = "📂 打开下载目录";
        _openFolderButton.Size = new Size(130, 30);
        _openFolderButton.Font = AppTheme.CreateFont(8.5F);
        _openFolderButton.FlatStyle = FlatStyle.Flat;
        _openFolderButton.BackColor = AppTheme.SurfaceAlt;
        _openFolderButton.ForeColor = AppTheme.TextPrimary;
        _openFolderButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _openFolderButton.Click += (_, _) => _downloadManager.OpenDownloadDirectory();

        _openFullButton.Text = "下载中心 ↗";
        _openFullButton.Size = new Size(110, 30);
        _openFullButton.Font = AppTheme.CreateFont(8.5F);
        _openFullButton.FlatStyle = FlatStyle.Flat;
        _openFullButton.BackColor = AppTheme.SurfaceAlt;
        _openFullButton.ForeColor = AppTheme.TextSecondary;
        _openFullButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _openFullButton.Click += (_, _) => _openDownloadsWindowCallback();

        buttonBar.Controls.Add(_openFolderButton);
        buttonBar.Controls.Add(_openFullButton);
        mainLayout.Controls.Add(buttonBar, 0, 1);

        Controls.Add(mainLayout);
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.AddRange(
        [
            _ctxOpenFile,
            _ctxOpenFolder,
            new ToolStripSeparator(),
            _ctxClear
        ]);

        _ctxOpenFile.Click += (_, _) => OpenSelectedDownload();
        _ctxOpenFolder.Click += (_, _) =>
        {
            if (_downloadsListBox.SelectedItem is DownloadItem item && File.Exists(item.FilePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                catch (Exception ex)
                {
                    _toastCallback($"定位文件失败: {ex.Message}");
                }
            }
        };

        _ctxClear.Click += (_, _) =>
        {
            _downloadManager.ClearAllFinished();
            ReloadDownloads();
            _toastCallback("已清空已完成下载记录");
        };

        _downloadsListBox.ContextMenuStrip = _contextMenu;
    }

    private void OpenSelectedDownload()
    {
        if (_downloadsListBox.SelectedItem is DownloadItem item && File.Exists(item.FilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _toastCallback($"无法打开文件: {ex.Message}");
            }
        }
    }

    public void ReloadDownloads()
    {
        _downloadsListBox.BeginUpdate();
        _downloadsListBox.Items.Clear();

        foreach (var item in _downloadManager.Items)
        {
            _downloadsListBox.Items.Add(item);
        }

        _downloadsListBox.EndUpdate();
    }

    private void OnDrawDownloadItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= _downloadsListBox.Items.Count)
        {
            return;
        }

        var item = (DownloadItem)_downloadsListBox.Items[eventArgs.Index];
        var isSelected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = eventArgs.Bounds;

        using var bgBrush = new SolidBrush(isSelected ? (AppTheme.IsDark ? Color.FromArgb(44, 56, 78) : Color.FromArgb(220, 235, 252)) : AppTheme.Surface);
        eventArgs.Graphics.FillRectangle(bgBrush, bounds);

        var fileName = item.FileName;
        var progressText = item.ProgressText;
        var statusText = item.StatusText;

        var titleRect = new Rectangle(bounds.Left + 8, bounds.Top + 5, bounds.Width - 90, 18);
        var subRect = new Rectangle(bounds.Left + 8, bounds.Top + 24, bounds.Width - 90, 16);
        var statusRect = new Rectangle(bounds.Right - 84, bounds.Top + 14, 76, 20);

        using var titleFont = AppTheme.CreateFont(8.8F, FontStyle.Bold);
        using var subFont = AppTheme.CreateFont(8.0F);
        using var textBrush = new SolidBrush(AppTheme.TextPrimary);
        using var secondaryBrush = new SolidBrush(AppTheme.TextSecondary);

        var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        var rightFormat = new StringFormat { Alignment = StringAlignment.Far };

        eventArgs.Graphics.DrawString($"📥 {fileName}", titleFont, textBrush, titleRect, format);
        eventArgs.Graphics.DrawString(progressText, subFont, secondaryBrush, subRect, format);

        var statusColor = statusText switch
        {
            DownloadStatusText.Completed => Color.FromArgb(40, 167, 69),
            DownloadStatusText.InProgress => AppTheme.Accent,
            DownloadStatusText.Failed => Color.FromArgb(220, 53, 69),
            _ => AppTheme.TextSecondary
        };
        using var statusBrush = new SolidBrush(statusColor);
        eventArgs.Graphics.DrawString(statusText, titleFont, statusBrush, statusRect, rightFormat);

        using var linePen = new Pen(AppTheme.PanelBorder, 1);
        eventArgs.Graphics.DrawLine(linePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _downloadsListBox.BackColor = AppTheme.Surface;
        _downloadsListBox.ForeColor = AppTheme.TextPrimary;
        _openFolderButton.BackColor = AppTheme.SurfaceAlt;
        _openFolderButton.ForeColor = AppTheme.TextPrimary;
        _openFolderButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        _openFullButton.BackColor = AppTheme.SurfaceAlt;
        _openFullButton.ForeColor = AppTheme.TextSecondary;
        _openFullButton.FlatAppearance.BorderColor = AppTheme.PanelBorder;
        AppTheme.ApplyContextMenu(_contextMenu);
    }
}
