using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Downloads;

public sealed class DownloadsWindow : Form
{
    private readonly DownloadManager _downloadManager;
    private readonly DataGridView _grid = new();
    private readonly BindingList<DownloadItem> _viewItems = [];
    private readonly Dictionary<Guid, DownloadItem> _subscribedItems = [];
    private readonly Dictionary<Guid, PropertyChangedEventHandler> _itemHandlers = [];
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _statusFilterComboBox = new();
    private readonly Label _summaryLabel = new();
    private readonly Button _openFileButton = new();
    private readonly Button _removeSelectedButton = new();
    private readonly Button _clearFinishedButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _closeButton = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _ctxOpenFile = new("打开文件");
    private readonly ToolStripMenuItem _ctxOpenFolder = new("打开所在目录");
    private readonly ToolStripMenuItem _ctxCopySourceUrl = new("复制来源链接");
    private readonly ToolStripMenuItem _ctxRemoveSelected = new("移除所选记录");
    private TableLayoutPanel? _headerPanel;
    private TableLayoutPanel? _filterBarPanel;
    private Panel? _gridHostPanel;
    private FlowLayoutPanel? _footerButtonsPanel;

    public DownloadsWindow(DownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
        Text = "下载管理";
        Width = 980;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        BuildLayout();
        ApplyTheme();
        WireEvents();
        RefreshView();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12, 10, 12, 12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 8, 10, 8)
        };
        _headerPanel = header;
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "下载管理",
            Font = AppTheme.CreateFont(10.6F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "支持按文件名、来源链接搜索，并按下载状态快速筛选。",
            ForeColor = AppTheme.TextSecondary
        }, 0, 1);

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 8, 10, 10)
        };
        _filterBarPanel = filterBar;
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        filterBar.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _searchTextBox.PlaceholderText = "按文件名或来源链接筛选";
        _searchTextBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_searchTextBox, 1, 0);

        filterBar.Controls.Add(new Label { Text = "状态", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterComboBox.Items.AddRange(["全部状态", "下载中", "已完成", "已取消", "失败"]);
        _statusFilterComboBox.SelectedIndex = 0;
        _statusFilterComboBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_statusFilterComboBox, 3, 0);

        _summaryLabel.AutoSize = true;
        _summaryLabel.Anchor = AnchorStyles.Right;
        _summaryLabel.Margin = new Padding(8, 4, 0, 0);
        filterBar.Controls.Add(_summaryLabel, 5, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = true;
        _grid.DataSource = new BindingSource(_viewItems, null);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DownloadItem.FileName),
            HeaderText = "文件名",
            Width = 300
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DownloadItem.StatusText),
            HeaderText = "状态",
            Width = 80
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DownloadItem.ProgressText),
            HeaderText = "进度",
            Width = 160
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DownloadItem.SourceUrl),
            HeaderText = "来源地址",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _gridHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _gridHostPanel.Controls.Add(_grid);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 8, 10, 8)
        };
        _footerButtonsPanel = buttonPanel;

        _closeButton.Text = "关闭";
        _closeButton.AutoSize = true;
        _closeButton.Click += (_, _) => Close();

        _openFileButton.Text = "打开文件";
        _openFileButton.AutoSize = true;
        _openFileButton.Click += (_, _) => OpenSelectedFile();

        _removeSelectedButton.Text = "移除所选";
        _removeSelectedButton.AutoSize = true;
        _removeSelectedButton.Click += (_, _) => RemoveSelectedItems();

        _clearFinishedButton.Text = "清理已完成/失败";
        _clearFinishedButton.AutoSize = true;
        _clearFinishedButton.Click += (_, _) => ClearFinishedItems();

        _openFolderButton.Text = "打开下载目录";
        _openFolderButton.AutoSize = true;
        _openFolderButton.Click += (_, _) => _downloadManager.OpenDownloadDirectory();

        buttonPanel.Controls.Add(_closeButton);
        buttonPanel.Controls.Add(_clearFinishedButton);
        buttonPanel.Controls.Add(_removeSelectedButton);
        buttonPanel.Controls.Add(_openFileButton);
        buttonPanel.Controls.Add(_openFolderButton);

        _grid.MouseDown += HandleGridMouseDown;
        _grid.CellDoubleClick += (_, _) => OpenSelectedFile();
        _grid.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode != Keys.Delete)
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            RemoveSelectedItems();
        };

        _contextMenu.Items.AddRange(
        [
            _ctxOpenFile,
            _ctxOpenFolder,
            _ctxCopySourceUrl,
            new ToolStripSeparator(),
            _ctxRemoveSelected
        ]);
        _contextMenu.Opening += (_, _) => UpdateContextMenuState();
        _ctxOpenFile.Click += (_, _) => OpenSelectedFile();
        _ctxOpenFolder.Click += (_, _) => OpenSelectedItemFolder();
        _ctxCopySourceUrl.Click += (_, _) => CopySelectedSourceUrl();
        _ctxRemoveSelected.Click += (_, _) => RemoveSelectedItems();

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(_gridHostPanel, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);
        Controls.Add(root);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        AppTheme.ApplyDataGrid(_grid);
        AppTheme.ApplyTextInput(_searchTextBox);
        AppTheme.ApplyComboBox(_statusFilterComboBox);
        _summaryLabel.ForeColor = AppTheme.TextSecondary;
        StyleControlTree(this);
        AppTheme.ApplyContextMenu(_contextMenu);
        ApplyCardVisual(_headerPanel, emphasizeAccent: true);
        ApplyCardVisual(_filterBarPanel, emphasizeAccent: false);
        ApplyCardVisual(_gridHostPanel, emphasizeAccent: false);
        ApplyCardVisual(_footerButtonsPanel, emphasizeAccent: false);

        StyleButton(_openFolderButton);
        StyleButton(_openFileButton);
        StyleButton(_removeSelectedButton);
        _removeSelectedButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        StyleButton(_clearFinishedButton);
        StyleButton(_closeButton, emphasize: true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.11F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.06F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.15F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glow = Color.FromArgb(AppTheme.IsDark ? 24 : 18, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle(-100, (int)(ClientSize.Height * 0.5F), 220, 180));
    }

    private void WireEvents()
    {
        _searchTextBox.TextChanged += (_, _) => RefreshView();
        _statusFilterComboBox.SelectedIndexChanged += (_, _) => RefreshView();
        _downloadManager.Items.ListChanged += (_, _) =>
        {
            SyncItemSubscriptions();
            RefreshView();
        };
        SyncItemSubscriptions();
    }

    private void SyncItemSubscriptions()
    {
        var ids = _downloadManager.Items.Select(item => item.Id).ToHashSet();
        foreach (var removedId in _itemHandlers.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            if (_subscribedItems.TryGetValue(removedId, out var removedItem))
            {
                removedItem.PropertyChanged -= _itemHandlers[removedId];
            }

            _subscribedItems.Remove(removedId);
            _itemHandlers.Remove(removedId);
        }

        foreach (var item in _downloadManager.Items)
        {
            if (_itemHandlers.ContainsKey(item.Id))
            {
                continue;
            }

            PropertyChangedEventHandler handler = (_, eventArgs) =>
            {
                if (eventArgs.PropertyName is nameof(DownloadItem.StatusText) or nameof(DownloadItem.ProgressText) or nameof(DownloadItem.BytesReceived))
                {
                    RefreshView();
                }
            };
            item.PropertyChanged += handler;
            _subscribedItems[item.Id] = item;
            _itemHandlers[item.Id] = handler;
        }
    }

    private void RefreshView()
    {
        var query = _searchTextBox.Text.Trim();
        var selectedStatus = _statusFilterComboBox.SelectedItem as string;
        var filtered = _downloadManager.Items
            .Where(item =>
                string.IsNullOrWhiteSpace(query) ||
                item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.SourceUrl.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(item =>
                string.IsNullOrWhiteSpace(selectedStatus) ||
                string.Equals(selectedStatus, "全部状态", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.StatusText, selectedStatus, StringComparison.Ordinal))
            .ToArray();

        _viewItems.RaiseListChangedEvents = false;
        _viewItems.Clear();
        foreach (var item in filtered)
        {
            _viewItems.Add(item);
        }
        _viewItems.RaiseListChangedEvents = true;
        _viewItems.ResetBindings();

        var total = _downloadManager.Items.Count;
        var downloading = _downloadManager.Items.Count(item => string.Equals(item.StatusText, "下载中", StringComparison.Ordinal));
        var completed = _downloadManager.Items.Count(item => string.Equals(item.StatusText, "已完成", StringComparison.Ordinal));
        _summaryLabel.Text = $"显示 {_viewItems.Count} / {total}  下载中 {downloading}  已完成 {completed}";
    }

    private void HandleGridMouseDown(object? _, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _grid.HitTest(eventArgs.X, eventArgs.Y);
        if (hit.RowIndex >= 0 && hit.RowIndex < _grid.Rows.Count)
        {
            var row = _grid.Rows[hit.RowIndex];
            if (!row.Selected)
            {
                _grid.ClearSelection();
                row.Selected = true;
                _grid.CurrentCell = row.Cells[0];
            }
        }

        _contextMenu.Show(_grid, eventArgs.Location);
    }

    private IReadOnlyList<DownloadItem> GetSelectedItems()
    {
        return _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as DownloadItem)
            .Where(item => item is not null)
            .Cast<DownloadItem>()
            .ToArray();
    }

    private void UpdateContextMenuState()
    {
        var selected = GetSelectedItems();
        var hasSelection = selected.Count > 0;
        var hasSingle = selected.Count == 1;
        _ctxOpenFile.Enabled = hasSingle;
        _ctxOpenFolder.Enabled = hasSingle;
        _ctxCopySourceUrl.Enabled = hasSingle;
        _ctxRemoveSelected.Enabled = hasSelection;
    }

    private void OpenSelectedFile()
    {
        var selected = GetSelectedItems();
        if (selected.Count != 1)
        {
            return;
        }

        var item = selected[0];
        if (!File.Exists(item.FilePath))
        {
            MessageBox.Show("文件不存在，可能尚未下载完成。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = item.FilePath,
            UseShellExecute = true
        });
    }

    private void OpenSelectedItemFolder()
    {
        var selected = GetSelectedItems();
        if (selected.Count != 1)
        {
            return;
        }

        var item = selected[0];
        var directory = Path.GetDirectoryName(item.FilePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            MessageBox.Show("目录不存在。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private void CopySelectedSourceUrl()
    {
        var selected = GetSelectedItems();
        if (selected.Count != 1)
        {
            return;
        }

        try
        {
            Clipboard.SetText(selected[0].SourceUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RemoveSelectedItems()
    {
        var selected = GetSelectedItems();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先选择要移除的下载记录。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var downloadingCount = selected.Count(item => string.Equals(item.StatusText, "下载中", StringComparison.Ordinal));
        if (downloadingCount > 0)
        {
            MessageBox.Show("下载中的记录暂不支持移除，请等待完成后再清理。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认移除所选 {selected.Count} 条下载记录？",
            "下载管理",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _downloadManager.RemoveByIds(selected.Select(item => item.Id));
    }

    private void ClearFinishedItems()
    {
        var removed = _downloadManager.RemoveCompletedAndFailed();
        if (removed == 0)
        {
            MessageBox.Show("没有可清理的已完成或失败记录。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show($"已清理 {removed} 条记录。", "下载管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void ApplyCardVisual(Control? panel, bool emphasizeAccent)
    {
        if (panel is null)
        {
            return;
        }

        panel.BackColor = AppTheme.Surface;
        panel.Tag = emphasizeAccent ? "downloads-card-accent" : "downloads-card";
        panel.Paint -= DrawCardBorder;
        panel.Paint += DrawCardBorder;
        panel.SizeChanged -= HandleCardSizeChanged;
        panel.SizeChanged += HandleCardSizeChanged;
        UpdateCardRegion(panel);
        panel.Invalidate();
    }

    private static void HandleCardSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Control panel)
        {
            UpdateCardRegion(panel);
        }
    }

    private static void UpdateCardRegion(Control panel)
    {
        if (panel.Width > 8 && panel.Height > 8)
        {
            using var path = AppTheme.BuildRoundedRect(panel.ClientRectangle, 10);
            panel.Region = new Region(path);
        }
    }

    private static void DrawCardBorder(object? sender, PaintEventArgs eventArgs)
    {
        if (sender is not Control panel)
        {
            return;
        }

        var rect = panel.ClientRectangle;
        rect.Inflate(-1, -1);
        if (rect.Width <= 4 || rect.Height <= 4)
        {
            return;
        }

        var emphasize = string.Equals(panel.Tag as string, "downloads-card-accent", StringComparison.Ordinal);
        var borderColor = emphasize
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.42F)
                : AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.22F))
            : AppTheme.PanelBorder;
        var glowColor = emphasize
            ? Color.FromArgb(AppTheme.IsDark ? 30 : 20, AppTheme.Accent)
            : Color.FromArgb(AppTheme.IsDark ? 16 : 10, AppTheme.AccentMuted);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppTheme.BuildRoundedRect(rect, 10);
        using var glowPen = new Pen(glowColor, 1F);
        using var borderPen = new Pen(borderColor, 1F);
        eventArgs.Graphics.DrawPath(glowPen, path);
        eventArgs.Graphics.DrawPath(borderPen, path);
    }

    private static void StyleButton(Button button, bool emphasize = false)
    {
        AppTheme.ApplyPrimaryButton(button, emphasize);
    }

    private static void StyleControlTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label label)
            {
                label.ForeColor = label.Font.Bold ? AppTheme.TextPrimary : AppTheme.TextSecondary;
            }

            StyleControlTree(control);
        }
    }
}

