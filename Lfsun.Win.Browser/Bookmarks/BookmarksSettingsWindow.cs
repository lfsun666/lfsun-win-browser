using System.Drawing.Drawing2D;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Bookmarks;

public sealed class BookmarksSettingsWindow : Form
{
    private readonly BookmarkManager _bookmarkManager;
    private readonly Action _onBookmarksChanged;
    private readonly Action<string> _log;
    private readonly TreeView _tree = new();
    private readonly Panel _treeCardPanel = new();
    private readonly Panel _editCardPanel = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Label _treeSummaryLabel = new();
    private readonly TextBox _nameTextBox = new();
    private readonly TextBox _urlTextBox = new();
    private readonly Label _typeLabel = new();
    private readonly Button _addFolderButton = new();
    private readonly Button _addBookmarkButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _importButton = new();
    private readonly Button _exportButton = new();
    private readonly Button _fetchIconsButton = new();
    private readonly Button _unwrapToolbarButton = new();
    private readonly Button _closeButton = new();
    private readonly SplitContainer _splitContainer = new();
    private readonly ImageList _treeImageList = new()
    {
        ColorDepth = ColorDepth.Depth32Bit,
        ImageSize = new Size(16, 16)
    };
    private readonly PictureBox _iconPreviewBox = new()
    {
        Size = new Size(20, 20),
        SizeMode = PictureBoxSizeMode.CenterImage,
        Margin = new Padding(0, 0, 8, 0)
    };
    private readonly ContextMenuStrip _treeContextMenu = new();
    private readonly ToolStripMenuItem _treeCtxAddFolder = new("新增文件夹");
    private readonly ToolStripMenuItem _treeCtxAddBookmark = new("新增书签");
    private readonly ToolStripMenuItem _treeCtxRenameOrSave = new("保存当前编辑");
    private readonly ToolStripMenuItem _treeCtxDeleteNode = new("删除节点");
    private readonly ToolStripMenuItem _treeCtxClearFolderChildren = new("清空该文件夹全部子项");
    private readonly ToolStripMenuItem _treeCtxClearAll = new("清空全部书签");

    public BookmarksSettingsWindow(
        BookmarkManager bookmarkManager,
        Action onBookmarksChanged,
        Action<string> log)
    {
        _bookmarkManager = bookmarkManager;
        _onBookmarksChanged = onBookmarksChanged;
        _log = log;

        Text = "设置 - 书签管理";
        Width = 980;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        WindowLayout.FitToWorkingArea(this);
        DoubleBuffered = true;
        Font = AppTheme.CreateFont(9F);
        // 中文注释：与设置窗口一致的高 DPI 自适应，高缩放屏上控件与字体等比例放大。
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Shown += (_, _) => UpdateSplitterDistance();
        Resize += (_, _) => UpdateSplitterDistance();

        BuildLayout();
        ApplyTheme();
        WireEvents();
        ReloadTree();
    }

    private void BuildLayout()
    {
        _splitContainer.Dock = DockStyle.Fill;
        _splitContainer.Orientation = Orientation.Vertical;
        _splitContainer.Panel1MinSize = 0;
        _splitContainer.Panel2MinSize = 0;

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12, 12, 8, 12)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        left.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "书签结构",
            Font = AppTheme.CreateFont(10.2F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        _searchTextBox.PlaceholderText = "搜索名称或地址";
        _searchTextBox.Dock = DockStyle.Top;
        left.Controls.Add(_searchTextBox, 0, 1);

        _treeSummaryLabel.AutoSize = true;
        _treeSummaryLabel.Margin = new Padding(0, 8, 0, 6);
        left.Controls.Add(_treeSummaryLabel, 0, 2);

        _treeCardPanel.Dock = DockStyle.Fill;
        _treeCardPanel.BackColor = AppTheme.Surface;
        _treeCardPanel.Padding = new Padding(4);
        _treeCardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _treeCardPanel.Width - 1, _treeCardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        AppTheme.ApplyControlRegion(_treeCardPanel, 8);

        _tree.Dock = DockStyle.Fill;
        _tree.HideSelection = false;
        _tree.BorderStyle = BorderStyle.None;
        _tree.ImageList = _treeImageList;
        _treeCardPanel.Controls.Add(_tree);
        left.Controls.Add(_treeCardPanel, 0, 3);
        _splitContainer.Panel1.Controls.Add(left);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 12, 12, 12)
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        right.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "详情编辑",
            Font = AppTheme.CreateFont(10.2F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        right.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "选中左侧节点后，可直接修改名称、地址，或执行新增、删除、导入、导出。",
            ForeColor = AppTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 1);

