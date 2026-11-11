using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;
using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Sessions;

public sealed class SessionsWindow : Form
{
    private const string WorkspaceSessionPrefix = "工作区-";
    private readonly NamedSessionStore _namedSessionStore;
    private readonly Func<string, Task> _restoreNamedSessionAsync;
    private readonly Action<string> _openNamedSessionInSharedWindow;
    private readonly Action<string> _openNamedSessionInIsolatedWindow;
    private readonly Action<string> _openNamedSessionInPrivateWindow;
    private readonly Action<string> _log;
    private readonly DataGridView _grid = new();
    private readonly BindingList<NamedSessionRow> _rows = [];
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _sortComboBox = new();
    private readonly ComboBox _statusFilterComboBox = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _selectionSummaryLabel = new();
    private readonly Label _statusOverviewLabel = new();
    private readonly Label _statusFilterLabel = new();
    private readonly Label _titleLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly Label _previewTitleLabel = new();
    private readonly Label _workspaceStatsLabel = new();
    private readonly Label _shortcutHintLabel = new();
    private readonly FlowLayoutPanel _kpiPanel = new();
    private readonly Label _kpiWorkspaceCountLabel = new();
    private readonly Label _kpiTabCountLabel = new();
    private readonly Label _kpiDuplicateLabel = new();
    private readonly Label _kpiAverageTabsLabel = new();
    private readonly Label _kpiLatestSavedLabel = new();
    private readonly Button _kpiDedupeButton = new();
    private readonly CheckBox _workspaceOnlyCheckBox = new();
    private readonly FlowLayoutPanel _quickFilterPanel = new();
    private readonly Button _quickFilterAllButton = new();
    private readonly Button _quickFilterFocusButton = new();
    private readonly Button _quickFilterHeavyButton = new();
    private readonly Button _quickFilterRecentButton = new();
    private readonly Button _quickFilterNormalButton = new();
    private readonly Button _quickFilterDuplicateButton = new();
    private readonly Button _selectVisibleButton = new();
    private readonly Button _openFirstVisibleButton = new();
    private readonly Button _resetFiltersButton = new();
    private readonly FlowLayoutPanel _statusLegendPanel = new();
    private readonly Label _legendFocusLabel = new();
    private readonly Label _legendHeavyLabel = new();
    private readonly Label _legendRecentLabel = new();
    private readonly Label _legendNormalLabel = new();
    private readonly Label _legendDuplicateLabel = new();
    private readonly FlowLayoutPanel _previewActionsPanel = new();
    private readonly ListBox _tabListBox = new();
    private readonly Button _restoreButton = new();
    private readonly Button _openSharedButton = new();
    private readonly Button _openIsolatedButton = new();
    private readonly Button _openPrivateButton = new();
    private readonly Button _renameButton = new();
    private readonly Button _dedupeSelectedButton = new();
    private readonly Button _dedupeAllButton = new();
    private readonly Button _previewOpenButton = new();
    private readonly Button _previewSharedButton = new();
    private readonly Button _previewDedupeButton = new();
    private readonly Button _previewDeleteButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _exportButton = new();
    private readonly Button _importButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _closeButton = new();
    private readonly Panel _footerHintPanel = new();
    private readonly Label _footerHintLabel = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _ctxRestore = new("恢复到当前窗口");
    private readonly ToolStripMenuItem _ctxOpenShared = new("在新共享窗口打开");
    private readonly ToolStripMenuItem _ctxOpenIsolated = new("在新独立窗口打开");
    private readonly ToolStripMenuItem _ctxOpenPrivate = new("在新无痕窗口打开");
    private readonly ToolStripMenuItem _ctxRename = new("重命名");
    private readonly ToolStripMenuItem _ctxDedupeSelected = new("所选工作区去重");
    private readonly ToolStripMenuItem _ctxDedupeAll = new("全部工作区去重");
    private readonly ToolStripMenuItem _ctxDelete = new("删除所选");
    private readonly ToolStripMenuItem _ctxExport = new("导出所选...");
    private readonly ToolStripMenuItem _ctxImport = new("导入会话...");
    private readonly ToolTip _uiHintToolTip = new()
    {
        ShowAlways = true,
        AutoPopDelay = 7600,
        InitialDelay = 280,
        ReshowDelay = 120
    };
    private List<NamedSessionDescriptor> _allSessions = [];
    private bool _workspaceOnlyMode;
    private TableLayoutPanel? _rootLayout;
    private TableLayoutPanel? _headerPanel;
    private TableLayoutPanel? _filterBarPanel;
    private SplitContainer? _contentSplitContainer;
    private bool _contentSplitInitialized;
    private TableLayoutPanel? _previewPanel;
    private FlowLayoutPanel? _footerButtonsPanel;

    private enum PreviewListItemKind
    {
        Header,
        Summary,
        ActiveTab,
        NormalTab,
        Url,
        Hint
    }

    public SessionsWindow(
        NamedSessionStore namedSessionStore,
        Func<string, Task> restoreNamedSessionAsync,
        Action<string> openNamedSessionInSharedWindow,
        Action<string> openNamedSessionInIsolatedWindow,
        Action<string> openNamedSessionInPrivateWindow,
        Action<string> log,
        bool workspaceOnlyMode = false)
    {
        _namedSessionStore = namedSessionStore;
        _restoreNamedSessionAsync = restoreNamedSessionAsync;
        _openNamedSessionInSharedWindow = openNamedSessionInSharedWindow;
        _openNamedSessionInIsolatedWindow = openNamedSessionInIsolatedWindow;
        _openNamedSessionInPrivateWindow = openNamedSessionInPrivateWindow;
        _log = log;
        _workspaceOnlyMode = workspaceOnlyMode;

        Text = "命名会话管理";
        Width = 1040;
        Height = 620;
        MinimumSize = new Size(920, 560);
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        KeyPreview = true;

        BuildLayout();
        UpdateModeTexts();
        ApplyTheme();
        WireEvents();
        RefreshData();
    }

