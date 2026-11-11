using System.ComponentModel;
using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Sessions;

public enum ImportConflictStrategyOption
{
    Overwrite,
    Rename,
    Skip
}

public sealed class ImportSessionPreviewItem
{
    public string Id { get; init; } = string.Empty;
    public bool Selected { get; set; } = true;
    public string SourceFileName { get; init; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public int TabCount { get; init; }
    public bool HasConflict { get; set; }
    public string Status { get; set; } = "新会话";
}

public sealed class ImportSessionsPreviewDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly ComboBox _strategyComboBox = new();
    private readonly Label _summaryLabel = new();
    private readonly CheckBox _onlyConflictCheckBox = new();
    private readonly Button _selectAllButton = new();
    private readonly Button _selectConflictButton = new();
    private readonly Button _invertButton = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly BindingList<ImportSessionPreviewItem> _rows = [];
    private readonly HashSet<string> _existingNames;
    private TableLayoutPanel? _headerCardPanel;
    private TableLayoutPanel? _toolBarCardPanel;
    private Panel? _gridCardPanel;
    private FlowLayoutPanel? _footerCardPanel;

    private ImportSessionsPreviewDialog(IEnumerable<ImportSessionPreviewItem> items, IEnumerable<string> existingSessionNames)
    {
        _existingNames = existingSessionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Text = "导入会话预览";
        Width = 960;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        foreach (var item in items)
        {
            _rows.Add(new ImportSessionPreviewItem
            {
                Id = item.Id,
                Selected = item.Selected,
                SourceFileName = item.SourceFileName,
                SessionName = item.SessionName,
                TabCount = item.TabCount,
                HasConflict = item.HasConflict,
                Status = item.Status
            });
        }

        RefreshConflictFlags();
        BuildLayout();
        ApplyTheme();
        WireEvents();
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        RefreshSummary();
    }

    public static bool ShowDialog(
        IWin32Window owner,
        IEnumerable<ImportSessionPreviewItem> items,
        IEnumerable<string> existingSessionNames,
        out List<ImportSessionPreviewItem> selectedItems,
        out ImportConflictStrategyOption strategy)
    {
        using var dialog = new ImportSessionsPreviewDialog(items, existingSessionNames);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            selectedItems = [];
            strategy = ImportConflictStrategyOption.Rename;
            return false;
        }

