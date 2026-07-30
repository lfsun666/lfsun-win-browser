using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.History;

public sealed class HistoryWindow : Form
{
    private readonly HistoryManager _historyManager;
    private readonly Func<IReadOnlyList<ClosedTabEntry>> _getRecentlyClosed;
    private readonly Func<string, bool, Task> _openUrlAsync;
    private readonly Func<Task<bool>> _reopenLastClosedTabAsync;
    private readonly Action<string> _log;

    private readonly TabControl _tabs = new();
    private readonly DataGridView _historyGrid = new();
    private readonly DataGridView _closedGrid = new();
    private readonly BindingList<HistoryRow> _historyRows = [];
    private readonly BindingList<ClosedTabRow> _closedRows = [];
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _siteFilterComboBox = new();
    private readonly Label _resultLabel = new();
    private readonly Button _openButton = new();
    private readonly Button _openInNewTabButton = new();
    private readonly Button _reopenLastButton = new();
    private readonly Button _deleteSelectedButton = new();
    private readonly Button _exportHistoryButton = new();
    private readonly Button _clearHistoryButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _closeButton = new();
    private readonly ContextMenuStrip _historyContextMenu = new();
    private readonly ToolStripMenuItem _historyCtxOpen = new("打开");
    private readonly ToolStripMenuItem _historyCtxOpenNewTab = new("新标签页打开");
    private readonly ToolStripMenuItem _historyCtxCopyUrl = new("复制链接");
    private readonly ToolStripMenuItem _historyCtxDeleteSelected = new("删除所选历史");
    private readonly ToolStripMenuItem _historyCtxDeleteSiteAll = new("删除该站点全部历史");
    private bool _updatingSiteFilter;
    private TableLayoutPanel? _headerPanel;
    private TableLayoutPanel? _filterBarPanel;
    private Panel? _tabsHostPanel;
    private FlowLayoutPanel? _footerButtonsPanel;

    public HistoryWindow(
        HistoryManager historyManager,
        Func<IReadOnlyList<ClosedTabEntry>> getRecentlyClosed,
        Func<string, bool, Task> openUrlAsync,
        Func<Task<bool>> reopenLastClosedTabAsync,
        Action<string> log)
    {
        _historyManager = historyManager;
        _getRecentlyClosed = getRecentlyClosed;
        _openUrlAsync = openUrlAsync;
        _reopenLastClosedTabAsync = reopenLastClosedTabAsync;
        _log = log;

        Text = "历史记录";
        Width = 1100;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        BuildLayout();
        ApplyTheme();
        WireEvents();
        RefreshData();
    }

    public void RefreshData()
    {
        _historyRows.Clear();
        var historyEntries = _historyManager.Entries.Take(1000).ToArray();
        RefreshSiteFilterItems(historyEntries);
        foreach (var item in ApplyHistoryFilters(historyEntries))
        {
            _historyRows.Add(new HistoryRow
            {
                Id = item.Id,
                Title = item.Title,
                Url = item.Url,
                VisitedAt = item.VisitedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        _closedRows.Clear();
        foreach (var item in _getRecentlyClosed())
        {
            _closedRows.Add(new ClosedTabRow
            {
                Title = item.Title,
                Url = item.Url,
                ClosedAt = item.ClosedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        _resultLabel.Text = $"历史: {_historyRows.Count} 条  最近关闭: {_closedRows.Count} 条";
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
            Text = "历史记录与最近关闭",
            Font = AppTheme.CreateFont(10.6F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "支持按标题、网址、站点筛选，并快速恢复最近关闭的标签页。",
            ForeColor = Color.FromArgb(70, 94, 138)
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
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        filterBar.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _searchTextBox.PlaceholderText = "按标题或网址关键词筛选";
        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.Margin = new Padding(0, 0, 6, 0);
        filterBar.Controls.Add(_searchTextBox, 1, 0);

        filterBar.Controls.Add(new Label { Text = "站点", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _siteFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _siteFilterComboBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_siteFilterComboBox, 3, 0);

        _resultLabel.AutoSize = true;
        _resultLabel.Anchor = AnchorStyles.Right;
        _resultLabel.Margin = new Padding(8, 4, 0, 0);
        filterBar.Controls.Add(_resultLabel, 5, 0);

        _tabs.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(BuildHistoryPage());
        _tabs.TabPages.Add(BuildClosedTabsPage());
        _tabsHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _tabsHostPanel.Controls.Add(_tabs);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(10, 8, 10, 8)
        };
        _footerButtonsPanel = buttons;
        _openButton.Text = "打开";
        _openInNewTabButton.Text = "新标签打开";
        _reopenLastButton.Text = "恢复最近关闭";
        _deleteSelectedButton.Text = "删除所选";
        _exportHistoryButton.Text = "导出历史";
        _clearHistoryButton.Text = "清空历史";
        _refreshButton.Text = "刷新";
        _closeButton.Text = "关闭";
        buttons.Controls.Add(_closeButton);
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_exportHistoryButton);
        buttons.Controls.Add(_clearHistoryButton);
        buttons.Controls.Add(_deleteSelectedButton);
        buttons.Controls.Add(_reopenLastButton);
        buttons.Controls.Add(_openInNewTabButton);
        buttons.Controls.Add(_openButton);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(_tabsHostPanel, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
    }

    private TabPage BuildHistoryPage()
    {
        var page = new TabPage("浏览历史");
        ConfigureGrid(_historyGrid);
        _historyGrid.MultiSelect = true;
        _historyGrid.DataSource = _historyRows;
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HistoryRow.Title),
            HeaderText = "标题",
            Width = 300
        });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HistoryRow.Url),
            HeaderText = "地址",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HistoryRow.VisitedAt),
            HeaderText = "访问时间",
            Width = 170
        });
        page.Controls.Add(_historyGrid);
        return page;
    }

