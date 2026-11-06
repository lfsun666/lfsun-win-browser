using Lfsun.Win.Browser.History;

namespace Lfsun.Win.Browser.UI.SidePanel;

/// <summary>
/// 中文注释：侧边栏快速历史记录视图。
/// 支持即时搜索、双击访问、快捷删除及清空。
/// </summary>
public sealed class QuickHistoryView : UserControl
{
    private readonly HistoryManager _historyManager;
    private readonly Func<Uri, bool, Task> _navigateCallback;
    private readonly Action<string> _toastCallback;
    private readonly Action _openHistoryWindowCallback;

    private readonly TextBox _searchBox = new();
    private readonly ListBox _historyListBox = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _ctxOpen = new("在当前标签打开");
    private readonly ToolStripMenuItem _ctxOpenNewTab = new("在新标签打开");
    private readonly ToolStripMenuItem _ctxCopyUrl = new("复制网址");
    private readonly ToolStripMenuItem _ctxDelete = new("删除此条记录");
    private readonly ToolStripMenuItem _ctxOpenFull = new("打开完整历史记录窗口");

    public QuickHistoryView(
        HistoryManager historyManager,
        Func<Uri, bool, Task> navigateCallback,
        Action<string> toastCallback,
        Action openHistoryWindowCallback)
    {
        _historyManager = historyManager;
        _navigateCallback = navigateCallback;
        _toastCallback = toastCallback;
        _openHistoryWindowCallback = openHistoryWindowCallback;

        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        BuildContextMenu();
        ApplyTheme();
        ReloadHistory();
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

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.PlaceholderText = "🔍 搜索浏览历史...";
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.Font = AppTheme.CreateFont(9F);
        _searchBox.Margin = new Padding(0, 0, 0, 6);
        _searchBox.TextChanged += (_, _) => FilterHistory(_searchBox.Text.Trim());
        mainLayout.Controls.Add(_searchBox, 0, 0);

        _historyListBox.Dock = DockStyle.Fill;
        _historyListBox.BorderStyle = BorderStyle.None;
        _historyListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _historyListBox.ItemHeight = 44;
        _historyListBox.DrawItem += OnDrawHistoryItem;
        _historyListBox.DoubleClick += async (_, _) =>
        {
            if (_historyListBox.SelectedItem is HistoryEntry entry && Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                var openNewTab = (ModifierKeys & Keys.Control) == Keys.Control;
                await _navigateCallback(uri, openNewTab);
            }
        };

        mainLayout.Controls.Add(_historyListBox, 0, 1);
        Controls.Add(mainLayout);
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.AddRange(
        [
            _ctxOpen,
            _ctxOpenNewTab,
            new ToolStripSeparator(),
            _ctxCopyUrl,
            _ctxDelete,
            new ToolStripSeparator(),
            _ctxOpenFull
        ]);

        _ctxOpen.Click += async (_, _) =>
        {
            if (_historyListBox.SelectedItem is HistoryEntry entry && Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                await _navigateCallback(uri, false);
            }
        };

        _ctxOpenNewTab.Click += async (_, _) =>
        {
            if (_historyListBox.SelectedItem is HistoryEntry entry && Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                await _navigateCallback(uri, true);
            }
        };

        _ctxCopyUrl.Click += (_, _) =>
        {
            if (_historyListBox.SelectedItem is HistoryEntry entry)
            {
                Clipboard.SetText(entry.Url);
                _toastCallback("已复制网址");
            }
        };

        _ctxDelete.Click += (_, _) =>
        {
            if (_historyListBox.SelectedItem is HistoryEntry entry)
            {
                _historyManager.RemoveEntriesByIds([entry.Id]);
                ReloadHistory();
                _toastCallback("已删除历史记录");
            }
        };

        _ctxOpenFull.Click += (_, _) => _openHistoryWindowCallback();
        _historyListBox.ContextMenuStrip = _contextMenu;
    }

    public void ReloadHistory()
    {
        FilterHistory(_searchBox.Text.Trim());
    }

    private void FilterHistory(string filter)
    {
        _historyListBox.BeginUpdate();
        _historyListBox.Items.Clear();

        var query = string.IsNullOrWhiteSpace(filter)
            ? _historyManager.Entries.Take(100)
            : _historyManager.Entries
                .Where(e => (!string.IsNullOrEmpty(e.Title) && e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(e.Url) && e.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .Take(100);

        foreach (var entry in query)
        {
            _historyListBox.Items.Add(entry);
        }

        _historyListBox.EndUpdate();
    }

    private void OnDrawHistoryItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= _historyListBox.Items.Count)
        {
            return;
        }

        var entry = (HistoryEntry)_historyListBox.Items[eventArgs.Index];
        var isSelected = (eventArgs.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = eventArgs.Bounds;

        using var bgBrush = new SolidBrush(isSelected ? (AppTheme.IsDark ? Color.FromArgb(44, 56, 78) : Color.FromArgb(220, 235, 252)) : AppTheme.Surface);
        eventArgs.Graphics.FillRectangle(bgBrush, bounds);

        var title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Url : entry.Title;
        var timeStr = entry.VisitedAtUtc.ToLocalTime().ToString("MM-dd HH:mm");

        var titleRect = new Rectangle(bounds.Left + 8, bounds.Top + 4, bounds.Width - 90, 18);
        var urlRect = new Rectangle(bounds.Left + 8, bounds.Top + 22, bounds.Width - 16, 16);
        var timeRect = new Rectangle(bounds.Right - 84, bounds.Top + 4, 80, 18);

        using var titleFont = AppTheme.CreateFont(8.8F, FontStyle.Bold);
        using var urlFont = AppTheme.CreateFont(8.0F);
        using var textBrush = new SolidBrush(AppTheme.TextPrimary);
        using var secondaryBrush = new SolidBrush(AppTheme.TextSecondary);

        var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        var rightFormat = new StringFormat { Alignment = StringAlignment.Far };

        eventArgs.Graphics.DrawString(title, titleFont, textBrush, titleRect, format);
        eventArgs.Graphics.DrawString(entry.Url, urlFont, secondaryBrush, urlRect, format);
        eventArgs.Graphics.DrawString(timeStr, urlFont, secondaryBrush, timeRect, rightFormat);

        using var linePen = new Pen(AppTheme.PanelBorder, 1);
        eventArgs.Graphics.DrawLine(linePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _searchBox.BackColor = AppTheme.SurfaceAlt;
        _searchBox.ForeColor = AppTheme.TextPrimary;
        _historyListBox.BackColor = AppTheme.Surface;
        _historyListBox.ForeColor = AppTheme.TextPrimary;
        AppTheme.ApplyContextMenu(_contextMenu);
    }
}