        selectedItems = dialog._rows.Where(row => row.Selected).ToList();
        strategy = dialog.GetSelectedStrategy();
        return true;
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
        _headerCardPanel = header;
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "导入会话预览",
            Font = AppTheme.CreateFont(10.6F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "请勾选要导入的会话，并指定重名冲突处理策略（自动重命名 / 覆盖同名 / 跳过重名）。",
            ForeColor = AppTheme.TextSecondary
        }, 0, 1);

        var toolBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 8, 10, 8)
        };
        _toolBarCardPanel = toolBar;
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        toolBar.Controls.Add(new Label
        {
            Text = "重名策略",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 0);

        _strategyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _strategyComboBox.Items.AddRange(
        [
            "自动重命名",
            "覆盖同名",
            "跳过重名"
        ]);
        _strategyComboBox.SelectedIndex = 0;
        _strategyComboBox.Dock = DockStyle.Fill;
        toolBar.Controls.Add(_strategyComboBox, 1, 0);

        _selectAllButton.Text = "全选";
        _selectConflictButton.Text = "勾选重名";
        _invertButton.Text = "反选";
        toolBar.Controls.Add(_selectAllButton, 3, 0);
        toolBar.Controls.Add(_selectConflictButton, 4, 0);
        toolBar.Controls.Add(_invertButton, 5, 0);
        _onlyConflictCheckBox.Text = "仅看重名";
        _onlyConflictCheckBox.AutoSize = true;
        _onlyConflictCheckBox.Anchor = AnchorStyles.Left;
        _onlyConflictCheckBox.Margin = new Padding(10, 5, 0, 0);
        toolBar.Controls.Add(_onlyConflictCheckBox, 6, 0);

        _summaryLabel.AutoSize = true;
        _summaryLabel.Anchor = AnchorStyles.Right;
        toolBar.Controls.Add(_summaryLabel, 8, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.BorderStyle = BorderStyle.None;
        _grid.DataSource = _rows;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ImportSessionPreviewItem.Selected),
            HeaderText = "导入",
            Width = 58
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ImportSessionPreviewItem.SourceFileName),
            HeaderText = "来源文件",
            Width = 220
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ImportSessionPreviewItem.SessionName),
            HeaderText = "会话名称",
            Width = 260
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ImportSessionPreviewItem.TabCount),
            HeaderText = "标签数",
            Width = 90
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ImportSessionPreviewItem.Status),
            HeaderText = "状态",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _gridCardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8)
        };
        _gridCardPanel.Controls.Add(_grid);

        var actionBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(10, 8, 10, 8)
        };
        _footerCardPanel = actionBar;
        _okButton.Text = "开始导入";
        _cancelButton.Text = "取消";
        actionBar.Controls.Add(_okButton);
        actionBar.Controls.Add(_cancelButton);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(toolBar, 0, 1);
        root.Controls.Add(_gridCardPanel, 0, 2);
        root.Controls.Add(actionBar, 0, 3);
        Controls.Add(root);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        AppTheme.ApplyDataGrid(_grid);
        AppTheme.ApplyComboBox(_strategyComboBox);
        _summaryLabel.ForeColor = AppTheme.TextSecondary;
        StyleControlTree(this);
        ApplyCardVisual(_headerCardPanel, emphasizeAccent: true);
        ApplyCardVisual(_toolBarCardPanel, emphasizeAccent: false);
        ApplyCardVisual(_gridCardPanel, emphasizeAccent: false);
        ApplyCardVisual(_footerCardPanel, emphasizeAccent: false);

        StyleButton(_selectAllButton, 84, emphasize: false);
        StyleButton(_selectConflictButton, 96, emphasize: false);
        StyleButton(_invertButton, 84, emphasize: false);
        StyleButton(_okButton, 110, emphasize: true);
        StyleButton(_cancelButton, 92, emphasize: false);
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
    }

    private void WireEvents()
    {
        _grid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex < 0)
            {
                return;
            }

            RefreshConflictFlags();
            ApplyRowFilter();
            RefreshSummary();
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grid.DataBindingComplete += (_, _) => ApplyRowFilter();

        _selectAllButton.Click += (_, _) =>
        {
            foreach (var row in _rows)
            {
                row.Selected = true;
            }

            _grid.Refresh();
            RefreshSummary();
        };
        _selectConflictButton.Click += (_, _) =>
        {
            foreach (var row in _rows)
            {
                if (row.HasConflict)
                {
                    row.Selected = true;
                }
            }

            _grid.Refresh();
            RefreshSummary();
        };

        _invertButton.Click += (_, _) =>
        {
            foreach (var row in _rows)
            {
                row.Selected = !row.Selected;
            }

            _grid.Refresh();
            RefreshSummary();
        };
        _onlyConflictCheckBox.CheckedChanged += (_, _) =>
        {
            ApplyRowFilter();
            RefreshSummary();
        };

        _okButton.Click += (_, _) =>
        {
            if (_rows.All(row => !row.Selected))
            {
                MessageBox.Show("请至少选择一个会话。", "导入会话预览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_rows.Any(row => row.Selected && string.IsNullOrWhiteSpace(row.SessionName)))
            {
                MessageBox.Show("已选会话中存在空名称，请先修正。", "导入会话预览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
    }

    private void RefreshSummary()
    {
        var selected = _rows.Count(row => row.Selected);
        var conflict = _rows.Count(row => row.Selected && row.HasConflict);
        var visible = _grid.Rows.Cast<DataGridViewRow>().Count(row => row.Visible);
        _summaryLabel.Text = $"可见 {visible}/{_rows.Count}，已选 {selected}，重名 {conflict}";
    }

    private ImportConflictStrategyOption GetSelectedStrategy()
    {
        return _strategyComboBox.SelectedIndex switch
        {
            1 => ImportConflictStrategyOption.Overwrite,
            2 => ImportConflictStrategyOption.Skip,
            _ => ImportConflictStrategyOption.Rename
        };
    }

    private void RefreshConflictFlags()
    {
        var nameGroups = _rows
            .Where(row => !string.IsNullOrWhiteSpace(row.SessionName))
            .GroupBy(row => row.SessionName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in _rows)
        {
            var normalized = row.SessionName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                row.HasConflict = true;
                row.Status = "名称为空";
                continue;
            }

            var hasDup = nameGroups.TryGetValue(normalized, out var count) && count > 1;
            var exists = _existingNames.Contains(normalized);
            row.HasConflict = hasDup || exists;
            row.Status = !row.HasConflict
                ? "新会话"
                : exists
                    ? "重名"
                    : "重复";
        }
    }

    private void ApplyRowFilter()
    {
        var showConflictOnly = _onlyConflictCheckBox.Checked;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is not ImportSessionPreviewItem item)
            {
                continue;
            }

            row.Visible = !showConflictOnly || item.HasConflict;
        }
    }

    private static void ApplyCardVisual(Control? panel, bool emphasizeAccent)
    {
        if (panel is null) return;
        panel.BackColor = AppTheme.Surface;
        panel.Tag = emphasizeAccent ? "import-card-accent" : "import-card-normal";
        panel.SizeChanged += (_, _) => UpdateCardRegion(panel);
        panel.Paint += DrawCardBorder;
        UpdateCardRegion(panel);
    }

    private static void UpdateCardRegion(Control panel)
    {
        if (panel.Width > 8 && panel.Height > 8)
        {
            using var path = AppTheme.BuildRoundedRect(panel.ClientRectangle, 8);
            panel.Region = new Region(path);
        }
    }

    private static void DrawCardBorder(object? sender, PaintEventArgs eventArgs)
    {
        if (sender is not Control panel) return;
        var rect = panel.ClientRectangle;
        rect.Inflate(-1, -1);
        if (rect.Width <= 4 || rect.Height <= 4) return;

        var emphasize = string.Equals(panel.Tag as string, "import-card-accent", StringComparison.Ordinal);
        var borderColor = emphasize
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.42F)
                : AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.22F))
            : AppTheme.PanelBorder;
        var glowColor = emphasize
            ? Color.FromArgb(AppTheme.IsDark ? 30 : 20, AppTheme.Accent)
            : Color.FromArgb(AppTheme.IsDark ? 16 : 10, AppTheme.AccentMuted);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppTheme.BuildRoundedRect(rect, 8);
        using var glowPen = new Pen(glowColor, 1F);
        using var borderPen = new Pen(borderColor, 1F);
        eventArgs.Graphics.DrawPath(glowPen, path);
        eventArgs.Graphics.DrawPath(borderPen, path);
    }

    private static void StyleButton(Button button, int width, bool emphasize)
    {
        AppTheme.ApplyPrimaryButton(button, emphasize);
        button.Width = width;
        button.MinimumSize = new Size(width, 38);
        AppTheme.ApplyControlRegion(button, 6);
    }

    private static void StyleControlTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label label)
            {
                label.ForeColor = AppTheme.TextSecondary;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = AppTheme.TextPrimary;
                checkBox.Font = AppTheme.CreateFont(checkBox.Font.Size, checkBox.Font.Style);
            }

            StyleControlTree(control);
        }
    }
}