        _editCardPanel.Dock = DockStyle.Fill;
        _editCardPanel.BackColor = AppTheme.Surface;
        _editCardPanel.Padding = new Padding(12);
        _editCardPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppTheme.BuildRoundedRect(new Rectangle(0, 0, _editCardPanel.Width - 1, _editCardPanel.Height - 1), 8);
            using var pen = new Pen(AppTheme.PanelBorder, 1F);
            e.Graphics.DrawPath(pen, path);
        };
        AppTheme.ApplyControlRegion(_editCardPanel, 8);

        var editCardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        editCardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editCardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _typeLabel.AutoSize = true;
        _typeLabel.Text = "类型: -";
        _typeLabel.TextAlign = ContentAlignment.MiddleLeft;

        var typeFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 6)
        };
        typeFlow.Controls.Add(_iconPreviewBox);
        typeFlow.Controls.Add(_typeLabel);

        var nameLabel = new Label
        {
            AutoSize = true,
            Text = "名称"
        };
        _nameTextBox.Dock = DockStyle.Top;
        _nameTextBox.MaximumSize = new Size(800, 30);
        _nameTextBox.BorderStyle = BorderStyle.FixedSingle;

        var urlLabel = new Label
        {
            AutoSize = true,
            Text = "地址（仅书签）"
        };
        _urlTextBox.Dock = DockStyle.Top;
        _urlTextBox.MaximumSize = new Size(800, 30);
        _urlTextBox.BorderStyle = BorderStyle.FixedSingle;

        var editPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true
        };
        _addFolderButton.Text = "新增文件夹";
        _addBookmarkButton.Text = "新增书签";
        _saveButton.Text = "保存修改";
        _deleteButton.Text = "删除选中";
        editPanel.Controls.Add(_addFolderButton);
        editPanel.Controls.Add(_addBookmarkButton);
        editPanel.Controls.Add(_saveButton);
        editPanel.Controls.Add(_deleteButton);

        var importExportPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        _importButton.Text = "导入书签";
        _exportButton.Text = "导出书签";
        _fetchIconsButton.Text = "补全网站图标";
        _unwrapToolbarButton.Text = "展开书签栏根目录";
        _closeButton.Text = "关闭";
        importExportPanel.Controls.Add(_importButton);
        importExportPanel.Controls.Add(_exportButton);
        importExportPanel.Controls.Add(_fetchIconsButton);
        importExportPanel.Controls.Add(_unwrapToolbarButton);
        importExportPanel.Controls.Add(_closeButton);

        editCardLayout.Controls.Add(typeFlow, 0, 0);
        editCardLayout.Controls.Add(nameLabel, 0, 1);
        editCardLayout.Controls.Add(_nameTextBox, 0, 2);
        editCardLayout.Controls.Add(urlLabel, 0, 3);
        editCardLayout.Controls.Add(_urlTextBox, 0, 4);

        var groupPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        };
        groupPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        groupPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        groupPanel.Controls.Add(editPanel, 0, 0);
        groupPanel.Controls.Add(importExportPanel, 0, 1);
        editCardLayout.Controls.Add(groupPanel, 0, 5);

        _editCardPanel.Controls.Add(editCardLayout);
        right.Controls.Add(_editCardPanel, 0, 2);

        _splitContainer.Panel2.Controls.Add(right);
        Controls.Add(_splitContainer);
    }

    private void UpdateSplitterDistance()
    {
        try
        {
            if (_splitContainer.Width <= 0)
            {
                return;
            }

            var available = Math.Max(0, _splitContainer.Width - _splitContainer.SplitterWidth);
            const int panel1Min = 240;
            const int panel2Min = 340;

            if (available < panel1Min + panel2Min)
            {
                return;
            }

            var desired = Math.Clamp(360, panel1Min, available - panel2Min);
            _splitContainer.SplitterDistance = desired;
        }
        catch
        {
            // 防御性忽略异常，等待下一次布局或 Resize 事件重新对齐
        }
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.WindowBackground;
        StyleControls(this);
        AppTheme.ApplyPrimaryButton(_saveButton, emphasize: true);
        AppTheme.ApplyPrimaryButton(_closeButton, emphasize: false);
        AppTheme.ApplyControlRegion(_saveButton, 6);
        AppTheme.ApplyControlRegion(_closeButton, 6);
        _deleteButton.FlatAppearance.MouseOverBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.25F);
        _deleteButton.FlatAppearance.MouseDownBackColor = AppTheme.Mix(AppTheme.Danger, AppTheme.SurfaceAlt, 0.40F);
        _treeCardPanel.BackColor = AppTheme.Surface;
        AppTheme.ApplyControlRegion(_treeCardPanel, 8);
        _treeCardPanel.Invalidate();
        _editCardPanel.BackColor = AppTheme.Surface;
        AppTheme.ApplyControlRegion(_editCardPanel, 8);
        _editCardPanel.Invalidate();
        AppTheme.ApplyContextMenu(_treeContextMenu);
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
        var glow = Color.FromArgb(AppTheme.IsDark ? 24 : 18, AppTheme.Accent);
        using var glowBrush = new SolidBrush(glow);
        eventArgs.Graphics.FillEllipse(glowBrush, new Rectangle((int)(ClientSize.Width * 0.7F), -80, 240, 170));
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
            else if (control is Label label)
            {
                label.ForeColor = label.Font.Bold ? AppTheme.TextPrimary : AppTheme.TextSecondary;
            }
            else if (control is TreeView treeView)
            {
                AppTheme.ApplyTreeView(treeView);
            }
            else if (control is SplitContainer splitContainer)
            {
                splitContainer.BackColor = AppTheme.PanelBorder;
            }

            StyleControls(control);
        }
    }

    private void WireEvents()
    {
        _splitContainer.SplitterMoving += (_, e) =>
        {
            var available = _splitContainer.Width - _splitContainer.SplitterWidth;
            if (available > 580)
            {
                e.SplitX = Math.Clamp(e.SplitX, 240, available - 340);
            }
        };

        _searchTextBox.TextChanged += (_, _) => ReloadTree();
        _tree.AfterSelect += (_, _) => RefreshEditor();
        _tree.NodeMouseClick += (_, eventArgs) =>
        {
            _tree.SelectedNode = eventArgs.Node;
            if (eventArgs.Button == MouseButtons.Right)
            {
                ShowTreeContextMenu(eventArgs.Location);
            }
        };
        _tree.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            var hit = _tree.HitTest(eventArgs.Location);
            if (hit.Node is not null)
            {
                return;
            }

            _tree.SelectedNode = null;
            ShowTreeContextMenu(eventArgs.Location);
        };

        _addFolderButton.Click += (_, _) =>
        {
            var created = _bookmarkManager.AddFolder(GetSelectedFolderId(), "新建文件夹");
            ReloadTree(created.Id);
            _onBookmarksChanged();
            _log("已新增文件夹");
        };

        _addBookmarkButton.Click += (_, _) =>
        {
            var created = _bookmarkManager.AddBookmark(GetSelectedFolderId(), "新书签", "https://example.com");
            if (created is null)
            {
                MessageBox.Show("创建书签失败。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ReloadTree(created.Id);
            _onBookmarksChanged();
            _log("已新增书签");
        };

        _saveButton.Click += (_, _) => SaveSelectedNode();
        _deleteButton.Click += (_, _) => DeleteSelectedNode();
        _importButton.Click += (_, _) => ImportBookmarks();
        _exportButton.Click += (_, _) => ExportBookmarks();
        _fetchIconsButton.Click += (_, _) =>
        {
            var queued = FaviconService.QueueBatchFetch(_bookmarkManager.Root.Children);
            if (queued > 0)
            {
                MessageBox.Show(this, $"已在后台任务队列发起 {queued} 个域名的图标抓取，下载完成后将自动更新图标与书签栏。", "获取图标", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _log($"正在后台为 {queued} 个书签获取网站图标...");
            }
            else
            {
                MessageBox.Show(this, "当前所有书签均已具备图标或已在本地缓存中，无需重复获取。", "获取图标", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _unwrapToolbarButton.Click += (_, _) =>
        {
            BookmarkManager.PromoteToolbarFolderChildren(_bookmarkManager.Root);
            _bookmarkManager.Save();
            ReloadTree();
            _onBookmarksChanged();
            MessageBox.Show(this, "已展开书签栏根目录结构，所有子分类与书签均已按层级直接呈现在书签栏。", "展开书签栏", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _log("已手动展开书签栏根目录结构");
        };
        _closeButton.Click += (_, _) => Close();

        _treeContextMenu.Items.AddRange(
        [
            _treeCtxAddFolder,
            _treeCtxAddBookmark,
            _treeCtxRenameOrSave,
            _treeCtxDeleteNode,
            new ToolStripSeparator(),
            _treeCtxClearFolderChildren,
            _treeCtxClearAll
        ]);
        _treeContextMenu.Opening += (_, _) =>
        {
            var selected = GetSelectedBookmarkNode();
            var hasSelection = selected is not null;
            _treeCtxRenameOrSave.Enabled = hasSelection;
            _treeCtxDeleteNode.Enabled = hasSelection;
            _treeCtxClearFolderChildren.Enabled = selected?.IsFolder == true;
            _treeCtxClearAll.Enabled = _bookmarkManager.TopLevelItems.Count > 0;
        };
        _treeCtxAddFolder.Click += (_, _) => _addFolderButton.PerformClick();
        _treeCtxAddBookmark.Click += (_, _) => _addBookmarkButton.PerformClick();
        _treeCtxRenameOrSave.Click += (_, _) => SaveSelectedNode();
        _treeCtxDeleteNode.Click += (_, _) => DeleteSelectedNode();
        _treeCtxClearFolderChildren.Click += (_, _) => ClearSelectedFolderChildren();
        _treeCtxClearAll.Click += (_, _) => ClearAllBookmarks();

        FaviconService.FaviconDownloaded += (_, _, _) =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(() =>
                {
                    var selected = GetSelectedBookmarkNode();
                    if (selected is not null)
                    {
                        _iconPreviewBox.Image = FaviconService.GetIcon(selected);
                    }
                });
            }
            catch
            {
            }
        };
    }

    private void ShowTreeContextMenu(Point location)
    {
        _treeContextMenu.Show(_tree, location);
    }

    private void ReloadTree(Guid? selectNodeId = null)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        _treeImageList.Images.Clear();
        _treeImageList.Images.Add("folder", FaviconService.GetFolderIcon());

        var totalFolderCount = 0;
        var totalBookmarkCount = 0;
        CountNodes(_bookmarkManager.TopLevelItems, ref totalFolderCount, ref totalBookmarkCount);
        var query = _searchTextBox.Text.Trim();
        var visibleNodeCount = 0;

        foreach (var node in _bookmarkManager.TopLevelItems)
        {
            var treeNode = string.IsNullOrWhiteSpace(query)
                ? BuildTreeNode(node)
                : BuildFilteredTreeNode(node, query, ref visibleNodeCount);
            if (treeNode is not null)
            {
                _tree.Nodes.Add(treeNode);
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // 中文注释：大数据量书签默认仅展开顶层目录，避免遍历渲染几千个节点导致界面卡死冻结
            foreach (TreeNode n in _tree.Nodes)
            {
                n.Expand();
            }
        }
        else
        {
            _tree.ExpandAll();
        }
        if (selectNodeId is not null)
        {
            var treeNode = FindTreeNodeById(_tree.Nodes, selectNodeId.Value);
            if (treeNode is not null)
            {
                _tree.SelectedNode = treeNode;
            }
        }
        else if (_tree.Nodes.Count > 0)
        {
            _tree.SelectedNode = _tree.Nodes[0];
        }

        _tree.EndUpdate();
        _treeSummaryLabel.Text = string.IsNullOrWhiteSpace(query)
            ? $"共 {totalFolderCount} 个文件夹，{totalBookmarkCount} 个书签"
            : $"筛选结果 {visibleNodeCount} 项 / 总计 {totalFolderCount + totalBookmarkCount} 项";
        RefreshEditor();
    }

    private TreeNode BuildTreeNode(BookmarkNode node)
    {
        var key = node.IsFolder ? "folder" : FaviconService.EnsureTreeIcon(_treeImageList, node);
        var treeNode = new TreeNode(node.Name)
        {
            Tag = node.Id,
            ImageKey = key,
            SelectedImageKey = key
        };

        foreach (var child in node.Children)
        {
            treeNode.Nodes.Add(BuildTreeNode(child));
        }

        return treeNode;
    }

    private TreeNode? BuildFilteredTreeNode(BookmarkNode node, string query, ref int visibleNodeCount)
    {
        var normalizedQuery = query.Trim();
        var selfMatches =
            node.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
            (node.IsLink && node.Url.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        var childMatches = new List<TreeNode>();
        foreach (var child in node.Children)
        {
            var childNode = BuildFilteredTreeNode(child, normalizedQuery, ref visibleNodeCount);
            if (childNode is not null)
            {
                childMatches.Add(childNode);
            }
        }

        if (!selfMatches && childMatches.Count == 0)
        {
            return null;
        }

        var key = node.IsFolder ? "folder" : FaviconService.EnsureTreeIcon(_treeImageList, node);
        var treeNode = new TreeNode(node.Name)
        {
            Tag = node.Id,
            ImageKey = key,
            SelectedImageKey = key
        };
        foreach (var childNode in childMatches)
        {
            treeNode.Nodes.Add(childNode);
        }

        visibleNodeCount++;
        return treeNode;
    }

    private static void CountNodes(IEnumerable<BookmarkNode> nodes, ref int folderCount, ref int bookmarkCount)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                folderCount++;
                CountNodes(node.Children, ref folderCount, ref bookmarkCount);
            }
            else
            {
                bookmarkCount++;
            }
        }
    }

    private void RefreshEditor()
    {
        var selectedNode = GetSelectedBookmarkNode();
        if (selectedNode is null)
        {
            _iconPreviewBox.Visible = false;
            _typeLabel.Text = "类型: -";
            _nameTextBox.Text = string.Empty;
            _urlTextBox.Text = string.Empty;
            _urlTextBox.Enabled = false;
            _saveButton.Enabled = false;
            _deleteButton.Enabled = false;
            return;
        }

        _iconPreviewBox.Image = FaviconService.GetIcon(selectedNode);
        _iconPreviewBox.Visible = true;
        _typeLabel.Text = selectedNode.IsFolder ? "类型: 文件夹" : "类型: 书签";
        _nameTextBox.Text = selectedNode.Name;
        _urlTextBox.Enabled = selectedNode.IsLink;
        _urlTextBox.Text = selectedNode.IsLink ? selectedNode.Url : string.Empty;
        _saveButton.Enabled = true;
        _deleteButton.Enabled = true;
    }

    private void SaveSelectedNode()
    {
        var selectedNode = GetSelectedBookmarkNode();
        if (selectedNode is null)
        {
            return;
        }

        var ok = _bookmarkManager.UpdateNode(
            selectedNode.Id,
            _nameTextBox.Text,
            _urlTextBox.Text,
            out var error);
        if (!ok)
        {
            MessageBox.Show(error, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReloadTree(selectedNode.Id);
        _onBookmarksChanged();
        _log("书签已更新");
    }

    private void DeleteSelectedNode()
    {
        var selectedNode = GetSelectedBookmarkNode();
        if (selectedNode is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除“{selectedNode.Name}”？",
            "删除确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (!_bookmarkManager.RemoveNode(selectedNode.Id))
        {
            MessageBox.Show("删除失败。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ReloadTree();
        _onBookmarksChanged();
        _log("书签已删除");
    }

    private void ClearSelectedFolderChildren()
    {
        var selectedNode = GetSelectedBookmarkNode();
        if (selectedNode?.IsFolder != true)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确认清空文件夹“{selectedNode.Name}”下的全部子项？",
            "批量清空",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var removed = _bookmarkManager.ClearFolderChildren(selectedNode.Id);
        ReloadTree(selectedNode.Id);
        _onBookmarksChanged();
        _log($"已清空 {removed} 个书签节点");
    }

    private void ClearAllBookmarks()
    {
        var confirm = MessageBox.Show(
            "确认清空全部书签？该操作不可撤销。",
            "清空全部书签",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        if (!_bookmarkManager.ClearAll())
        {
            MessageBox.Show("当前没有可清空的书签。", "书签管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ReloadTree();
        _onBookmarksChanged();
        _log("已清空全部书签");
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

        ReloadTree();
        _onBookmarksChanged();
        _log(replace ? "书签已导入（替换模式）" : "书签已导入（合并模式）");

        var queuedIcons = FaviconService.QueueBatchFetch(_bookmarkManager.Root.Children);
        if (queuedIcons > 0)
        {
            _log($"已自动在后台为 {queuedIcons} 个导入的书签获取网站图标...");
        }
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

        var extension = Path.GetExtension(dialog.FileName);
        _log(string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            ? "书签已导出为 JSON"
            : "书签已导出为 HTML");
    }

    private Guid? GetSelectedFolderId()
    {
        var selected = GetSelectedBookmarkNode();
        if (selected is null)
        {
            return null;
        }

        if (selected.IsFolder)
        {
            return selected.Id;
        }

        if (_tree.SelectedNode?.Parent?.Tag is Guid parentId)
        {
            return parentId;
        }

        return null;
    }

    private BookmarkNode? GetSelectedBookmarkNode()
    {
        if (_tree.SelectedNode?.Tag is not Guid id)
        {
            return null;
        }

        return _bookmarkManager.FindNode(id);
    }

    private static TreeNode? FindTreeNodeById(TreeNodeCollection nodes, Guid id)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is Guid nodeId && nodeId == id)
            {
                return node;
            }

            var nested = FindTreeNodeById(node.Nodes, id);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    public void FocusNode(Guid id)
    {
        var node = FindTreeNodeById(_tree.Nodes, id);
        if (node is null)
        {
            return;
        }

        _tree.SelectedNode = node;
        node.EnsureVisible();
        Activate();
    }
}

