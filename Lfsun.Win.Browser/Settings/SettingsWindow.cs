using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.Security;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Settings;

public sealed class SettingsWindow : Form
{
    private const int SidebarMinWidth = 220;
    private const int ContentMinWidth = 320;
    private const int PreferredSidebarWidth = 248;

    private readonly BrowserConfig _config;
    private readonly DownloadManager _downloadManager;
    private readonly PluginManager _pluginManager;
    private readonly CredentialStore _credentialStore;
    private readonly BookmarkManager _bookmarkManager;
    private readonly Action _onBookmarksChanged;
    private readonly Action _onConfigChanged;
    private readonly Action<string> _log;
    private readonly Action? _openShortcutsOverview;

    private readonly TextBox _startPageTextBox = new();
    private readonly Button _resetStartPageButton = new();
    private readonly NumericUpDown _maxLiveTabsNumber = new();
    private readonly NumericUpDown _suspendMinutesNumber = new();
    private readonly ComboBox _uiThemeComboBox = new();
    private readonly ComboBox _uiAccentComboBox = new();
    private readonly ComboBox _uiAccentStrengthComboBox = new();
    private readonly ComboBox _uiFontComboBox = new();
    private readonly ComboBox _uiDensityComboBox = new();
    private readonly TextBox _taskbarIconPathTextBox = new();
    private readonly Button _taskbarIconBrowseButton = new();
    private readonly Button _taskbarIconResetButton = new();
    private readonly PictureBox _taskbarIconPreviewBox = new();
    private readonly Label _taskbarIconPreviewLabel = new();
    private readonly TextBox _downloadDirectoryTextBox = new();
    private readonly Button _downloadBrowseButton = new();
    private readonly CheckBox _restoreLastSessionCheckBox = new();
    private readonly CheckBox _askBeforeRestoringSessionCheckBox = new();
    private readonly CheckBox _autofillCheckBox = new();
    private readonly CheckBox _passwordAutosaveCheckBox = new();
    private readonly CheckBox _credentialAutofillCheckBox = new();
    private readonly CheckBox _credentialAutosaveCheckBox = new();
    private readonly DataGridView _credentialAutosaveRuleGrid = new();
    private readonly BindingList<CredentialAutosaveRuleRow> _credentialAutosaveRuleRows = [];
    private readonly Button _credentialAutosaveRuleDeleteButton = new();
    private readonly CheckBox _adBlockEnabledCheckBox = new();
    private readonly TextBox _adBlockCustomDomainsTextBox = new();
    private readonly CheckBox _enableAddressBarSuggestionsCheckBox = new();
    private readonly ComboBox _searchEngineComboBox = new();
    private readonly TextBox _searchEngineTemplateTextBox = new();
    private readonly DataGridView _customSearchEngineGrid = new();
    private readonly BindingList<CustomSearchEngineRow> _customSearchEngineRows = [];
    private readonly Button _customSearchEngineDeleteButton = new();
    private readonly CheckBox _showBookmarksBarCheckBox = new();
    private readonly CheckBox _shortcutAddBookmarkEnabledCheckBox = new();
    private readonly TextBox _shortcutAddBookmarkTextBox = new();
    private readonly CheckBox _shortcutNewTabEnabledCheckBox = new();
    private readonly TextBox _shortcutNewTabTextBox = new();
    private readonly CheckBox _shortcutCloseTabEnabledCheckBox = new();
    private readonly TextBox _shortcutCloseTabTextBox = new();
    private readonly CheckBox _shortcutFocusAddressEnabledCheckBox = new();
    private readonly TextBox _shortcutFocusAddressTextBox = new();
    private readonly CheckBox _shortcutRefreshEnabledCheckBox = new();
    private readonly TextBox _shortcutRefreshTextBox = new();
    private readonly CheckBox _shortcutDevToolsEnabledCheckBox = new();
    private readonly TextBox _shortcutDevToolsTextBox = new();
    private readonly CheckBox _shortcutToggleBookmarksBarEnabledCheckBox = new();
    private readonly TextBox _shortcutToggleBookmarksBarTextBox = new();
    private readonly CheckBox _shortcutRestoreClosedTabEnabledCheckBox = new();
    private readonly TextBox _shortcutRestoreClosedTabTextBox = new();
    private readonly CheckBox _shortcutOpenHistoryEnabledCheckBox = new();
    private readonly TextBox _shortcutOpenHistoryTextBox = new();
    private readonly CheckBox _shortcutFindInPageEnabledCheckBox = new();
    private readonly TextBox _shortcutFindInPageTextBox = new();
    private readonly Button _shortcutResetDefaultsButton = new();
    private readonly Button _viewAllShortcutsButton = new();
    private readonly CheckBox _enforcePluginPermissionCheckBox = new();
    private readonly TextBox _allowedPermissionsTextBox = new();
    private readonly DataGridView _pluginGrid = new();
    private readonly BindingList<PluginRow> _pluginRows = [];
    private readonly Button _pluginInstallFolderButton = new();
    private readonly Button _pluginInstallZipButton = new();
    private readonly Button _pluginEnableButton = new();
    private readonly Button _pluginDisableButton = new();
    private readonly Button _pluginUninstallButton = new();
    private readonly Button _pluginRefreshButton = new();
    private readonly Label _bookmarkCountLabel = new();
    private readonly DataGridView _credentialGrid = new();
    private readonly BindingList<CredentialRow> _credentialRows = [];
    private readonly Button _credentialAddButton = new();
    private readonly Button _credentialEditButton = new();
    private readonly Button _credentialDeleteButton = new();
    private readonly Button _credentialCopyPasswordButton = new();
    private readonly Button _bookmarkManageButton = new();
    private readonly Button _bookmarkImportButton = new();
    private readonly Button _bookmarkExportButton = new();
    private readonly DataGridView _toolbarGrid = new();
    private readonly BindingList<ToolbarButtonRow> _toolbarRows = [];
    private readonly Button _toolbarMoveUpButton = new();
    private readonly Button _toolbarMoveDownButton = new();
    private readonly Button _toolbarResetButton = new();
    private readonly DataGridView _addressCommandGrid = new();
    private readonly BindingList<AddressCommandRow> _addressCommandRows = [];
    private readonly Button _addressCommandMoveUpButton = new();
    private readonly Button _addressCommandMoveDownButton = new();
    private readonly Button _addressCommandResetButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();
    private readonly TabControl _tabs = new();
    private readonly ListBox _categoryListBox = new();
    private readonly TextBox _settingSearchTextBox = new();
    private readonly ListBox _settingSearchResultList = new();
    private readonly Label _searchResultHintLabel = new();
    private readonly Label _sidebarSummaryLabel = new();
    private readonly Label _contentHeaderTitleLabel = new();
    private readonly Label _contentHeaderSubtitleLabel = new();
    private readonly Panel _sidebarCardPanel = new();
    private readonly Panel _contentCardPanel = new();
    private readonly Panel _tabsCardPanel = new();
    private readonly Panel _changeHintPanel = new();
    private readonly Label _changeHintLabel = new();
    private readonly List<SettingSearchItem> _settingSearchItems = [];
    private readonly SplitContainer _bodySplitContainer = new();
    private bool _isSyncingCategorySelection;
    private bool _isSyncingTabSelection;
    private bool _didSaveChanges;
    private bool _hasPendingChanges;
    public bool HasPendingChanges => _hasPendingChanges;
    private bool _suppressPendingChangeTracking = true;
    private Image? _taskbarIconPreviewImage;

    private BookmarksSettingsWindow? _bookmarksSettingsWindow;

    public SettingsWindow(
        BrowserConfig config,
        DownloadManager downloadManager,
        PluginManager pluginManager,
        CredentialStore credentialStore,
        BookmarkManager bookmarkManager,
        Action onBookmarksChanged,
        Action onConfigChanged,
        Action<string> log,
        Action? openShortcutsOverview = null)
    {
        _config = config;
        _downloadManager = downloadManager;
        _pluginManager = pluginManager;
        _credentialStore = credentialStore;
        _bookmarkManager = bookmarkManager;
        _onBookmarksChanged = onBookmarksChanged;
        _onConfigChanged = onConfigChanged;
        _log = log;
        _openShortcutsOverview = openShortcutsOverview;
        AppTheme.Configure(_config.UiTheme, _config.UiDensity, _config.UiAccent, _config.UiFont, _config.UiAccentStrength);

        Text = "设置";
        Width = 1080;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        WindowLayout.FitToWorkingArea(this);
        CancelButton = _cancelButton;

        BuildLayout();
        InitializeCategorySidebar();
        BuildSettingSearchIndex();
        ApplyVisualStyle();
        LoadFromConfig();
        RefreshPluginGrid();
        WireEvents();
        WirePendingChangeTracking(this);
        SetPendingChangeState(false, "当前配置已加载");
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 12),
            BackColor = AppTheme.Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _tabs.Dock = DockStyle.Fill;
        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(0, 1);
        _tabs.Padding = new Point(0, 0);

        _tabs.TabPages.Add(BuildGeneralPage());
        _tabs.TabPages.Add(BuildInterfacePage());
        _tabs.TabPages.Add(BuildSearchAndCredentialsPage());
        _tabs.TabPages.Add(BuildShortcutsPage());
        _tabs.TabPages.Add(BuildDownloadsPage());
        _tabs.TabPages.Add(BuildPrivacyPage());
        _tabs.TabPages.Add(BuildPluginsPage());
        _tabs.TabPages.Add(BuildBookmarksPage());
        _tabs.TabPages.Add(BuildAboutPage());

        var sidebar = BuildSidebarPanel();
        _sidebarCardPanel.Dock = DockStyle.Fill;
        _sidebarCardPanel.Padding = new Padding(6);
        _sidebarCardPanel.Controls.Add(sidebar);

