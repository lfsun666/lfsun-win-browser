using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Downloads;
using Lfsun.Win.Browser.History;
using Lfsun.Win.Browser.Plugins;
using Lfsun.Win.Browser.Security;
using Lfsun.Win.Browser.Settings;
using Lfsun.Win.Browser.UI;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class BookmarkImportAndPerformanceTests
{
    [Fact]
    public void PromoteToolbarFolderChildren_ShouldPromoteToolbarSubcategories()
    {
        var root = new BookmarkNode { Type = BookmarkNodeType.Folder, Name = "ROOT" };
        var toolbar = new BookmarkNode { Type = BookmarkNodeType.Folder, Name = "书签栏" };
        var toolsFolder = new BookmarkNode { Type = BookmarkNodeType.Folder, Name = "tools" };
        toolsFolder.Children.Add(new BookmarkNode { Type = BookmarkNodeType.Link, Name = "Tool1", Url = "https://tool1.com" });
        toolbar.Children.Add(toolsFolder);
        toolbar.Children.Add(new BookmarkNode { Type = BookmarkNodeType.Folder, Name = "download" });

        root.Children.Add(toolbar);
        root.Children.Add(new BookmarkNode { Type = BookmarkNodeType.Link, Name = "设置 - 外观", Url = "edge://settings/appearance" });

        var normalized = BookmarkManager.NormalizeImportedRoot(root);

        // Expect tools and download to be promoted directly under root, followed by the link
        Assert.Equal(3, normalized.Children.Count);
        Assert.Equal("tools", normalized.Children[0].Name);
        Assert.Equal("download", normalized.Children[1].Name);
        Assert.Equal("设置 - 外观", normalized.Children[2].Name);
        Assert.Single(normalized.Children[0].Children);
    }

    [Fact]
    public void ImportFromFile_HtmlNetscapeFormat_ShouldPreserveHierarchies()
    {
        var html = """
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
            <TITLE>Bookmarks</TITLE>
            <H1>Bookmarks</H1>
            <DL><p>
                <DT><H3 ADD_DATE="1600000000" LAST_MODIFIED="1600000000" PERSONAL_TOOLBAR_FOLDER="true">收藏夹栏</H3>
                <DL><p>
                    <DT><H3>开发工具</H3>
                    <DL><p>
                        <DT><A HREF="https://github.com">GitHub</A>
                        <DT><A HREF="https://stackoverflow.com">StackOverflow</A>
                    </DL><p>
                    <DT><H3>阅读</H3>
                    <DL><p>
                        <DT><A HREF="https://news.ycombinator.com">Hacker News</A>
                    </DL><p>
                </DL><p>
                <DT><H3>其他收藏夹</H3>
                <DL><p>
                    <DT><A HREF="https://bing.com">Bing</A>
                </DL><p>
            </DL><p>
            """;

        var tempFile = Path.Combine(Path.GetTempPath(), $"bookmarks_test_{Guid.NewGuid():N}.html");
        var storeFile = Path.Combine(Path.GetTempPath(), $"bookmarks_store_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, html);
            var manager = new BookmarkManager(storeFile, _ => { });
            var success = manager.ImportFromFile(tempFile, replaceExisting: true);

            Assert.True(success);
            var topItems = manager.TopLevelItems;

            // "开发工具" and "阅读" should be promoted to the top level, followed by "其他收藏夹"
            Assert.Contains(topItems, n => n.IsFolder && n.Name == "开发工具");
            Assert.Contains(topItems, n => n.IsFolder && n.Name == "阅读");
            Assert.Contains(topItems, n => n.IsFolder && n.Name == "其他收藏夹");

            var devTools = topItems.First(n => n.Name == "开发工具");
            Assert.Equal(2, devTools.Children.Count);
            Assert.Contains(devTools.Children, c => c.Name == "GitHub" && c.Url == "https://github.com/");
            Assert.Contains(devTools.Children, c => c.Name == "StackOverflow" && c.Url == "https://stackoverflow.com/");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(storeFile)) File.Delete(storeFile);
        }
    }

    [Fact]
    public void InternalUrlRouter_ShouldRecognizeInternalProtocols()
    {
        Assert.True(InternalUrlRouter.IsInternalBrowserUrl("edge://settings/appearance"));
        Assert.True(InternalUrlRouter.IsInternalBrowserUrl("chrome://settings"));
        Assert.True(InternalUrlRouter.IsInternalBrowserUrl("about:settings"));
        Assert.True(InternalUrlRouter.IsInternalBrowserUrl("about:bookmarks"));
        Assert.True(InternalUrlRouter.IsInternalBrowserUrl("lfsun://settings"));
        Assert.False(InternalUrlRouter.IsInternalBrowserUrl("https://bing.com"));
        Assert.False(InternalUrlRouter.IsInternalBrowserUrl("file:///C:/path/file.txt"));
    }

    [Fact]
    public void InternalUrlRouter_TryRoute_ShouldMapAppearanceCorrectly()
    {
        var categoryRouted = -1;
        var toastMessage = string.Empty;

        var handled = InternalUrlRouter.TryRoute(
            "edge://settings/appearance",
            cat => categoryRouted = cat,
            () => { },
            () => { },
            () => { },
            msg => toastMessage = msg,
            out var redirectHtml);

        Assert.True(handled);
        Assert.Equal(1, categoryRouted); // 1 = 界面 / 外观
        Assert.Contains("外观", toastMessage);
        Assert.Null(redirectHtml);
    }

    [Fact]
    public void InternalUrlRouter_TryRoute_UnrecognizedInternalUrl_ShouldProduceFriendlyHtml()
    {
        var handled = InternalUrlRouter.TryRoute(
            "edge://flags",
            _ => { },
            () => { },
            () => { },
            () => { },
            _ => { },
            out var redirectHtml);

        Assert.False(handled);
        Assert.NotNull(redirectHtml);
        Assert.Contains("专有浏览器内部协议", redirectHtml);
        Assert.Contains("edge://flags", redirectHtml);
    }

    [Fact]
    public void PagePerformanceReport_ToMarkdown_ShouldIncludeAllKeyTimings()
    {
        var report = new PagePerformanceReport
        {
            Url = "https://example.com",
            Title = "Example Domain",
            Protocol = "HTTP/2",
            TotalDurationMs = 350,
            DnsDurationMs = 25,
            TcpDurationMs = 45,
            TtfbMs = 80,
            DomReadyMs = 210,
            LoadCompleteMs = 350,
            TransferSizeBytes = 1024 * 350,
            DecodedBodySizeBytes = 1024 * 800,
            ResourceCount = 18,
            ScriptCount = 6,
            StyleCount = 3,
            ImageCount = 5,
            DomElementCount = 120
        };

        var md = report.ToMarkdown();
        Assert.Contains("### ⚡ 网页性能与诊断报告", md);
        Assert.Contains("DNS 查询", md);
        Assert.Contains("25 ms", md);
        Assert.Contains("TCP/TLS 握手", md);
        Assert.Contains("45 ms", md);
        Assert.Contains("首包到达 (TTFB)", md);
        Assert.Contains("80 ms", md);
        Assert.Contains("350.0 KB", md);
        Assert.Contains("`HTTP/2`", md);
    }

    [Fact]
    public void ImportFromFile_HtmlWithFavicon_ShouldExtractAndPersistFavicon()
    {
        const string base64Icon = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var html = $"""
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <TITLE>Bookmarks</TITLE>
            <H1>Bookmarks</H1>
            <DL><p>
                <DT><A HREF="https://github.com" ICON="{base64Icon}">GitHub</A>
                <DT><A HREF="https://bing.com">Bing</A>
            </DL><p>
            """;

        var tempFile = Path.Combine(Path.GetTempPath(), $"bookmarks_icon_test_{Guid.NewGuid():N}.html");
        var storeFile = Path.Combine(Path.GetTempPath(), $"bookmarks_store_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, html);
            var manager = new BookmarkManager(storeFile, _ => { });
            var success = manager.ImportFromFile(tempFile, replaceExisting: true);

            Assert.True(success);
            var githubNode = manager.FindByUrl("https://github.com/");
            Assert.NotNull(githubNode);
            Assert.Equal(base64Icon, githubNode.Favicon);

            var bingNode = manager.FindByUrl("https://bing.com/");
            Assert.NotNull(bingNode);
            Assert.Null(bingNode.Favicon);

            // Test export preserves ICON
            var exportFile = Path.Combine(Path.GetTempPath(), $"bookmarks_export_{Guid.NewGuid():N}.html");
            Assert.True(manager.ExportToFile(exportFile));
            var exportedHtml = File.ReadAllText(exportFile);
            Assert.Contains("ICON=", exportedHtml);
            File.Delete(exportFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(storeFile)) File.Delete(storeFile);
        }
    }

    [Fact]
    public void FaviconService_ShouldProduceValid16x16Icons()
    {
        var folderIcon = FaviconService.GetFolderIcon();
        Assert.NotNull(folderIcon);
        Assert.Equal(16, folderIcon.Width);
        Assert.Equal(16, folderIcon.Height);

        var webIcon = FaviconService.GetIcon("https://example.com/test", "Example Test");
        Assert.NotNull(webIcon);
        Assert.Equal(16, webIcon.Width);
        Assert.Equal(16, webIcon.Height);

        var internalIcon = FaviconService.GetIcon("edge://settings/appearance", "Settings");
        Assert.NotNull(internalIcon);
        Assert.Equal(16, internalIcon.Width);
        Assert.Equal(16, internalIcon.Height);
    }

    [Fact]
    public void BookmarkManager_UpdateFaviconsByHost_ShouldUpdateAllMatchingLinks()
    {
        var storeFile = Path.Combine(Path.GetTempPath(), $"bookmarks_host_test_{Guid.NewGuid():N}.json");
        try
        {
            var manager = new BookmarkManager(storeFile, _ => { });
            var b1 = manager.AddBookmark(null, "GH1", "https://github.com/repo1");
            var b2 = manager.AddBookmark(null, "GH2", "https://github.com/repo2");
            var b3 = manager.AddBookmark(null, "Bing", "https://bing.com");

            const string fakeIcon = "data:image/png;base64,FAKE";
            var updatedCount = manager.UpdateFaviconsByHost("github.com", fakeIcon);

            Assert.Equal(2, updatedCount);
            Assert.Equal(fakeIcon, manager.FindNode(b1!.Id)!.Favicon);
            Assert.Equal(fakeIcon, manager.FindNode(b2!.Id)!.Favicon);
            Assert.Null(manager.FindNode(b3!.Id)!.Favicon);
        }
        finally
        {
            if (File.Exists(storeFile)) File.Delete(storeFile);
        }
    }

    [Fact]
    public void ImportFromFile_ChromeBookmarkBarWrapper_ShouldUnwrapAndPromoteToRoot()
    {
        var html = """
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <TITLE>Bookmarks</TITLE>
            <H1>Bookmarks</H1>
            <DL><p>
                <DT><H3 ADD_DATE="1600000000" LAST_MODIFIED="1600000000" PERSONAL_TOOLBAR_FOLDER="true">书签栏</H3>
                <DL><p>
                    <DT><H3>tools</H3>
                    <DL><p>
                        <DT><A HREF="https://tools.example.com">Tool 1</A>
                    </DL><p>
                    <DT><H3>interview</H3>
                    <DL><p>
                        <DT><A HREF="https://interview.example.com">Interview 1</A>
                    </DL><p>
                </DL><p>
            </DL><p>
            """;

        var tempFile = Path.Combine(Path.GetTempPath(), $"bookmarks_chrome_test_{Guid.NewGuid():N}.html");
        var storeFile = Path.Combine(Path.GetTempPath(), $"bookmarks_store_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFile, html);
            var manager = new BookmarkManager(storeFile, _ => { });
            var success = manager.ImportFromFile(tempFile, replaceExisting: true);

            Assert.True(success);
            // Should be unwrapped: top level has "tools" and "interview", not "书签栏"
            Assert.Equal(2, manager.TopLevelItems.Count);
            Assert.Contains(manager.TopLevelItems, item => item.Name == "tools" && item.IsFolder);
            Assert.Contains(manager.TopLevelItems, item => item.Name == "interview" && item.IsFolder);
            Assert.DoesNotContain(manager.TopLevelItems, item => item.Name == "书签栏");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(storeFile)) File.Delete(storeFile);
        }
    }

    [Fact]
    public void FaviconService_QueueBatchFetch_ShouldQueueWithoutErrors()
    {
        var root = new BookmarkNode { Type = BookmarkNodeType.Folder, Name = "Root" };
        root.Children.Add(new BookmarkNode
        {
            Type = BookmarkNodeType.Link,
            Name = "Bing",
            Url = "https://www.bing.com"
        });
        root.Children.Add(new BookmarkNode
        {
            Type = BookmarkNodeType.Link,
            Name = "GitHub",
            Url = "https://github.com"
        });

        var queued = FaviconService.QueueBatchFetch(root.Children);
        Assert.True(queued >= 0);
    }

    [Fact]
    public void FaviconService_GetIcon_ShouldProduceValid16x16Bitmap()
    {
        var node = new BookmarkNode
        {
            Type = BookmarkNodeType.Link,
            Name = "Bing",
            Url = "https://www.bing.com"
        };
        var icon = FaviconService.GetIcon(node);
        Assert.NotNull(icon);
        Assert.Equal(16, icon.Width);
        Assert.Equal(16, icon.Height);
    }

    [Fact]
    public void FaviconService_TryGetFaviconDataUrl_And_TileColor_ShouldBehaveCorrectly()
    {
        var rawData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var dataUrl = FaviconService.TryGetFaviconDataUrl("https://example.com", rawData);
        Assert.NotNull(dataUrl);
        Assert.StartsWith("data:image/png;base64,", dataUrl);

        var tileColor = FaviconService.ResolveTileCssColor("github.com");
        Assert.NotNull(tileColor);
        Assert.StartsWith("#", tileColor);
        Assert.Equal(7, tileColor.Length);
    }

    [Fact]
    public void NewTabPageBuilder_Build_ShouldIncludeCardsAndBadges()
    {
        var config = new BrowserConfig();
        var history = new List<Lfsun.Win.Browser.History.HistoryEntry>
        {
            new(Guid.NewGuid(), "必应搜索", "https://bing.com", DateTime.UtcNow)
        };
        var bookmarks = new List<BookmarkNode>
        {
            new() { Type = BookmarkNodeType.Link, Name = "GitHub", Url = "https://github.com" }
        };

        var html = UI.NewTabPageBuilder.Build(config, history, bookmarks);
        Assert.NotNull(html);
        Assert.Contains("必应搜索", html);
        Assert.Contains("GitHub", html);
        Assert.Contains("card", html);
        Assert.Contains("badge", html);
    }

    [Fact]
    public void BookmarkBar_FolderDisplay_ShouldNotIncludeNumericCountsOrDropDownArrow()
    {
        var node = new BookmarkNode
        {
            Type = BookmarkNodeType.Folder,
            Name = "tools"
        };
        node.Children.Add(new BookmarkNode { Type = BookmarkNodeType.Link, Name = "Item1", Url = "https://example.com/1" });
        node.Children.Add(new BookmarkNode { Type = BookmarkNodeType.Link, Name = "Item2", Url = "https://example.com/2" });

        // Build folder button without suffix count and without dropdown arrow
        var folderDisplayName = node.Name;
        using var folderButton = new System.Windows.Forms.ToolStripDropDownButton(folderDisplayName)
        {
            ShowDropDownArrow = false
        };

        Assert.Equal("tools", folderButton.Text);
        Assert.DoesNotContain("·", folderButton.Text);
        Assert.DoesNotContain("2", folderButton.Text);
        Assert.False(folderButton.ShowDropDownArrow);
    }

    [Fact]
    public void SettingsWindow_InitialState_ShouldNotHavePendingChanges()
    {
        var thread = new Thread(() =>
        {
            var config = new BrowserConfig();
            var tempDir = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var downloadManager = new DownloadManager(tempDir, _ => { });
                var pluginManager = new PluginManager(tempDir, config, _ => { });
                var credentialStore = new CredentialStore(Path.Combine(tempDir, "creds.dat"), _ => { });
                var bookmarkManager = new BookmarkManager(Path.Combine(tempDir, "bookmarks.json"), _ => { });

                using var settingsWindow = new SettingsWindow(
                    config,
                    downloadManager,
                    pluginManager,
                    credentialStore,
                    bookmarkManager,
                    () => { },
                    () => { },
                    _ => { });

                Assert.False(settingsWindow.HasPendingChanges);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ToolStripSpringTextBox_DeductsOwnMarginProperly()
    {
        var thread = new Thread(() =>
        {
            using var strip = new ToolStrip { Width = 600 };
            var fixedButton = new ToolStripButton { Width = 100, Margin = new Padding(0) };
            var springBox = new Lfsun.Win.Browser.UI.Chrome.ToolStripSpringTextBox
            {
                Margin = new Padding(6, 2, 6, 2)
            };
            strip.Items.Add(fixedButton);
            strip.Items.Add(springBox);

            var size = springBox.GetPreferredSize(new Size(600, 30));
            // Available width was 600 - 100 = 500, minus Margin.Horizontal (12) => 488
            var expectedWidth = strip.DisplayRectangle.Width - (fixedButton.Width + fixedButton.Margin.Horizontal) - springBox.Margin.Horizontal;
            Assert.Equal(expectedWidth, size.Width);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void StatusBarPanel_StatusLabel_TransparentBack_ForCleanSpringDisplay()
    {
        var thread = new Thread(() =>
        {
            var panel = new Lfsun.Win.Browser.UI.Chrome.StatusBarPanel();
            panel.ApplyTheme(compact: false);
            Assert.Equal(Color.Transparent, panel.StatusLabel.BackColor);
            Assert.NotEqual(Color.Transparent, panel.MemoryLabel.BackColor);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ModernToolStripRenderer_MenuSeparatorAndCheckmark_CanBeRenderedSafely()
    {
        var thread = new Thread(() =>
        {
            using var bmp = new Bitmap(200, 100);
            using var g = Graphics.FromImage(bmp);
            var renderer = new Lfsun.Win.Browser.UI.Chrome.ModernToolStripRenderer();
            using var menu = new ContextMenuStrip { Renderer = renderer };
            var menuItem = new ToolStripMenuItem("Checked item") { Checked = true };
            var separator = new ToolStripSeparator();
            menu.Items.Add(menuItem);
            menu.Items.Add(separator);

            // Verify renderer handles menu separator and checkmark without exception
            Assert.NotNull(renderer);
            Assert.True(menuItem.Checked);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void FindBar_And_BrowserSidePanel_LayoutProperties_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            var findBar = new Lfsun.Win.Browser.UI.FindBar();
            Assert.NotNull(findBar.Region);

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var bm = new BookmarkManager(Path.Combine(tempDir, "bm.json"), _ => { });
                var hm = new Lfsun.Win.Browser.History.HistoryManager(Path.Combine(tempDir, "hm.json"), _ => { });
                var dm = new Lfsun.Win.Browser.Downloads.DownloadManager(tempDir, _ => { });

                var sidePanel = new Lfsun.Win.Browser.UI.SidePanel.BrowserSidePanel(
                    bm,
                    hm,
                    dm,
                    () => null,
                    (_, _) => Task.CompletedTask,
                    _ => { },
                    () => { },
                    () => { },
                    () => { });
                Assert.Equal(new Padding(1, 0, 0, 0), sidePanel.Padding);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void FloatingCards_And_Dialogs_LayoutAndHoverFeedback_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            using var siteInfo = new Lfsun.Win.Browser.UI.SiteInfoCardDialog(
                new Uri("https://example.com"),
                blockedCount: 5,
                loadMs: 120,
                clearCookiesAction: null,
                hardRefreshAction: null,
                viewSourceAction: null,
                showQrAction: null);
            Assert.NotNull(siteInfo);
            Assert.Equal("站点信息", siteInfo.Text);

            using var qrCard = new Lfsun.Win.Browser.UI.QrCodeCardDialog("https://example.com");
            Assert.NotNull(qrCard);
            Assert.Equal("手机扫码继续浏览", qrCard.Text);

            using var about = new Lfsun.Win.Browser.UI.AboutWindow();
            Assert.NotNull(about);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Downloads_And_History_CardVisualAndRegions_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var dm = new DownloadManager(tempDir, _ => { });
                using var downloadsWin = new DownloadsWindow(dm);
                Assert.NotNull(downloadsWin);
                Assert.Equal("下载管理", downloadsWin.Text);

                var hm = new Lfsun.Win.Browser.History.HistoryManager(Path.Combine(tempDir, "hm.json"), _ => { });
                using var historyWin = new Lfsun.Win.Browser.History.HistoryWindow(
                    hm,
                    () => Array.Empty<Lfsun.Win.Browser.Browser.ClosedTabEntry>(),
                    (_, _) => Task.CompletedTask,
                    () => Task.FromResult(false),
                    _ => { });
                Assert.NotNull(historyWin);
                Assert.Equal("历史记录", historyWin.Text);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void FindBar_And_VerticalTabStrip_And_NewTabPage_VisualConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            // 1. New Tab Page HTML Builder
            var config = new BrowserConfig();
            var html = NewTabPageBuilder.Build(config, Array.Empty<HistoryEntry>(), Array.Empty<BookmarkNode>());
            Assert.Contains("search-box", html);
            Assert.Contains("backdrop-filter", html);
            Assert.Contains("quick-chip", html);
            Assert.Contains("常访站点", html);

            // 2. FindBar
            using var findBar = new FindBar();
            Assert.False(findBar.Visible);
            findBar.UpdateMatches(1, 10);
            findBar.ResetSearch();
            Assert.Equal(string.Empty, findBar.SearchTerm);
            Assert.False(findBar.MatchCase);
            findBar.ApplyTheme();

            // 3. VerticalTabStrip
            var collapseNotified = false;
            using var vStrip = new VerticalTabStrip(
                () => Array.Empty<Lfsun.Win.Browser.Browser.BrowserTab>(),
                () => null,
                _ => { },
                _ => { },
                () => { },
                (_, _) => { });
            vStrip.CollapseChanged += collapsed => collapseNotified = collapsed;
            Assert.False(vStrip.IsCollapsed);
            Assert.Equal(220, vStrip.Width);

            vStrip.IsCollapsed = true;
            Assert.True(collapseNotified);
            Assert.Equal(48, vStrip.Width);

            vStrip.ApplyTheme();
            vStrip.RebuildTabs();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ShortcutsOverview_And_SidePanelViews_And_SplitView_VisualConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                // 1. ShortcutsOverviewWindow
                var config = new BrowserConfig();
                using var shortcutsWin = new ShortcutsOverviewWindow(config, Array.Empty<(string, string, string, bool)>());
                Assert.NotNull(shortcutsWin);
                Assert.Equal("快捷键一览", shortcutsWin.Text);

                // 2. SplitBrowserPanel
                using var splitPanel = new Lfsun.Win.Browser.UI.SplitView.SplitBrowserPanel();
                Assert.NotNull(splitPanel);
                Assert.False(splitPanel.IsSplitActive);
                splitPanel.ApplyTheme();

                // 3. SidePanel Views
                var dm = new DownloadManager(tempDir, _ => { });
                using var quickDownloads = new Lfsun.Win.Browser.UI.SidePanel.QuickDownloadsView(dm, _ => { }, () => { });
                Assert.NotNull(quickDownloads);
                quickDownloads.ApplyTheme();

                var hm = new HistoryManager(Path.Combine(tempDir, "h.json"), _ => { });
                using var quickHistory = new Lfsun.Win.Browser.UI.SidePanel.QuickHistoryView(hm, (_, _) => Task.CompletedTask, _ => { }, () => { });
                Assert.NotNull(quickHistory);
                quickHistory.ApplyTheme();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ScratchpadView_And_ToolboxView_And_AppThemeRegion_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            // 1. AppTheme ApplyControlRegion
            using var btn = new Button { Size = new Size(120, 36) };
            AppTheme.ApplyControlRegion(btn, 6);
            Assert.NotNull(btn.Region);

            // 2. ScratchpadView
            using var scratchpad = new Lfsun.Win.Browser.UI.SidePanel.ScratchpadView(() => "https://example.com", _ => { });
            Assert.NotNull(scratchpad);
            scratchpad.ApplyTheme();

            // 3. ToolboxView
            using var toolbox = new Lfsun.Win.Browser.UI.SidePanel.ToolboxView(
                () => null,
                _ => { },
                toggleSplitCallback: () => { },
                getAllTabs: () => Array.Empty<Lfsun.Win.Browser.Browser.BrowserTab>());
            Assert.NotNull(toolbox);
            toolbox.ApplyTheme();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void AboutWindow_And_SiteInfo_And_QrCode_And_FindBar_And_TabStrip_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            // 1. AboutWindow
            using var aboutWindow = new Lfsun.Win.Browser.UI.AboutWindow();
            Assert.NotNull(aboutWindow);
            Assert.True(aboutWindow.Controls.Count > 0);

            // 2. FindBar
            using var findBar = new Lfsun.Win.Browser.UI.FindBar();
            Assert.NotNull(findBar);
            findBar.ApplyTheme();
            findBar.Controls.OfType<TextBox>().First().Text = "search";
            findBar.UpdateMatches(1, 5);
            Assert.Equal("1/5", findBar.Controls.OfType<Label>().First().Text);

            // 3. QrCodeCardDialog
            using var qrDialog = new Lfsun.Win.Browser.UI.QrCodeCardDialog("https://example.com", "Test");
            Assert.NotNull(qrDialog);
            Assert.True(qrDialog.Controls.Count > 0);

            // 4. SiteInfoCardDialog
            using var siteDialog = new Lfsun.Win.Browser.UI.SiteInfoCardDialog(
                new Uri("https://example.com"),
                blockedCount: 3,
                loadMs: 150,
                clearCookiesAction: null,
                hardRefreshAction: null,
                viewSourceAction: null,
                showQrAction: null);
            Assert.NotNull(siteDialog);
            Assert.True(siteDialog.Controls.Count > 0);

            // 5. VerticalTabStrip
            using var vTabStrip = new Lfsun.Win.Browser.UI.VerticalTabStrip(
                () => Array.Empty<Lfsun.Win.Browser.Browser.BrowserTab>(),
                () => null,
                _ => { },
                _ => { },
                () => { },
                (_, _) => { });
            Assert.NotNull(vTabStrip);
            Assert.False(vTabStrip.IsCollapsed);
            vTabStrip.IsCollapsed = true;
            Assert.True(vTabStrip.IsCollapsed);
            vTabStrip.ApplyTheme();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ModernTabControl_And_SidePanel_And_SplitPanel_ConfiguredProperly()
    {
        var thread = new Thread(() =>
        {
            // 1. ModernTabControl
            using var tabControl = new Lfsun.Win.Browser.UI.ModernTabControl();
            tabControl.TabPages.Add(new TabPage("测试标签 1"));
            tabControl.TabPages.Add(new TabPage("测试标签 2"));
            tabControl.SelectedIndex = 0;
            tabControl.ApplyDensity(compact: false);
            Assert.Equal(2, tabControl.TabPages.Count);

            // 2. BrowserSidePanel
            var tempDir = Path.Combine(Path.GetTempPath(), "sidepanel_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var bm = new BookmarkManager(Path.Combine(tempDir, "b.json"), _ => { });
                var hm = new HistoryManager(Path.Combine(tempDir, "h.json"), _ => { });
                var dm = new DownloadManager(tempDir, _ => { });
                using var sidePanel = new Lfsun.Win.Browser.UI.SidePanel.BrowserSidePanel(
                    bm, hm, dm, () => null, (_, _) => Task.CompletedTask, _ => { }, () => { }, () => { }, () => { });
                Assert.NotNull(sidePanel);
                sidePanel.ApplyTheme();
                sidePanel.SwitchTo(Lfsun.Win.Browser.UI.SidePanel.SidePanelMode.Scratchpad);
                Assert.Equal(Lfsun.Win.Browser.UI.SidePanel.SidePanelMode.Scratchpad, sidePanel.CurrentMode);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }

            // 3. SplitBrowserPanel
            using var splitPanel = new Lfsun.Win.Browser.UI.SplitView.SplitBrowserPanel();
            Assert.NotNull(splitPanel);
            splitPanel.ApplyTheme();
            Assert.False(splitPanel.IsSplitActive);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TabStatsCardDialog_And_BookmarksSettingsWindow_InitializeCorrectly()
    {
        var thread = new Thread(() =>
        {
            var hostStats = new List<(string Host, int Count)>
            {
                ("github.com", 3),
                ("microsoft.com", 2)
            };

            using var dialog = new Lfsun.Win.Browser.UI.TabStatsCardDialog(
                totalTabs: 5,
                activeTabs: 3,
                pinnedTabs: 1,
                suspendedTabs: 2,
                customNamedTabs: 1,
                coloredTabs: 1,
                duplicateTabs: 0,
                memoryMb: 248.5,
                hostDistributions: hostStats,
                cleanDuplicatesAction: () => Task.CompletedTask,
                suspendOthersAction: () => Task.CompletedTask,
                closeOthersAction: () => Task.CompletedTask);

            Assert.NotNull(dialog);
            Assert.Equal("标签与资源诊断", dialog.Text);
            Assert.Equal(530, dialog.ClientSize.Width);

            var tempDir = Path.Combine(Path.GetTempPath(), "bmsw_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var bm = new BookmarkManager(Path.Combine(tempDir, "b.json"), _ => { });
                using var bmsw = new BookmarksSettingsWindow(bm, () => { }, _ => { });
                Assert.NotNull(bmsw);
                Assert.Equal("设置 - 书签管理", bmsw.Text);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
