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
    private readonly Panel _listCardPanel = new();
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
        AppTheme.ApplyTextInput(_searchBox);
        _searchBox.Margin = new Padding(0, 0, 0, 6);
        _searchBox.TextChanged += (_, _) => FilterHistory(_searchBox.Text.Trim());
        mainLayout.Controls.Add(_searchBox, 0, 0);

        _listCardPanel.Dock = DockStyle.Fill;
        _listCardPanel.Padding = new Padding(2);
        _listCardPanel.BackColor = AppTheme.Surface;
        ApplyCardVisual(_listCardPanel);

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

        _listCardPanel.Controls.Add(_historyListBox);
        mainLayout.Controls.Add(_listCardPanel, 0, 1);
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

        var cardRect = Rectangle.Inflate(bounds, -2, -1);
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (isSelected)
        {
            var selectBg = AppTheme.IsDark
                ? AppTheme.Mix(AppTheme.AccentHover, AppTheme.SurfaceAlt, 0.35F)
                : AppTheme.Mix(AppTheme.AccentHover, Color.White, 0.60F);
            using var bgBrush = new SolidBrush(selectBg);
            using var path = AppTheme.BuildRoundedRect(cardRect, 6);
            eventArgs.Graphics.FillPath(bgBrush, path);
        }
        else
        {
            using var bgBrush = new SolidBrush(AppTheme.Surface);
            eventArgs.Graphics.FillRectangle(bgBrush, bounds);
        }

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
        _listCardPanel.BackColor = AppTheme.Surface;
        AppTheme.ApplyTextInput(_searchBox);
        _historyListBox.BackColor = AppTheme.Surface;
        _historyListBox.ForeColor = AppTheme.TextPrimary;
        AppTheme.ApplyContextMenu(_contextMenu);
        UpdateCardRegion(_listCardPanel);
    }

    private static void ApplyCardVisual(Panel panel)
    {
        panel.Paint -= HandleCardPaint;
        panel.Paint += HandleCardPaint;
        panel.SizeChanged -= HandleCardSizeChanged;
        panel.SizeChanged += HandleCardSizeChanged;
        UpdateCardRegion(panel);
    }

    private static void HandleCardSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Panel panel)
        {
            UpdateCardRegion(panel);
            panel.Invalidate();
        }
    }

    private static void UpdateCardRegion(Panel panel)
    {
        if (panel.Width <= 0 || panel.Height <= 0)
        {
            return;
        }

        var old = panel.Region;
        using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, panel.Width, panel.Height), 8);
        panel.Region = new Region(path);
        old?.Dispose();
    }

    private static void HandleCardPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;
        var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var borderPen = new Pen(AppTheme.PanelBorder, 1F);
        using var path = AppTheme.BuildRoundedRect(rect, 8);
        e.Graphics.DrawPath(borderPen, path);
    }
}
