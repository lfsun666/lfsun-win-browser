using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser;

/// <summary>
/// 中文注释：快捷键一览窗口。把「浏览器可自定义快捷键 / 浏览器内置快捷键 / 插件快捷键 / 地址栏命令」
/// 全部汇总到一个窗口里，分分类、可搜索、可一键复制，方便用户完整查看所有快捷键使用情况。
/// </summary>
public sealed class ShortcutsOverviewWindow : Form
{
    // 中文注释：单条快捷键记录的数据模型（仅用于本窗口展示）。
    private sealed record ShortcutItem(string Category, string Function, string Keys, string Scope, string Note);

    private readonly BrowserConfig _config;
    private readonly IReadOnlyList<(string Id, string Name, string Shortcut, bool Enabled)> _pluginBindings;

    private readonly TextBox _searchBox = new();
    private readonly Label _countLabel = new();
    private readonly ListView _listView = new();
    private readonly Button _copyButton = new();
    private readonly Button _closeButton = new();

    private List<ShortcutItem> _allItems = new();

    public ShortcutsOverviewWindow(
        BrowserConfig config,
        IReadOnlyList<(string Id, string Name, string Shortcut, bool Enabled)> pluginBindings)
    {
        _config = config;
        _pluginBindings = pluginBindings;

        Text = "快捷键一览";
        ShowIcon = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Width = 820;
        Height = 600;
        Font = AppTheme.CreateFont(9.2F);

        BuildLayout();
        ApplyTheme();
        RefreshData();
    }

