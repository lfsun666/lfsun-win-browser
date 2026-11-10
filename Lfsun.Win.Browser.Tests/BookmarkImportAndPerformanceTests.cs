using Lfsun.Win.Browser.Bookmarks;
using Lfsun.Win.Browser.Core;
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
}