    private TabPage BuildClosedTabsPage()
    {
        var page = new TabPage("最近关闭");
        ConfigureGrid(_closedGrid);
        _closedGrid.DataSource = _closedRows;
        _closedGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ClosedTabRow.Title),
            HeaderText = "标题",
            Width = 300
        });
        _closedGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ClosedTabRow.Url),
            HeaderText = "地址",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _closedGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ClosedTabRow.ClosedAt),
            HeaderText = "关闭时间",
            Width = 170
        });
        page.Controls.Add(_closedGrid);
        return page;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        _tabs.Font = AppTheme.CreateFont(9.2F, FontStyle.Bold);
        _searchTextBox.Font = AppTheme.CreateFont(9.3F);
        _siteFilterComboBox.Font = AppTheme.CreateFont(9.3F);
        _resultLabel.Font = AppTheme.CreateFont(8.9F);
        _resultLabel.ForeColor = AppTheme.TextSecondary;
        StyleControlTree(this);
        AppTheme.ApplyContextMenu(_historyContextMenu);
        ApplyCardVisual(_headerPanel, emphasizeAccent: true);
        ApplyCardVisual(_filterBarPanel, emphasizeAccent: false);
        ApplyCardVisual(_tabsHostPanel, emphasizeAccent: false);
        ApplyCardVisual(_footerButtonsPanel, emphasizeAccent: false);
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
        var glow = Color.FromArgb(AppTheme.IsDark ? 26 : 20, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle((int)(ClientSize.Width * 0.7F), -90, 240, 180));
    }

    private static void StyleControlTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                AppTheme.ApplyPrimaryButton(button);
            }
            else if (control is TextBox textBox)
            {
                AppTheme.ApplyTextInput(textBox);
            }
            else if (control is ComboBox comboBox)
            {
                AppTheme.ApplyComboBox(comboBox);
            }
            else if (control is Label label)
            {
                label.ForeColor = label.Font.Bold ? AppTheme.TextPrimary : AppTheme.TextSecondary;
            }
            else if (control is DataGridView grid)
            {
                AppTheme.ApplyDataGrid(grid);
            }

            StyleControlTree(control);
        }
    }

    private void WireEvents()
    {
        Shown += (_, _) => RefreshData();
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }

            if (eventArgs.Control && eventArgs.KeyCode == Keys.F)
            {
                _searchTextBox.Focus();
                _searchTextBox.SelectAll();
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
            }
        };
        _refreshButton.Click += (_, _) => RefreshData();
        _closeButton.Click += (_, _) => Close();
        _searchTextBox.TextChanged += (_, _) => RefreshData();
        _searchTextBox.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode != Keys.Enter)
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            RefreshData();
            FocusFirstRow();
        };
        _siteFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingSiteFilter)
            {
                return;
            }

            RefreshData();
        };

        _openButton.Click += async (_, _) => await OpenSelectedAsync(inNewTab: false);
        _openInNewTabButton.Click += async (_, _) => await OpenSelectedAsync(inNewTab: true);
        _deleteSelectedButton.Click += (_, _) => DeleteSelectedHistoryRows();
        _reopenLastButton.Click += async (_, _) =>
        {
            var reopened = await _reopenLastClosedTabAsync();
            if (!reopened)
            {
                MessageBox.Show("没有可恢复的标签页。", "历史记录", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _log("已恢复最近关闭标签");
            RefreshData();
        };

        _clearHistoryButton.Click += (_, _) =>
        {
            var confirm = MessageBox.Show(
                "确认清空全部浏览历史？",
                "清空历史",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            _historyManager.Clear();
            RefreshData();
            _log("浏览历史已清空");
        };

        _exportHistoryButton.Click += (_, _) => ExportFilteredHistory();

        _historyGrid.CellDoubleClick += async (_, _) => await OpenSelectedAsync(inNewTab: false);
        _historyGrid.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode != Keys.Delete)
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            DeleteSelectedHistoryRows();
        };
        _historyGrid.MouseDown += HandleHistoryGridMouseDown;
        _closedGrid.CellDoubleClick += async (_, _) => await OpenSelectedAsync(inNewTab: true);

        _historyContextMenu.Items.AddRange(
        [
            _historyCtxOpen,
            _historyCtxOpenNewTab,
            _historyCtxCopyUrl,
            new ToolStripSeparator(),
            _historyCtxDeleteSelected,
            _historyCtxDeleteSiteAll
        ]);
        AppTheme.ApplyContextMenu(_historyContextMenu);
        _historyContextMenu.Opening += (_, _) => PrepareHistoryContextMenu();
        _historyCtxOpen.Click += async (_, _) => await OpenSelectedAsync(inNewTab: false);
        _historyCtxOpenNewTab.Click += async (_, _) => await OpenSelectedAsync(inNewTab: true);
        _historyCtxCopyUrl.Click += (_, _) => CopySelectedHistoryUrl();
        _historyCtxDeleteSelected.Click += (_, _) => DeleteSelectedHistoryRows();
        _historyCtxDeleteSiteAll.Click += (_, _) => DeleteSelectedSiteHistoryRows();
    }

    private void FocusFirstRow()
    {
        var grid = _tabs.SelectedIndex == 0 ? _historyGrid : _closedGrid;
        if (grid.Rows.Count <= 0)
        {
            return;
        }

        grid.ClearSelection();
        grid.Rows[0].Selected = true;
        grid.CurrentCell = grid.Rows[0].Cells[0];
    }

    private void ExportFilteredHistory()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出历史记录",
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = $"history-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var data = _historyRows.Select(item => new
            {
                title = item.Title,
                url = item.Url,
                visitedAt = item.VisitedAt
            }).ToArray();
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            _log("历史记录已导出");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "历史记录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshSiteFilterItems(IReadOnlyList<HistoryEntry> historyEntries)
    {
        var selected = _siteFilterComboBox.SelectedItem as string;
        var sites = historyEntries
            .Select(entry => TryGetHost(entry.Url))
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _updatingSiteFilter = true;
        _siteFilterComboBox.Items.Clear();
        _siteFilterComboBox.Items.Add("全部站点");
        foreach (var site in sites)
        {
            _siteFilterComboBox.Items.Add(site);
        }

        if (!string.IsNullOrWhiteSpace(selected) && _siteFilterComboBox.Items.Contains(selected))
        {
            _siteFilterComboBox.SelectedItem = selected;
        }
        else
        {
            _siteFilterComboBox.SelectedIndex = 0;
        }

        _updatingSiteFilter = false;
    }

    private IEnumerable<HistoryEntry> ApplyHistoryFilters(IEnumerable<HistoryEntry> source)
    {
        var query = _searchTextBox.Text.Trim();
        var selectedSite = _siteFilterComboBox.SelectedItem as string;
        var filterBySite = !string.IsNullOrWhiteSpace(selectedSite) &&
                           !string.Equals(selectedSite, "全部站点", StringComparison.OrdinalIgnoreCase);

        var filtered = source;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Url.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (filterBySite)
        {
            filtered = filtered.Where(item =>
                string.Equals(TryGetHost(item.Url), selectedSite, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private static string TryGetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri is not null
            ? uri.Host
            : string.Empty;
    }

    private async Task OpenSelectedAsync(bool inNewTab)
    {
        var url = _tabs.SelectedIndex == 0
            ? (_historyGrid.SelectedRows.Count > 0 ? (_historyGrid.SelectedRows[0].DataBoundItem as HistoryRow)?.Url : null)
            : (_closedGrid.SelectedRows.Count > 0 ? (_closedGrid.SelectedRows[0].DataBoundItem as ClosedTabRow)?.Url : null);
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("请先选择一条记录。", "历史记录", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await _openUrlAsync(url, inNewTab);
    }

    private void HandleHistoryGridMouseDown(object? _, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _historyGrid.HitTest(eventArgs.X, eventArgs.Y);
        if (hit.RowIndex >= 0 && hit.RowIndex < _historyGrid.Rows.Count)
        {
            var row = _historyGrid.Rows[hit.RowIndex];
            if (!row.Selected)
            {
                _historyGrid.ClearSelection();
                row.Selected = true;
                _historyGrid.CurrentCell = row.Cells[0];
            }
        }

        _historyContextMenu.Show(_historyGrid, eventArgs.Location);
    }

    private void PrepareHistoryContextMenu()
    {
        var selectedRows = GetSelectedHistoryRows();
        var hasSelection = selectedRows.Count > 0;
        var hasSingleSelection = selectedRows.Count == 1;

        _historyCtxOpen.Enabled = hasSingleSelection;
        _historyCtxOpenNewTab.Enabled = hasSelection;
        _historyCtxCopyUrl.Enabled = hasSingleSelection;
        _historyCtxDeleteSelected.Enabled = hasSelection;
        _historyCtxDeleteSiteAll.Enabled = hasSingleSelection;
    }

    private List<HistoryRow> GetSelectedHistoryRows()
    {
        return _historyGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as HistoryRow)
            .Where(item => item is not null)
            .Cast<HistoryRow>()
            .ToList();
    }

    private void DeleteSelectedHistoryRows()
    {
        if (_tabs.SelectedIndex != 0)
        {
            return;
        }

        var selectedRows = GetSelectedHistoryRows();
        if (selectedRows.Count == 0)
        {
            MessageBox.Show("请先选择要删除的历史记录。", "历史记录", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除所选 {selectedRows.Count} 条历史记录？",
            "删除历史记录",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var removed = _historyManager.RemoveEntriesByIds(selectedRows.Select(item => item.Id));
        RefreshData();
        _log($"已删除 {removed} 条历史记录");
    }

    private void DeleteSelectedSiteHistoryRows()
    {
        var selectedRows = GetSelectedHistoryRows();
        if (selectedRows.Count != 1)
        {
            return;
        }

        var host = TryGetHost(selectedRows[0].Url);
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除站点 {host} 的全部历史记录？",
            "删除站点历史",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var removed = _historyManager.RemoveEntriesByHost(host);
        RefreshData();
        _log($"已删除站点 {host} 的 {removed} 条历史");
    }

    private void CopySelectedHistoryUrl()
    {
        var selectedRows = GetSelectedHistoryRows();
        if (selectedRows.Count != 1)
        {
            return;
        }

        try
        {
            Clipboard.SetText(selectedRows[0].Url);
            _log("链接已复制");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "历史记录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void ApplyCardVisual(Control? panel, bool emphasizeAccent)
    {
        if (panel is null)
        {
            return;
        }

        panel.BackColor = AppTheme.Surface;
        panel.Tag = emphasizeAccent ? "history-card-accent" : "history-card";
        panel.Paint -= DrawCardBorder;
        panel.Paint += DrawCardBorder;
        panel.Invalidate();
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

        var emphasize = string.Equals(panel.Tag as string, "history-card-accent", StringComparison.Ordinal);
        var borderColor = emphasize
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.42F)
                : AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.22F))
            : AppTheme.PanelBorder;
        var glowColor = emphasize
            ? Color.FromArgb(AppTheme.IsDark ? 30 : 20, AppTheme.Accent)
            : Color.FromArgb(AppTheme.IsDark ? 16 : 10, AppTheme.AccentMuted);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildCardPath(rect, 10);
        using var glowPen = new Pen(glowColor, 1F);
        using var borderPen = new Pen(borderColor, 1F);
        eventArgs.Graphics.DrawPath(glowPen, path);
        eventArgs.Graphics.DrawPath(borderPen, path);
    }

    private static GraphicsPath BuildCardPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rect.Left, rect.Top, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class HistoryRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string VisitedAt { get; init; } = string.Empty;
    }

    private sealed class ClosedTabRow
    {
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string ClosedAt { get; init; } = string.Empty;
    }
}