        var contentShell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6)
        };
        contentShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var contentHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10, 9, 10, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        contentHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _contentHeaderTitleLabel.AutoSize = true;
        _contentHeaderTitleLabel.Font = AppTheme.CreateFont(10.3F, FontStyle.Bold);
        _contentHeaderSubtitleLabel.AutoSize = true;
        _contentHeaderSubtitleLabel.Margin = new Padding(0, 2, 0, 0);
        contentHeader.Controls.Add(_contentHeaderTitleLabel, 0, 0);
        contentHeader.Controls.Add(_contentHeaderSubtitleLabel, 0, 1);

        _tabsCardPanel.Dock = DockStyle.Fill;
        _tabsCardPanel.Padding = new Padding(8);
        _tabsCardPanel.Controls.Add(_tabs);

        var contentCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        contentCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentCardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contentCardLayout.Controls.Add(contentHeader, 0, 0);
        contentCardLayout.Controls.Add(_tabsCardPanel, 0, 1);

        _contentCardPanel.Dock = DockStyle.Fill;
        _contentCardPanel.Padding = new Padding(6);
        _contentCardPanel.Controls.Add(contentCardLayout);
        contentShell.Controls.Add(_contentCardPanel, 0, 0);
        contentShell.SetRowSpan(_contentCardPanel, 2);

        _bodySplitContainer.Dock = DockStyle.Fill;
        _bodySplitContainer.FixedPanel = FixedPanel.Panel1;
        _bodySplitContainer.Panel1.Controls.Add(_sidebarCardPanel);
        _bodySplitContainer.Panel2.Controls.Add(contentShell);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _changeHintPanel.Dock = DockStyle.Fill;
        _changeHintPanel.Margin = new Padding(0, 0, 8, 0);
        _changeHintPanel.Padding = new Padding(10, 4, 10, 4);
        _changeHintLabel.AutoSize = true;
        _changeHintLabel.Dock = DockStyle.Left;
        _changeHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        _changeHintPanel.Controls.Add(_changeHintLabel);

        var actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _saveButton.Text = "保存";
        _cancelButton.Text = "取消";
        actionButtons.Controls.Add(_saveButton);
        actionButtons.Controls.Add(_cancelButton);
        footer.Controls.Add(_changeHintPanel, 0, 0);
        footer.Controls.Add(actionButtons, 1, 0);

        root.Controls.Add(_bodySplitContainer, 0, 0);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);
        UpdateActiveCategoryHeader(0);
    }

    private Control BuildSidebarPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(8, 8, 8, 8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "设置搜索",
            Font = AppTheme.CreateFont(9.3F, FontStyle.Bold)
        }, 0, 0);

        _settingSearchTextBox.PlaceholderText = "搜索设置项、功能、快捷键";
        _settingSearchTextBox.Dock = DockStyle.Fill;
        panel.Controls.Add(_settingSearchTextBox, 0, 1);

        _searchResultHintLabel.AutoSize = true;
        _searchResultHintLabel.Text = "搜索结果";
        panel.Controls.Add(_searchResultHintLabel, 0, 2);

        _settingSearchResultList.Dock = DockStyle.Fill;
        _settingSearchResultList.HorizontalScrollbar = true;
        panel.Controls.Add(_settingSearchResultList, 0, 3);

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "分类导航",
            Font = AppTheme.CreateFont(9.3F, FontStyle.Bold)
        }, 0, 4);

        _categoryListBox.Dock = DockStyle.Fill;
        _categoryListBox.IntegralHeight = false;
        panel.Controls.Add(_categoryListBox, 0, 5);

        _sidebarSummaryLabel.AutoSize = true;
        _sidebarSummaryLabel.Margin = new Padding(0, 8, 0, 0);
        panel.Controls.Add(_sidebarSummaryLabel, 0, 6);

        return panel;
    }

    private void InitializeCategorySidebar()
    {
        _categoryListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _categoryListBox.ItemHeight = 36;
        _settingSearchResultList.DrawMode = DrawMode.OwnerDrawFixed;
        _settingSearchResultList.ItemHeight = 30;
        _categoryListBox.Items.Clear();
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            _categoryListBox.Items.Add(_tabs.TabPages[i].Text);
        }

        if (_categoryListBox.Items.Count > 0)
        {
            _categoryListBox.SelectedIndex = 0;
        }
    }

    private TabPage BuildGeneralPage()
    {
        var page = new TabPage("常规");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12),
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // 中文注释：为每一行设置 AutoSize，避免高 DPI 下行高被错误压缩导致控件错位。
        for (var i = 0; i < 16; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Text = "启动与资源策略",
            AutoSize = true,
            Font = AppTheme.CreateFont(10.2F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        panel.Controls.Add(titleLabel, 0, 0);
        panel.SetColumnSpan(titleLabel, 3);

        panel.Controls.Add(new Label { Text = "启动页", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        panel.Controls.Add(_startPageTextBox, 1, 1);
        _resetStartPageButton.Text = "恢复空白页";
        _resetStartPageButton.AutoSize = true;
        panel.Controls.Add(_resetStartPageButton, 2, 1);

        panel.Controls.Add(new Label { Text = "最大活跃标签页", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _maxLiveTabsNumber.Minimum = 1;
        _maxLiveTabsNumber.Maximum = 64;
        panel.Controls.Add(_maxLiveTabsNumber, 1, 2);

        panel.Controls.Add(new Label { Text = "空闲休眠分钟", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        _suspendMinutesNumber.Minimum = 1;
        _suspendMinutesNumber.Maximum = 120;
        panel.Controls.Add(_suspendMinutesNumber, 1, 3);

        panel.Controls.Add(new Label { Text = "界面主题", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        _uiThemeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _uiThemeComboBox.Items.AddRange(["浅色", "深色", "跟随系统"]);
        panel.Controls.Add(_uiThemeComboBox, 1, 4);

        panel.Controls.Add(new Label { Text = "界面强调色", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        _uiAccentComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _uiAccentComboBox.Items.AddRange(["蓝色", "青绿", "琥珀", "玫红"]);
        panel.Controls.Add(_uiAccentComboBox, 1, 5);

        panel.Controls.Add(new Label { Text = "强调强度", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        _uiAccentStrengthComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _uiAccentStrengthComboBox.Items.AddRange(["柔和", "标准", "鲜明"]);
        panel.Controls.Add(_uiAccentStrengthComboBox, 1, 6);

        panel.Controls.Add(new Label { Text = "界面字体", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        _uiFontComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _uiFontComboBox.Items.AddRange(["雅黑（默认）", "等线（现代）", "宋体（传统）"]);
        panel.Controls.Add(_uiFontComboBox, 1, 7);

        panel.Controls.Add(new Label { Text = "界面密度", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        _uiDensityComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _uiDensityComboBox.Items.AddRange(["标准", "紧凑"]);
        panel.Controls.Add(_uiDensityComboBox, 1, 8);

        panel.Controls.Add(new Label { Text = "任务栏图标", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        _taskbarIconPathTextBox.PlaceholderText = "留空使用内置图标；支持 .ico/.png/.jpg/.bmp";
        panel.Controls.Add(_taskbarIconPathTextBox, 1, 9);
        var taskbarIconButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        _taskbarIconBrowseButton.Text = "选择图标...";
        _taskbarIconResetButton.Text = "恢复默认";
        taskbarIconButtons.Controls.Add(_taskbarIconBrowseButton);
        taskbarIconButtons.Controls.Add(_taskbarIconResetButton);
        panel.Controls.Add(taskbarIconButtons, 2, 9);

        _taskbarIconPreviewBox.Size = new Size(36, 36);
        _taskbarIconPreviewBox.SizeMode = PictureBoxSizeMode.Zoom;
        _taskbarIconPreviewBox.BorderStyle = BorderStyle.FixedSingle;
        _taskbarIconPreviewBox.Margin = new Padding(0, 4, 8, 0);
        _taskbarIconPreviewBox.BackColor = AppTheme.InputBackground;

        _taskbarIconPreviewLabel.AutoSize = true;
        _taskbarIconPreviewLabel.Text = "预览：内置默认图标";
        _taskbarIconPreviewLabel.Anchor = AnchorStyles.Left;
        _taskbarIconPreviewLabel.Margin = new Padding(0, 12, 0, 0);

        var taskbarIconPreviewPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        taskbarIconPreviewPanel.Controls.Add(_taskbarIconPreviewBox);
        taskbarIconPreviewPanel.Controls.Add(_taskbarIconPreviewLabel);
        panel.Controls.Add(taskbarIconPreviewPanel, 1, 10);
        panel.SetColumnSpan(taskbarIconPreviewPanel, 2);

        var iconHintLabel = new Label
        {
            AutoSize = true,
            Text = "保存后任务栏图标即时生效；建议使用 32x32 或 64x64。",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 2, 0, 0)
        };
        panel.Controls.Add(iconHintLabel, 1, 11);
        panel.SetColumnSpan(iconHintLabel, 2);

        _restoreLastSessionCheckBox.Text = "启动时恢复上次会话（无痕窗口除外）";
        _restoreLastSessionCheckBox.AutoSize = true;
        panel.Controls.Add(_restoreLastSessionCheckBox, 1, 12);

        _askBeforeRestoringSessionCheckBox.Text = "启动时先询问：恢复上次会话或打开空白页";
        _askBeforeRestoringSessionCheckBox.AutoSize = true;
        _askBeforeRestoringSessionCheckBox.Visible = false;
        panel.Controls.Add(_askBeforeRestoringSessionCheckBox, 1, 13);

        _showBookmarksBarCheckBox.Text = "显示书签栏（第二行）";
        _showBookmarksBarCheckBox.AutoSize = true;
        panel.Controls.Add(_showBookmarksBarCheckBox, 1, 14);

        var hintLabel = new Label
        {
            AutoSize = true,
            Text = "建议启动页保持为 about:blank，可降低冷启动时的网络与页面资源占用。",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 10, 0, 0)
        };
        panel.Controls.Add(hintLabel, 1, 15);
        panel.SetColumnSpan(hintLabel, 2);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildInterfacePage()
    {
        var page = new TabPage("界面");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "顶部工具栏按钮：可设置显示/隐藏并调整顺序，地址栏固定显示。"
        }, 0, 0);

        _toolbarGrid.Dock = DockStyle.Fill;
        _toolbarGrid.AutoGenerateColumns = false;
        _toolbarGrid.AllowUserToAddRows = false;
        _toolbarGrid.AllowUserToDeleteRows = false;
        _toolbarGrid.RowHeadersVisible = false;
        _toolbarGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _toolbarGrid.MultiSelect = false;
        _toolbarGrid.DataSource = _toolbarRows;
        _toolbarGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ToolbarButtonRow.Visible),
            HeaderText = "显示",
            Width = 70
        });
        _toolbarGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ToolbarButtonRow.DisplayName),
            HeaderText = "按钮",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _toolbarGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ToolbarButtonRow.OrderDisplay),
            HeaderText = "顺序",
            Width = 90
        });
        panel.Controls.Add(_toolbarGrid, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _toolbarMoveUpButton.Text = "上移";
        _toolbarMoveDownButton.Text = "下移";
        _toolbarResetButton.Text = "恢复默认工具栏";
        buttons.Controls.Add(_toolbarMoveUpButton);
        buttons.Controls.Add(_toolbarMoveDownButton);
        buttons.Controls.Add(_toolbarResetButton);
        panel.Controls.Add(buttons, 0, 2);

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "地址栏快捷指令：可启用/禁用、修改指令词、调整顺序（输入 :指令词 执行）。",
            Margin = new Padding(0, 12, 0, 0)
        }, 0, 3);

        _addressCommandGrid.Dock = DockStyle.Fill;
        _addressCommandGrid.AutoGenerateColumns = false;
        _addressCommandGrid.AllowUserToAddRows = false;
        _addressCommandGrid.AllowUserToDeleteRows = false;
        _addressCommandGrid.RowHeadersVisible = false;
        _addressCommandGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _addressCommandGrid.MultiSelect = false;
        _addressCommandGrid.DataSource = _addressCommandRows;
        _addressCommandGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(AddressCommandRow.Enabled),
            HeaderText = "启用",
            Width = 70
        });
        _addressCommandGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AddressCommandRow.DisplayName),
            HeaderText = "功能",
            ReadOnly = true,
            Width = 170
        });
        _addressCommandGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AddressCommandRow.Command),
            HeaderText = "指令词（不含冒号）",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _addressCommandGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AddressCommandRow.OrderDisplay),
            HeaderText = "顺序",
            Width = 80,
            ReadOnly = true
        });
        panel.Controls.Add(_addressCommandGrid, 0, 4);

        var commandButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _addressCommandMoveUpButton.Text = "上移";
        _addressCommandMoveDownButton.Text = "下移";
        _addressCommandResetButton.Text = "恢复默认指令";
        commandButtons.Controls.Add(_addressCommandMoveUpButton);
        commandButtons.Controls.Add(_addressCommandMoveDownButton);
        commandButtons.Controls.Add(_addressCommandResetButton);
        panel.Controls.Add(commandButtons, 0, 5);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildSearchAndCredentialsPage()
    {
        var page = new TabPage("搜索与账号");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoSize = true
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        searchRow.Controls.Add(new Label { Text = "默认搜索引擎", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _searchEngineComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _searchEngineComboBox.Width = 160;
        _searchEngineComboBox.Items.AddRange(SearchEngineCatalog.BuiltIns.Select(preset => preset.Name).Cast<object>().ToArray());
        _searchEngineComboBox.Items.Add("自定义");
        searchRow.Controls.Add(_searchEngineComboBox, 1, 0);
        searchRow.Controls.Add(new Label { Text = "模板", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _searchEngineTemplateTextBox.Dock = DockStyle.Fill;
        searchRow.Controls.Add(_searchEngineTemplateTextBox, 3, 0);

        panel.Controls.Add(searchRow, 0, 0);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "模板格式示例：https://www.bing.com/search?q={query}（必须包含 {query}）",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 4, 0, 10)
        }, 0, 1);

        _enableAddressBarSuggestionsCheckBox.Text = "地址栏启用输入建议（来自历史、书签和标签）";
        _enableAddressBarSuggestionsCheckBox.AutoSize = true;
        _enableAddressBarSuggestionsCheckBox.Margin = new Padding(0, 0, 0, 10);
        panel.Controls.Add(_enableAddressBarSuggestionsCheckBox, 0, 2);

        // 中文注释：自定义搜索引擎表格，支持直接在单元格内编辑，底部空行新增。
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "自定义搜索引擎（关键字可在地址栏前缀触发，如输入 “gh 关键词”）",
            Font = AppTheme.CreateFont(9.2F, FontStyle.Bold)
        }, 0, 3);

        _customSearchEngineGrid.Dock = DockStyle.Fill;
        _customSearchEngineGrid.AutoGenerateColumns = false;
        _customSearchEngineGrid.AllowUserToAddRows = true;
        _customSearchEngineGrid.AllowUserToDeleteRows = true;
        _customSearchEngineGrid.RowHeadersVisible = false;
        _customSearchEngineGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _customSearchEngineGrid.MultiSelect = false;
        _customSearchEngineGrid.DataSource = _customSearchEngineRows;
        _customSearchEngineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CustomSearchEngineRow.Name),
            HeaderText = "名称",
            Width = 140
        });
        _customSearchEngineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CustomSearchEngineRow.Keyword),
            HeaderText = "关键字",
            Width = 100
        });
        _customSearchEngineGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CustomSearchEngineRow.Template),
            HeaderText = "模板（必须包含 {query}）",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        panel.Controls.Add(_customSearchEngineGrid, 0, 4);

        var customEngineButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _customSearchEngineDeleteButton.Text = "删除选中引擎";
        _customSearchEngineDeleteButton.Click += (_, _) =>
        {
            if (_customSearchEngineGrid.CurrentRow is { IsNewRow: false } currentRow &&
                currentRow.DataBoundItem is CustomSearchEngineRow row)
            {
                _customSearchEngineRows.Remove(row);
                MarkPendingChange("已删除搜索引擎配置");
            }
        };
        customEngineButtons.Controls.Add(_customSearchEngineDeleteButton);
        customEngineButtons.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "示例：名称 GitHub、关键字 gh、模板 https://github.com/search?q={query}",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(10, 6, 0, 0)
        });
        panel.Controls.Add(customEngineButtons, 0, 5);

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "网站账号密码记录（本机当前 Windows 用户加密保存）",
            Font = AppTheme.CreateFont(9.2F, FontStyle.Bold)
        }, 0, 6);

        _credentialGrid.Dock = DockStyle.Fill;
        _credentialGrid.AutoGenerateColumns = false;
        _credentialGrid.ReadOnly = true;
        _credentialGrid.AllowUserToAddRows = false;
        _credentialGrid.AllowUserToDeleteRows = false;
        _credentialGrid.RowHeadersVisible = false;
        _credentialGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _credentialGrid.MultiSelect = false;
        _credentialGrid.DataSource = _credentialRows;
        _credentialGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CredentialRow.Site),
            HeaderText = "站点",
            Width = 260
        });
        _credentialGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CredentialRow.Username),
            HeaderText = "用户名",
            Width = 220
        });
        _credentialGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CredentialRow.PasswordMasked),
            HeaderText = "密码",
            Width = 140
        });
        _credentialGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CredentialRow.UpdatedAt),
            HeaderText = "更新时间",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        panel.Controls.Add(_credentialGrid, 0, 7);

        var credentialButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _credentialAddButton.Text = "新增账号";
        _credentialEditButton.Text = "编辑账号";
        _credentialDeleteButton.Text = "删除账号";
        _credentialCopyPasswordButton.Text = "复制密码";
        credentialButtons.Controls.Add(_credentialAddButton);
        credentialButtons.Controls.Add(_credentialEditButton);
        credentialButtons.Controls.Add(_credentialDeleteButton);
        credentialButtons.Controls.Add(_credentialCopyPasswordButton);
        panel.Controls.Add(credentialButtons, 0, 8);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildShortcutsPage()
    {
        var page = new TabPage("快捷键");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12),
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddShortcutRow(panel, 0, "收藏当前页", _shortcutAddBookmarkEnabledCheckBox, _shortcutAddBookmarkTextBox);
        AddShortcutRow(panel, 1, "新建标签页", _shortcutNewTabEnabledCheckBox, _shortcutNewTabTextBox);
        AddShortcutRow(panel, 2, "关闭当前标签页", _shortcutCloseTabEnabledCheckBox, _shortcutCloseTabTextBox);
        AddShortcutRow(panel, 3, "聚焦地址栏", _shortcutFocusAddressEnabledCheckBox, _shortcutFocusAddressTextBox);
        AddShortcutRow(panel, 4, "刷新页面", _shortcutRefreshEnabledCheckBox, _shortcutRefreshTextBox);
        AddShortcutRow(panel, 5, "打开开发者工具", _shortcutDevToolsEnabledCheckBox, _shortcutDevToolsTextBox);
        AddShortcutRow(panel, 6, "隐藏/显示书签栏", _shortcutToggleBookmarksBarEnabledCheckBox, _shortcutToggleBookmarksBarTextBox);
        AddShortcutRow(panel, 7, "恢复最近关闭标签", _shortcutRestoreClosedTabEnabledCheckBox, _shortcutRestoreClosedTabTextBox);
        AddShortcutRow(panel, 8, "打开历史记录页", _shortcutOpenHistoryEnabledCheckBox, _shortcutOpenHistoryTextBox);
        AddShortcutRow(panel, 9, "页内查找", _shortcutFindInPageEnabledCheckBox, _shortcutFindInPageTextBox);

        panel.Controls.Add(
            new Label
            {
                AutoSize = true,
                Text = "格式示例：Ctrl+D、Ctrl+Shift+L、F12",
                ForeColor = AppTheme.TextSecondary,
                Margin = new Padding(0, 12, 0, 0)
            }, 2, 10);

        _shortcutResetDefaultsButton.Text = "恢复默认快捷键";
        _shortcutResetDefaultsButton.Anchor = AnchorStyles.Left;
        _shortcutResetDefaultsButton.Margin = new Padding(0, 10, 0, 0);
        panel.Controls.Add(_shortcutResetDefaultsButton, 2, 11);

        _viewAllShortcutsButton.Text = "查看完整快捷键一览…";
        _viewAllShortcutsButton.Anchor = AnchorStyles.Left;
        _viewAllShortcutsButton.Margin = new Padding(0, 10, 0, 0);
        _viewAllShortcutsButton.Click += (_, _) =>
        {
            if (_openShortcutsOverview is not null)
            {
                _openShortcutsOverview.Invoke();
            }
            else
            {
                MessageBox.Show(this, "快捷键概览当前不可用。", "快捷键", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        panel.Controls.Add(_viewAllShortcutsButton, 2, 12);

        page.Controls.Add(panel);
        return page;
    }

    private static void AddShortcutRow(
        TableLayoutPanel panel,
        int row,
        string actionName,
        CheckBox enabledCheckBox,
        TextBox shortcutTextBox)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = actionName,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 9, 8, 0)
        }, 0, row);

        enabledCheckBox.Text = "启用";
        enabledCheckBox.AutoSize = true;
        enabledCheckBox.Anchor = AnchorStyles.Left;
        enabledCheckBox.Margin = new Padding(0, 6, 0, 0);
        panel.Controls.Add(enabledCheckBox, 1, row);

        shortcutTextBox.Width = 220;
        shortcutTextBox.Anchor = AnchorStyles.Left;
        shortcutTextBox.Margin = new Padding(0, 4, 0, 0);
        panel.Controls.Add(shortcutTextBox, 2, row);
    }

    private TabPage BuildDownloadsPage()
    {
        var page = new TabPage("下载");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12),
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label { Text = "下载目录", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _downloadDirectoryTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        panel.Controls.Add(_downloadDirectoryTextBox, 1, 0);
        _downloadBrowseButton.Text = "浏览...";
        _downloadBrowseButton.AutoSize = true;
        panel.Controls.Add(_downloadBrowseButton, 2, 0);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildPrivacyPage()
    {
        var page = new TabPage("隐私与登录");
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        _autofillCheckBox.Text = "启用自动填充";
        _autofillCheckBox.AutoSize = true;
        _credentialAutosaveCheckBox.Text = "登录后自动保存密码到本机账号库（不弹窗，推荐）";
        _credentialAutosaveCheckBox.AutoSize = true;
        _credentialAutofillCheckBox.Text = "访问登录页时自动填充已保存账号密码";
        _credentialAutofillCheckBox.AutoSize = true;
        _passwordAutosaveCheckBox.Text = "WebView2 内核密码保存（会弹出内核保存确认框，点保存时可能卡顿，不推荐）";
        _passwordAutosaveCheckBox.AutoSize = true;
        panel.Controls.Add(_autofillCheckBox);
        panel.Controls.Add(_credentialAutosaveCheckBox);
        panel.Controls.Add(_credentialAutofillCheckBox);
        panel.Controls.Add(_passwordAutosaveCheckBox);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "说明：开启「登录后自动保存密码」后，登录表单提交时自动把账号密码存入本机账号库（当前 Windows 用户加密），\n下次访问自动填充，不再弹出内核保存气泡；下方规则可按域名或 IP 单独允许/禁止，未列出的站点按全局开关处理。"
        });

        // 中文注释：自动保存站点规则表格（域名/IP + 允许/不保存），底部空行新增、行尾 Delete 删除。
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "自动保存站点规则（每行一条，示例：example.com、192.168.1.1、192.168.1.1:8080）",
            Margin = new Padding(0, 12, 0, 4)
        });
        _credentialAutosaveRuleGrid.Width = 460;
        _credentialAutosaveRuleGrid.Height = 120;
        _credentialAutosaveRuleGrid.Anchor = AnchorStyles.Left;
        _credentialAutosaveRuleGrid.AutoGenerateColumns = false;
        _credentialAutosaveRuleGrid.AllowUserToAddRows = true;
        _credentialAutosaveRuleGrid.AllowUserToDeleteRows = true;
        _credentialAutosaveRuleGrid.RowHeadersVisible = false;
        _credentialAutosaveRuleGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _credentialAutosaveRuleGrid.MultiSelect = false;
        _credentialAutosaveRuleGrid.DataSource = _credentialAutosaveRuleRows;
        _credentialAutosaveRuleGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CredentialAutosaveRuleRow.Host),
            HeaderText = "站点或 IP",
            Width = 240
        });
        _credentialAutosaveRuleGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(CredentialAutosaveRuleRow.Mode),
            HeaderText = "策略",
            Width = 120,
            DataSource = new[] { "允许保存", "不保存" }
        });
        panel.Controls.Add(_credentialAutosaveRuleGrid);

        var credentialAutosaveRuleButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        _credentialAutosaveRuleDeleteButton.Text = "删除选中规则";
        _credentialAutosaveRuleDeleteButton.Click += (_, _) =>
        {
            if (_credentialAutosaveRuleGrid.CurrentRow is { IsNewRow: false } currentRow &&
                currentRow.DataBoundItem is CredentialAutosaveRuleRow row)
            {
                _credentialAutosaveRuleRows.Remove(row);
                MarkPendingChange("已删除自动保存站点规则");
            }
        };
        credentialAutosaveRuleButtons.Controls.Add(_credentialAutosaveRuleDeleteButton);
        credentialAutosaveRuleButtons.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "策略为「允许保存」的站点强制保存，「不保存」的站点禁止保存；其余站点跟随全局开关",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(10, 6, 0, 0)
        });
        panel.Controls.Add(credentialAutosaveRuleButtons);

        // 中文注释：简版广告过滤开关与自定义屏蔽域名。
        _adBlockEnabledCheckBox.Text = "启用广告过滤（拦截常见广告/统计域名）";
        _adBlockEnabledCheckBox.AutoSize = true;
        _adBlockEnabledCheckBox.Margin = new Padding(3, 14, 3, 3);
        panel.Controls.Add(_adBlockEnabledCheckBox);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "自定义屏蔽域名（每行一个，示例：ads.example.com）"
        });
        _adBlockCustomDomainsTextBox.Multiline = true;
        _adBlockCustomDomainsTextBox.ScrollBars = ScrollBars.Vertical;
        _adBlockCustomDomainsTextBox.Width = 380;
        _adBlockCustomDomainsTextBox.Height = 96;
        panel.Controls.Add(_adBlockCustomDomainsTextBox);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildPluginsPage()
    {
        var page = new TabPage("插件");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _enforcePluginPermissionCheckBox.Text = "启用插件权限白名单";
        _enforcePluginPermissionCheckBox.AutoSize = true;
        panel.Controls.Add(_enforcePluginPermissionCheckBox, 0, 0);

        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "允许权限（每行一个，示例：content:dom_read、content:inject）"
        }, 0, 1);

        _allowedPermissionsTextBox.Multiline = true;
        _allowedPermissionsTextBox.ScrollBars = ScrollBars.Vertical;
        _allowedPermissionsTextBox.Height = 90;
        _allowedPermissionsTextBox.Dock = DockStyle.Fill;
        panel.Controls.Add(_allowedPermissionsTextBox, 0, 2);

        _pluginGrid.Dock = DockStyle.Fill;
        _pluginGrid.AutoGenerateColumns = false;
        _pluginGrid.ReadOnly = true;
        _pluginGrid.AllowUserToAddRows = false;
        _pluginGrid.AllowUserToDeleteRows = false;
        _pluginGrid.RowHeadersVisible = false;
        _pluginGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _pluginGrid.MultiSelect = false;
        _pluginGrid.DataSource = _pluginRows;
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PluginRow.Name),
            HeaderText = "插件名",
            Width = 180
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PluginRow.Id),
            HeaderText = "ID",
            Width = 150
        });
        _pluginGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(PluginRow.Enabled),
            HeaderText = "启用",
            Width = 60
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PluginRow.Status),
            HeaderText = "状态",
            Width = 180
        });
        _pluginGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PluginRow.DirectoryPath),
            HeaderText = "目录",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        panel.Controls.Add(_pluginGrid, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _pluginInstallFolderButton.Text = "安装(目录)";
        _pluginInstallZipButton.Text = "安装(zip)";
        _pluginEnableButton.Text = "启用";
        _pluginDisableButton.Text = "禁用";
        _pluginUninstallButton.Text = "卸载";
        _pluginRefreshButton.Text = "刷新列表";
        buttons.Controls.Add(_pluginInstallFolderButton);
        buttons.Controls.Add(_pluginInstallZipButton);
        buttons.Controls.Add(_pluginEnableButton);
        buttons.Controls.Add(_pluginDisableButton);
        buttons.Controls.Add(_pluginUninstallButton);
        buttons.Controls.Add(_pluginRefreshButton);
        panel.Controls.Add(buttons, 0, 4);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildBookmarksPage()
    {
        var page = new TabPage("书签");
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        _bookmarkCountLabel.AutoSize = true;

        _bookmarkManageButton.Text = "打开书签管理";
        _bookmarkImportButton.Text = "导入书签";
        _bookmarkExportButton.Text = "导出书签";

        panel.Controls.Add(_bookmarkCountLabel);
        panel.Controls.Add(_bookmarkManageButton);
        panel.Controls.Add(_bookmarkImportButton);
        panel.Controls.Add(_bookmarkExportButton);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildAboutPage()
    {
        var page = new TabPage("关于");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(14),
            AutoScroll = true
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4)
        };
        var appTitle = new Label
        {
            Text = AppVersionInfo.ProductName,
            Font = AppTheme.CreateFont(14F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0)
        };
        var versionBadge = new Label
        {
            Text = $"v{AppVersionInfo.CurrentVersion}",
            Font = AppTheme.CreateMonoFont(9F, FontStyle.Bold),
            BackColor = AppTheme.AccentMuted,
            ForeColor = AppTheme.Accent,
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 2, 0, 0)
        };
        titleRow.Controls.Add(appTitle);
        titleRow.Controls.Add(versionBadge);

        var sloganLabel = new Label
        {
            Text = $"{AppVersionInfo.Slogan} · .NET {Environment.Version.Major}.{Environment.Version.Minor} · Windows {(Environment.Is64BitProcess ? "x64" : "x86")}",
            Font = AppTheme.CreateFont(9F),
            ForeColor = AppTheme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        var historyHeader = new Label
        {
            Text = "版本更新记录与特性演进：",
            Font = AppTheme.CreateFont(9.8F, FontStyle.Bold),
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };

        var changelogBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = AppTheme.SurfaceAlt,
            ForeColor = AppTheme.TextPrimary,
            Font = AppTheme.CreateFont(9F),
            Margin = new Padding(0, 4, 0, 8)
        };

        foreach (var release in AppVersionInfo.ReleaseHistory)
        {
            var isCurrent = string.Equals(release.Version, AppVersionInfo.CurrentVersion, StringComparison.OrdinalIgnoreCase);
            changelogBox.SelectionFont = AppTheme.CreateFont(9.8F, FontStyle.Bold);
            changelogBox.SelectionColor = isCurrent ? AppTheme.Accent : AppTheme.TextPrimary;
            changelogBox.AppendText($"v{release.Version} ({release.ReleaseDate}) - {release.Title}");
            if (isCurrent)
            {
                changelogBox.SelectionFont = AppTheme.CreateFont(8.5F, FontStyle.Bold);
                changelogBox.SelectionColor = AppTheme.Success;
                changelogBox.AppendText("  [当前版本]");
            }
            changelogBox.AppendText(Environment.NewLine);

            changelogBox.SelectionFont = AppTheme.CreateFont(9F);
            changelogBox.SelectionColor = AppTheme.TextPrimary;
            foreach (var highlight in release.Highlights)
            {
                changelogBox.AppendText($"  • {highlight}{Environment.NewLine}");
            }
            changelogBox.AppendText(Environment.NewLine);
        }
        changelogBox.Select(0, 0);

        var actionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0)
        };

        var openReleaseButton = new Button { Text = "打开发布目录 (release/)", AutoSize = true };
        openReleaseButton.Click += (_, _) =>
        {
            try
            {
                var target = Path.Combine(Environment.CurrentDirectory, "release");
                Directory.CreateDirectory(target);
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法打开发布目录: {ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        AppTheme.ApplyPrimaryButton(openReleaseButton);

        var openRepoButton = new Button { Text = "访问项目仓库", AutoSize = true };
        openRepoButton.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = AppVersionInfo.RepositoryUrl, UseShellExecute = true }); } catch { }
        };
        AppTheme.ApplyPrimaryButton(openRepoButton);

        actionRow.Controls.Add(openReleaseButton);
        actionRow.Controls.Add(openRepoButton);

        panel.Controls.Add(titleRow, 0, 0);
        panel.Controls.Add(sloganLabel, 0, 1);
        panel.Controls.Add(historyHeader, 0, 2);
        panel.Controls.Add(changelogBox, 0, 3);
        panel.Controls.Add(actionRow, 0, 4);

        page.Controls.Add(panel);
        return page;
    }

    private void ApplyVisualStyle()
    {
        BackColor = AppTheme.WindowBackground;
        _bodySplitContainer.BackColor = AppTheme.PanelBorder;
        _bodySplitContainer.Panel1.BackColor = AppTheme.Surface;
        _bodySplitContainer.Panel2.BackColor = AppTheme.Surface;
        _bodySplitContainer.SplitterWidth = 2;
        ApplyCardVisual(_sidebarCardPanel);
        ApplyCardVisual(_contentCardPanel);
        ApplyCardVisual(_tabsCardPanel);
        StyleControls(this);
        AppTheme.ApplyPrimaryButton(_saveButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_cancelButton);
        _customSearchEngineDeleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        _credentialDeleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        _credentialAutosaveRuleDeleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        _pluginUninstallButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        _shortcutResetDefaultsButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.25F);
        _toolbarResetButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.25F);
        _addressCommandResetButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.25F);
        AppTheme.ApplyControlRegion(_changeHintPanel, 6);
        _contentHeaderTitleLabel.ForeColor = AppTheme.TextPrimary;
        _contentHeaderSubtitleLabel.ForeColor = AppTheme.TextSecondary;
        _sidebarSummaryLabel.ForeColor = AppTheme.TextSecondary;
        _sidebarSummaryLabel.Font = AppTheme.CreateFont(8.8F, FontStyle.Bold);
        _changeHintLabel.Font = AppTheme.CreateFont(8.7F, FontStyle.Bold);
        SetPendingChangeState(_hasPendingChanges, _hasPendingChanges ? "有更改尚未保存" : "当前配置已同步");
    }

    private static void StyleControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                AppTheme.ApplyPrimaryButton(button);
                AppTheme.ApplyControlRegion(button, 6);
            }
            else if (control is TextBox textBox)
            {
                AppTheme.ApplyTextInput(textBox);
            }
            else if (control is NumericUpDown numeric)
            {
                AppTheme.ApplyNumericInput(numeric);
            }
            else if (control is ComboBox comboBox)
            {
                AppTheme.ApplyComboBox(comboBox);
            }
            else if (control is TabControl tabs)
            {
                tabs.Font = AppTheme.CreateFont(9.2F, FontStyle.Bold);
            }
            else if (control is TabPage page)
            {
                page.BackColor = AppTheme.Surface;
            }
            else if (control is ListBox listBox)
            {
                AppTheme.ApplyListBox(listBox);
            }
            else if (control is DataGridView grid)
            {
                AppTheme.ApplyDataGrid(grid);
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.Font = AppTheme.CreateFont(checkBox.Font.Size, checkBox.Font.Style);
                checkBox.ForeColor = AppTheme.TextPrimary;
                checkBox.Margin = new Padding(0, 6, 0, 2);
            }
            else if (control is Label label)
            {
                label.Font = AppTheme.CreateFont(label.Font.Size, label.Font.Style);
                label.ForeColor = AppTheme.TextSecondary;
                label.Margin = new Padding(0, 8, 6, 4);
            }
            else if (control is FlowLayoutPanel flowLayout)
            {
                flowLayout.Padding = flowLayout.Padding == Padding.Empty ? new Padding(0, 2, 0, 0) : flowLayout.Padding;
                flowLayout.Margin = new Padding(0, 4, 0, 0);
            }
            else if (control is SplitContainer splitContainer)
            {
                splitContainer.BackColor = AppTheme.PanelBorder;
            }

            StyleControls(control);
        }
    }

    private void LoadFromConfig()
    {
        _startPageTextBox.Text = _config.StartPage;
        _maxLiveTabsNumber.Value = Math.Min(_maxLiveTabsNumber.Maximum, Math.Max(_maxLiveTabsNumber.Minimum, _config.MaxLiveTabs));
        _suspendMinutesNumber.Value = Math.Min(_suspendMinutesNumber.Maximum, Math.Max(_suspendMinutesNumber.Minimum, _config.SuspendAfterMinutes));
        _downloadDirectoryTextBox.Text = _downloadManager.DownloadDirectory;
        _autofillCheckBox.Checked = _config.IsGeneralAutofillEnabled;
        _passwordAutosaveCheckBox.Checked = _config.IsPasswordAutosaveEnabled;
        _credentialAutofillCheckBox.Checked = _config.IsCredentialAutofillEnabled;
        _credentialAutosaveCheckBox.Checked = _config.IsCredentialAutosaveEnabled;
        _adBlockEnabledCheckBox.Checked = _config.AdBlockEnabled;
        _adBlockCustomDomainsTextBox.Text = string.Join(Environment.NewLine, _config.AdBlockCustomDomains);
        _searchEngineTemplateTextBox.Text = _config.DefaultSearchEngineUrlTemplate;
        SelectSearchEnginePreset();
        _customSearchEngineRows.Clear();
        foreach (var engine in _config.CustomSearchEngines)
        {
            _customSearchEngineRows.Add(new CustomSearchEngineRow
            {
                Name = engine.Name,
                Keyword = engine.Keyword,
                Template = engine.Template
            });
        }
        _restoreLastSessionCheckBox.Checked = _config.RestoreLastSessionOnStartup;
        _askBeforeRestoringSessionCheckBox.Checked = _config.AskBeforeRestoringSessionOnStartup;
        _enableAddressBarSuggestionsCheckBox.Checked = _config.EnableAddressBarSuggestions;
        _showBookmarksBarCheckBox.Checked = _config.ShowBookmarksBar;
        _taskbarIconPathTextBox.Text = _config.TaskbarIconPath;
        UpdateTaskbarIconPreview();
        _uiThemeComboBox.SelectedItem = _config.UiTheme switch
        {
            var theme when string.Equals(theme, ThemeOptionNames.ThemeSystem, StringComparison.OrdinalIgnoreCase) => "跟随系统",
            var theme when string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) => "深色",
            _ => "浅色"
        };
        _uiAccentComboBox.SelectedItem = _config.UiAccent switch
        {
            "Green" => "青绿",
            "Orange" => "琥珀",
            "Rose" => "玫红",
            _ => "蓝色"
        };
        _uiAccentStrengthComboBox.SelectedItem = _config.UiAccentStrength switch
        {
            "Soft" => "柔和",
            "Vivid" => "鲜明",
            _ => "标准"
        };
        _uiFontComboBox.SelectedItem = _config.UiFont switch
        {
            "DengXian" => "等线（现代）",
            "Song" => "宋体（传统）",
            _ => "雅黑（默认）"
        };
        _uiDensityComboBox.SelectedItem = string.Equals(_config.UiDensity, "Compact", StringComparison.OrdinalIgnoreCase) ? "紧凑" : "标准";
        _shortcutAddBookmarkEnabledCheckBox.Checked = _config.ShortcutAddBookmarkEnabled;
        _shortcutAddBookmarkTextBox.Text = _config.ShortcutAddBookmark;
        _shortcutNewTabEnabledCheckBox.Checked = _config.ShortcutNewTabEnabled;
        _shortcutNewTabTextBox.Text = _config.ShortcutNewTab;
        _shortcutCloseTabEnabledCheckBox.Checked = _config.ShortcutCloseTabEnabled;
        _shortcutCloseTabTextBox.Text = _config.ShortcutCloseTab;
        _shortcutFocusAddressEnabledCheckBox.Checked = _config.ShortcutFocusAddressEnabled;
        _shortcutFocusAddressTextBox.Text = _config.ShortcutFocusAddress;
        _shortcutRefreshEnabledCheckBox.Checked = _config.ShortcutRefreshEnabled;
        _shortcutRefreshTextBox.Text = _config.ShortcutRefresh;
        _shortcutDevToolsEnabledCheckBox.Checked = _config.ShortcutDevToolsEnabled;
        _shortcutDevToolsTextBox.Text = _config.ShortcutDevTools;
        _shortcutToggleBookmarksBarEnabledCheckBox.Checked = _config.ShortcutToggleBookmarksBarEnabled;
        _shortcutToggleBookmarksBarTextBox.Text = _config.ShortcutToggleBookmarksBar;
        _shortcutRestoreClosedTabEnabledCheckBox.Checked = _config.ShortcutRestoreClosedTabEnabled;
        _shortcutRestoreClosedTabTextBox.Text = _config.ShortcutRestoreClosedTab;
        _shortcutOpenHistoryEnabledCheckBox.Checked = _config.ShortcutOpenHistoryEnabled;
        _shortcutOpenHistoryTextBox.Text = _config.ShortcutOpenHistory;
        _shortcutFindInPageEnabledCheckBox.Checked = _config.ShortcutFindInPageEnabled;
        _shortcutFindInPageTextBox.Text = _config.ShortcutFindInPage;
        _enforcePluginPermissionCheckBox.Checked = _config.EnforcePluginPermissions;
        _allowedPermissionsTextBox.Text = string.Join(Environment.NewLine, _config.AllowedPluginPermissions);
        RefreshToolbarGrid();
        RefreshAddressCommandGrid();
        RefreshCredentialGrid();
        RefreshCredentialAutosaveRuleGrid();
        RefreshBookmarkCount();
    }

    private void BuildSettingSearchIndex()
    {
        _settingSearchItems.Clear();
        _settingSearchItems.AddRange(
        [
            new SettingSearchItem("启动页 首页 默认打开 start page", "常规 / 启动页", 0, () => _startPageTextBox),
            new SettingSearchItem("最大活跃标签 内存 休眠 tab", "常规 / 标签与休眠", 0, () => _maxLiveTabsNumber),
            new SettingSearchItem("恢复会话 上次会话 启动恢复 session", "常规 / 上次会话恢复", 0, () => _restoreLastSessionCheckBox),
            new SettingSearchItem("启动询问 恢复会话 空白页 restore prompt", "常规 / 启动时询问恢复", 0, () => _askBeforeRestoringSessionCheckBox),
            new SettingSearchItem("书签栏 显示 隐藏", "常规 / 书签栏显示", 0, () => _showBookmarksBarCheckBox),
            new SettingSearchItem("主题 深色 浅色 night dark light", "常规 / 界面主题", 0, () => _uiThemeComboBox),
            new SettingSearchItem("强调色 accent blue green orange rose", "常规 / 界面强调色", 0, () => _uiAccentComboBox),
            new SettingSearchItem("强调强度 soft standard vivid 柔和 标准 鲜明", "常规 / 强调强度", 0, () => _uiAccentStrengthComboBox),
            new SettingSearchItem("字体 font yahei dengxian song", "常规 / 界面字体", 0, () => _uiFontComboBox),
            new SettingSearchItem("密度 紧凑 compact normal", "常规 / 界面密度", 0, () => _uiDensityComboBox),
            new SettingSearchItem("任务栏 图标 icon ico png 自定义", "常规 / 任务栏图标", 0, () => _taskbarIconPathTextBox),
            new SettingSearchItem("工具栏 按钮 顺序 隐藏 显示", "界面 / 工具栏按钮", 1, () => _toolbarGrid),
            new SettingSearchItem("地址栏 指令 命令 :settings :history :downloads :darkmode :lightmode :compactui :normalui :togglebookmarksbar :theme :density :accent :accentstrength :uipreset :recentcommands :pincommand :resetui :font :bookmarksbar :statusbar :tabname :tabcolor :tabops :zen :help :palette ctrl+shift+p ctrl+shift+z ctrl+k command palette @ tabswitch quick tab switch rename name dark light compact normal blue green orange rose soft standard vivid balanced focus cycle show clear add remove list pin unpin detail yahei dengxian song on off toggle immersive tab label color dedupe closehost samehost focushost keephost isolatehost closeblank blanktabs cleanup unique suspendhost sleephost resumehost wakehost hoststats hosts domains", "界面 / 地址栏快捷指令", 1, () => _addressCommandGrid),
            new SettingSearchItem("搜索引擎 默认引擎 template", "搜索与账号 / 默认搜索引擎", 2, () => _searchEngineComboBox),
            new SettingSearchItem("自定义搜索引擎 关键字 keyword 前缀", "搜索与账号 / 自定义搜索引擎", 2, () => _customSearchEngineGrid),
            new SettingSearchItem("地址栏 建议 自动补全 autocomplete suggest", "搜索与账号 / 地址栏输入建议", 2, () => _enableAddressBarSuggestionsCheckBox),
            new SettingSearchItem("账号 密码 凭据 记录", "搜索与账号 / 账号密码管理", 2, () => _credentialGrid),
            new SettingSearchItem("快捷键 收藏 新建 关闭 F12", "快捷键 / 全部快捷键", 3, () => _shortcutAddBookmarkTextBox),
            new SettingSearchItem("下载目录 download path", "下载 / 下载目录", 4, () => _downloadDirectoryTextBox),
            new SettingSearchItem("自动填充 自动保存 密码 弹窗 域名 规则", "隐私与登录 / 自动填充与密码保存", 5, () => _credentialAutosaveCheckBox),
            new SettingSearchItem("广告过滤 拦截 屏蔽域名 adblock", "隐私与登录 / 广告过滤", 5, () => _adBlockEnabledCheckBox),
            new SettingSearchItem("插件 安装 启用 禁用 卸载 权限 白名单", "插件 / 插件权限与管理", 6, () => _pluginGrid),
            new SettingSearchItem("书签 导入 导出 管理", "书签 / 书签管理", 7, () => _bookmarkManageButton),
            new SettingSearchItem("关于 版本 更新说明 日志 version changelog release", "关于 / 版本更新说明", 8, null)
        ]);
        UpdateSidebarSummary();
    }

    private void RefreshToolbarGrid()
    {
        var prevSuppress = _suppressPendingChangeTracking;
        _suppressPendingChangeTracking = true;
        try
        {
            _toolbarRows.Clear();
            foreach (var item in _config.ToolbarButtons.OrderBy(button => button.Order))
            {
                _toolbarRows.Add(new ToolbarButtonRow
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    Visible = item.Visible,
                    Order = item.Order
                });
            }

            RefreshToolbarRowOrderDisplay();
        }
        finally
        {
            _suppressPendingChangeTracking = prevSuppress;
        }
    }

    private void RefreshToolbarRowOrderDisplay()
    {
        for (var i = 0; i < _toolbarRows.Count; i++)
        {
            _toolbarRows[i].Order = i;
            _toolbarRows[i].OrderDisplay = (i + 1).ToString();
        }

        _toolbarGrid.Refresh();
    }

    private void RefreshAddressCommandGrid()
    {
        var prevSuppress = _suppressPendingChangeTracking;
        _suppressPendingChangeTracking = true;
        try
        {
            _addressCommandRows.Clear();
            foreach (var item in _config.AddressCommands.OrderBy(command => command.Order))
            {
                _addressCommandRows.Add(new AddressCommandRow
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    Enabled = item.Enabled,
                    Command = item.Command,
                    Order = item.Order
                });
            }

            RefreshAddressCommandRowOrderDisplay();
        }
        finally
        {
            _suppressPendingChangeTracking = prevSuppress;
        }
    }

    private void RefreshAddressCommandRowOrderDisplay()
    {
        for (var i = 0; i < _addressCommandRows.Count; i++)
        {
            _addressCommandRows[i].Order = i;
            _addressCommandRows[i].OrderDisplay = (i + 1).ToString();
        }

        _addressCommandGrid.Refresh();
    }

    private void UpdateSettingSearchResults()
    {
        var query = _settingSearchTextBox.Text.Trim();
        _settingSearchResultList.BeginUpdate();
        _settingSearchResultList.Items.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            _settingSearchResultList.EndUpdate();
            _searchResultHintLabel.Text = "搜索结果";
            return;
        }

        var matches = _settingSearchItems
            .Where(item => item.Matches(query))
            .Take(24)
            .ToArray();
        foreach (var item in matches)
        {
            _settingSearchResultList.Items.Add(item);
        }

        _settingSearchResultList.EndUpdate();
        _searchResultHintLabel.Text = matches.Length == 0 ? "搜索结果 (0)" : $"搜索结果 ({matches.Length})";
        _settingSearchResultList.ClearSelected();
    }

    private void NavigateToSelectedSearchResult()
    {
        if (_settingSearchResultList.SelectedItem is not SettingSearchItem item)
        {
            return;
        }

        NavigateToCategory(item.TabIndex);
        var target = item.FocusTargetFactory?.Invoke();
        if (target is null)
        {
            return;
        }

        target.Focus();
        if (target is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    public void NavigateToCategory(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.TabPages.Count)
        {
            return;
        }

        _isSyncingCategorySelection = true;
        _tabs.SelectedIndex = tabIndex;
        _isSyncingCategorySelection = false;

        _isSyncingTabSelection = true;
        _categoryListBox.SelectedIndex = tabIndex;
        _isSyncingTabSelection = false;
        UpdateActiveCategoryHeader(tabIndex);
    }

    // 中文注释：供外部入口（标题栏插件图标/插件下拉“管理插件...”）直达“插件”分类页。
    public void NavigateToPluginSection()
    {
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            if (_tabs.TabPages[i].Text == "插件")
            {
                NavigateToCategory(i);
                return;
            }
        }
    }

    private void WireEvents()
    {
        Shown += (_, _) =>
        {
            UpdateBodySplitterDistance();
            BeginInvoke(() =>
            {
                _suppressPendingChangeTracking = false;
                SetPendingChangeState(false, "当前配置已加载");
            });
        };
        Resize += (_, _) => UpdateBodySplitterDistance();
        FormClosed += (_, _) =>
        {
            if (_didSaveChanges)
            {
                return;
            }

            AppTheme.Configure(_config.UiTheme, _config.UiDensity, _config.UiAccent, _config.UiFont, _config.UiAccentStrength);
        };
        _resetStartPageButton.Click += (_, _) => _startPageTextBox.Text = "about:blank";
        WireShortcutToggle(_shortcutAddBookmarkEnabledCheckBox, _shortcutAddBookmarkTextBox);
        WireShortcutToggle(_shortcutNewTabEnabledCheckBox, _shortcutNewTabTextBox);
        WireShortcutToggle(_shortcutCloseTabEnabledCheckBox, _shortcutCloseTabTextBox);
        WireShortcutToggle(_shortcutFocusAddressEnabledCheckBox, _shortcutFocusAddressTextBox);
        WireShortcutToggle(_shortcutRefreshEnabledCheckBox, _shortcutRefreshTextBox);
        WireShortcutToggle(_shortcutDevToolsEnabledCheckBox, _shortcutDevToolsTextBox);
        WireShortcutToggle(_shortcutToggleBookmarksBarEnabledCheckBox, _shortcutToggleBookmarksBarTextBox);
        WireShortcutToggle(_shortcutRestoreClosedTabEnabledCheckBox, _shortcutRestoreClosedTabTextBox);
        WireShortcutToggle(_shortcutOpenHistoryEnabledCheckBox, _shortcutOpenHistoryTextBox);
        WireShortcutToggle(_shortcutFindInPageEnabledCheckBox, _shortcutFindInPageTextBox);

        _categoryListBox.SelectedIndexChanged += (_, _) =>
        {
            if (_isSyncingTabSelection || _categoryListBox.SelectedIndex < 0)
            {
                return;
            }

            _isSyncingCategorySelection = true;
            _tabs.SelectedIndex = _categoryListBox.SelectedIndex;
            _isSyncingCategorySelection = false;
        };
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_isSyncingCategorySelection)
            {
                return;
            }

            _isSyncingTabSelection = true;
            _categoryListBox.SelectedIndex = _tabs.SelectedIndex;
            _isSyncingTabSelection = false;
            UpdateActiveCategoryHeader(_tabs.SelectedIndex);
        };
        _restoreLastSessionCheckBox.CheckedChanged += (_, _) => SyncSessionRestoreOptions();
        _uiThemeComboBox.SelectedIndexChanged += (_, _) => ApplyUiPreviewFromSelection();
        _uiAccentComboBox.SelectedIndexChanged += (_, _) => ApplyUiPreviewFromSelection();
        _uiAccentStrengthComboBox.SelectedIndexChanged += (_, _) => ApplyUiPreviewFromSelection();
        _uiFontComboBox.SelectedIndexChanged += (_, _) => ApplyUiPreviewFromSelection();
        _uiDensityComboBox.SelectedIndexChanged += (_, _) => ApplyUiPreviewFromSelection();
        _settingSearchTextBox.TextChanged += (_, _) => UpdateSettingSearchResults();
        _settingSearchResultList.DoubleClick += (_, _) => NavigateToSelectedSearchResult();
        _settingSearchResultList.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode != Keys.Enter)
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            NavigateToSelectedSearchResult();
        };
        _categoryListBox.DrawItem += (_, eventArgs) => DrawSidebarListItem(_categoryListBox, eventArgs, isSearchResult: false);
        _settingSearchResultList.DrawItem += (_, eventArgs) => DrawSidebarListItem(_settingSearchResultList, eventArgs, isSearchResult: true);
        _settingSearchTextBox.KeyDown += (_, eventArgs) =>
        {
            if (_settingSearchResultList.Items.Count == 0)
            {
                return;
            }

            if (eventArgs.KeyCode == Keys.Enter)
            {
                eventArgs.Handled = true;
                eventArgs.SuppressKeyPress = true;
                if (_settingSearchResultList.SelectedIndex < 0)
                {
                    _settingSearchResultList.SelectedIndex = 0;
                }

                NavigateToSelectedSearchResult();
                return;
            }

            if (eventArgs.KeyCode != Keys.Down)
            {
                return;
            }

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
            _settingSearchResultList.Focus();
            // 中文注释：搜索结果默认不预选，只有用户按下方向键时才选中首项，保持行为与地址栏一致。
            if (_settingSearchResultList.SelectedIndex < 0 && _settingSearchResultList.Items.Count > 0)
            {
                _settingSearchResultList.SelectedIndex = 0;
            }
        };

        _downloadBrowseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择下载目录"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _downloadDirectoryTextBox.Text = dialog.SelectedPath;
            }
        };
        _taskbarIconBrowseButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "选择任务栏图标",
                Filter = "图标或图片 (*.ico;*.png;*.jpg;*.jpeg;*.bmp)|*.ico;*.png;*.jpg;*.jpeg;*.bmp|图标文件 (*.ico)|*.ico|图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _taskbarIconPathTextBox.Text = dialog.FileName;
            }
        };
        _taskbarIconResetButton.Click += (_, _) => _taskbarIconPathTextBox.Clear();
        _taskbarIconPathTextBox.TextChanged += (_, _) => UpdateTaskbarIconPreview();

        _pluginInstallFolderButton.Click += (_, _) => InstallPluginFromFolder();
        _pluginInstallZipButton.Click += (_, _) => InstallPluginFromZip();
        _pluginEnableButton.Click += (_, _) => ToggleSelectedPlugin(true);
        _pluginDisableButton.Click += (_, _) => ToggleSelectedPlugin(false);
        _pluginUninstallButton.Click += (_, _) => UninstallSelectedPlugin();
        _pluginRefreshButton.Click += (_, _) => RefreshPluginGrid();
        _searchEngineComboBox.SelectedIndexChanged += (_, _) => ApplySelectedSearchEnginePreset();
        _credentialAddButton.Click += (_, _) => AddCredential();
        _credentialEditButton.Click += (_, _) => EditCredential();
        _credentialDeleteButton.Click += (_, _) => DeleteCredential();
        _credentialCopyPasswordButton.Click += (_, _) => CopyCredentialPassword();
        _toolbarMoveUpButton.Click += (_, _) =>
        {
            MoveSelectedToolbarRow(-1);
            MarkPendingChange("已调整工具栏按钮顺序");
        };
        _toolbarMoveDownButton.Click += (_, _) =>
        {
            MoveSelectedToolbarRow(1);
            MarkPendingChange("已调整工具栏按钮顺序");
        };
        _toolbarResetButton.Click += (_, _) =>
        {
            _config.ToolbarButtons = BrowserConfig.CreateDefaultToolbarButtons();
            RefreshToolbarGrid();
            MarkPendingChange("已恢复默认工具栏按钮布局");
        };
        _addressCommandMoveUpButton.Click += (_, _) =>
        {
            MoveSelectedAddressCommandRow(-1);
            MarkPendingChange("已调整地址栏指令顺序");
        };
        _addressCommandMoveDownButton.Click += (_, _) =>
        {
            MoveSelectedAddressCommandRow(1);
            MarkPendingChange("已调整地址栏指令顺序");
        };
        _addressCommandResetButton.Click += (_, _) =>
        {
            _config.AddressCommands = BrowserConfig.CreateDefaultAddressCommands();
            RefreshAddressCommandGrid();
            MarkPendingChange("已恢复默认地址栏指令");
        };

        _bookmarkManageButton.Click += (_, _) => ShowBookmarkManager();
        _bookmarkImportButton.Click += (_, _) => ImportBookmarks();
        _bookmarkExportButton.Click += (_, _) => ExportBookmarks();
        _shortcutResetDefaultsButton.Click += (_, _) => ResetShortcutsToDefaults();
        _saveButton.Click += (_, _) => SaveAll();
        _cancelButton.Click += (_, _) => Close();
        SyncSessionRestoreOptions();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_didSaveChanges || !_hasPendingChanges)
        {
            return;
        }

        var result = MessageBox.Show(
            "当前有未保存的更改，确定直接关闭吗？",
            "确认关闭",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void UpdateSidebarSummary()
    {
        var categoryCount = _tabs.TabPages.Count;
        var searchableCount = _settingSearchItems.Count;
        _sidebarSummaryLabel.Text = $"分类 {categoryCount} 项 · 可搜索 {searchableCount} 项";
    }

    private void UpdateActiveCategoryHeader(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.TabPages.Count)
        {
            tabIndex = 0;
        }

        var title = _tabs.TabPages[tabIndex].Text;
        _contentHeaderTitleLabel.Text = title == "关于" ? "关于 LfsunBrowser" : $"{title}设置";
        _contentHeaderSubtitleLabel.Text = ResolveCategorySubtitle(tabIndex);
    }

    private static string ResolveCategorySubtitle(int tabIndex)
    {
        return tabIndex switch
        {
            0 => "启动策略、外观主题、任务栏图标与会话恢复",
            1 => "工具栏按钮与地址栏指令的展示和顺序管理",
            2 => "搜索引擎模板、输入建议与账号密码记录",
            3 => "浏览器常用快捷键配置与冲突控制",
            4 => "下载目录路径管理",
            5 => "自动填充与密码保存策略",
            6 => "插件权限白名单与插件生命周期管理",
            7 => "书签导入导出与集中管理",
            8 => "关于 LfsunBrowser、版本信息与更新历史记录",
            _ => "浏览器配置"
        };
    }

    private static void ApplyCardVisual(Control panel)
    {
        panel.BackColor = AppTheme.Surface;
        panel.SizeChanged -= OnCardPanelSizeChanged;
        panel.SizeChanged += OnCardPanelSizeChanged;
        panel.Paint -= DrawCardFrame;
        panel.Paint += DrawCardFrame;
        UpdateCardRegion(panel, 10);
        panel.Invalidate();
    }

    private static void OnCardPanelSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Control panel)
        {
            UpdateCardRegion(panel, 10);
        }
    }

    private static void UpdateCardRegion(Control panel, int radius)
    {
        if (panel.Width > radius * 2 && panel.Height > radius * 2)
        {
            using var path = AppTheme.BuildRoundedRect(panel.ClientRectangle, radius);
            panel.Region = new Region(path);
        }
    }

    private static void DrawCardFrame(object? sender, PaintEventArgs eventArgs)
    {
        if (sender is not Control control)
        {
            return;
        }

        var rect = control.ClientRectangle;
        rect.Inflate(-1, -1);
        if (rect.Width <= 4 || rect.Height <= 4)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundRectPath(rect, 10);
        using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
        var glowColor = AppTheme.IsDark
            ? Color.FromArgb(28, AppTheme.Accent)
            : Color.FromArgb(22, AppTheme.Accent);
        using var glowPen = new Pen(glowColor, 1F);
        eventArgs.Graphics.DrawPath(glowPen, path);
        eventArgs.Graphics.DrawPath(borderPen, path);
    }

    private static GraphicsPath BuildRoundRectPath(Rectangle rect, int radius)
        => AppTheme.BuildRoundedRect(rect, radius);

    private static void DrawSidebarListItem(ListBox listBox, DrawItemEventArgs eventArgs, bool isSearchResult)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= listBox.Items.Count)
        {
            return;
        }

        // 中文注释：用容器背景色替代 Win32 默认绘制，避免老旧系统深蓝高亮闪烁
        using (var clearBrush = new SolidBrush(listBox.BackColor))
        {
            eventArgs.Graphics.FillRectangle(clearBrush, eventArgs.Bounds);
        }

        var selected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected
            ? AppTheme.Mix(AppTheme.AccentHover, AppTheme.Surface, AppTheme.IsDark ? 0.32F : 0.22F)
            : isSearchResult
                ? AppTheme.InputBackground
                : AppTheme.Surface;
        var surfaceBounds = Rectangle.Inflate(eventArgs.Bounds, -2, -2);
        using var surfacePath = BuildRoundRectPath(surfaceBounds, 7);
        using var backgroundBrush = new SolidBrush(background);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.FillPath(backgroundBrush, surfacePath);

        if (selected)
        {
            using var borderPen = new Pen(AppTheme.Mix(AppTheme.Accent, AppTheme.PanelBorder, 0.40F), 1F);
            eventArgs.Graphics.DrawPath(borderPen, surfacePath);
        }

        using var textBrush = new SolidBrush(selected ? AppTheme.TextPrimary : AppTheme.TextSecondary);
        using var selectedFont = selected ? new Font(listBox.Font, FontStyle.Bold) : null;
        var textBounds = Rectangle.Inflate(eventArgs.Bounds, -10, -2);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            listBox.Items[eventArgs.Index].ToString(),
            selectedFont ?? listBox.Font,
            textBounds,
            textBrush.Color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (selected && !isSearchResult)
        {
            using var indicatorBrush = new SolidBrush(AppTheme.Accent);
            using var indicatorPath = BuildRoundRectPath(new Rectangle(surfaceBounds.Left + 2, surfaceBounds.Top + 4, 4, surfaceBounds.Height - 8), 2);
            eventArgs.Graphics.FillPath(indicatorBrush, indicatorPath);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var startColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.14F)
            : AppTheme.Mix(AppTheme.WindowBackground, AppTheme.Accent, 0.07F);
        var endColor = AppTheme.IsDark
            ? AppTheme.Mix(AppTheme.WindowBackground, Color.Black, 0.18F)
            : AppTheme.Mix(AppTheme.WindowBackground, Color.White, 0.04F);

        using var gradient = new LinearGradientBrush(ClientRectangle, startColor, endColor, 90F);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var glowAccent = Color.FromArgb(AppTheme.IsDark ? 38 : 28, AppTheme.Accent);
        var glowSupportBase = AppTheme.Mix(AppTheme.Accent, AppTheme.Success, 0.34F);
        var glowSupport = Color.FromArgb(AppTheme.IsDark ? 28 : 20, glowSupportBase);

        var blobA = new Rectangle((int)(ClientSize.Width * 0.72F), -110, 300, 230);
        var blobB = new Rectangle(-120, (int)(ClientSize.Height * 0.45F), 240, 220);
        using var brushA = new SolidBrush(glowAccent);
        using var brushB = new SolidBrush(glowSupport);
        eventArgs.Graphics.FillEllipse(brushA, blobA);
        eventArgs.Graphics.FillEllipse(brushB, blobB);
    }

    private void UpdateBodySplitterDistance()
    {
        if (_bodySplitContainer.Width <= 0)
        {
            return;
        }

        var availableWidth = _bodySplitContainer.ClientSize.Width;
        var splitterWidth = _bodySplitContainer.SplitterWidth;
        var minDistance = SidebarMinWidth;
        var maxDistance = availableWidth - ContentMinWidth - splitterWidth;
        if (availableWidth <= 0 || maxDistance < minDistance)
        {
            _bodySplitContainer.Panel1MinSize = 0;
            _bodySplitContainer.Panel2MinSize = 0;
            return;
        }

        var targetDistance = Math.Min(PreferredSidebarWidth, maxDistance);
        targetDistance = Math.Max(minDistance, targetDistance);

        _bodySplitContainer.Panel1MinSize = 0;
        _bodySplitContainer.Panel2MinSize = 0;
        try
        {
            if (_bodySplitContainer.SplitterDistance != targetDistance)
            {
                _bodySplitContainer.SplitterDistance = targetDistance;
            }
        }
        catch
        {
            // 防御性忽略极端 DPI 缩放或临界宽度异常
        }



    }

    private void MoveSelectedToolbarRow(int offset)
    {
        if (_toolbarGrid.SelectedRows.Count == 0)
        {
            return;
        }

        var selectedRow = _toolbarGrid.SelectedRows[0].DataBoundItem as ToolbarButtonRow;
        if (selectedRow is null)
        {
            return;
        }

        var currentIndex = _toolbarRows.IndexOf(selectedRow);
        var newIndex = currentIndex + offset;
        if (currentIndex < 0 || newIndex < 0 || newIndex >= _toolbarRows.Count)
        {
            return;
        }

        _toolbarRows.RemoveAt(currentIndex);
        _toolbarRows.Insert(newIndex, selectedRow);
        RefreshToolbarRowOrderDisplay();
        _toolbarGrid.ClearSelection();
        _toolbarGrid.Rows[newIndex].Selected = true;
        _toolbarGrid.CurrentCell = _toolbarGrid.Rows[newIndex].Cells[1];
    }

    private void MoveSelectedAddressCommandRow(int offset)
    {
        if (_addressCommandGrid.SelectedRows.Count == 0)
        {
            return;
        }

        var selectedRow = _addressCommandGrid.SelectedRows[0].DataBoundItem as AddressCommandRow;
        if (selectedRow is null)
        {
            return;
        }

        var currentIndex = _addressCommandRows.IndexOf(selectedRow);
        var newIndex = currentIndex + offset;
        if (currentIndex < 0 || newIndex < 0 || newIndex >= _addressCommandRows.Count)
        {
            return;
        }

        _addressCommandRows.RemoveAt(currentIndex);
        _addressCommandRows.Insert(newIndex, selectedRow);
        RefreshAddressCommandRowOrderDisplay();
        _addressCommandGrid.ClearSelection();
        _addressCommandGrid.Rows[newIndex].Selected = true;
        _addressCommandGrid.CurrentCell = _addressCommandGrid.Rows[newIndex].Cells[2];
    }

    private static void WireShortcutToggle(CheckBox enabledCheckBox, TextBox shortcutTextBox)
    {
        void SyncState()
        {
            shortcutTextBox.Enabled = enabledCheckBox.Checked;
            shortcutTextBox.BackColor = enabledCheckBox.Checked ? AppTheme.InputBackground : (AppTheme.IsDark ? Color.FromArgb(55, 64, 78) : Color.FromArgb(236, 241, 247));
        }

        enabledCheckBox.CheckedChanged += (_, _) => SyncState();
        SyncState();
    }

    private void SyncSessionRestoreOptions()
    {
        _askBeforeRestoringSessionCheckBox.Enabled = _restoreLastSessionCheckBox.Checked;
        _askBeforeRestoringSessionCheckBox.BackColor = _restoreLastSessionCheckBox.Checked
            ? BackColor
            : (AppTheme.IsDark ? Color.FromArgb(52, 60, 74) : Color.FromArgb(232, 239, 248));
    }

    private void ResetShortcutsToDefaults()
    {
        var defaults = new BrowserConfig();

        _shortcutAddBookmarkEnabledCheckBox.Checked = defaults.ShortcutAddBookmarkEnabled;
        _shortcutAddBookmarkTextBox.Text = defaults.ShortcutAddBookmark;
        _shortcutNewTabEnabledCheckBox.Checked = defaults.ShortcutNewTabEnabled;
        _shortcutNewTabTextBox.Text = defaults.ShortcutNewTab;
        _shortcutCloseTabEnabledCheckBox.Checked = defaults.ShortcutCloseTabEnabled;
        _shortcutCloseTabTextBox.Text = defaults.ShortcutCloseTab;
        _shortcutFocusAddressEnabledCheckBox.Checked = defaults.ShortcutFocusAddressEnabled;
        _shortcutFocusAddressTextBox.Text = defaults.ShortcutFocusAddress;
        _shortcutRefreshEnabledCheckBox.Checked = defaults.ShortcutRefreshEnabled;
        _shortcutRefreshTextBox.Text = defaults.ShortcutRefresh;
        _shortcutDevToolsEnabledCheckBox.Checked = defaults.ShortcutDevToolsEnabled;
        _shortcutDevToolsTextBox.Text = defaults.ShortcutDevTools;
        _shortcutToggleBookmarksBarEnabledCheckBox.Checked = defaults.ShortcutToggleBookmarksBarEnabled;
        _shortcutToggleBookmarksBarTextBox.Text = defaults.ShortcutToggleBookmarksBar;
        _shortcutRestoreClosedTabEnabledCheckBox.Checked = defaults.ShortcutRestoreClosedTabEnabled;
        _shortcutRestoreClosedTabTextBox.Text = defaults.ShortcutRestoreClosedTab;
        _shortcutOpenHistoryEnabledCheckBox.Checked = defaults.ShortcutOpenHistoryEnabled;
        _shortcutOpenHistoryTextBox.Text = defaults.ShortcutOpenHistory;
        _shortcutFindInPageEnabledCheckBox.Checked = defaults.ShortcutFindInPageEnabled;
        _shortcutFindInPageTextBox.Text = defaults.ShortcutFindInPage;
    }

    private void SelectSearchEnginePreset()
    {
        var byTemplate = SearchEngineCatalog.BuiltIns
            .FirstOrDefault(preset => string.Equals(
                preset.Template,
                _config.DefaultSearchEngineUrlTemplate,
                StringComparison.OrdinalIgnoreCase));
        if (byTemplate is not null)
        {
            _searchEngineComboBox.SelectedItem = byTemplate.Name;
            return;
        }

        var byName = SearchEngineCatalog.BuiltIns
            .FirstOrDefault(preset => string.Equals(
                preset.Name,
                _config.DefaultSearchEngineName,
                StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            _searchEngineComboBox.SelectedItem = byName.Name;
            _searchEngineTemplateTextBox.Text = byName.Template;
            return;
        }

        _searchEngineComboBox.SelectedItem = "自定义";
    }

    private void ApplySelectedSearchEnginePreset()
    {
        if (_searchEngineComboBox.SelectedItem is not string selectedName)
        {
            return;
        }

        var preset = SearchEngineCatalog.BuiltIns.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            return;
        }

        _searchEngineTemplateTextBox.Text = preset.Template;
    }

    private void ApplyUiPreviewFromSelection()
    {
        // 中文注释：预览时“跟随系统”按当前系统深浅色解析。
        var previewTheme = (_uiThemeComboBox.SelectedItem as string) switch
        {
            "深色" => "Dark",
            "跟随系统" => AppTheme.IsSystemDarkTheme() ? "Dark" : "Light",
            _ => "Light"
        };
        var previewAccent = (_uiAccentComboBox.SelectedItem as string) switch
        {
            "青绿" => "Green",
            "琥珀" => "Orange",
            "玫红" => "Rose",
            _ => "Blue"
        };
        var previewStrength = (_uiAccentStrengthComboBox.SelectedItem as string) switch
        {
            "柔和" => "Soft",
            "鲜明" => "Vivid",
            _ => "Standard"
        };
        var previewFont = (_uiFontComboBox.SelectedItem as string) switch
        {
            "等线（现代）" => "DengXian",
            "宋体（传统）" => "Song",
            _ => "YaHei"
        };
        var previewDensity = (_uiDensityComboBox.SelectedItem as string) == "紧凑" ? "Compact" : "Normal";
        AppTheme.Configure(previewTheme, previewDensity, previewAccent, previewFont, previewStrength);
        ApplyVisualStyle();
        Refresh();
    }

    private void RefreshCredentialGrid()
    {
        var prevSuppress = _suppressPendingChangeTracking;
        _suppressPendingChangeTracking = true;
        try
        {
            _credentialRows.Clear();
            foreach (var entry in _credentialStore.ListEntries())
            {
                _credentialRows.Add(new CredentialRow
                {
                    Id = entry.Id,
                    Site = entry.Site,
                    Username = entry.Username,
                    PasswordMasked = string.IsNullOrWhiteSpace(entry.Password) ? "(空)" : new string('*', Math.Min(12, Math.Max(6, entry.Password.Length))),
                    PasswordPlain = entry.Password,
                    UpdatedAt = entry.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
        finally
        {
            _suppressPendingChangeTracking = prevSuppress;
        }
    }

    private void RefreshCredentialAutosaveRuleGrid()
    {
        var prevSuppress = _suppressPendingChangeTracking;
        _suppressPendingChangeTracking = true;
        try
        {
            _credentialAutosaveRuleRows.Clear();
            foreach (var rule in _config.CredentialAutosaveRules)
            {
                _credentialAutosaveRuleRows.Add(new CredentialAutosaveRuleRow
                {
                    Host = rule.Host,
                    Mode = string.Equals(rule.Mode, CredentialAutosaveRule.ModeAllow, StringComparison.OrdinalIgnoreCase)
                        ? "允许保存"
                        : "不保存"
                });
            }
        }
        finally
        {
            _suppressPendingChangeTracking = prevSuppress;
        }
    }

    private CredentialRow? GetSelectedCredential()
    {
        if (_credentialGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _credentialGrid.SelectedRows[0].DataBoundItem as CredentialRow;
    }

    private void AddCredential()
    {
        if (!CredentialEditDialog.ShowDialog(
                this,
                "新增账号",
                defaultSite: string.Empty,
                defaultUsername: string.Empty,
                defaultPassword: string.Empty,
                out var site,
                out var username,
                out var password))
        {
            return;
        }

        if (!_credentialStore.SaveEntry(id: null, site, username, password, out var error))
        {
            MessageBox.Show($"保存失败：{error}", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshCredentialGrid();
    }

    private void EditCredential()
    {
        var selected = GetSelectedCredential();
        if (selected is null)
        {
            MessageBox.Show("请先选中一条账号记录。", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!CredentialEditDialog.ShowDialog(
                this,
                "编辑账号",
                selected.Site,
                selected.Username,
                selected.PasswordPlain,
                out var site,
                out var username,
                out var password))
        {
            return;
        }

        if (!_credentialStore.SaveEntry(selected.Id, site, username, password, out var error))
        {
            MessageBox.Show($"保存失败：{error}", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshCredentialGrid();
    }

    private void DeleteCredential()
    {
        var selected = GetSelectedCredential();
        if (selected is null)
        {
            MessageBox.Show("请先选中一条账号记录。", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除站点“{selected.Site}”的账号记录？",
            "删除确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (!_credentialStore.RemoveEntry(selected.Id, out var error))
        {
            MessageBox.Show($"删除失败：{error}", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshCredentialGrid();
    }

    private void CopyCredentialPassword()
    {
        var selected = GetSelectedCredential();
        if (selected is null)
        {
            MessageBox.Show("请先选中一条账号记录。", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(selected.PasswordPlain))
        {
            MessageBox.Show("该记录密码为空，无法复制。", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Clipboard.SetText(selected.PasswordPlain);
            _log("密码已复制到剪贴板");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "账号管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshPluginGrid()
    {
        var prevSuppress = _suppressPendingChangeTracking;
        _suppressPendingChangeTracking = true;
        try
        {
            _pluginRows.Clear();
            foreach (var info in _pluginManager.DiscoverPackages())
            {
                _pluginRows.Add(new PluginRow
                {
                    Id = info.Id,
                    Name = info.Name,
                    Enabled = info.Enabled,
                    Status = info.LoadStatus,
                    DirectoryPath = info.DirectoryPath
                });
            }
        }
        finally
        {
            _suppressPendingChangeTracking = prevSuppress;
        }
    }

    private PluginRow? GetSelectedPlugin()
    {
        if (_pluginGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _pluginGrid.SelectedRows[0].DataBoundItem as PluginRow;
    }

    private async void InstallPluginFromFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择插件目录（目录内应包含 manifest.json）"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var sourcePath = dialog.SelectedPath;
        // 中文注释：复制大目录（如 Chrome 扩展上千个文件）放后台线程，同步执行会让窗口“未响应”。
        var (ok, error) = await RunPluginOperationAsync(() =>
        {
            var success = _pluginManager.InstallPluginFromDirectory(sourcePath, out var message);
            return (success, message);
        });
        if (!ok)
        {
            MessageBox.Show($"安装失败：{error}", "插件安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _pluginManager.Reload();
        RefreshPluginGrid();
        _log("插件已安装（目录）");
    }

    private async void InstallPluginFromZip()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择插件压缩包",
            Filter = "Zip 压缩包 (*.zip)|*.zip"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var zipPath = dialog.FileName;
        var (ok, error) = await RunPluginOperationAsync(() =>
        {
            var success = _pluginManager.InstallPluginFromZip(zipPath, out var message);
            return (success, message);
        });
        if (!ok)
        {
            MessageBox.Show($"安装失败：{error}", "插件安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _pluginManager.Reload();
        RefreshPluginGrid();
        _log("插件已安装（zip）");
    }

    // 中文注释：在后台线程执行耗时插件操作（安装复制/卸载删除），期间禁用按钮并显示等待光标，
    // 避免 UI 线程被大量文件 IO 卡成“未响应”。
    private async Task<(bool Ok, string Error)> RunPluginOperationAsync(Func<(bool, string)> operation)
    {
        _pluginInstallFolderButton.Enabled = false;
        _pluginInstallZipButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            return await Task.Run(operation);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
            _pluginInstallFolderButton.Enabled = true;
            _pluginInstallZipButton.Enabled = true;
        }
    }

    private void ToggleSelectedPlugin(bool enabled)
    {
        var selected = GetSelectedPlugin();
        if (selected is null)
        {
            MessageBox.Show("请先选中一个插件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_pluginManager.SetPluginEnabled(selected.Id, enabled, out var error))
        {
            MessageBox.Show($"操作失败：{error}", "插件管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _pluginManager.Reload();
        RefreshPluginGrid();
        _log(enabled ? $"插件已启用：{selected.Name}" : $"插件已禁用：{selected.Name}");
    }

    private async void UninstallSelectedPlugin()
    {
        var selected = GetSelectedPlugin();
        if (selected is null)
        {
            MessageBox.Show("请先选中一个插件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认卸载插件“{selected.Name}”？",
            "卸载插件",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var (ok, error) = await RunPluginOperationAsync(() =>
        {
            var success = _pluginManager.UninstallPlugin(selected.Id, out var message);
            return (success, message);
        });
        if (!ok)
        {
            MessageBox.Show($"卸载失败：{error}", "插件管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _pluginManager.Reload();
        RefreshPluginGrid();
        _log($"插件已卸载：{selected.Name}");
    }

    private void SaveAll()
    {
        if (!TryBuildUri(_startPageTextBox.Text, out var startPage))
        {
            MessageBox.Show("启动页地址无效。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var requestedDownloadDir = _downloadDirectoryTextBox.Text.Trim();
        if (!_downloadManager.UpdateDownloadDirectory(requestedDownloadDir, out var downloadError))
        {
            MessageBox.Show($"下载目录无效：{downloadError}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryNormalizeSearchTemplate(_searchEngineTemplateTextBox.Text, out var searchTemplate))
        {
            MessageBox.Show("默认搜索引擎模板无效，必须是绝对 URL 且包含 {query}。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _searchEngineTemplateTextBox.Focus();
            return;
        }

        var searchEngineName = (_searchEngineComboBox.SelectedItem as string)?.Trim();
        if (string.IsNullOrWhiteSpace(searchEngineName))
        {
            searchEngineName = "自定义";
        }

        // 中文注释：校验自定义搜索引擎表格，全空行跳过，半据行报错。
        var customSearchEngines = new List<CustomSearchEngine>();
        foreach (var row in _customSearchEngineRows)
        {
            var engineName = row.Name?.Trim() ?? string.Empty;
            var engineKeyword = row.Keyword?.Trim() ?? string.Empty;
            var engineTemplate = row.Template?.Trim() ?? string.Empty;
            if (engineName.Length == 0 && engineKeyword.Length == 0 && engineTemplate.Length == 0)
            {
                continue;
            }

            if (engineName.Length == 0 || !SearchEngineCatalog.IsValidTemplate(engineTemplate))
            {
                MessageBox.Show(
                    "自定义搜索引擎校验失败：名称必填，且模板必须包含 {query}。",
                    "保存失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            customSearchEngines.Add(new CustomSearchEngine
            {
                Name = engineName,
                Keyword = engineKeyword,
                Template = engineTemplate
            });
        }

        var uiTheme = (_uiThemeComboBox.SelectedItem as string) switch
        {
            "深色" => "Dark",
            "跟随系统" => ThemeOptionNames.ThemeSystem,
            _ => "Light"
        };
        var uiAccent = (_uiAccentComboBox.SelectedItem as string) switch
        {
            "青绿" => "Green",
            "琥珀" => "Orange",
            "玫红" => "Rose",
            _ => "Blue"
        };
        var uiAccentStrength = (_uiAccentStrengthComboBox.SelectedItem as string) switch
        {
            "柔和" => "Soft",
            "鲜明" => "Vivid",
            _ => "Standard"
        };
        var uiFont = (_uiFontComboBox.SelectedItem as string) switch
        {
            "等线（现代）" => "DengXian",
            "宋体（传统）" => "Song",
            _ => "YaHei"
        };
        var uiDensity = (_uiDensityComboBox.SelectedItem as string) == "紧凑" ? "Compact" : "Normal";
        if (!TryNormalizeTaskbarIconPath(_taskbarIconPathTextBox.Text, out var taskbarIconPath, out var taskbarIconError))
        {
            // 中文注释：图标文件失效（如已被删除/移动）时不硬阻断整个保存，允许一键回退默认图标继续保存。
            var choice = MessageBox.Show(
                $"{taskbarIconError}\n\n是否恢复默认图标并继续保存其他设置？",
                "任务栏图标",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                _taskbarIconPathTextBox.Focus();
                return;
            }

            _taskbarIconPathTextBox.Clear();
            taskbarIconPath = string.Empty;
        }

        if (!TryNormalizeShortcutInput("收藏当前页", _shortcutAddBookmarkEnabledCheckBox.Checked, _shortcutAddBookmarkTextBox, _config.ShortcutAddBookmark, out var shortcutAddBookmark) ||
            !TryNormalizeShortcutInput("新建标签页", _shortcutNewTabEnabledCheckBox.Checked, _shortcutNewTabTextBox, _config.ShortcutNewTab, out var shortcutNewTab) ||
            !TryNormalizeShortcutInput("关闭当前标签页", _shortcutCloseTabEnabledCheckBox.Checked, _shortcutCloseTabTextBox, _config.ShortcutCloseTab, out var shortcutCloseTab) ||
            !TryNormalizeShortcutInput("聚焦地址栏", _shortcutFocusAddressEnabledCheckBox.Checked, _shortcutFocusAddressTextBox, _config.ShortcutFocusAddress, out var shortcutFocusAddress) ||
            !TryNormalizeShortcutInput("刷新页面", _shortcutRefreshEnabledCheckBox.Checked, _shortcutRefreshTextBox, _config.ShortcutRefresh, out var shortcutRefresh) ||
            !TryNormalizeShortcutInput("打开开发者工具", _shortcutDevToolsEnabledCheckBox.Checked, _shortcutDevToolsTextBox, _config.ShortcutDevTools, out var shortcutDevTools) ||
            !TryNormalizeShortcutInput("隐藏/显示书签栏", _shortcutToggleBookmarksBarEnabledCheckBox.Checked, _shortcutToggleBookmarksBarTextBox, _config.ShortcutToggleBookmarksBar, out var shortcutToggleBookmarksBar) ||
            !TryNormalizeShortcutInput("恢复最近关闭标签", _shortcutRestoreClosedTabEnabledCheckBox.Checked, _shortcutRestoreClosedTabTextBox, _config.ShortcutRestoreClosedTab, out var shortcutRestoreClosedTab) ||
            !TryNormalizeShortcutInput("打开历史记录页", _shortcutOpenHistoryEnabledCheckBox.Checked, _shortcutOpenHistoryTextBox, _config.ShortcutOpenHistory, out var shortcutOpenHistory) ||
            !TryNormalizeShortcutInput("页内查找", _shortcutFindInPageEnabledCheckBox.Checked, _shortcutFindInPageTextBox, _config.ShortcutFindInPage, out var shortcutFindInPage))
        {
            return;
        }

        var shortcutMappings = new[]
        {
            new ShortcutMapping("收藏当前页", _shortcutAddBookmarkEnabledCheckBox.Checked, shortcutAddBookmark),
            new ShortcutMapping("新建标签页", _shortcutNewTabEnabledCheckBox.Checked, shortcutNewTab),
            new ShortcutMapping("关闭当前标签页", _shortcutCloseTabEnabledCheckBox.Checked, shortcutCloseTab),
            new ShortcutMapping("聚焦地址栏", _shortcutFocusAddressEnabledCheckBox.Checked, shortcutFocusAddress),
            new ShortcutMapping("刷新页面", _shortcutRefreshEnabledCheckBox.Checked, shortcutRefresh),
            new ShortcutMapping("打开开发者工具", _shortcutDevToolsEnabledCheckBox.Checked, shortcutDevTools),
            new ShortcutMapping("隐藏/显示书签栏", _shortcutToggleBookmarksBarEnabledCheckBox.Checked, shortcutToggleBookmarksBar),
            new ShortcutMapping("恢复最近关闭标签", _shortcutRestoreClosedTabEnabledCheckBox.Checked, shortcutRestoreClosedTab),
            new ShortcutMapping("打开历史记录页", _shortcutOpenHistoryEnabledCheckBox.Checked, shortcutOpenHistory),
            new ShortcutMapping("页内查找", _shortcutFindInPageEnabledCheckBox.Checked, shortcutFindInPage)
        };
        if (!TryValidateShortcutConflicts(shortcutMappings, out var conflictMessage))
        {
            MessageBox.Show(conflictMessage, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.StartPage = startPage.AbsoluteUri;
        _config.MaxLiveTabs = (int)_maxLiveTabsNumber.Value;
        _config.SuspendAfterMinutes = (int)_suspendMinutesNumber.Value;
        _config.DownloadDirectory = _downloadManager.DownloadDirectory;
        _config.IsGeneralAutofillEnabled = _autofillCheckBox.Checked;
        _config.IsPasswordAutosaveEnabled = _passwordAutosaveCheckBox.Checked;
        _config.IsCredentialAutofillEnabled = _credentialAutofillCheckBox.Checked;
        _config.IsCredentialAutosaveEnabled = _credentialAutosaveCheckBox.Checked;
        _config.CredentialAutosaveRules = _credentialAutosaveRuleRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Host))
            .Select(row => new CredentialAutosaveRule
            {
                Host = row.Host.Trim(),
                Mode = string.Equals(row.Mode, "允许保存", StringComparison.Ordinal)
                    ? CredentialAutosaveRule.ModeAllow
                    : CredentialAutosaveRule.ModeBlock
            })
            .ToList();
        _config.AdBlockEnabled = _adBlockEnabledCheckBox.Checked;
        _config.AdBlockCustomDomains = _adBlockCustomDomainsTextBox.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        _config.RestoreLastSessionOnStartup = _restoreLastSessionCheckBox.Checked;
        _config.AskBeforeRestoringSessionOnStartup = _askBeforeRestoringSessionCheckBox.Checked;
        _config.EnableAddressBarSuggestions = _enableAddressBarSuggestionsCheckBox.Checked;
        _config.DefaultSearchEngineName = searchEngineName;
        _config.DefaultSearchEngineUrlTemplate = searchTemplate;
        _config.CustomSearchEngines = customSearchEngines;
        _config.ShowBookmarksBar = _showBookmarksBarCheckBox.Checked;
        _config.UiTheme = uiTheme;
        _config.UiAccent = uiAccent;
        _config.UiAccentStrength = uiAccentStrength;
        _config.UiFont = uiFont;
        _config.UiDensity = uiDensity;
        _config.TaskbarIconPath = taskbarIconPath;
        _config.ShortcutAddBookmarkEnabled = _shortcutAddBookmarkEnabledCheckBox.Checked;
        _config.ShortcutAddBookmark = shortcutAddBookmark;
        _config.ShortcutNewTabEnabled = _shortcutNewTabEnabledCheckBox.Checked;
        _config.ShortcutNewTab = shortcutNewTab;
        _config.ShortcutCloseTabEnabled = _shortcutCloseTabEnabledCheckBox.Checked;
        _config.ShortcutCloseTab = shortcutCloseTab;
        _config.ShortcutFocusAddressEnabled = _shortcutFocusAddressEnabledCheckBox.Checked;
        _config.ShortcutFocusAddress = shortcutFocusAddress;
        _config.ShortcutRefreshEnabled = _shortcutRefreshEnabledCheckBox.Checked;
        _config.ShortcutRefresh = shortcutRefresh;
        _config.ShortcutDevToolsEnabled = _shortcutDevToolsEnabledCheckBox.Checked;
        _config.ShortcutDevTools = shortcutDevTools;
        _config.ShortcutToggleBookmarksBarEnabled = _shortcutToggleBookmarksBarEnabledCheckBox.Checked;
        _config.ShortcutToggleBookmarksBar = shortcutToggleBookmarksBar;
        _config.ShortcutRestoreClosedTabEnabled = _shortcutRestoreClosedTabEnabledCheckBox.Checked;
        _config.ShortcutRestoreClosedTab = shortcutRestoreClosedTab;
        _config.ShortcutOpenHistoryEnabled = _shortcutOpenHistoryEnabledCheckBox.Checked;
        _config.ShortcutOpenHistory = shortcutOpenHistory;
        _config.ShortcutFindInPageEnabled = _shortcutFindInPageEnabledCheckBox.Checked;
        _config.ShortcutFindInPage = shortcutFindInPage;
        _config.EnforcePluginPermissions = _enforcePluginPermissionCheckBox.Checked;
        _config.AllowedPluginPermissions = ParsePermissions(_allowedPermissionsTextBox.Text);
        _config.ToolbarButtons = _toolbarRows
            .Select((row, index) => new ToolbarButtonLayout
            {
                Id = row.Id,
                DisplayName = row.DisplayName,
                Visible = row.Visible,
                Order = index
            })
            .ToList();
        if (!TryNormalizeAddressCommandRows(out var normalizedAddressCommands, out var addressCommandError))
        {
            MessageBox.Show(addressCommandError, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.AddressCommands = normalizedAddressCommands;

        _pluginManager.UpdatePolicy(_config);
        _pluginManager.Reload();
        RefreshPluginGrid();

        if (!BrowserConfig.SaveDefault(_config, out var configError))
        {
            MessageBox.Show($"配置文件保存失败：{configError}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _onConfigChanged();
        _log("设置已保存");
        SetPendingChangeState(false, "配置已保存");
        _didSaveChanges = true;
        Close();
    }

    private void WirePendingChangeTracking(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control == _settingSearchTextBox)
            {
                continue;
            }

            switch (control)
            {
                case TextBox textBox:
                    textBox.TextChanged += (_, _) => MarkPendingChange("已修改文本配置");
                    break;
                case CheckBox checkBox:
                    checkBox.CheckedChanged += (_, _) => MarkPendingChange("已修改开关配置");
                    break;
                case ComboBox comboBox:
                    comboBox.SelectedIndexChanged += (_, _) => MarkPendingChange("已修改下拉配置");
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.ValueChanged += (_, _) => MarkPendingChange("已修改数值配置");
                    break;
                case DataGridView grid:
                    grid.CurrentCellDirtyStateChanged += (_, _) =>
                    {
                        if (grid.IsCurrentCellDirty)
                        {
                            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                        }
                    };
                    grid.CellValueChanged += (_, _) => MarkPendingChange("已修改表格配置");
                    grid.UserAddedRow += (_, _) => MarkPendingChange("已新增表格项");
                    grid.UserDeletedRow += (_, _) => MarkPendingChange("已删除表格项");
                    break;
            }

            WirePendingChangeTracking(control);
        }
    }

    private void MarkPendingChange(string detail)
    {
        if (_suppressPendingChangeTracking)
        {
            return;
        }

        SetPendingChangeState(true, detail);
    }

    private void SetPendingChangeState(bool pending, string detail)
    {
        _hasPendingChanges = pending;
        if (pending)
        {
            _changeHintPanel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Warning, AppTheme.SurfaceAlt, 0.72F)
                : AppTheme.Mix(AppTheme.Warning, Color.White, 0.88F);
            _changeHintLabel.ForeColor = AppTheme.IsDark
                ? Color.FromArgb(255, 237, 189)
                : Color.FromArgb(102, 72, 18);
            _changeHintLabel.Text = string.IsNullOrWhiteSpace(detail) ? "有更改尚未保存" : $"{detail} · 请点击“保存”";
            AppTheme.ApplyControlRegion(_changeHintPanel, 6);
            _changeHintPanel.Invalidate();
            return;
        }

        _changeHintPanel.BackColor = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.Success, AppTheme.SurfaceAlt, 0.76F)
                : AppTheme.Mix(AppTheme.Success, Color.White, 0.90F);
        _changeHintLabel.ForeColor = AppTheme.IsDark
            ? Color.FromArgb(191, 244, 208)
            : Color.FromArgb(25, 90, 54);
        _changeHintLabel.Text = string.IsNullOrWhiteSpace(detail) ? "当前配置已同步" : detail;
        AppTheme.ApplyControlRegion(_changeHintPanel, 6);
        _changeHintPanel.Invalidate();
    }

    private void ShowBookmarkManager()
    {
        if (_bookmarksSettingsWindow is null || _bookmarksSettingsWindow.IsDisposed)
        {
            _bookmarksSettingsWindow = new BookmarksSettingsWindow(
                _bookmarkManager,
                () =>
                {
                    RefreshBookmarkCount();
                    _onBookmarksChanged();
                },
                _log);
        }

        // 中文注释：窗口已可见时再调 Show(owner) 会抛异常，改为激活前置。
        if (!_bookmarksSettingsWindow.Visible)
        {
            _bookmarksSettingsWindow.Show(this);
        }
        else if (_bookmarksSettingsWindow.WindowState == FormWindowState.Minimized)
        {
            _bookmarksSettingsWindow.WindowState = FormWindowState.Normal;
        }

        _bookmarksSettingsWindow.BringToFront();
        _bookmarksSettingsWindow.Activate();
    }

    private void ImportBookmarks()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入书签",
            Filter = "书签文件 (*.html;*.htm;*.json)|*.html;*.htm;*.json|HTML 文件 (*.html;*.htm)|*.html;*.htm|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var mode = MessageBox.Show(
            "导入方式：\r\n是 = 替换当前书签\r\n否 = 合并到当前书签",
            "导入书签",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (mode == DialogResult.Cancel)
        {
            return;
        }

        var replace = mode == DialogResult.Yes;
        if (!_bookmarkManager.ImportFromFile(dialog.FileName, replace))
        {
            MessageBox.Show("导入失败，请确认文件格式。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshBookmarkCount();
        _onBookmarksChanged();
        _log(replace ? "书签已导入（替换模式）" : "书签已导入（合并模式）");
    }

    private void ExportBookmarks()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出书签",
            FileName = $"bookmarks-{DateTime.Now:yyyyMMdd-HHmmss}.html",
            Filter = "HTML 文件 (*.html)|*.html|JSON 文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!_bookmarkManager.ExportToFile(dialog.FileName))
        {
            MessageBox.Show("导出失败。", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _log("书签已导出");
    }

    private void RefreshBookmarkCount()
    {
        _bookmarkCountLabel.Text = $"当前书签节点总数：{CountNodes(_bookmarkManager.TopLevelItems)}";
    }

    private static int CountNodes(IEnumerable<BookmarkNode> nodes)
    {
        var total = 0;
        foreach (var node in nodes)
        {
            total++;
            total += CountNodes(node.Children);
        }

        return total;
    }

    private static bool TryBuildUri(string rawInput, out Uri uri)
    {
        var value = rawInput.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute is not null)
        {
            uri = absolute;
            return true;
        }

        if (Uri.TryCreate($"https://{value}", UriKind.Absolute, out var httpsUri) && httpsUri is not null)
        {
            uri = httpsUri;
            return true;
        }

        uri = new Uri("about:blank");
        return false;
    }

    private bool TryNormalizeShortcutInput(
        string actionName,
        bool enabled,
        TextBox textBox,
        string fallbackShortcut,
        out string normalized)
    {
        var raw = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (!enabled)
            {
                normalized = ShortcutParser.Normalize(fallbackShortcut);
                textBox.Text = normalized;
                return true;
            }

            MessageBox.Show($"快捷键“{actionName}”不能为空。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            textBox.Focus();
            normalized = string.Empty;
            return false;
        }

        if (!ShortcutParser.TryParse(raw, out var keyData))
        {
            MessageBox.Show(
                $"快捷键“{actionName}”格式无效，请使用例如 Ctrl+D / Ctrl+Shift+L / F12 的格式。",
                "保存失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            textBox.Focus();
            normalized = string.Empty;
            return false;
        }

        normalized = ShortcutParser.ToDisplayString(keyData);
        textBox.Text = normalized;
        return true;
    }

    private static bool TryNormalizeSearchTemplate(string rawTemplate, out string normalizedTemplate)
    {
        normalizedTemplate = string.Empty;
        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return false;
        }

        var template = rawTemplate.Trim();
        if (!template.Contains("{query}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sample = template.Replace("{query}", "sample", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(sample, UriKind.Absolute, out var sampleUri) || sampleUri is null)
        {
            return false;
        }

        if (sampleUri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        normalizedTemplate = template;
        return true;
    }

    private static bool TryNormalizeTaskbarIconPath(string rawPath, out string normalizedPath, out string errorMessage)
    {
        normalizedPath = string.Empty;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return true;
        }

        var path = rawPath.Trim();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp"))
        {
            errorMessage = "任务栏图标仅支持 .ico/.png/.jpg/.jpeg/.bmp 文件。";
            return false;
        }

        if (!File.Exists(path))
        {
            errorMessage = "任务栏图标文件不存在，请重新选择。";
            return false;
        }

        normalizedPath = path;
        return true;
    }

    private void UpdateTaskbarIconPreview()
    {
        if (string.IsNullOrWhiteSpace(_taskbarIconPathTextBox.Text))
        {
            SetTaskbarIconPreview(CreateDefaultTaskbarIconPreviewImage(), "预览：内置默认图标");
            return;
        }

        if (!TryNormalizeTaskbarIconPath(_taskbarIconPathTextBox.Text, out var normalizedPath, out var errorMessage))
        {
            SetTaskbarIconPreview(CreateDefaultTaskbarIconPreviewImage(), $"预览：{errorMessage}");
            return;
        }

        if (TryLoadTaskbarIconPreviewImage(normalizedPath, out var image))
        {
            SetTaskbarIconPreview(image, $"预览：{Path.GetFileName(normalizedPath)}");
            return;
        }

        SetTaskbarIconPreview(CreateDefaultTaskbarIconPreviewImage(), "预览：图标读取失败，已回退默认图标");
    }

    private void SetTaskbarIconPreview(Image image, string statusText)
    {
        var oldImage = _taskbarIconPreviewImage;
        _taskbarIconPreviewImage = image;
        _taskbarIconPreviewBox.Image = _taskbarIconPreviewImage;
        _taskbarIconPreviewLabel.Text = statusText;
        oldImage?.Dispose();
    }

    private static bool TryLoadTaskbarIconPreviewImage(string iconPath, out Image image)
    {
        image = null!;
        try
        {
            var extension = Path.GetExtension(iconPath).ToLowerInvariant();
            if (string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var icon = new Icon(iconPath, new Size(32, 32));
                image = icon.ToBitmap();
                return true;
            }

            using var source = Image.FromFile(iconPath);
            image = new Bitmap(source, new Size(32, 32));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Image CreateDefaultTaskbarIconPreviewImage()
    {
        var image = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(45, 92, 165));
        using var framePen = new Pen(Color.FromArgb(31, 62, 116), 2F);
        graphics.DrawRectangle(framePen, 1, 1, 30, 30);
        using var markBrush = new SolidBrush(Color.FromArgb(230, 242, 255));
        graphics.FillRectangle(markBrush, 7, 6, 5, 20);
        graphics.FillRectangle(markBrush, 12, 21, 13, 5);
        using var accentBrush = new SolidBrush(Color.FromArgb(255, 199, 78));
        graphics.FillEllipse(accentBrush, 20, 6, 6, 6);
        return image;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _taskbarIconPreviewBox.Image = null;
            _taskbarIconPreviewImage?.Dispose();
            _taskbarIconPreviewImage = null;
        }

        base.Dispose(disposing);
    }

    private static bool TryValidateShortcutConflicts(
        IEnumerable<ShortcutMapping> mappings,
        out string message)
    {
        var conflicts = mappings
            .Where(mapping => mapping.Enabled)
            .GroupBy(mapping => mapping.Shortcut, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (conflicts.Length == 0)
        {
            message = string.Empty;
            return true;
        }

        var lines = conflicts.Select(group =>
            $"{group.Key} 被重复占用：{string.Join("、", group.Select(mapping => mapping.ActionName))}");
        message = $"检测到快捷键冲突，请修改后再保存：{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        return false;
    }

    private bool TryNormalizeAddressCommandRows(
        out List<AddressCommandLayout> normalizedRows,
        out string errorMessage)
    {
        normalizedRows = [];
        errorMessage = string.Empty;
        var usedEnabledCommands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _addressCommandRows.Count; i++)
        {
            var row = _addressCommandRows[i];
            var normalizedToken = (row.Command ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                errorMessage = $"地址栏指令“{row.DisplayName}”不能为空。";
                FocusAddressCommandRow(i);
                return false;
            }

            if (normalizedToken.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_')))
            {
                errorMessage = $"地址栏指令“{row.DisplayName}”格式无效，仅支持字母、数字、-、_。";
                FocusAddressCommandRow(i);
                return false;
            }

            row.Command = normalizedToken;
            row.Order = i;
            row.OrderDisplay = (i + 1).ToString();

            if (row.Enabled && usedEnabledCommands.TryGetValue(normalizedToken, out var conflictDisplayName))
            {
                errorMessage = $"地址栏指令冲突：{row.DisplayName} 与 {conflictDisplayName} 都使用了 :{normalizedToken}。";
                FocusAddressCommandRow(i);
                return false;
            }

            if (row.Enabled)
            {
                usedEnabledCommands[normalizedToken] = row.DisplayName;
            }

            normalizedRows.Add(new AddressCommandLayout
            {
                Id = row.Id,
                DisplayName = row.DisplayName,
                Enabled = row.Enabled,
                Command = normalizedToken,
                Order = i
            });
        }

        _addressCommandGrid.Refresh();
        return true;
    }

    private void FocusAddressCommandRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _addressCommandGrid.Rows.Count)
        {
            return;
        }

        NavigateToCategory(1);
        _addressCommandGrid.Focus();
        _addressCommandGrid.ClearSelection();
        _addressCommandGrid.Rows[rowIndex].Selected = true;
        _addressCommandGrid.CurrentCell = _addressCommandGrid.Rows[rowIndex].Cells[2];
    }

    private static string[] ParsePermissions(string raw)
    {
        return raw
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class SettingSearchItem(
        string keywords,
        string displayText,
        int tabIndex,
        Func<Control?>? focusTargetFactory)
    {
        private readonly string _keywords = keywords;
        public int TabIndex { get; } = tabIndex;
        public Func<Control?>? FocusTargetFactory { get; } = focusTargetFactory;

        public bool Matches(string query)
        {
            return _keywords.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   displayText.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return displayText;
        }
    }

    private sealed record ShortcutMapping(string ActionName, bool Enabled, string Shortcut);
    private sealed class PluginRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public string Status { get; init; } = string.Empty;
        public string DirectoryPath { get; init; } = string.Empty;
    }

    private sealed class ToolbarButtonRow
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public string OrderDisplay { get; set; } = string.Empty;
    }

    private sealed class AddressCommandRow
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool Enabled { get; set; }
        public string Command { get; set; } = string.Empty;
        public int Order { get; set; }
        public string OrderDisplay { get; set; } = string.Empty;
    }

    private sealed class CredentialRow
    {
        public Guid Id { get; init; }
        public string Site { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string PasswordMasked { get; init; } = string.Empty;
        public string PasswordPlain { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
    }

    // 中文注释：登录密码自动保存的站点规则可编辑行（域名/IP + 允许保存/不保存），绑定 DataGridView 直接编辑。
    private sealed class CredentialAutosaveRuleRow
    {
        public string Host { get; set; } = string.Empty;
        public string Mode { get; set; } = "允许保存";
    }

    // 中文注释：自定义搜索引擎可编辑行，绑定 DataGridView 直接编辑。
    private sealed class CustomSearchEngineRow
    {
        public string Name { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
    }
}