    public void RefreshData()
    {
        var sourceSessions = _namedSessionStore.ListSessions();
        _allSessions = _workspaceOnlyMode
            ? sourceSessions
                .Where(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : sourceSessions.ToList();

        var query = _searchTextBox.Text.Trim();
        var visibleSessions = string.IsNullOrWhiteSpace(query)
            ? _allSessions
            : _allSessions
                .Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    GetSessionDisplayName(item.Name).Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        var visualThreshold = BuildSessionVisualThreshold(_allSessions);
        if (_workspaceOnlyMode && _statusFilterComboBox.SelectedIndex > 0)
        {
            var duplicateRiskCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            visibleSessions = visibleSessions
                .Where(item => MatchesStatusFilter(
                    item,
                    visualThreshold,
                    _statusFilterComboBox.SelectedIndex,
                    duplicateRiskCache))
                .ToList();
        }

        visibleSessions = SortVisibleSessions(visibleSessions).ToList();

        _rows.Clear();
        var displayRank = 1;
        foreach (var session in visibleSessions)
        {
            var visualState = EvaluateSessionVisualState(session, visualThreshold);
            _rows.Add(new NamedSessionRow
            {
                Rank = displayRank++,
                Name = session.Name,
                DisplayName = GetSessionDisplayName(session.Name),
                TabCount = session.TabCount,
                SavedAt = session.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Priority = visualState.DisplayText
            });
        }

        var summaryText = _workspaceOnlyMode
            ? $"工作区: {_rows.Count} / {_allSessions.Count}"
            : $"命名会话: {_rows.Count} / {_allSessions.Count}";
        if (_workspaceOnlyMode && _statusFilterComboBox.SelectedIndex > 0)
        {
            summaryText = $"{summaryText} | 筛选: {_statusFilterComboBox.SelectedItem}";
        }

        _summaryLabel.Text = summaryText;
        UpdateStatusOverviewLabel(visualThreshold);
        UpdateWorkspaceStatsLabel();
        UpdateKpiOverview();
        ApplyGridPriorityVisuals();
        if (_rows.Count > 0)
        {
            _grid.ClearSelection();
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                if (_grid.Columns.Count > 0 && _grid.Rows[0].Cells.Count > 0)
                {
                    _grid.CurrentCell = _grid.Rows[0].Cells[0];
                }
            }
        }

        RefreshSelectedSessionDetails();
        UpdateContextMenuState();
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
        _rootLayout = root;
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
        _titleLabel.AutoSize = true;
        _titleLabel.Font = AppTheme.CreateFont(10.6F, FontStyle.Bold);
        _titleLabel.Margin = new Padding(0, 0, 0, 4);
        header.Controls.Add(_titleLabel, 0, 0);

        _subtitleLabel.AutoSize = true;
        _subtitleLabel.ForeColor = AppTheme.TextSecondary;
        header.Controls.Add(_subtitleLabel, 0, 1);
        _kpiPanel.AutoSize = true;
        _kpiPanel.FlowDirection = FlowDirection.LeftToRight;
        _kpiPanel.WrapContents = true;
        _kpiPanel.Padding = new Padding(0, 1, 0, 1);
        _kpiPanel.Margin = new Padding(0, 6, 0, 0);
        _kpiWorkspaceCountLabel.AutoSize = true;
        _kpiWorkspaceCountLabel.Text = "工作区 0";
        _kpiTabCountLabel.AutoSize = true;
        _kpiTabCountLabel.Text = "标签总数 0";
        _kpiDuplicateLabel.AutoSize = true;
        _kpiDuplicateLabel.Text = "重复风险 0";
        _kpiAverageTabsLabel.AutoSize = true;
        _kpiAverageTabsLabel.Text = "平均标签 0.0";
        _kpiLatestSavedLabel.AutoSize = true;
        _kpiLatestSavedLabel.Text = "最近更新 --";
        _kpiDedupeButton.Text = "一键去重";
        _kpiPanel.Controls.Add(_kpiWorkspaceCountLabel);
        _kpiPanel.Controls.Add(_kpiTabCountLabel);
        _kpiPanel.Controls.Add(_kpiDuplicateLabel);
        _kpiPanel.Controls.Add(_kpiAverageTabsLabel);
        _kpiPanel.Controls.Add(_kpiLatestSavedLabel);
        _kpiPanel.Controls.Add(_kpiDedupeButton);
        header.Controls.Add(_kpiPanel, 0, 2);
        _statusOverviewLabel.AutoSize = true;
        _statusOverviewLabel.Margin = new Padding(0, 2, 0, 0);
        header.Controls.Add(_statusOverviewLabel, 0, 3);
        _quickFilterPanel.AutoSize = true;
        _quickFilterPanel.FlowDirection = FlowDirection.LeftToRight;
        _quickFilterPanel.WrapContents = true;
        _quickFilterPanel.Padding = new Padding(0, 1, 0, 1);
        _quickFilterPanel.Margin = new Padding(0, 6, 0, 0);
        _quickFilterAllButton.Text = "全部";
        _quickFilterFocusButton.Text = "重点";
        _quickFilterHeavyButton.Text = "高负载";
        _quickFilterRecentButton.Text = "最近更新";
        _quickFilterNormalButton.Text = "常规";
        _quickFilterDuplicateButton.Text = "重复风险";
        _selectVisibleButton.Text = "全选可见";
        _openFirstVisibleButton.Text = "打开首个";
        _resetFiltersButton.Text = "清空筛选";
        _quickFilterAllButton.FlatStyle = FlatStyle.Flat;
        _quickFilterFocusButton.FlatStyle = FlatStyle.Flat;
        _quickFilterHeavyButton.FlatStyle = FlatStyle.Flat;
        _quickFilterRecentButton.FlatStyle = FlatStyle.Flat;
        _quickFilterNormalButton.FlatStyle = FlatStyle.Flat;
        _quickFilterDuplicateButton.FlatStyle = FlatStyle.Flat;
        _selectVisibleButton.FlatStyle = FlatStyle.Flat;
        _openFirstVisibleButton.FlatStyle = FlatStyle.Flat;
        _resetFiltersButton.FlatStyle = FlatStyle.Flat;
        _quickFilterAllButton.Cursor = Cursors.Hand;
        _quickFilterFocusButton.Cursor = Cursors.Hand;
        _quickFilterHeavyButton.Cursor = Cursors.Hand;
        _quickFilterRecentButton.Cursor = Cursors.Hand;
        _quickFilterNormalButton.Cursor = Cursors.Hand;
        _quickFilterDuplicateButton.Cursor = Cursors.Hand;
        _selectVisibleButton.Cursor = Cursors.Hand;
        _openFirstVisibleButton.Cursor = Cursors.Hand;
        _resetFiltersButton.Cursor = Cursors.Hand;
        _quickFilterPanel.Controls.Add(_quickFilterAllButton);
        _quickFilterPanel.Controls.Add(_quickFilterFocusButton);
        _quickFilterPanel.Controls.Add(_quickFilterHeavyButton);
        _quickFilterPanel.Controls.Add(_quickFilterRecentButton);
        _quickFilterPanel.Controls.Add(_quickFilterNormalButton);
        _quickFilterPanel.Controls.Add(_quickFilterDuplicateButton);
        _quickFilterPanel.Controls.Add(_selectVisibleButton);
        _quickFilterPanel.Controls.Add(_openFirstVisibleButton);
        _quickFilterPanel.Controls.Add(_resetFiltersButton);
        header.Controls.Add(_quickFilterPanel, 0, 4);
        _statusLegendPanel.AutoSize = true;
        _statusLegendPanel.FlowDirection = FlowDirection.LeftToRight;
        _statusLegendPanel.WrapContents = true;
        _statusLegendPanel.Padding = new Padding(0, 0, 0, 0);
        _statusLegendPanel.Margin = new Padding(0, 3, 0, 0);
        _legendFocusLabel.Text = "重点";
        _legendHeavyLabel.Text = "高负载";
        _legendRecentLabel.Text = "最近";
        _legendNormalLabel.Text = "常规";
        _legendDuplicateLabel.Text = "重复风险";
        _statusLegendPanel.Controls.Add(_legendFocusLabel);
        _statusLegendPanel.Controls.Add(_legendHeavyLabel);
        _statusLegendPanel.Controls.Add(_legendRecentLabel);
        _statusLegendPanel.Controls.Add(_legendNormalLabel);
        _statusLegendPanel.Controls.Add(_legendDuplicateLabel);
        header.Controls.Add(_statusLegendPanel, 0, 5);
        _shortcutHintLabel.AutoSize = true;
        _shortcutHintLabel.Margin = new Padding(0, 4, 0, 0);
        header.Controls.Add(_shortcutHintLabel, 0, 6);

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(10, 8, 10, 8)
        };
        _filterBarPanel = filterBar;
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        filterBar.Controls.Add(new Label { Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _searchTextBox.PlaceholderText = "按会话名称筛选";
        _searchTextBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_searchTextBox, 1, 0);
        filterBar.Controls.Add(new Label { Text = "排序", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 0, 3) }, 2, 0);
        _sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortComboBox.Items.AddRange(
        [
            "保存时间（新->旧）",
            "保存时间（旧->新）",
            "名称（A->Z）",
            "名称（Z->A）",
            "标签数（多->少）",
            "标签数（少->多）",
            "状态优先（高负载->最近）"
        ]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_sortComboBox, 3, 0);
        _statusFilterLabel.Text = "状态";
        _statusFilterLabel.AutoSize = true;
        _statusFilterLabel.Anchor = AnchorStyles.Left;
        _statusFilterLabel.Margin = new Padding(10, 3, 0, 3);
        filterBar.Controls.Add(_statusFilterLabel, 4, 0);
        _statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterComboBox.Items.AddRange(
        [
            "全部状态",
            "高负载+最近",
            "高负载(含最近)",
            "最近更新(含高负载+最近)",
            "常规",
            "重复风险"
        ]);
        _statusFilterComboBox.SelectedIndex = 0;
        _statusFilterComboBox.Dock = DockStyle.Fill;
        filterBar.Controls.Add(_statusFilterComboBox, 5, 0);
        _summaryLabel.AutoSize = true;
        _summaryLabel.Margin = new Padding(10, 4, 0, 0);
        filterBar.Controls.Add(_summaryLabel, 6, 0);
        _selectionSummaryLabel.AutoSize = true;
        _selectionSummaryLabel.Margin = new Padding(10, 4, 0, 0);
        filterBar.Controls.Add(_selectionSummaryLabel, 7, 0);
        _workspaceOnlyCheckBox.Text = "仅工作区";
        _workspaceOnlyCheckBox.AutoSize = true;
        _workspaceOnlyCheckBox.Margin = new Padding(12, 4, 0, 0);
        _workspaceOnlyCheckBox.Checked = _workspaceOnlyMode;
        filterBar.Controls.Add(_workspaceOnlyCheckBox, 8, 0);

        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2
        };
        _contentSplitContainer = content;
        // 中文注释：SplitterDistance 与两侧最小宽度必须等控件拿到真实宽度后再设置；
        // 高 DPI 或窗口偏小时直接写死 680/520/260 会触发“SplitterDistance 必须在…”异常。
        content.SizeChanged += (_, _) => ApplyContentSplitConstraints();

        ConfigureGrid();
        content.Panel1.Controls.Add(_grid);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10, 10, 10, 10)
        };
        _previewPanel = previewPanel;
        previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _previewTitleLabel.AutoSize = true;
        _previewTitleLabel.Font = AppTheme.CreateFont(9.3F, FontStyle.Bold);
        _previewTitleLabel.Margin = new Padding(0, 0, 0, 4);
        previewPanel.Controls.Add(_previewTitleLabel, 0, 0);

        _workspaceStatsLabel.AutoSize = true;
        _workspaceStatsLabel.Margin = new Padding(0, 0, 0, 6);
        previewPanel.Controls.Add(_workspaceStatsLabel, 0, 1);

        _previewActionsPanel.AutoSize = true;
        _previewActionsPanel.FlowDirection = FlowDirection.LeftToRight;
        _previewActionsPanel.WrapContents = true;
        _previewActionsPanel.Padding = new Padding(0, 1, 0, 1);
        _previewActionsPanel.Margin = new Padding(0, 0, 0, 8);
        _previewOpenButton.Text = "打开";
        _previewSharedButton.Text = "共享打开";
        _previewDedupeButton.Text = "去重";
        _previewDeleteButton.Text = "删除";
        _previewActionsPanel.Controls.Add(_previewOpenButton);
        _previewActionsPanel.Controls.Add(_previewSharedButton);
        _previewActionsPanel.Controls.Add(_previewDedupeButton);
        _previewActionsPanel.Controls.Add(_previewDeleteButton);
        previewPanel.Controls.Add(_previewActionsPanel, 0, 2);

        _tabListBox.Dock = DockStyle.Fill;
        _tabListBox.HorizontalScrollbar = true;
        _tabListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _tabListBox.ItemHeight = AppTheme.IsCompact ? 22 : 24;
        previewPanel.Controls.Add(_tabListBox, 0, 3);
        content.Panel2.Controls.Add(previewPanel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 0),
            WrapContents = true
        };
        _footerButtonsPanel = buttons;
        _closeButton.Text = "× 关闭";
        _refreshButton.Text = "↻ 刷新";
        _importButton.Text = "⇧ 导入会话";
        _deleteButton.Text = "⌫ 删除所选";
        _dedupeSelectedButton.Text = "◎ 所选去重";
        _dedupeAllButton.Text = "◎ 全部去重";
        _exportButton.Text = "⇩ 导出所选";
        _renameButton.Text = "✎ 重命名";
        _openPrivateButton.Text = "◐ 无痕窗口";
        _openIsolatedButton.Text = "⧉ 独立窗口";
        _openSharedButton.Text = "⇱ 共享窗口";
        _restoreButton.Text = "↺ 打开到当前窗口";
        buttons.Controls.Add(_closeButton);
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_importButton);
        buttons.Controls.Add(_exportButton);
        buttons.Controls.Add(_dedupeAllButton);
        buttons.Controls.Add(_dedupeSelectedButton);
        buttons.Controls.Add(_deleteButton);
        buttons.Controls.Add(_renameButton);
        buttons.Controls.Add(_openPrivateButton);
        buttons.Controls.Add(_openIsolatedButton);
        buttons.Controls.Add(_openSharedButton);
        buttons.Controls.Add(_restoreButton);

        var footerShell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0)
        };
        footerShell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footerShell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _footerHintPanel.Dock = DockStyle.Fill;
        _footerHintPanel.Padding = new Padding(10, 4, 10, 4);
        _footerHintPanel.Margin = new Padding(0, 0, 8, 0);
        _footerHintLabel.AutoSize = true;
        _footerHintLabel.Dock = DockStyle.Left;
        _footerHintLabel.Text = "请选择一个工作区后执行操作";
        _footerHintPanel.Controls.Add(_footerHintLabel);
        footerShell.Controls.Add(_footerHintPanel, 0, 0);
        footerShell.Controls.Add(buttons, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(content, 0, 2);
        root.Controls.Add(footerShell, 0, 3);
        Controls.Add(root);

        // 中文注释：关键操作添加提示，降低学习成本并避免误操作。
        _uiHintToolTip.SetToolTip(_quickFilterAllButton, "显示所有工作区");
        _uiHintToolTip.SetToolTip(_quickFilterFocusButton, "优先显示“高负载+最近更新”工作区");
        _uiHintToolTip.SetToolTip(_quickFilterHeavyButton, "筛选高负载工作区（含重点）");
        _uiHintToolTip.SetToolTip(_quickFilterRecentButton, "筛选最近更新工作区（含重点）");
        _uiHintToolTip.SetToolTip(_quickFilterNormalButton, "仅显示常规工作区");
        _uiHintToolTip.SetToolTip(_quickFilterDuplicateButton, "筛选存在重复页面风险的工作区");
        _uiHintToolTip.SetToolTip(_selectVisibleButton, "一键选中当前筛选结果中的全部可见行");
        _uiHintToolTip.SetToolTip(_openFirstVisibleButton, "快速打开当前筛选结果中的第一个条目");
        _uiHintToolTip.SetToolTip(_resetFiltersButton, "清空搜索词并重置排序/状态筛选");
        _uiHintToolTip.SetToolTip(_legendFocusLabel, "点击筛选重点工作区");
        _uiHintToolTip.SetToolTip(_legendHeavyLabel, "点击筛选高负载工作区");
        _uiHintToolTip.SetToolTip(_legendRecentLabel, "点击筛选最近更新工作区");
        _uiHintToolTip.SetToolTip(_legendNormalLabel, "点击筛选常规工作区");
        _uiHintToolTip.SetToolTip(_legendDuplicateLabel, "点击筛选重复风险工作区");
        _uiHintToolTip.SetToolTip(_previewOpenButton, "在当前窗口打开所选会话/工作区");
        _uiHintToolTip.SetToolTip(_previewSharedButton, "在新共享窗口打开所选会话/工作区");
        _uiHintToolTip.SetToolTip(_previewDedupeButton, "清理所选工作区中的重复页面");
        _uiHintToolTip.SetToolTip(_previewDeleteButton, "删除所选会话/工作区");
        _uiHintToolTip.SetToolTip(_tabListBox, "双击可直接打开当前选中的会话/工作区");
        _uiHintToolTip.SetToolTip(_kpiDedupeButton, "对全部工作区执行一次重复页面清理");
        _uiHintToolTip.SetToolTip(_footerHintLabel, "根据当前选中数量显示可执行动作");
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.DataSource = _rows;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(NamedSessionRow.Rank),
            HeaderText = "#",
            Width = 54,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(NamedSessionRow.DisplayName),
            HeaderText = "名称",
            Width = 260
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(NamedSessionRow.TabCount),
            HeaderText = "标签数",
            Width = 90
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(NamedSessionRow.Priority),
            HeaderText = "状态",
            Width = 138,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(NamedSessionRow.SavedAt),
            HeaderText = "保存时间",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        AppTheme.ApplyDataGrid(_grid);
        AppTheme.ApplyListBox(_tabListBox);
        AppTheme.ApplyTextInput(_searchTextBox);
        AppTheme.ApplyComboBox(_sortComboBox);
        AppTheme.ApplyComboBox(_statusFilterComboBox);
        _summaryLabel.ForeColor = AppTheme.TextSecondary;
        _selectionSummaryLabel.ForeColor = AppTheme.TextSecondary;
        _statusOverviewLabel.ForeColor = AppTheme.TextSecondary;
        _shortcutHintLabel.ForeColor = AppTheme.TextSecondary;
        _workspaceStatsLabel.ForeColor = AppTheme.TextSecondary;
        _tabListBox.ItemHeight = AppTheme.IsCompact ? 22 : 24;
        _kpiWorkspaceCountLabel.ForeColor = AppTheme.TextPrimary;
        _kpiTabCountLabel.ForeColor = AppTheme.TextPrimary;
        _kpiDuplicateLabel.ForeColor = AppTheme.TextPrimary;
        _kpiAverageTabsLabel.ForeColor = AppTheme.TextPrimary;
        _kpiLatestSavedLabel.ForeColor = AppTheme.TextPrimary;
        StyleControlTree(this);
        AppTheme.ApplyContextMenu(_contextMenu);
        ApplySurfaceCardStyle(_headerPanel, emphasizeAccent: true);
        ApplySurfaceCardStyle(_filterBarPanel, emphasizeAccent: false);
        ApplySurfaceCardStyle(_previewPanel, emphasizeAccent: false);
        ApplySurfaceCardStyle(_footerButtonsPanel, emphasizeAccent: false);
        ApplySurfaceCardStyle(_footerHintPanel, emphasizeAccent: false);
        ApplyContentSplitSurface();

        StyleButton(_restoreButton, emphasize: true);
        StyleButton(_openSharedButton);
        StyleButton(_openIsolatedButton);
        StyleButton(_openPrivateButton);
        StyleButton(_renameButton);
        StyleButton(_dedupeSelectedButton);
        StyleButton(_dedupeAllButton);
        StyleButton(_quickFilterAllButton);
        StyleButton(_quickFilterFocusButton);
        StyleButton(_quickFilterHeavyButton);
        StyleButton(_quickFilterRecentButton);
        StyleButton(_quickFilterNormalButton);
        StyleButton(_quickFilterDuplicateButton);
        StyleButton(_selectVisibleButton);
        StyleButton(_openFirstVisibleButton);
        StyleButton(_resetFiltersButton);
        StyleButton(_kpiDedupeButton);
        StyleButton(_previewOpenButton);
        StyleButton(_previewSharedButton);
        StyleButton(_previewDedupeButton);
        StyleButton(_previewDeleteButton);
        _previewDeleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        StyleButton(_deleteButton);
        _deleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        StyleButton(_exportButton);
        StyleButton(_importButton);
        StyleButton(_refreshButton);
        StyleButton(_closeButton, emphasize: true);
        StyleKpiLabel(_kpiWorkspaceCountLabel);
        StyleKpiLabel(_kpiTabCountLabel);
        StyleKpiLabel(_kpiDuplicateLabel);
        StyleKpiLabel(_kpiAverageTabsLabel);
        StyleKpiLabel(_kpiLatestSavedLabel);
        StyleLegendLabel(_legendFocusLabel, AppTheme.Warning);
        StyleLegendLabel(_legendHeavyLabel, AppTheme.Danger);
        StyleLegendLabel(_legendRecentLabel, AppTheme.Success);
        StyleLegendLabel(_legendNormalLabel, AppTheme.Accent);
        StyleLegendLabel(_legendDuplicateLabel, AppTheme.Danger);
        _footerHintLabel.Font = AppTheme.CreateFont(8.8F, FontStyle.Bold);
        _footerHintLabel.ForeColor = AppTheme.TextSecondary;
        UpdateQuickFilterButtonsVisual();
        UpdateModeTexts();
    }

    private void ApplyContentSplitConstraints()
    {
        var split = _contentSplitContainer;
        if (split is null || split.Width <= 0)
        {
            return;
        }

        // 中文注释：按实际宽度收紧两侧最小宽度，宽度不足 520+260 时按比例回退，保证取值永远合法。
        var available = Math.Max(0, split.Width - split.SplitterWidth);
        var panel2Min = Math.Min(260, available / 3);
        var panel1Min = Math.Min(520, Math.Max(0, available - panel2Min));
        var desired = _contentSplitInitialized ? split.SplitterDistance : 680;
        desired = Math.Clamp(desired, panel1Min, Math.Max(panel1Min, available - panel2Min));

        // 中文注释：先放宽最小宽度再设置分隔位置，避免调整过程中的临时状态触发范围校验异常。
        split.Panel1MinSize = 0;
        split.Panel2MinSize = 0;
        try
        {
            split.SplitterDistance = desired;
        }
        catch (Exception)
        {
            // 中文注释：极端窗口尺寸或 DPI 缩放下兜底忽略，等待下一次 SizeChanged 重新校准。
        }

        _contentSplitInitialized = true;
    }

    private void ApplyContentSplitSurface()
    {
        if (_contentSplitContainer is null)
        {
            return;
        }

        _contentSplitContainer.BackColor = AppTheme.PanelBorder;
        _contentSplitContainer.SplitterWidth = 5;
        _contentSplitContainer.Panel1.BackColor = AppTheme.Surface;
        _contentSplitContainer.Panel2.BackColor = AppTheme.Surface;
        _contentSplitContainer.Panel1.Padding = new Padding(10, 10, 8, 10);
        _contentSplitContainer.Panel2.Padding = new Padding(8, 10, 10, 10);
    }

    private static void ApplySurfaceCardStyle(Control? panel, bool emphasizeAccent)
    {
        if (panel is null)
        {
            return;
        }

        panel.BackColor = AppTheme.Surface;
        panel.Paint -= DrawSurfaceCardFrame;
        panel.Paint += DrawSurfaceCardFrame;
        panel.SizeChanged -= HandleCardSizeChanged;
        panel.SizeChanged += HandleCardSizeChanged;
        panel.Tag = emphasizeAccent ? "surface-card-accent" : "surface-card-normal";
        UpdateCardRegion(panel);
        panel.Invalidate();
    }

    private static void HandleCardSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            UpdateCardRegion(control);
            control.Invalidate();
        }
    }

    private static void UpdateCardRegion(Control panel)
    {
        if (panel.Width <= 0 || panel.Height <= 0)
        {
            return;
        }

        var old = panel.Region;
        using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, panel.Width, panel.Height), 10);
        panel.Region = new Region(path);
        old?.Dispose();
    }

    private static void DrawSurfaceCardFrame(object? sender, PaintEventArgs eventArgs)
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

        var emphasizeAccent = string.Equals(panel.Tag as string, "surface-card-accent", StringComparison.Ordinal);
        var borderColor = emphasizeAccent
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.44F)
                : AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.24F))
            : AppTheme.PanelBorder;
        var glowColor = emphasizeAccent
            ? Color.FromArgb(AppTheme.IsDark ? 32 : 22, AppTheme.Accent)
            : Color.FromArgb(AppTheme.IsDark ? 18 : 12, AppTheme.AccentMuted);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildCardPath(rect, 10);
        using var glowPen = new Pen(glowColor, 1F);
        using var borderPen = new Pen(borderColor, 1F);
        eventArgs.Graphics.DrawPath(glowPen, path);
        eventArgs.Graphics.DrawPath(borderPen, path);
    }

    private static GraphicsPath BuildCardPath(Rectangle rect, int radius) =>
        AppTheme.BuildRoundedRect(rect, radius);

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
        var glow = Color.FromArgb(AppTheme.IsDark ? 24 : 18, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle((int)(ClientSize.Width * 0.68F), -90, 260, 190));
    }

    private void WireEvents()
    {
        KeyDown += (_, eventArgs) =>
        {
            if (!HandleWindowShortcut(eventArgs))
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        };
        _searchTextBox.TextChanged += (_, _) => RefreshData();
        _sortComboBox.SelectedIndexChanged += (_, _) => RefreshData();
        _statusFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateQuickFilterButtonsVisual();
            RefreshData();
        };
        _quickFilterAllButton.Click += (_, _) => ApplyQuickStatusFilter(0);
        _quickFilterFocusButton.Click += (_, _) => ApplyQuickStatusFilter(1);
        _quickFilterHeavyButton.Click += (_, _) => ApplyQuickStatusFilter(2);
        _quickFilterRecentButton.Click += (_, _) => ApplyQuickStatusFilter(3);
        _quickFilterNormalButton.Click += (_, _) => ApplyQuickStatusFilter(4);
        _quickFilterDuplicateButton.Click += (_, _) => ApplyQuickStatusFilter(5);
        _selectVisibleButton.Click += (_, _) => SelectAllVisibleRows();
        _openFirstVisibleButton.Click += async (_, _) => await OpenFirstVisibleSessionAsync();
        _resetFiltersButton.Click += (_, _) => ResetFilters();
        _legendFocusLabel.Click += (_, _) => ApplyQuickStatusFilter(1);
        _legendHeavyLabel.Click += (_, _) => ApplyQuickStatusFilter(2);
        _legendRecentLabel.Click += (_, _) => ApplyQuickStatusFilter(3);
        _legendNormalLabel.Click += (_, _) => ApplyQuickStatusFilter(4);
        _legendDuplicateLabel.Click += (_, _) => ApplyQuickStatusFilter(5);
        _workspaceOnlyCheckBox.CheckedChanged += (_, _) => SetWorkspaceOnlyMode(_workspaceOnlyCheckBox.Checked);
        _grid.SelectionChanged += (_, _) => RefreshSelectedSessionDetails();
        _grid.CellDoubleClick += async (_, _) => await RestoreSelectedSessionAsync();
        _grid.CellFormatting += (_, eventArgs) => ApplyGridStatusCellVisual(eventArgs);
        _grid.MouseDown += HandleGridMouseDown;
        _grid.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.A)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    row.Selected = true;
                }

                eventArgs.Handled = true;
                return;
            }

            if (eventArgs.KeyCode != Keys.Delete)
            {
                return;
            }

            DeleteSelectedSessions();
            eventArgs.Handled = true;
        };

        _contextMenu.Items.AddRange(
        [
            _ctxRestore,
            _ctxOpenShared,
            _ctxOpenIsolated,
            _ctxOpenPrivate,
            new ToolStripSeparator(),
            _ctxRename,
            _ctxDedupeSelected,
            _ctxDedupeAll,
            _ctxDelete,
            _ctxExport,
            _ctxImport
        ]);
        AppTheme.ApplyContextMenu(_contextMenu);
        _contextMenu.Opening += (_, _) => UpdateContextMenuState();
        _ctxRestore.Click += async (_, _) => await RestoreSelectedSessionAsync();
        _ctxOpenShared.Click += (_, _) => OpenSelectedSessionInSharedWindow();
        _ctxOpenIsolated.Click += (_, _) => OpenSelectedSessionInIsolatedWindow();
        _ctxOpenPrivate.Click += (_, _) => OpenSelectedSessionInPrivateWindow();
        _ctxRename.Click += (_, _) => RenameSelectedSession();
        _ctxDedupeSelected.Click += (_, _) => DedupeSelectedSessions();
        _ctxDedupeAll.Click += (_, _) => DedupeAllWorkspaceSessions();
        _ctxDelete.Click += (_, _) => DeleteSelectedSessions();
        _ctxExport.Click += (_, _) => ExportSelectedSessions();
        _ctxImport.Click += (_, _) => ImportSessions();

        _restoreButton.Click += async (_, _) => await RestoreSelectedSessionAsync();
        _openSharedButton.Click += (_, _) => OpenSelectedSessionInSharedWindow();
        _openIsolatedButton.Click += (_, _) => OpenSelectedSessionInIsolatedWindow();
        _openPrivateButton.Click += (_, _) => OpenSelectedSessionInPrivateWindow();
        _renameButton.Click += (_, _) => RenameSelectedSession();
        _dedupeSelectedButton.Click += (_, _) => DedupeSelectedSessions();
        _dedupeAllButton.Click += (_, _) => DedupeAllWorkspaceSessions();
        _deleteButton.Click += (_, _) => DeleteSelectedSessions();
        _exportButton.Click += (_, _) => ExportSelectedSessions();
        _importButton.Click += (_, _) => ImportSessions();
        _refreshButton.Click += (_, _) => RefreshData();
        _closeButton.Click += (_, _) => Close();

        _previewOpenButton.Click += async (_, _) => await RestoreSelectedSessionAsync();
        _previewSharedButton.Click += (_, _) => OpenSelectedSessionInSharedWindow();
        _previewDedupeButton.Click += (_, _) => DedupeSelectedSessions();
        _previewDeleteButton.Click += (_, _) => DeleteSelectedSessions();
        _tabListBox.DoubleClick += async (_, _) => await RestoreSelectedSessionAsync();
        _tabListBox.DrawItem += DrawPreviewListItem;
        _kpiDedupeButton.Click += (_, _) =>
        {
            if (!_workspaceOnlyMode)
            {
                return;
            }

            DedupeAllWorkspaceSessions();
            UpdateKpiOverview();
        };
    }

    private bool HandleWindowShortcut(KeyEventArgs eventArgs)
    {
        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.A)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Selected = true;
            }

            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.A)
        {
            SelectAllVisibleRows();
            return true;
        }

        if (eventArgs.Alt && !eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.Enter)
        {
            _ = OpenFirstVisibleSessionAsync();
            return true;
        }

        if (eventArgs.Control && eventArgs.Shift && eventArgs.KeyCode == Keys.D && _workspaceOnlyMode)
        {
            DedupeAllWorkspaceSessions();
            return true;
        }

        if (eventArgs.KeyCode == Keys.Enter && !eventArgs.Control && !eventArgs.Shift && !eventArgs.Alt)
        {
            _ = RestoreSelectedSessionAsync();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.R)
        {
            RefreshData();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.I)
        {
            ImportSessions();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.E)
        {
            ExportSelectedSessions();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.D && _workspaceOnlyMode)
        {
            DedupeSelectedSessions();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.F)
        {
            _searchTextBox.Focus();
            _searchTextBox.SelectAll();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift && eventArgs.KeyCode == Keys.D0)
        {
            ResetFilters();
            return true;
        }

        if (eventArgs.Control && !eventArgs.Shift)
        {
            var filterIndex = eventArgs.KeyCode switch
            {
                Keys.D1 or Keys.NumPad1 => 0,
                Keys.D2 or Keys.NumPad2 => 1,
                Keys.D3 or Keys.NumPad3 => 2,
                Keys.D4 or Keys.NumPad4 => 3,
                Keys.D5 or Keys.NumPad5 => 4,
                Keys.D6 or Keys.NumPad6 => 5,
                _ => -1
            };
            if (filterIndex >= 0)
            {
                ApplyQuickStatusFilter(filterIndex);
                return true;
            }
        }

        if (eventArgs.KeyCode != Keys.Escape)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_searchTextBox.Text))
        {
            _searchTextBox.Clear();
            return true;
        }

        if (_workspaceOnlyMode && _statusFilterComboBox.SelectedIndex != 0)
        {
            ApplyQuickStatusFilter(0);
            return true;
        }

        return false;
    }

    private async Task RestoreSelectedSessionAsync()
    {
        var selected = GetPrimarySelectedSessionName();
        if (selected is null)
        {
            return;
        }

        await _restoreNamedSessionAsync(selected);
        RefreshData();
    }

    private void RenameSelectedSession()
    {
        var selected = GetPrimarySelectedSessionName();
        if (selected is null)
        {
            return;
        }

        if (!SessionNameDialog.ShowDialog(this, selected, out var newName))
        {
            return;
        }

        if (!_namedSessionStore.Rename(selected, newName, out var error))
        {
            MessageBox.Show($"重命名失败：{error}", EntityLabel, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _log($"{EntityLabel}已重命名：{selected} -> {newName}");
        RefreshData();
    }

    private void OpenSelectedSessionInSharedWindow()
    {
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        if (!ConfirmBatchOpen("共享窗口", selectedSessions.Count))
        {
            return;
        }

        foreach (var selected in selectedSessions)
        {
            _openNamedSessionInSharedWindow(selected);
        }
    }

    private void OpenSelectedSessionInIsolatedWindow()
    {
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        if (!ConfirmBatchOpen("独立窗口", selectedSessions.Count))
        {
            return;
        }

        foreach (var selected in selectedSessions)
        {
            _openNamedSessionInIsolatedWindow(selected);
        }
    }

    private void OpenSelectedSessionInPrivateWindow()
    {
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        if (!ConfirmBatchOpen("无痕窗口", selectedSessions.Count))
        {
            return;
        }

        foreach (var selected in selectedSessions)
        {
            _openNamedSessionInPrivateWindow(selected);
        }
    }

    private void DeleteSelectedSessions()
    {
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        var confirmMessage = selectedSessions.Count == 1
            ? $"确认删除{EntityLabel}“{selectedSessions[0]}”？"
            : $"确认删除所选 {selectedSessions.Count} 个{EntityLabel}？";
        if (MessageBox.Show(
                confirmMessage,
                $"删除{EntityLabel}",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var failed = new List<string>();
        foreach (var name in selectedSessions)
        {
            if (_namedSessionStore.Delete(name, out _))
            {
                continue;
            }

            failed.Add(name);
        }

        if (failed.Count > 0)
        {
            MessageBox.Show(
                $"以下会话删除失败：{string.Join("、", failed)}",
                EntityLabel,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else
        {
            _log($"{EntityLabel}已删除: {string.Join(", ", selectedSessions)}");
        }

        RefreshData();
    }

    private void DedupeSelectedSessions()
    {
        if (!_workspaceOnlyMode)
        {
            return;
        }

        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        var updated = 0;
        var removedTotal = 0;
        var failed = 0;
        foreach (var sessionName in selectedSessions)
        {
            var session = _namedSessionStore.Load(sessionName);
            if (session is null || session.Tabs.Count == 0)
            {
                continue;
            }

            var deduplicatedTabs = DeduplicateSessionTabs(session.Tabs, out var removedCount);
            if (removedCount <= 0)
            {
                continue;
            }

            if (deduplicatedTabs.Count == 0)
            {
                failed++;
                continue;
            }

            var updatedSession = new BrowserSessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(session.ActiveIndex, 0, deduplicatedTabs.Count - 1),
                ExitedCleanly = true,
                Tabs = deduplicatedTabs
            };

            if (!_namedSessionStore.Save(sessionName, updatedSession, out _))
            {
                failed++;
                continue;
            }

            updated++;
            removedTotal += removedCount;
        }

        RefreshData();
        if (updated == 0)
        {
            _log("所选工作区无需去重");
            return;
        }

        var failHint = failed > 0 ? $"，失败 {failed}" : string.Empty;
        _log($"所选工作区去重完成：更新 {updated} 个，清理重复 {removedTotal} 个{failHint}");
    }

    private void DedupeAllWorkspaceSessions()
    {
        if (!_workspaceOnlyMode)
        {
            return;
        }

        var workspaceNames = _namedSessionStore.ListSessions()
            .Where(item => item.Name.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Name)
            .ToList();
        if (workspaceNames.Count == 0)
        {
            _log("当前没有可维护的工作区");
            return;
        }

        var updated = 0;
        var removedTotal = 0;
        var failed = 0;
        foreach (var sessionName in workspaceNames)
        {
            var session = _namedSessionStore.Load(sessionName);
            if (session is null || session.Tabs.Count == 0)
            {
                continue;
            }

            var deduplicatedTabs = DeduplicateSessionTabs(session.Tabs, out var removedCount);
            if (removedCount <= 0)
            {
                continue;
            }

            if (deduplicatedTabs.Count == 0)
            {
                failed++;
                continue;
            }

            var updatedSession = new BrowserSessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(session.ActiveIndex, 0, deduplicatedTabs.Count - 1),
                ExitedCleanly = true,
                Tabs = deduplicatedTabs
            };

            if (!_namedSessionStore.Save(sessionName, updatedSession, out _))
            {
                failed++;
                continue;
            }

            updated++;
            removedTotal += removedCount;
        }

        RefreshData();
        if (updated == 0)
        {
            _log("全部工作区去重完成：无重复页面");
            return;
        }

        var failHint = failed > 0 ? $"，失败 {failed}" : string.Empty;
        _log($"全部工作区去重完成：更新 {updated} 个，清理重复 {removedTotal} 个{failHint}");
    }

    private void ExportSelectedSessions()
    {
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "选择导出目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var exported = 0;
        var failed = new List<string>();
        foreach (var name in selectedSessions)
        {
            var session = _namedSessionStore.Load(name);
            if (session is null || session.Tabs.Count == 0)
            {
                failed.Add(name);
                continue;
            }

            var fileName = $"{MakeSafeFileName(name)}-{DateTime.Now:yyyyMMddHHmmss}.json";
            var outputPath = Path.Combine(dialog.SelectedPath, fileName);
            try
            {
                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(outputPath, json);
                exported++;
            }
            catch
            {
                failed.Add(name);
            }
        }

        if (failed.Count > 0)
        {
            MessageBox.Show(
                $"已导出 {exported} 个会话，失败 {failed.Count} 个。\r\n失败项：{string.Join("、", failed)}",
                "导出命名会话",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show(
                $"已导出 {exported} 个会话到：{dialog.SelectedPath}",
                "导出命名会话",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        _log($"已导出命名会话 {exported} 个");
    }

    private void ImportSessions()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            Multiselect = true,
            Title = "选择要导入的会话文件"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
        {
            return;
        }

        var parseFailed = new List<string>();
        var candidates = BuildImportCandidates(dialog.FileNames, parseFailed);
        if (candidates.Count == 0)
        {
            MessageBox.Show(
                "未找到可导入的有效会话文件。",
                "导入命名会话",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var existingNames = _namedSessionStore.ListSessions()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previewItems = candidates
            .Select(candidate => new ImportSessionPreviewItem
            {
                Id = candidate.Id,
                Selected = true,
                SourceFileName = candidate.SourceFileName,
                SessionName = candidate.BaseName,
                TabCount = candidate.Session.Tabs.Count,
                HasConflict = existingNames.Contains(candidate.BaseName),
                Status = existingNames.Contains(candidate.BaseName) ? "重名" : "新会话"
            })
            .ToList();
        if (!ImportSessionsPreviewDialog.ShowDialog(this, previewItems, existingNames, out var selectedPreviewItems, out var conflictStrategy))
        {
            return;
        }
        if (selectedPreviewItems.Count == 0)
        {
            return;
        }

        var selectedNameMap = selectedPreviewItems.ToDictionary(
            item => item.Id,
            item => string.IsNullOrWhiteSpace(item.SessionName) ? "导入会话" : item.SessionName.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var overwritten = 0;
        var renamed = 0;
        var skipped = 0;
        var failed = new List<string>(parseFailed);

        foreach (var candidate in candidates.Where(item => selectedNameMap.ContainsKey(item.Id)))
        {
            try
            {
                var targetName = selectedNameMap[candidate.Id];
                if (existingNames.Contains(targetName))
                {
                    switch (conflictStrategy)
                    {
                        case ImportConflictStrategyOption.Overwrite:
                            overwritten++;
                            break;
                        case ImportConflictStrategyOption.Rename:
                            targetName = BuildUniqueImportedSessionName(targetName, existingNames);
                            if (!string.Equals(targetName, candidate.BaseName, StringComparison.OrdinalIgnoreCase))
                            {
                                renamed++;
                            }

                            break;
                        case ImportConflictStrategyOption.Skip:
                            skipped++;
                            continue;
                    }
                }

                if (!_namedSessionStore.Save(targetName, candidate.Session, out _))
                {
                    failed.Add(candidate.SourceFileName);
                    continue;
                }

                existingNames.Add(targetName);
                imported++;
            }
            catch
            {
                failed.Add(candidate.SourceFileName);
            }
        }

        RefreshData();
        if (failed.Count > 0)
        {
            MessageBox.Show(
                $"已导入 {imported} 个会话（覆盖 {overwritten}，重命名 {renamed}，跳过 {skipped}），失败 {failed.Count} 个。\r\n失败项：{string.Join("、", failed)}",
                "导入命名会话",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show(
                $"已成功导入 {imported} 个会话（覆盖 {overwritten}，重命名 {renamed}，跳过 {skipped}）。",
                "导入命名会话",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        _log($"已导入命名会话 {imported} 个，覆盖 {overwritten}，重命名 {renamed}，跳过 {skipped}");
    }

    private List<ImportedSessionCandidate> BuildImportCandidates(IEnumerable<string> filePaths, List<string> parseFailed)
    {
        var candidates = new List<ImportedSessionCandidate>();
        foreach (var filePath in filePaths)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                if (!TryParseImportedSession(content, Path.GetFileNameWithoutExtension(filePath), out var parsedSession, out var baseName))
                {
                    parseFailed.Add(Path.GetFileName(filePath));
                    continue;
                }

                candidates.Add(new ImportedSessionCandidate(
                    Guid.NewGuid().ToString("N"),
                    Path.GetFileName(filePath),
                    string.IsNullOrWhiteSpace(baseName) ? "导入会话" : baseName.Trim(),
                    parsedSession));
            }
            catch
            {
                parseFailed.Add(Path.GetFileName(filePath));
            }
        }

        return candidates;
    }

    private static bool TryParseImportedSession(
        string json,
        string fallbackName,
        out BrowserSessionState session,
        out string baseName)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var named = JsonSerializer.Deserialize<NamedBrowserSessionState>(json, options);
        if (named is not null && named.Tabs.Count > 0)
        {
            var normalizedTabs = named.Tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (normalizedTabs.Count > 0)
            {
                session = new BrowserSessionState
                {
                    SavedAtUtc = named.SavedAtUtc,
                    ActiveIndex = Math.Clamp(named.ActiveIndex, 0, normalizedTabs.Count - 1),
                    ExitedCleanly = true,
                    Tabs = normalizedTabs
                };
                baseName = string.IsNullOrWhiteSpace(named.Name) ? fallbackName : named.Name.Trim();
                return true;
            }
        }

        var browserSession = JsonSerializer.Deserialize<BrowserSessionState>(json, options);
        if (browserSession is null || browserSession.Tabs.Count == 0)
        {
            session = new BrowserSessionState();
            baseName = fallbackName;
            return false;
        }

        var tabs = browserSession.Tabs
            .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
            .ToList();
        if (tabs.Count == 0)
        {
            session = new BrowserSessionState();
            baseName = fallbackName;
            return false;
        }

        session = new BrowserSessionState
        {
            SavedAtUtc = browserSession.SavedAtUtc,
            ActiveIndex = Math.Clamp(browserSession.ActiveIndex, 0, tabs.Count - 1),
            ExitedCleanly = true,
            Tabs = tabs
        };
        baseName = string.IsNullOrWhiteSpace(fallbackName) ? "导入会话" : fallbackName.Trim();
        return true;
    }

    private void AddPreviewListItem(string text, PreviewListItemKind kind)
    {
        _tabListBox.Items.Add(new PreviewListItem(text, kind));
    }

    private void DrawPreviewListItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= _tabListBox.Items.Count)
        {
            return;
        }

        var raw = _tabListBox.Items[eventArgs.Index];
        var item = raw as PreviewListItem ?? new PreviewListItem(raw?.ToString() ?? string.Empty, PreviewListItemKind.Summary);
        var isSelected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        var isDark = AppTheme.IsDark;
        var selectedBack = isDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.52F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.68F);
        var normalBack = _tabListBox.BackColor;
        using var backBrush = new SolidBrush(isSelected ? selectedBack : normalBack);
        eventArgs.Graphics.FillRectangle(backBrush, eventArgs.Bounds);
        if (isSelected && !item.Kind.Equals(PreviewListItemKind.Header))
        {
            var selectedRect = Rectangle.Inflate(eventArgs.Bounds, -4, -1);
            var selectionSurface = isDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.38F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, 0.52F);
            var selectionBorder = isDark
                ? AppTheme.Mix(AppTheme.Accent, Color.White, 0.24F)
                : AppTheme.Mix(AppTheme.Accent, Color.Black, 0.16F);
            using var surfaceBrush = new SolidBrush(selectionSurface);
            using var borderPen = new Pen(selectionBorder, 1F);
            using var path = BuildCardPath(selectedRect, 7);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.FillPath(surfaceBrush, path);
            eventArgs.Graphics.DrawPath(borderPen, path);
        }

        var defaultFore = isSelected ? AppTheme.TextPrimary : _tabListBox.ForeColor;
        var foreColor = item.Kind switch
        {
            PreviewListItemKind.Header => isSelected ? AppTheme.TextPrimary : AppTheme.Accent,
            PreviewListItemKind.ActiveTab => isSelected
                ? AppTheme.TextPrimary
                : (isDark ? Color.FromArgb(178, 224, 255) : Color.FromArgb(18, 82, 156)),
            PreviewListItemKind.Url => isSelected
                ? AppTheme.TextPrimary
                : AppTheme.TextSecondary,
            PreviewListItemKind.Hint => isSelected
                ? AppTheme.TextPrimary
                : AppTheme.TextSecondary,
            _ => defaultFore
        };
        var fontStyle = item.Kind is PreviewListItemKind.Header or PreviewListItemKind.ActiveTab
            ? FontStyle.Bold
            : FontStyle.Regular;
        using var textBrush = new SolidBrush(foreColor);
        using var textFont = new Font(_tabListBox.Font, fontStyle);
        var textBounds = Rectangle.Inflate(eventArgs.Bounds, -6, 0);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            item.Text,
            textFont,
            textBounds,
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static string BuildUniqueImportedSessionName(string baseName, HashSet<string> existingNames)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(baseName) ? "导入会话" : baseName.Trim();
        if (!existingNames.Contains(normalizedBase))
        {
            return normalizedBase;
        }

        for (var i = 2; i <= 9999; i++)
        {
            var candidate = $"{normalizedBase} ({i})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{normalizedBase}-{DateTime.Now:yyyyMMddHHmmss}";
    }

    private void RefreshSelectedSessionDetails()
    {
        _tabListBox.Items.Clear();
        var selectedSessions = GetSelectedSessionNames();
        if (selectedSessions.Count == 0)
        {
            UpdateContextMenuState();
            return;
        }

        if (selectedSessions.Count > 1)
        {
            AddPreviewListItem($"批量选择：{selectedSessions.Count} 个{EntityLabel}", PreviewListItemKind.Header);
            AddPreviewListItem("提示：恢复、重命名仅在单选时可用；新窗口打开/删除/导出支持批量。", PreviewListItemKind.Hint);
            var selectedSet = new HashSet<string>(selectedSessions, StringComparer.OrdinalIgnoreCase);
            var selectedDescriptors = _allSessions
                .Where(item => selectedSet.Contains(item.Name))
                .ToList();
            if (selectedDescriptors.Count > 0)
            {
                var totalTabs = selectedDescriptors.Sum(item => item.TabCount);
                var avgTabs = totalTabs / (double)selectedDescriptors.Count;
                var largest = selectedDescriptors
                    .OrderByDescending(item => item.TabCount)
                    .ThenByDescending(item => item.SavedAtUtc)
                    .First();
                AddPreviewListItem($"批量摘要：共标签 {totalTabs}，平均 {avgTabs:F1}，最大 {GetSessionDisplayName(largest.Name)} ({largest.TabCount})", PreviewListItemKind.Summary);
                var topNames = selectedDescriptors
                    .OrderByDescending(item => item.TabCount)
                    .ThenByDescending(item => item.SavedAtUtc)
                    .Take(3)
                    .Select(item => $"{GetSessionDisplayName(item.Name)}({item.TabCount})")
                    .ToArray();
                if (topNames.Length > 0)
                {
                    AddPreviewListItem($"Top: {string.Join(" / ", topNames)}", PreviewListItemKind.Summary);
                }
            }

            UpdateContextMenuState();
            return;
        }

        var selected = selectedSessions[0];
        var session = _namedSessionStore.Load(selected);
        if (session is null)
        {
            UpdateContextMenuState();
            return;
        }

        var activeIndexText = session.Tabs.Count == 0
            ? "无"
            : $"{Math.Clamp(session.ActiveIndex + 1, 1, session.Tabs.Count)}/{session.Tabs.Count}";
        AddPreviewListItem($"{EntityLabel}摘要：共 {session.Tabs.Count} 个标签，当前激活 {activeIndexText}", PreviewListItemKind.Header);
        AddPreviewListItem($"会话名：{GetSessionDisplayName(selected)}", PreviewListItemKind.Summary);

        for (var i = 0; i < session.Tabs.Count; i++)
        {
            var tab = session.Tabs[i];
            var hasCustomTitle = !string.IsNullOrWhiteSpace(tab.CustomTitle);
            var title = hasCustomTitle
                ? tab.CustomTitle.Trim()
                : (string.IsNullOrWhiteSpace(tab.Title) ? tab.Url : tab.Title.Trim());
            var prefix = i == session.ActiveIndex ? "当前" : "标签";
            var pinned = tab.IsPinned ? " [固定]" : string.Empty;
            var color = ResolveTabColorTagText(tab.TabColorKey);
            var colorTag = string.IsNullOrWhiteSpace(color) ? string.Empty : $" [色标:{color}]";
            var customTag = hasCustomTitle ? " [自定义名]" : string.Empty;
            var rowKind = i == session.ActiveIndex ? PreviewListItemKind.ActiveTab : PreviewListItemKind.NormalTab;
            AddPreviewListItem($"{prefix}{pinned}{colorTag}{customTag} | {title}", rowKind);
            AddPreviewListItem($"  ↳ {tab.Url}", PreviewListItemKind.Url);
        }

        UpdateContextMenuState();
    }

    private static string ResolveTabColorTagText(string? colorKey)
    {
        var normalized = BrowserTab.NormalizeColorTagKey(colorKey);
        return normalized switch
        {
            "Blue" => "蓝",
            "Green" => "绿",
            "Orange" => "橙",
            "Rose" => "红",
            _ => string.Empty
        };
    }

    private void UpdateWorkspaceStatsLabel()
    {
        if (!_workspaceOnlyMode)
        {
            _workspaceStatsLabel.Text = "预览: 选择会话查看标签详情";
            return;
        }

        if (_allSessions.Count == 0)
        {
            _workspaceStatsLabel.Text = "工作区统计: 暂无数据";
            return;
        }

        var totalTabs = _allSessions.Sum(item => item.TabCount);
        var averageTabs = totalTabs / (double)_allSessions.Count;
        var largest = _allSessions
            .OrderByDescending(item => item.TabCount)
            .ThenByDescending(item => item.SavedAtUtc)
            .First();
        _workspaceStatsLabel.Text =
            $"工作区统计: 总标签 {totalTabs} | 平均 {averageTabs:F1} | 最大 {GetSessionDisplayName(largest.Name)} ({largest.TabCount})";
    }

    private void UpdateKpiOverview()
    {
        var sessionCount = _allSessions.Count;
        var totalTabs = _allSessions.Sum(item => item.TabCount);
        var averageTabs = sessionCount == 0 ? 0D : totalTabs / (double)sessionCount;
        var latestSavedAt = _allSessions.Count == 0
            ? (DateTime?)null
            : _allSessions.Max(item => item.SavedAtUtc);
        _kpiWorkspaceCountLabel.Text = _workspaceOnlyMode ? $"工作区 {sessionCount}" : $"会话 {sessionCount}";
        _kpiTabCountLabel.Text = $"标签总数 {totalTabs}";
        _kpiAverageTabsLabel.Text = $"平均标签 {averageTabs:F1}";
        _kpiLatestSavedLabel.Text = latestSavedAt is null
            ? "最近更新 --"
            : $"最近更新 {BuildRelativeTimeText(latestSavedAt.Value)}";

        if (!_workspaceOnlyMode)
        {
            _kpiDuplicateLabel.Text = "重复风险 --";
            _kpiDuplicateLabel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.62F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, 0.76F);
            _kpiDedupeButton.Visible = false;
            return;
        }

        var duplicateCount = 0;
        var affectedWorkspaceCount = 0;
        foreach (var descriptor in _allSessions)
        {
            var loaded = _namedSessionStore.Load(descriptor.Name);
            if (loaded is null || loaded.Tabs.Count <= 1)
            {
                continue;
            }

            _ = DeduplicateSessionTabs(loaded.Tabs, out var removedCount);
            if (removedCount <= 0)
            {
                continue;
            }

            duplicateCount += removedCount;
            affectedWorkspaceCount++;
        }

        _kpiDuplicateLabel.Text = duplicateCount > 0
            ? $"重复风险 {duplicateCount}（{affectedWorkspaceCount} 个工作区）"
            : "重复风险 0";
        _kpiDuplicateLabel.BackColor = duplicateCount > 0
            ? (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.62F)
                : AppTheme.Mix(AppTheme.Warning, Color.White, 0.80F))
            : (AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.62F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, 0.76F));
        _kpiDedupeButton.Visible = true;
        _kpiDedupeButton.Enabled = duplicateCount > 0;
        _kpiDedupeButton.Text = duplicateCount > 0 ? $"一键去重 ({duplicateCount})" : "一键去重";
    }

    private static string BuildRelativeTimeText(DateTime utcTime)
    {
        var delta = DateTime.UtcNow - utcTime;
        if (delta.TotalMinutes < 1)
        {
            return "刚刚";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(delta.TotalMinutes))} 分钟前";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(delta.TotalHours))} 小时前";
        }

        return $"{Math.Max(1, (int)Math.Round(delta.TotalDays))} 天前";
    }

    private void UpdateStatusOverviewLabel(SessionVisualThreshold threshold)
    {
        if (!_workspaceOnlyMode)
        {
            UpdateQuickFilterButtonText(0, 0, 0, 0, 0, 0);
            _statusOverviewLabel.Text = "状态概览: 命名会话模式";
            return;
        }

        if (_allSessions.Count == 0)
        {
            UpdateQuickFilterButtonText(0, 0, 0, 0, 0, 0);
            _statusOverviewLabel.Text = "状态概览: 暂无工作区";
            return;
        }

        var heavyRecentCount = 0;
        var heavyOnlyCount = 0;
        var recentOnlyCount = 0;
        var normalCount = 0;
        var duplicateRiskCount = 0;
        var duplicateRiskCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in _allSessions)
        {
            var state = EvaluateSessionVisualState(session, threshold);
            switch (state.SortWeight)
            {
                case 3:
                    heavyRecentCount++;
                    break;
                case 2:
                    heavyOnlyCount++;
                    break;
                case 1:
                    recentOnlyCount++;
                    break;
                default:
                    normalCount++;
                    break;
            }

            if (HasSessionDuplicateRisk(session, duplicateRiskCache))
            {
                duplicateRiskCount++;
            }
        }
        UpdateQuickFilterButtonText(
            totalCount: _allSessions.Count,
            heavyRecentCount,
            heavyOnlyCount,
            recentOnlyCount,
            normalCount,
            duplicateRiskCount);

        var filterText = _statusFilterComboBox.SelectedIndex <= 0
            ? "全部状态"
            : _statusFilterComboBox.SelectedItem?.ToString() ?? "全部状态";
        _statusOverviewLabel.Text =
            $"状态概览: 高负载+最近 {heavyRecentCount} | 高负载 {heavyOnlyCount} | 最近更新 {recentOnlyCount} | 常规 {normalCount} | 重复风险 {duplicateRiskCount} | 阈值 ≥{threshold.HeavyTabCountThreshold} | 当前筛选: {filterText}";
    }

    private void ApplyQuickStatusFilter(int statusIndex)
    {
        if (statusIndex < 0 || statusIndex >= _statusFilterComboBox.Items.Count)
        {
            return;
        }

        _statusFilterComboBox.SelectedIndex = statusIndex;
    }

    private void ResetFilters()
    {
        _searchTextBox.Clear();
        _sortComboBox.SelectedIndex = 0;
        ApplyQuickStatusFilter(0);
    }

    private void SelectAllVisibleRows()
    {
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        _grid.ClearSelection();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Selected = true;
        }

        UpdateContextMenuState();
    }

    private async Task OpenFirstVisibleSessionAsync()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var firstName = _rows[0].Name;
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return;
        }

        await _restoreNamedSessionAsync(firstName);
        RefreshData();
    }

    private void UpdateQuickFilterButtonText(
        int totalCount,
        int heavyRecentCount,
        int heavyOnlyCount,
        int recentOnlyCount,
        int normalCount,
        int duplicateRiskCount)
    {
        var heavyCount = heavyRecentCount + heavyOnlyCount;
        var recentCount = heavyRecentCount + recentOnlyCount;
        _quickFilterAllButton.Text = $"◻ 全部({totalCount})";
        _quickFilterFocusButton.Text = $"◆ 重点({heavyRecentCount})";
        _quickFilterHeavyButton.Text = $"▲ 高负载({heavyCount})";
        _quickFilterRecentButton.Text = $"● 最近更新({recentCount})";
        _quickFilterNormalButton.Text = $"○ 常规({normalCount})";
        _quickFilterDuplicateButton.Text = $"◎ 重复风险({duplicateRiskCount})";
    }

    private void UpdateQuickFilterButtonsVisual()
    {
        var activeIndex = _statusFilterComboBox.SelectedIndex;
        ApplyQuickFilterButtonState(_quickFilterAllButton, activeIndex == 0);
        ApplyQuickFilterButtonState(_quickFilterFocusButton, activeIndex == 1);
        ApplyQuickFilterButtonState(_quickFilterHeavyButton, activeIndex == 2);
        ApplyQuickFilterButtonState(_quickFilterRecentButton, activeIndex == 3);
        ApplyQuickFilterButtonState(_quickFilterNormalButton, activeIndex == 4);
        ApplyQuickFilterButtonState(_quickFilterDuplicateButton, activeIndex == 5);
        ApplyLegendActiveState(_legendFocusLabel, activeIndex == 1);
        ApplyLegendActiveState(_legendHeavyLabel, activeIndex == 2);
        ApplyLegendActiveState(_legendRecentLabel, activeIndex == 3);
        ApplyLegendActiveState(_legendNormalLabel, activeIndex == 4);
        ApplyLegendActiveState(_legendDuplicateLabel, activeIndex == 5);
    }

    private void ApplyQuickFilterButtonState(Button button, bool active)
    {
        // 中文注释：快速筛选按钮采用“选中高亮”样式，让当前过滤条件在视觉上可立即识别。
        var semanticBack = ResolveQuickFilterButtonBackColor(button, active);
        button.BackColor = semanticBack;
        button.ForeColor = AppTheme.TextPrimary;
        button.FlatAppearance.BorderColor = active ? AppTheme.Accent : AppTheme.AccentMuted;
        button.FlatAppearance.BorderSize = active ? 2 : 1;
        button.Font = AppTheme.CreateFont(8.9F, active ? FontStyle.Bold : FontStyle.Regular);
    }

    private static Color ResolveQuickFilterButtonBackColor(Button button, bool active)
    {
        if (!active)
        {
            return AppTheme.AccentMuted;
        }

        if (button.Text.Contains("重点", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.60F)
                : AppTheme.Mix(AppTheme.Warning, Color.White, 0.78F);
        }

        if (button.Text.Contains("高负载", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.64F)
                : AppTheme.Mix(AppTheme.Danger, Color.White, 0.80F);
        }

        if (button.Text.Contains("最近更新", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.58F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.76F);
        }

        if (button.Text.Contains("重复风险", StringComparison.Ordinal))
        {
            return AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.58F)
                : AppTheme.Mix(AppTheme.Danger, Color.White, 0.78F);
        }

        return AppTheme.AccentHover;
    }

    private static void ApplyLegendActiveState(Label label, bool active)
    {
        label.Font = AppTheme.CreateFont(8.5F, active ? FontStyle.Bold : FontStyle.Regular);
        label.Padding = active ? new Padding(9, 4, 9, 4) : new Padding(8, 3, 8, 3);
    }

    private void ApplyGridStatusCellVisual(DataGridViewCellFormattingEventArgs eventArgs)
    {
        // 中文注释：状态列做“语义色”强化，让重点/高负载/最近更新在表格中一眼可见。
        if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex != 3 || eventArgs.Value is not string text)
        {
            return;
        }

        var style = eventArgs.CellStyle ?? _grid.DefaultCellStyle;
        style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        style.Font = AppTheme.CreateFont(8.7F, FontStyle.Bold);

        if (text.StartsWith("◆", StringComparison.Ordinal))
        {
            style.ForeColor = AppTheme.IsDark ? Color.FromArgb(255, 226, 164) : Color.FromArgb(146, 84, 4);
            return;
        }

        if (text.StartsWith("▲", StringComparison.Ordinal))
        {
            style.ForeColor = AppTheme.IsDark ? Color.FromArgb(255, 188, 188) : Color.FromArgb(148, 42, 48);
            return;
        }

        if (text.StartsWith("●", StringComparison.Ordinal))
        {
            style.ForeColor = AppTheme.IsDark ? Color.FromArgb(173, 232, 202) : Color.FromArgb(24, 104, 72);
            return;
        }

        style.ForeColor = AppTheme.TextSecondary;
    }

    private void ApplyGridPriorityVisuals()
    {
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        var baseBack = _workspaceOnlyMode
            ? (AppTheme.IsDark ? Color.FromArgb(38, 47, 61) : Color.White)
            : (AppTheme.IsDark ? Color.FromArgb(40, 50, 64) : Color.FromArgb(249, 252, 255));
        var recentBack = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.76F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.90F);
        var heavyBack = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.74F)
            : AppTheme.Mix(AppTheme.Warning, Color.White, 0.90F);

        var visualThreshold = BuildSessionVisualThreshold(_allSessions);
        var descriptorMap = _allSessions.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is not NamedSessionRow boundRow)
            {
                continue;
            }

            if (!descriptorMap.TryGetValue(boundRow.Name, out var descriptor))
            {
                row.DefaultCellStyle.BackColor = baseBack;
                continue;
            }

            var visualState = EvaluateSessionVisualState(descriptor, visualThreshold);
            var background = visualState.SortWeight >= 2
                ? heavyBack
                : visualState.SortWeight == 1
                    ? recentBack
                    : baseBack;
            if (!_workspaceOnlyMode && visualState.SortWeight == 0 && row.Index % 2 == 1)
            {
                background = AppTheme.IsDark
                    ? AppTheme.Mix(background, Color.White, 0.03F)
                    : AppTheme.Mix(background, Color.Black, 0.035F);
            }

            row.DefaultCellStyle.BackColor = background;
            row.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            row.DefaultCellStyle.SelectionBackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.Surface, 0.44F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, 0.74F);
            row.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            row.Height = 30;
            row.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            if (row.Cells.Count > 3)
            {
                row.Cells[3].ToolTipText = visualState.ToolTipText;
            }
        }
    }

    private string? GetPrimarySelectedSessionName()
    {
        var selected = GetSelectedSessionNames();
        return selected.Count == 1 ? selected[0] : null;
    }

    private List<string> GetSelectedSessionNames()
    {
        return _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.DataBoundItem)
            .OfType<NamedSessionRow>()
            .Select(row => row.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool ConfirmBatchOpen(string modeText, int selectedCount)
    {
        if (selectedCount <= 4)
        {
            return true;
        }

        return MessageBox.Show(
            $"当前选择了 {selectedCount} 个会话，确认在新{modeText}批量打开？",
            "批量打开确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void HandleGridMouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _grid.HitTest(eventArgs.X, eventArgs.Y);
        if (hit.RowIndex >= 0)
        {
            if (!_grid.Rows[hit.RowIndex].Selected)
            {
                _grid.ClearSelection();
                _grid.Rows[hit.RowIndex].Selected = true;
                if (_grid.Columns.Count > 0 && _grid.Rows[hit.RowIndex].Cells.Count > 0)
                {
                    _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[0];
                }
            }

            _contextMenu.Show(_grid, eventArgs.Location);
        }
    }

    private void UpdateContextMenuState()
    {
        var selectedNames = GetSelectedSessionNames();
        var selectionCount = selectedNames.Count;
        var hasAny = selectionCount > 0;
        var hasSingle = selectionCount == 1;

        _ctxRestore.Enabled = hasSingle;
        _ctxOpenShared.Enabled = hasAny;
        _ctxOpenIsolated.Enabled = hasAny;
        _ctxOpenPrivate.Enabled = hasAny;
        _ctxRename.Enabled = hasSingle;
        _ctxDedupeSelected.Enabled = hasAny && _workspaceOnlyMode;
        _ctxDedupeSelected.Visible = _workspaceOnlyMode;
        _ctxDedupeAll.Enabled = _workspaceOnlyMode && _allSessions.Count > 0;
        _ctxDedupeAll.Visible = _workspaceOnlyMode;
        _ctxDelete.Enabled = hasAny;
        _ctxExport.Enabled = hasAny;
        _ctxImport.Enabled = true;

        _restoreButton.Enabled = hasSingle;
        _openSharedButton.Enabled = hasAny;
        _openIsolatedButton.Enabled = hasAny;
        _openPrivateButton.Enabled = hasAny;
        _renameButton.Enabled = hasSingle;
        _dedupeSelectedButton.Enabled = hasAny && _workspaceOnlyMode;
        _dedupeSelectedButton.Visible = _workspaceOnlyMode;
        _dedupeAllButton.Enabled = _workspaceOnlyMode && _allSessions.Count > 0;
        _dedupeAllButton.Visible = _workspaceOnlyMode;
        _deleteButton.Enabled = hasAny;
        _exportButton.Enabled = hasAny;
        _importButton.Enabled = true;

        _previewOpenButton.Enabled = hasSingle;
        _previewSharedButton.Enabled = hasAny;
        _previewDedupeButton.Enabled = hasAny && _workspaceOnlyMode;
        _previewDedupeButton.Visible = _workspaceOnlyMode;
        _previewDeleteButton.Enabled = hasAny;
        _selectVisibleButton.Enabled = _rows.Count > 0;
        _openFirstVisibleButton.Enabled = _rows.Count > 0;
        _resetFiltersButton.Enabled = true;
        UpdateSelectionSummaryLabel(selectedNames);
        UpdateSelectionActionHint(selectedNames.Count);
    }

    private void UpdateSelectionSummaryLabel(IReadOnlyCollection<string> selectedNames)
    {
        if (_rows.Count == 0)
        {
            _selectionSummaryLabel.Text = string.Empty;
            return;
        }

        if (selectedNames.Count == 0)
        {
            _selectionSummaryLabel.Text = "未选择";
            return;
        }

        var selectedSet = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        var selectedTabCount = _allSessions
            .Where(item => selectedSet.Contains(item.Name))
            .Sum(item => item.TabCount);
        _selectionSummaryLabel.Text = $"已选: {selectedNames.Count} 个 / 标签 {selectedTabCount}";
    }

    private void UpdateSelectionActionHint(int selectedCount)
    {
        if (_rows.Count == 0)
        {
            _footerHintLabel.Text = "暂无可操作数据，可先导入会话或保存工作区";
            _footerHintPanel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.74F)
                : AppTheme.Mix(AppTheme.Accent, Color.White, 0.90F);
            return;
        }

        if (selectedCount <= 0)
        {
            _footerHintLabel.Text = "未选择条目：可使用“全选可见”或双击列表快速打开";
            _footerHintPanel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.AccentMuted, AppTheme.SurfaceAlt, 0.72F)
                : AppTheme.Mix(AppTheme.AccentMuted, Color.White, 0.88F);
            return;
        }

        if (selectedCount == 1)
        {
            _footerHintLabel.Text = _workspaceOnlyMode
                ? "已选 1 项：支持打开、共享窗口、重命名、去重和删除"
                : "已选 1 项：支持恢复、共享窗口、重命名和删除";
            _footerHintPanel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.70F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.88F);
            return;
        }

        _footerHintLabel.Text = _workspaceOnlyMode
            ? $"已选 {selectedCount} 项：支持批量打开、导出、删除和去重"
            : $"已选 {selectedCount} 项：支持批量打开、导出和删除";
        _footerHintPanel.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.68F)
            : AppTheme.Mix(AppTheme.Warning, Color.White, 0.88F);
    }

    private IEnumerable<NamedSessionDescriptor> SortVisibleSessions(IEnumerable<NamedSessionDescriptor> source)
    {
        var visualThreshold = BuildSessionVisualThreshold(_allSessions);
        return _sortComboBox.SelectedIndex switch
        {
            1 => source.OrderBy(item => item.SavedAtUtc),
            2 => source.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
            3 => source.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
            4 => source.OrderByDescending(item => item.TabCount).ThenByDescending(item => item.SavedAtUtc),
            5 => source.OrderBy(item => item.TabCount).ThenByDescending(item => item.SavedAtUtc),
            6 => source
                .OrderByDescending(item => EvaluateSessionVisualState(item, visualThreshold).SortWeight)
                .ThenByDescending(item => item.SavedAtUtc)
                .ThenByDescending(item => item.TabCount),
            _ => source.OrderByDescending(item => item.SavedAtUtc)
        };
    }

    private static SessionVisualThreshold BuildSessionVisualThreshold(IReadOnlyCollection<NamedSessionDescriptor> sessions)
    {
        if (sessions.Count == 0)
        {
            return new SessionVisualThreshold(int.MaxValue, DateTime.MinValue);
        }

        // 中文注释：阈值采用“平均标签数+2”做重负载判定，同时把最近 8 小时视作近期更新窗口。
        var averageTabs = sessions.Average(item => item.TabCount);
        var heavyThreshold = Math.Max(6, (int)Math.Ceiling(averageTabs + 2));
        return new SessionVisualThreshold(heavyThreshold, DateTime.UtcNow.AddHours(-8));
    }

    private static SessionVisualState EvaluateSessionVisualState(
        NamedSessionDescriptor descriptor,
        SessionVisualThreshold threshold)
    {
        var isHeavy = descriptor.TabCount >= threshold.HeavyTabCountThreshold;
        var isRecent = descriptor.SavedAtUtc >= threshold.RecentSinceUtc;
        if (isHeavy && isRecent)
        {
            return new SessionVisualState("◆ 重点", 3, $"标签数较高（≥{threshold.HeavyTabCountThreshold}）且近 8 小时更新");
        }

        if (isHeavy)
        {
            return new SessionVisualState("▲ 高负载", 2, $"标签数较高（≥{threshold.HeavyTabCountThreshold}）");
        }

        if (isRecent)
        {
            return new SessionVisualState("● 最近更新", 1, "近 8 小时内有保存记录");
        }

        return new SessionVisualState("○ 常规", 0, "常规工作区");
    }

    private bool MatchesStatusFilter(
        NamedSessionDescriptor descriptor,
        SessionVisualThreshold threshold,
        int filterIndex,
        Dictionary<string, bool> duplicateRiskCache)
    {
        var visualState = EvaluateSessionVisualState(descriptor, threshold);
        var hasDuplicateRisk = HasSessionDuplicateRisk(descriptor, duplicateRiskCache);
        // 中文注释：状态筛选使用组合逻辑，便于快速定位“重负载”或“最近更新”的关键工作区。
        return filterIndex switch
        {
            1 => visualState.SortWeight == 3,
            2 => visualState.SortWeight >= 2,
            3 => visualState.SortWeight is 1 or 3,
            4 => visualState.SortWeight == 0,
            5 => hasDuplicateRisk,
            _ => true
        };
    }

    private bool HasSessionDuplicateRisk(
        NamedSessionDescriptor descriptor,
        Dictionary<string, bool> duplicateRiskCache)
    {
        if (duplicateRiskCache.TryGetValue(descriptor.Name, out var cachedResult))
        {
            return cachedResult;
        }

        var loaded = _namedSessionStore.Load(descriptor.Name);
        var hasRisk = false;
        if (loaded is not null && loaded.Tabs.Count > 1)
        {
            _ = DeduplicateSessionTabs(loaded.Tabs, out var removedCount);
            hasRisk = removedCount > 0;
        }

        duplicateRiskCache[descriptor.Name] = hasRisk;
        return hasRisk;
    }

    private static List<BrowserSessionTabState> DeduplicateSessionTabs(
        IEnumerable<BrowserSessionTabState> tabs,
        out int removedCount)
    {
        removedCount = 0;
        var uniqueTabs = new List<BrowserSessionTabState>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in tabs)
        {
            if (!TryBuildWorkspaceTabKey(tab, out var key))
            {
                continue;
            }

            if (!keys.Add(key))
            {
                removedCount++;
                continue;
            }

            uniqueTabs.Add(tab);
        }

        return uniqueTabs;
    }

    private static bool TryBuildWorkspaceTabKey(BrowserSessionTabState tab, out string key)
    {
        if (!Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) || uri is null)
        {
            key = string.Empty;
            return false;
        }

        key = BuildWorkspaceTabKey(uri);
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string BuildWorkspaceTabKey(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var authority = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        var path = uri.AbsolutePath;
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        return $"{scheme}://{authority}{path}{uri.Query}";
    }

    private static string MakeSafeFileName(string rawName)
    {
        var safeChars = rawName
            .Trim()
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        var safe = new string(safeChars).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "session" : safe;
    }

    private void StyleButton(Button button, bool emphasize = false)
    {
        AppTheme.ApplyPrimaryButton(button, emphasize);
        AppTheme.ApplyControlRegion(button, 6);
        if (IsKpiActionButton(button))
        {
            button.MinimumSize = new Size(AppTheme.IsCompact ? 98 : 110, AppTheme.IsCompact ? 28 : 32);
            button.Height = AppTheme.IsCompact ? 28 : 32;
            button.Padding = AppTheme.IsCompact ? new Padding(8, 3, 8, 3) : new Padding(10, 4, 10, 4);
            button.Margin = new Padding(6, 2, 2, 2);
            return;
        }

        if (IsQuickFilterButton(button))
        {
            button.MinimumSize = new Size(AppTheme.IsCompact ? 78 : 92, AppTheme.IsCompact ? 30 : 34);
            button.Height = AppTheme.IsCompact ? 30 : 34;
            button.Padding = AppTheme.IsCompact ? new Padding(8, 4, 8, 4) : new Padding(10, 5, 10, 5);
            button.Margin = new Padding(4, 2, 4, 2);
            return;
        }

        if (IsPreviewActionButton(button))
        {
            button.MinimumSize = new Size(AppTheme.IsCompact ? 82 : 96, AppTheme.IsCompact ? 30 : 34);
            button.Height = AppTheme.IsCompact ? 30 : 34;
            button.Padding = AppTheme.IsCompact ? new Padding(8, 4, 8, 4) : new Padding(10, 5, 10, 5);
            button.Margin = new Padding(4, 2, 4, 2);
        }
    }

    private static void StyleKpiLabel(Label label)
    {
        label.AutoSize = true;
        label.Font = AppTheme.CreateFont(8.9F, FontStyle.Bold);
        label.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.Accent, AppTheme.SurfaceAlt, 0.62F)
            : AppTheme.Mix(AppTheme.Accent, Color.White, 0.76F);
        label.ForeColor = AppTheme.TextPrimary;
        label.Padding = AppTheme.IsCompact ? new Padding(8, 4, 8, 4) : new Padding(10, 5, 10, 5);
        label.Margin = new Padding(0, 2, 6, 2);
    }

    private static void StyleLegendLabel(Label label, Color semanticColor)
    {
        label.AutoSize = true;
        label.Font = AppTheme.CreateFont(8.5F, FontStyle.Bold);
        label.BackColor = AppTheme.IsDark
            ? AppTheme.Mix(semanticColor, AppTheme.SurfaceAlt, 0.62F)
            : AppTheme.Mix(semanticColor, Color.White, 0.82F);
        label.ForeColor = AppTheme.TextPrimary;
        label.Padding = new Padding(8, 3, 8, 3);
        label.Margin = new Padding(0, 1, 6, 1);
        label.Cursor = Cursors.Hand;
    }

    private bool IsKpiActionButton(Button button)
    {
        return ReferenceEquals(button, _kpiDedupeButton);
    }

    private bool IsQuickFilterButton(Button button)
    {
        return ReferenceEquals(button, _quickFilterAllButton) ||
               ReferenceEquals(button, _quickFilterFocusButton) ||
               ReferenceEquals(button, _quickFilterHeavyButton) ||
               ReferenceEquals(button, _quickFilterRecentButton) ||
               ReferenceEquals(button, _quickFilterNormalButton) ||
               ReferenceEquals(button, _quickFilterDuplicateButton) ||
               ReferenceEquals(button, _selectVisibleButton) ||
               ReferenceEquals(button, _openFirstVisibleButton) ||
               ReferenceEquals(button, _resetFiltersButton);
    }

    private bool IsPreviewActionButton(Button button)
    {
        return ReferenceEquals(button, _previewOpenButton) ||
               ReferenceEquals(button, _previewSharedButton) ||
               ReferenceEquals(button, _previewDedupeButton) ||
               ReferenceEquals(button, _previewDeleteButton);
    }

    public void SetWorkspaceOnlyMode(bool workspaceOnlyMode, bool refresh = true)
    {
        _workspaceOnlyMode = workspaceOnlyMode;
        if (_workspaceOnlyCheckBox.Checked != workspaceOnlyMode)
        {
            _workspaceOnlyCheckBox.Checked = workspaceOnlyMode;
        }

        UpdateModeTexts();
        if (refresh)
        {
            RefreshData();
        }
    }

    private void UpdateModeTexts()
    {
        Text = _workspaceOnlyMode ? "工作区中心" : "命名会话管理";
        _titleLabel.Text = _workspaceOnlyMode ? "工作区中心" : "命名会话管理";
        _subtitleLabel.Text = _workspaceOnlyMode
            ? "查看已保存工作区，支持一键恢复、重命名、删除和标签预览。"
            : "查看已保存的命名会话，支持恢复、重命名和删除，并预览标签页列表。";
        _previewTitleLabel.Text = _workspaceOnlyMode ? "工作区标签预览" : "标签页预览";
        _workspaceStatsLabel.Visible = true;
        _searchTextBox.PlaceholderText = _workspaceOnlyMode ? "按工作区名称筛选" : "按会话名称筛选";
        _statusFilterLabel.Visible = _workspaceOnlyMode;
        _statusFilterComboBox.Visible = _workspaceOnlyMode;
        _quickFilterPanel.Visible = _workspaceOnlyMode;
        _statusLegendPanel.Visible = _workspaceOnlyMode;
        if (!_workspaceOnlyMode)
        {
            _statusFilterComboBox.SelectedIndex = 0;
        }
        UpdateQuickFilterButtonsVisual();
        if (_grid.Columns.Count > 1)
        {
            _grid.Columns[1].HeaderText = _workspaceOnlyMode ? "工作区名称" : "会话名称";
        }

        _restoreButton.Text = _workspaceOnlyMode ? "↺ 打开到当前窗口" : "↺ 恢复到当前窗口";
        _dedupeSelectedButton.Text = _workspaceOnlyMode ? "◎ 所选去重" : "◎ 所选去重";
        _dedupeAllButton.Text = _workspaceOnlyMode ? "◎ 全部去重" : "◎ 全部去重";
        _importButton.Text = _workspaceOnlyMode ? "⇧ 导入工作区" : "⇧ 导入会话";
        _exportButton.Text = _workspaceOnlyMode ? "⇩ 导出所选" : "⇩ 导出所选";
        _ctxRestore.Text = _workspaceOnlyMode ? "打开到当前窗口" : "恢复到当前窗口";
        _ctxDedupeSelected.Text = "所选工作区去重";
        _ctxDedupeAll.Text = "全部工作区去重";
        _ctxImport.Text = _workspaceOnlyMode ? "导入工作区..." : "导入会话...";
        _previewOpenButton.Text = _workspaceOnlyMode ? "↺ 打开工作区" : "↺ 打开会话";
        _previewSharedButton.Text = "⇱ 共享打开";
        _previewDedupeButton.Text = "◎ 去重";
        _previewDeleteButton.Text = "⌫ 删除";
        _shortcutHintLabel.Text = _workspaceOnlyMode
            ? "快捷键: Enter 打开 | Alt+Enter 打开首个 | Ctrl+A 全选 | Ctrl+Shift+A 全选可见 | Ctrl+F 搜索 | Ctrl+1..6 状态筛选 | Ctrl+0 重置筛选 | Ctrl+D 所选去重 | Ctrl+Shift+D 全部去重 | Ctrl+R 刷新 | Ctrl+I 导入 | Ctrl+E 导出 | Esc 清空"
            : "快捷键: Enter 打开 | Alt+Enter 打开首个 | Ctrl+A 全选 | Ctrl+Shift+A 全选可见 | Ctrl+F 搜索 | Ctrl+0 重置筛选 | Ctrl+R 刷新 | Ctrl+I 导入 | Ctrl+E 导出 | Esc 清空";
        UpdateSelectionActionHint(GetSelectedSessionNames().Count);
    }

    private string GetSessionDisplayName(string rawName)
    {
        if (!_workspaceOnlyMode)
        {
            return rawName;
        }

        return rawName.StartsWith(WorkspaceSessionPrefix, StringComparison.OrdinalIgnoreCase)
            ? rawName[WorkspaceSessionPrefix.Length..]
            : rawName;
    }

    private string EntityLabel => _workspaceOnlyMode ? "工作区" : "命名会话";

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

    private sealed record ImportedSessionCandidate(
        string Id,
        string SourceFileName,
        string BaseName,
        BrowserSessionState Session);

    private sealed class NamedSessionRow
    {
        public int Rank { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int TabCount { get; init; }
        public string SavedAt { get; init; } = string.Empty;
        public string Priority { get; init; } = string.Empty;
    }

    private sealed record PreviewListItem(string Text, PreviewListItemKind Kind);

    private sealed record SessionVisualThreshold(int HeavyTabCountThreshold, DateTime RecentSinceUtc);
    private sealed record SessionVisualState(string DisplayText, int SortWeight, string ToolTipText);
}

