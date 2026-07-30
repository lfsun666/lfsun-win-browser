using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Bookmarks;

public sealed class BookmarkAddToFolderDialog : Form
{
    private readonly BookmarkManager _bookmarkManager;
    private readonly TreeView _folderTree = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Label _summaryLabel = new();
    private readonly TextBox _nameTextBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private BookmarkAddToFolderDialog(BookmarkManager bookmarkManager, string defaultName)
    {
        _bookmarkManager = bookmarkManager;
        Text = "收藏到书签";
        Width = 520;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);

        BuildLayout(defaultName);
        ApplyTheme();
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        _searchTextBox.TextChanged += (_, _) => LoadFolders();
        LoadFolders();
    }

    public Guid? SelectedFolderId { get; private set; }
    public string BookmarkName => _nameTextBox.Text.Trim();

    public static bool ShowDialog(
        IWin32Window owner,
        BookmarkManager bookmarkManager,
        string defaultName,
        out Guid? folderId,
        out string bookmarkName)
    {
        using var dialog = new BookmarkAddToFolderDialog(bookmarkManager, defaultName);
        var ok = dialog.ShowDialog(owner) == DialogResult.OK;
        folderId = dialog.SelectedFolderId;
        bookmarkName = dialog.BookmarkName;
        return ok;
    }

    private void BuildLayout(string defaultName)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "书签名称"
        }, 0, 0);

        _nameTextBox.Text = defaultName;
        _nameTextBox.Dock = DockStyle.Top;
        root.Controls.Add(_nameTextBox, 0, 1);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "选择收藏文件夹（默认根目录）"
        }, 0, 2);

        _searchTextBox.PlaceholderText = "搜索文件夹名称";
        _searchTextBox.Dock = DockStyle.Top;
        root.Controls.Add(_searchTextBox, 0, 3);

        _summaryLabel.AutoSize = true;
        _summaryLabel.Margin = new Padding(0, 8, 0, 6);
        root.Controls.Add(_summaryLabel, 0, 4);

        _folderTree.Dock = DockStyle.Fill;
        _folderTree.HideSelection = false;
        root.Controls.Add(_folderTree, 0, 5);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _okButton.Text = "确定";
        _okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("名称不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_folderTree.SelectedNode?.Tag is Guid id)
            {
                SelectedFolderId = id;
            }
            else
            {
                SelectedFolderId = null;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        _cancelButton.Text = "取消";
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        footer.Controls.Add(_okButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 6);

        Controls.Add(root);
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        AppTheme.ApplyTreeView(_folderTree);
        AppTheme.ApplyTextInput(_nameTextBox);
        AppTheme.ApplyTextInput(_searchTextBox);
        _summaryLabel.ForeColor = AppTheme.TextSecondary;
        AppTheme.ApplyPrimaryButton(_okButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_cancelButton);
        StyleControlTree(this);
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

    private void LoadFolders()
    {
        _folderTree.Nodes.Clear();

        var rootNode = new TreeNode("根目录")
        {
            Tag = null
        };
        var query = _searchTextBox.Text.Trim();
        var visibleFolderCount = 0;
        var totalFolderCount = CountFolders(_bookmarkManager.TopLevelItems);
        foreach (var node in _bookmarkManager.TopLevelItems.Where(item => item.IsFolder))
        {
            var childNode = string.IsNullOrWhiteSpace(query)
                ? BuildFolderNode(node)
                : BuildFilteredFolderNode(node, query, ref visibleFolderCount);
            if (childNode is not null)
            {
                rootNode.Nodes.Add(childNode);
                if (string.IsNullOrWhiteSpace(query))
                {
                    visibleFolderCount++;
                }
            }
        }

        _folderTree.Nodes.Add(rootNode);
        rootNode.Expand();
        _folderTree.SelectedNode = rootNode;
        _summaryLabel.Text = string.IsNullOrWhiteSpace(query)
            ? $"共 {totalFolderCount} 个文件夹"
            : $"筛选结果 {visibleFolderCount} 个文件夹 / 总计 {totalFolderCount} 个";
    }

    private static TreeNode BuildFolderNode(BookmarkNode node)
    {
        var treeNode = new TreeNode(node.Name)
        {
            Tag = node.Id
        };

        foreach (var child in node.Children.Where(item => item.IsFolder))
        {
            treeNode.Nodes.Add(BuildFolderNode(child));
        }

        return treeNode;
    }

    private static TreeNode? BuildFilteredFolderNode(BookmarkNode node, string query, ref int visibleFolderCount)
    {
        var selfMatches = node.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        var children = new List<TreeNode>();
        foreach (var child in node.Children.Where(item => item.IsFolder))
        {
            var childNode = BuildFilteredFolderNode(child, query, ref visibleFolderCount);
            if (childNode is not null)
            {
                children.Add(childNode);
            }
        }

        if (!selfMatches && children.Count == 0)
        {
            return null;
        }

        var treeNode = new TreeNode(node.Name)
        {
            Tag = node.Id
        };
        foreach (var child in children)
        {
            treeNode.Nodes.Add(child);
        }

        visibleFolderCount++;
        return treeNode;
    }

    private static int CountFolders(IEnumerable<BookmarkNode> nodes)
    {
        var total = 0;
        foreach (var node in nodes.Where(item => item.IsFolder))
        {
            total++;
            total += CountFolders(node.Children);
        }

        return total;
    }
}