    private void BuildLayout()
    {
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 9, 12, 9)
        };

        var searchLabel = new Label
        {
            Text = "搜索：",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 6, 0)
        };
        _searchBox.Width = 240;
        AppTheme.ApplyTextInput(_searchBox);
        _searchBox.PlaceholderText = "按功能 / 快捷键 / 说明筛选";
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        _countLabel.AutoSize = true;
        _countLabel.Anchor = AnchorStyles.Left;
        _countLabel.Margin = new Padding(12, 0, 0, 0);
        _countLabel.Font = AppTheme.CreateFont(9F);
        _countLabel.ForeColor = AppTheme.TextSecondary;

        _copyButton.Text = "复制全部";
        _copyButton.Anchor = AnchorStyles.Right;
        _copyButton.Click += (_, _) => CopyAll();
        AppTheme.ApplyPrimaryButton(_copyButton, emphasize: false);

        _closeButton.Text = "关闭";
        _closeButton.Anchor = AnchorStyles.Right;
        _closeButton.Click += (_, _) => Close();
        AppTheme.ApplyPrimaryButton(_closeButton, emphasize: true);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false
        };
        flow.Controls.Add(searchLabel);
        flow.Controls.Add(_searchBox);
        flow.Controls.Add(_countLabel);
        flow.Controls.Add(new Panel { Width = 8, Height = 1 });
        flow.Controls.Add(_copyButton);
        flow.Controls.Add(_closeButton);
        topPanel.Controls.Add(flow);

        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.MultiSelect = false;
        _listView.ShowGroups = true;
        _listView.HideSelection = false;
        _listView.GridLines = false;
        _listView.Columns.Add("功能", 232);
        _listView.Columns.Add("快捷键", 210);
        _listView.Columns.Add("范围", 84);
        _listView.Columns.Add("说明", 240);

        Controls.Add(_listView);
        Controls.Add(topPanel);
        topPanel.BringToFront();
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        ForeColor = AppTheme.TextPrimary;
        _listView.BackColor = AppTheme.InputBackground;
        _listView.ForeColor = AppTheme.TextPrimary;
        _listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _countLabel.ForeColor = AppTheme.TextSecondary;
    }

    /// <summary>
    /// 中文注释：每次打开时重新汇总快捷键（配置/插件可能在设置里被改过）。
    /// </summary>
    public void RefreshData()
    {
        _allItems = BuildAllShortcuts();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = _searchBox.Text.Trim().ToLowerInvariant();
        var items = string.IsNullOrWhiteSpace(keyword)
            ? _allItems
            : _allItems.Where(item =>
                item.Function.ToLowerInvariant().Contains(keyword) ||
                item.Keys.ToLowerInvariant().Contains(keyword) ||
                item.Note.ToLowerInvariant().Contains(keyword) ||
                item.Category.ToLowerInvariant().Contains(keyword)).ToList();

        _listView.BeginUpdate();
        _listView.Groups.Clear();
        _listView.Items.Clear();

        var groups = items
            .Select(item => item.Category)
            .Distinct()
            .ToList();
        var groupMap = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupName in groups)
        {
            var group = new ListViewGroup(groupName, HorizontalAlignment.Left);
            _listView.Groups.Add(group);
            groupMap[groupName] = group;
        }

        foreach (var item in items)
        {
            var listItem = new ListViewItem(item.Function) { Group = groupMap[item.Category] };
            listItem.SubItems.Add(item.Keys);
            listItem.SubItems.Add(item.Scope);
            listItem.SubItems.Add(item.Note);
            _listView.Items.Add(listItem);
        }

        _listView.EndUpdate();
        _countLabel.Text = $"共 {items.Count} 条快捷键（可自定义 {CountByScope(items, "可改键")} · 内置 {CountByScope(items, "内置")} · 插件 {CountByScope(items, "已启用") + CountByScope(items, "未启用")}）";
    }

    private static int CountByScope(IEnumerable<ShortcutItem> items, string scope) =>
        items.Count(item => item.Scope == scope);

    private void CopyAll()
    {
        if (_allItems.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("LfsunBrowser 快捷键一览");
        builder.AppendLine();
        foreach (var group in _listView.Groups.Cast<ListViewGroup>())
        {
            builder.AppendLine($"## {group.Header}");
            foreach (ListViewItem item in group.Items)
            {
                var keys = item.SubItems[1].Text;
                var scope = item.SubItems[2].Text;
                var note = item.SubItems[3].Text;
                var notePart = string.IsNullOrWhiteSpace(note) ? string.Empty : $"  {note}";
                builder.AppendLine($"- {item.Text}：`{keys}`  [{scope}]{notePart}");
            }

            builder.AppendLine();
        }

        try
        {
            Clipboard.SetText(builder.ToString());
            MessageBox.Show(this, "已复制全部快捷键到剪贴板。", "快捷键一览", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"复制失败：{exception.Message}", "快捷键一览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // 中文注释：汇总四大类快捷键。
    private List<ShortcutItem> BuildAllShortcuts()
    {
        var items = new List<ShortcutItem>();

        // 1) 浏览器可自定义快捷键（直接读当前配置）。
        AddConfigurable(items, "收藏当前页", _config.ShortcutAddBookmarkEnabled, _config.ShortcutAddBookmark);
        AddConfigurable(items, "新建标签页", _config.ShortcutNewTabEnabled, _config.ShortcutNewTab);
        AddConfigurable(items, "关闭当前标签页", _config.ShortcutCloseTabEnabled, _config.ShortcutCloseTab);
        AddConfigurable(items, "聚焦地址栏", _config.ShortcutFocusAddressEnabled, _config.ShortcutFocusAddress);
        AddConfigurable(items, "刷新页面", _config.ShortcutRefreshEnabled, _config.ShortcutRefresh);
        AddConfigurable(items, "打开开发者工具", _config.ShortcutDevToolsEnabled, _config.ShortcutDevTools);
        AddConfigurable(items, "显示 / 隐藏书签栏", _config.ShortcutToggleBookmarksBarEnabled, _config.ShortcutToggleBookmarksBar);
        AddConfigurable(items, "恢复最近关闭标签", _config.ShortcutRestoreClosedTabEnabled, _config.ShortcutRestoreClosedTab);
        AddConfigurable(items, "打开历史记录页", _config.ShortcutOpenHistoryEnabled, _config.ShortcutOpenHistory);
        AddConfigurable(items, "页内查找", _config.ShortcutFindInPageEnabled, _config.ShortcutFindInPage);

        // 2) 浏览器内置（固定）快捷键。
        const string builtin = "浏览器快捷键（内置）";
        items.Add(new(builtin, "打开命令面板", "Ctrl+Shift+P", "内置", "地址栏输入 : 触发命令 / @ 切换标签"));
        items.Add(new(builtin, "标签快速切换", "Ctrl+K", "内置", "地址栏输入 @ 关键词跳转"));
        items.Add(new(builtin, "切换沉浸（禅）模式", "Ctrl+Shift+Z", "内置", ""));
        items.Add(new(builtin, "保存当前为工作区", "Ctrl+Shift+S", "内置", ""));
        items.Add(new(builtin, "会话 / 工作区管理", "Ctrl+Shift+O", "内置", ""));
        items.Add(new(builtin, "关闭其他标签（保留当前）", "Ctrl+Shift+W", "内置", ""));
        items.Add(new(builtin, "关闭左侧标签", "Ctrl+Shift+L", "内置", ""));
        items.Add(new(builtin, "关闭右侧标签", "Ctrl+Shift+R", "内置", ""));
        items.Add(new(builtin, "清理重复标签", "Ctrl+Shift+D", "内置", ""));
        items.Add(new(builtin, "后退", "Alt+←", "内置", ""));
        items.Add(new(builtin, "前进", "Alt+→", "内置", ""));
        items.Add(new(builtin, "刷新页面（替代）", "Ctrl+R", "内置", "F5 亦可（F5 可在设置改键）"));
        items.Add(new(builtin, "左移当前标签", "Ctrl+Shift+PageUp / Ctrl+Shift+←", "内置", ""));
        items.Add(new(builtin, "右移当前标签", "Ctrl+Shift+PageDown / Ctrl+Shift+→", "内置", ""));
        items.Add(new(builtin, "切换到下一个标签", "Ctrl+Tab", "内置", ""));
        items.Add(new(builtin, "切换到上一个标签", "Ctrl+Shift+Tab", "内置", ""));
        items.Add(new(builtin, "跳到第 N 个标签", "Ctrl+1 … Ctrl+9", "内置", ""));
        items.Add(new(builtin, "聚焦地址栏（替代）", "Alt+D", "内置", "Ctrl+L 亦可（可在设置改键）"));
        items.Add(new(builtin, "打开无痕窗口", "Ctrl+Shift+N", "内置", ""));
        items.Add(new(builtin, "打开独立双开窗口", "Ctrl+Shift+M", "内置", "全新独立 profile，数据不共享"));
        items.Add(new(builtin, "查看页面源码", "Ctrl+U", "内置", "新标签打开 view-source"));
        items.Add(new(builtin, "页面放大", "Ctrl+加号", "内置", "含小键盘 +"));
        items.Add(new(builtin, "页面缩小", "Ctrl+减号", "内置", "含小键盘 -"));
        items.Add(new(builtin, "页面缩放重置", "Ctrl+0", "内置", "含小键盘 0"));

        // 3) 插件快捷键（来自插件 manifest 的 runShortcut）。
        const string pluginCat = "插件快捷键";
        if (_pluginBindings.Count == 0)
        {
            items.Add(new(pluginCat, "（暂无声明运行快捷键的插件）", "—", "插件", "在插件 manifest 中配置 runShortcut 后会出现在此"));
        }
        else
        {
            foreach (var (_, name, shortcut, enabled) in _pluginBindings)
            {
                var display = ShortcutParser.TryParse(shortcut, out var keys) ? ShortcutParser.ToDisplayString(keys) : shortcut;
                items.Add(new(
                    pluginCat,
                    $"在页面运行：{name}",
                    string.IsNullOrWhiteSpace(display) ? "—" : display,
                    enabled ? "已启用" : "未启用",
                    enabled ? "在工具栏/地址栏聚焦时按此键对当前页面运行" : "插件未启用，快捷键不会触发"));
            }
        }

        // 4) 地址栏命令（补充，非按键但也是常用触发方式）。
        const string cmd = "地址栏命令（补充）";
        items.Add(new(cmd, "打开开发者工具", ":devtools", "地址栏", "在地址栏输入"));
        items.Add(new(cmd, "查看页面源码", ":viewsource", "地址栏", ""));
        items.Add(new(cmd, "切换深色模式", ":darkmode", "地址栏", ""));
        items.Add(new(cmd, "切换浅色模式", ":lightmode", "地址栏", ""));
        items.Add(new(cmd, "重载插件", ":reloadplugins", "地址栏", ""));
        items.Add(new(cmd, "标签批处理", ":tabops <子命令>", "地址栏", "如 :tabops closeothers"));
        items.Add(new(cmd, "切换书签栏", ":bookmarksbar <on/off/toggle>", "地址栏", ""));
        items.Add(new(cmd, "切换沉浸模式", ":zen <on/off/toggle>", "地址栏", ""));

        return items;
    }

    private static void AddConfigurable(List<ShortcutItem> items, string name, bool enabled, string rawShortcut)
    {
        var display = ShortcutParser.TryParse(rawShortcut, out var keys) ? ShortcutParser.ToDisplayString(keys) : rawShortcut;
        items.Add(new(
            "浏览器快捷键（可自定义）",
            name,
            string.IsNullOrWhiteSpace(display) ? "—" : display,
            enabled ? "可改键" : "已禁用",
            enabled ? "可在「设置 → 快捷键」中修改" : "已在「设置 → 快捷键」中禁用"));
    }
}
