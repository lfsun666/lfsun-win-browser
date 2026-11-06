using Lfsun.Win.Browser.Bookmarks;

namespace Lfsun.Win.Browser.UI.SidePanel;

/// <summary>
/// 中文注释：侧边栏快速书签视图。
/// 支持即时搜索、树状层级浏览、一键打开、Ctrl 新标签打开及右键便捷菜单。
/// </summary>
public sealed class QuickBookmarksView : UserControl
{
    private readonly BookmarkManager _bookmarkManager;
    private readonly Func<Uri, bool, Task> _navigateCallback;
    private readonly Action<string> _toastCallback;
    private readonly Action _openManagerCallback;

    private readonly TextBox _searchBox = new();
    private readonly TreeView _tree = new();
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _ctxOpen = new("在当前标签打开");
    private readonly ToolStripMenuItem _ctxOpenNewTab = new("在新标签打开");
    private readonly ToolStripMenuItem _ctxCopyUrl = new("复制网址");
    private readonly ToolStripMenuItem _ctxOpenManager = new("打开完整书签管理器");

    public QuickBookmarksView(
        BookmarkManager bookmarkManager,
        Func<Uri, bool, Task> navigateCallback,
        Action<string> toastCallback,
        Action openManagerCallback)
    {
        _bookmarkManager = bookmarkManager;
        _navigateCallback = navigateCallback;
        _toastCallback = toastCallback;
        _openManagerCallback = openManagerCallback;

        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Surface;

        BuildLayout();
        BuildContextMenu();
        ApplyTheme();
        ReloadBookmarks();
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
        _searchBox.PlaceholderText = "🔍 搜索书签 (名称或网址)...";
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.Font = AppTheme.CreateFont(9F);
        _searchBox.Margin = new Padding(0, 0, 0, 6);
        _searchBox.TextChanged += (_, _) => FilterBookmarks(_searchBox.Text.Trim());
        mainLayout.Controls.Add(_searchBox, 0, 0);

        _tree.Dock = DockStyle.Fill;
        _tree.BorderStyle = BorderStyle.None;
        _tree.ShowLines = true;
        _tree.ShowPlusMinus = true;
        _tree.FullRowSelect = true;
        _tree.HideSelection = false;
        _tree.Font = AppTheme.CreateFont(9F);
        _tree.NodeMouseClick += async (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Right)
            {
                _tree.SelectedNode = eventArgs.Node;
                return;
            }

            if (eventArgs.Button == MouseButtons.Left && eventArgs.Node?.Tag is BookmarkNode bookmark && bookmark.IsLink)
            {
                if (Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var uri))
                {
                    var openNewTab = (ModifierKeys & Keys.Control) == Keys.Control;
                    await _navigateCallback(uri, openNewTab);
                }
            }
        };

        mainLayout.Controls.Add(_tree, 0, 1);
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
            new ToolStripSeparator(),
            _ctxOpenManager
        ]);

        _ctxOpen.Click += async (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is BookmarkNode node && node.IsLink && Uri.TryCreate(node.Url, UriKind.Absolute, out var uri))
            {
                await _navigateCallback(uri, false);
            }
        };

        _ctxOpenNewTab.Click += async (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is BookmarkNode node && node.IsLink && Uri.TryCreate(node.Url, UriKind.Absolute, out var uri))
            {
                await _navigateCallback(uri, true);
            }
        };

        _ctxCopyUrl.Click += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is BookmarkNode node && node.IsLink)
            {
                Clipboard.SetText(node.Url);
                _toastCallback("已复制书签网址");
            }
        };

        _ctxOpenManager.Click += (_, _) => _openManagerCallback();
        _tree.ContextMenuStrip = _contextMenu;
    }

    public void ReloadBookmarks()
    {
        FilterBookmarks(_searchBox.Text.Trim());
    }

    private void FilterBookmarks(string filter)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (var item in _bookmarkManager.TopLevelItems)
            {
                _tree.Nodes.Add(CreateTreeNode(item));
            }
            _tree.ExpandAll();
        }
        else
        {
            var matched = FindBookmarksRecursive(_bookmarkManager.Root, filter);
            foreach (var bookmark in matched)
            {
                var node = new TreeNode($"🌐 {bookmark.Name}")
                {
                    Tag = bookmark,
                    ToolTipText = bookmark.Url
                };
                _tree.Nodes.Add(node);
            }
        }

        _tree.EndUpdate();
    }

    private static List<BookmarkNode> FindBookmarksRecursive(BookmarkNode parent, string filter)
    {
        var list = new List<BookmarkNode>();
        foreach (var child in parent.Children)
        {
            if (child.IsLink && (child.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || child.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(child);
            }
            if (child.IsFolder)
            {
                list.AddRange(FindBookmarksRecursive(child, filter));
            }
        }
        return list;
    }

    private static TreeNode CreateTreeNode(BookmarkNode node)
    {
        var treeNode = new TreeNode
        {
            Text = node.IsFolder ? $"📁 {node.Name}" : $"🌐 {node.Name}",
            Tag = node,
            ToolTipText = node.IsFolder ? $"{node.Children.Count} 项" : node.Url
        };

        if (node.IsFolder)
        {
            foreach (var child in node.Children)
            {
                treeNode.Nodes.Add(CreateTreeNode(child));
            }
        }

        return treeNode;
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Surface;
        _searchBox.BackColor = AppTheme.SurfaceAlt;
        _searchBox.ForeColor = AppTheme.TextPrimary;
        _tree.BackColor = AppTheme.Surface;
        _tree.ForeColor = AppTheme.TextPrimary;
        _tree.LineColor = AppTheme.PanelBorder;
        AppTheme.ApplyContextMenu(_contextMenu);
    }
}
