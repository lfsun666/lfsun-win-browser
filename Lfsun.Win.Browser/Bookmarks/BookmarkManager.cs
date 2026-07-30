using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lfsun.Win.Browser.Bookmarks;

public sealed class BookmarkManager
{
    private readonly string _storePath;
    private readonly Action<string> _log;
    private BookmarkNode _root = CreateRoot();

    public BookmarkManager(string storePath, Action<string> log)
    {
        _storePath = storePath;
        _log = log;
    }

    public BookmarkNode Root => _root;
    public IReadOnlyList<BookmarkNode> TopLevelItems => _root.Children;

    public void Reload()
    {
        _root = CreateRoot();
        if (!File.Exists(_storePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_storePath);

            var loadedRoot = JsonSerializer.Deserialize<BookmarkNode>(json, BuildJsonOptions());
            if (loadedRoot is not null)
            {
                _root = NormalizeImportedRoot(loadedRoot);
                return;
            }

            // Compatibility with old flat-bookmark format.
            var legacy = JsonSerializer.Deserialize<List<LegacyBookmark>>(json, BuildJsonOptions());
            if (legacy is null)
            {
                return;
            }

            foreach (var item in legacy)
            {
                if (string.IsNullOrWhiteSpace(item.Url))
                {
                    continue;
                }

                var node = CreateLinkNode(
                    string.IsNullOrWhiteSpace(item.Title) ? item.Url : item.Title.Trim(),
                    item.Url.Trim());
                _root.Children.Add(node);
            }
        }
        catch (Exception ex)
        {
            _log($"书签加载失败: {ex.Message}");
        }
    }

    public BookmarkNode AddFolder(Guid? parentId, string name)
    {
        var parent = ResolveFolder(parentId) ?? _root;
        var node = new BookmarkNode
        {
            Type = BookmarkNodeType.Folder,
            Name = string.IsNullOrWhiteSpace(name) ? "新建文件夹" : name.Trim()
        };

        parent.Children.Add(node);
        Save();
        return node;
    }

    public BookmarkNode? AddBookmark(Guid? parentId, string name, string url)
    {
        if (!TryNormalizeUrl(url, out var normalizedUrl))
        {
            return null;
        }

        var parent = ResolveFolder(parentId) ?? _root;
        var node = CreateLinkNode(
            string.IsNullOrWhiteSpace(name) ? normalizedUrl : name.Trim(),
            normalizedUrl);
        parent.Children.Add(node);
        Save();
        return node;
    }

    public BookmarkNode AddOrUpdateByUrl(Uri uri, string name, Guid? parentId = null)
    {
        var parentFolder = ResolveFolder(parentId) ?? _root;
        var existing = FindByUrl(uri.AbsoluteUri);
        if (existing is not null)
        {
            existing.Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim();
            existing.Url = uri.AbsoluteUri;
            if (parentId is not null)
            {
                MoveNode(existing.Id, parentId);
            }
            Save();
            return existing;
        }

        var node = CreateLinkNode(
            string.IsNullOrWhiteSpace(name) ? uri.AbsoluteUri : name.Trim(),
            uri.AbsoluteUri);
        if (ReferenceEquals(parentFolder, _root))
        {
            _root.Children.Insert(0, node);
        }
        else
        {
            parentFolder.Children.Add(node);
        }
        Save();
        return node;
    }

    public bool RemoveByUrl(Uri uri)
    {
        var target = FindByUrl(uri.AbsoluteUri);
        if (target is null)
        {
            return false;
        }

        var removed = RemoveNode(target.Id);
        if (removed)
        {
            Save();
        }

        return removed;
    }

    public bool ClearAll()
    {
        if (_root.Children.Count == 0)
        {
            return false;
        }

        _root.Children.Clear();
        Save();
        return true;
    }

    public int ClearFolderChildren(Guid? folderId)
    {
        var folder = ResolveFolder(folderId);
        if (folder is null || folder.Children.Count == 0)
        {
            return 0;
        }

        var removed = CountNodes(folder.Children);
        folder.Children.Clear();
        Save();
        return removed;
    }

    public BookmarkNode? FindNode(Guid id)
    {
        return FindNodeRecursive(_root, id);
    }

    public Guid? FindParentId(Guid id)
    {
        var parent = FindParent(id);
        if (parent is null || parent.Id == _root.Id)
        {
            return null;
        }

        return parent.Id;
    }

    public bool RemoveNode(Guid id)
    {
        var removed = RemoveNodeRecursive(_root, id);
        if (removed)
        {
            Save();
        }

        return removed;
    }

    public bool UpdateNode(Guid id, string name, string? url, out string error)
    {
        var target = FindNode(id);
        if (target is null)
        {
            error = "未找到书签节点。";
            return false;
        }

        target.Name = string.IsNullOrWhiteSpace(name)
            ? (target.IsFolder ? "新建文件夹" : "新书签")
            : name.Trim();

        if (target.IsLink)
        {
            if (!TryNormalizeUrl(url ?? string.Empty, out var normalizedUrl))
            {
                error = "URL 无效。";
                return false;
            }

            target.Url = normalizedUrl;
        }
        else
        {
            target.Url = string.Empty;
        }

        Save();
        error = string.Empty;
        return true;
    }

    public bool MoveNode(Guid id, Guid? newParentId)
    {
        if (newParentId == id)
        {
            return false;
        }

        var target = FindNode(id);
        if (target is null)
        {
            return false;
        }

        var newParent = ResolveFolder(newParentId) ?? _root;
        if (target.IsFolder && IsDescendant(target, newParent.Id))
        {
            return false;
        }

        if (!RemoveNodeRecursive(_root, id))
        {
            return false;
        }

        newParent.Children.Add(target);
        Save();
        return true;
    }

    public bool ExportToFile(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase))
            {
                var html = BuildBookmarkHtml(_root.Children);
                File.WriteAllText(filePath, html, Encoding.UTF8);
                return true;
            }

            var json = JsonSerializer.Serialize(_root, BuildJsonOptions());
            File.WriteAllText(filePath, json, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            _log($"书签导出失败: {ex.Message}");
            return false;
        }
    }

    public bool ImportFromFile(string filePath, bool replaceExisting)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var content = File.ReadAllText(filePath);
            var extension = Path.GetExtension(filePath);
            var importedRoot = ResolveImportedRoot(content, extension);
            if (importedRoot is null)
            {
                return false;
            }

            importedRoot = NormalizeImportedRoot(importedRoot);
            if (replaceExisting)
            {
                _root = importedRoot;
            }
            else
            {
                _root.Children.AddRange(importedRoot.Children);
            }

            Save();
            return true;
        }
        catch (Exception ex)
        {
            _log($"书签导入失败: {ex.Message}");
            return false;
        }
    }

    private static BookmarkNode? ResolveImportedRoot(string content, string? extension)
    {
        var preferHtml = string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
        if (preferHtml)
        {
            return TryImportFromHtml(content) ?? TryImportFromJson(content);
        }

        return TryImportFromJson(content) ?? TryImportFromHtml(content);
    }

    private static BookmarkNode? TryImportFromJson(string content)
    {
        try
        {
            var importedRoot = JsonSerializer.Deserialize<BookmarkNode>(content, BuildJsonOptions());
            if (importedRoot is not null)
            {
                return importedRoot;
            }

            var legacy = JsonSerializer.Deserialize<List<LegacyBookmark>>(content, BuildJsonOptions());
            if (legacy is null)
            {
                return null;
            }

            var root = CreateRoot();
            foreach (var item in legacy)
            {
                if (string.IsNullOrWhiteSpace(item.Url) || !TryNormalizeUrl(item.Url, out var normalizedUrl))
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(item.Title) ? normalizedUrl : item.Title.Trim();
                root.Children.Add(CreateLinkNode(title, normalizedUrl));
            }

            return root;
        }
        catch
        {
            return null;
        }
    }

    private static BookmarkNode? TryImportFromHtml(string content)
    {
        try
        {
            var anchorRegex = new Regex(
                "<A\\s+[^>]*HREF\\s*=\\s*(['\"])(.*?)\\1[^>]*>(.*?)</A>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var folderRegex = new Regex(
                "<H3[^>]*>(.*?)</H3>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var beginFolderRegex = new Regex("<DL\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var endFolderRegex = new Regex("</DL>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var root = CreateRoot();
            var stack = new Stack<BookmarkNode>();
            stack.Push(root);
            BookmarkNode? pendingFolder = null;

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var folderMatch = folderRegex.Match(line);
                if (folderMatch.Success)
                {
                    var folderName = WebUtility.HtmlDecode(folderMatch.Groups[1].Value).Trim();
                    pendingFolder = new BookmarkNode
                    {
                        Type = BookmarkNodeType.Folder,
                        Name = string.IsNullOrWhiteSpace(folderName) ? "新建文件夹" : folderName
                    };
                    stack.Peek().Children.Add(pendingFolder);
                    continue;
                }

                var linkMatch = anchorRegex.Match(line);
                if (linkMatch.Success)
                {
                    var rawUrl = WebUtility.HtmlDecode(linkMatch.Groups[2].Value).Trim();
                    var title = WebUtility.HtmlDecode(linkMatch.Groups[3].Value).Trim();
                    if (!TryNormalizeUrl(rawUrl, out var normalizedUrl))
                    {
                        continue;
                    }

                    stack.Peek().Children.Add(CreateLinkNode(
                        string.IsNullOrWhiteSpace(title) ? normalizedUrl : title,
                        normalizedUrl));
                    continue;
                }

                if (beginFolderRegex.IsMatch(line))
                {
                    if (pendingFolder is not null)
                    {
                        stack.Push(pendingFolder);
                        pendingFolder = null;
                    }

                    continue;
                }

                if (endFolderRegex.IsMatch(line))
                {
                    if (stack.Count > 1)
                    {
                        stack.Pop();
                    }

                    continue;
                }
            }

            return root;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildBookmarkHtml(IEnumerable<BookmarkNode> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- This is an automatically generated file. -->");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");
        AppendBookmarkHtmlNodes(items, sb, 1);
        sb.AppendLine("</DL><p>");
        return sb.ToString();
    }

    private static void AppendBookmarkHtmlNodes(IEnumerable<BookmarkNode> items, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 4);
        foreach (var item in items)
        {
            if (item.IsFolder)
            {
                var folderName = WebUtility.HtmlEncode(item.Name);
                sb.AppendLine($"{indent}<DT><H3>{folderName}</H3>");
                sb.AppendLine($"{indent}<DL><p>");
                AppendBookmarkHtmlNodes(item.Children, sb, depth + 1);
                sb.AppendLine($"{indent}</DL><p>");
                continue;
            }

            var name = WebUtility.HtmlEncode(item.Name);
            var url = WebUtility.HtmlEncode(item.Url);
            sb.AppendLine($"{indent}<DT><A HREF=\"{url}\">{name}</A>");
        }
    }

    private void Save()
    {
        if (!Core.JsonFileStore.Save(_storePath, _root, out var error))
        {
            _log($"书签保存失败: {error}");
        }
    }

    private static JsonSerializerOptions BuildJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    private static BookmarkNode CreateRoot()
    {
        return new BookmarkNode
        {
            Type = BookmarkNodeType.Folder,
            Name = "ROOT"
        };
    }

    private static BookmarkNode CreateLinkNode(string name, string url)
    {
        return new BookmarkNode
        {
            Type = BookmarkNodeType.Link,
            Name = string.IsNullOrWhiteSpace(name) ? url : name,
            Url = url
        };
    }

    private static BookmarkNode NormalizeImportedRoot(BookmarkNode sourceRoot)
    {
        var normalizedRoot = NormalizeTree(sourceRoot, isRoot: true);
        return PromoteToolbarFolderChildren(normalizedRoot);
    }

    private static BookmarkNode PromoteToolbarFolderChildren(BookmarkNode root)
    {
        if (root.Children.Count != 1)
        {
            return root;
        }

        var first = root.Children[0];
        if (!first.IsFolder || first.Children.Count == 0 || !LooksLikeToolbarFolder(first.Name))
        {
            return root;
        }

        var promoted = first.Children.ToList();
        root.Children.Clear();
        root.Children.AddRange(promoted);
        return root;
    }

    private static bool LooksLikeToolbarFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        var normalized = folderName.Trim().ToLowerInvariant();
        return normalized is
            "书签栏" or
            "书签工具栏" or
            "收藏栏" or
            "收藏夹栏" or
            "bookmarks bar" or
            "bookmark bar" or
            "favorites bar";
    }

    private BookmarkNode? ResolveFolder(Guid? id)
    {
        if (id is null)
        {
            return _root;
        }

        var node = FindNode(id.Value);
        if (node is null)
        {
            return null;
        }

        return node.IsFolder ? node : FindParent(id.Value);
    }

    private BookmarkNode? FindParent(Guid childId)
    {
        return FindParentRecursive(_root, childId);
    }

    private static BookmarkNode? FindParentRecursive(BookmarkNode parent, Guid childId)
    {
        foreach (var child in parent.Children)
        {
            if (child.Id == childId)
            {
                return parent;
            }

            if (!child.IsFolder)
            {
                continue;
            }

            var nested = FindParentRecursive(child, childId);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static BookmarkNode? FindNodeRecursive(BookmarkNode current, Guid id)
    {
        if (current.Id == id)
        {
            return current;
        }

        foreach (var child in current.Children)
        {
            var node = FindNodeRecursive(child, id);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    private BookmarkNode? FindByUrl(string absoluteUrl)
    {
        return FindByUrlRecursive(_root, absoluteUrl);
    }

    private static BookmarkNode? FindByUrlRecursive(BookmarkNode current, string absoluteUrl)
    {
        if (current.IsLink && string.Equals(current.Url, absoluteUrl, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        foreach (var child in current.Children)
        {
            var node = FindByUrlRecursive(child, absoluteUrl);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
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

    private static bool RemoveNodeRecursive(BookmarkNode parent, Guid id)
    {
        var index = parent.Children.FindIndex(item => item.Id == id);
        if (index >= 0)
        {
            parent.Children.RemoveAt(index);
            return true;
        }

        foreach (var child in parent.Children.Where(item => item.IsFolder))
        {
            if (RemoveNodeRecursive(child, id))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDescendant(BookmarkNode ancestor, Guid nodeId)
    {
        if (ancestor.Id == nodeId)
        {
            return true;
        }

        foreach (var child in ancestor.Children.Where(c => c.IsFolder))
        {
            if (IsDescendant(child, nodeId))
            {
                return true;
            }
        }

        return false;
    }

    private static BookmarkNode NormalizeTree(BookmarkNode node, bool isRoot = false)
    {
        var safeNode = new BookmarkNode
        {
            Id = node.Id == Guid.Empty ? Guid.NewGuid() : node.Id,
            Type = node.Type,
            Name = isRoot
                ? "ROOT"
                : (string.IsNullOrWhiteSpace(node.Name)
                    ? (node.Type == BookmarkNodeType.Folder ? "新建文件夹" : "新书签")
                    : node.Name.Trim()),
            Url = node.Type == BookmarkNodeType.Link ? node.Url?.Trim() ?? string.Empty : string.Empty,
            CreatedUtc = node.CreatedUtc == default ? DateTime.UtcNow : node.CreatedUtc
        };

        if (safeNode.Type == BookmarkNodeType.Link && !TryNormalizeUrl(safeNode.Url, out _))
        {
            safeNode.Url = "https://example.com";
        }

        foreach (var child in node.Children ?? [])
        {
            safeNode.Children.Add(NormalizeTree(child));
        }

        return safeNode;
    }

    private static bool TryNormalizeUrl(string input, out string normalizedUrl)
    {
        var value = input.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute is not null)
        {
            normalizedUrl = absolute.AbsoluteUri;
            return true;
        }

        if (Uri.TryCreate($"https://{value}", UriKind.Absolute, out var httpsUri) && httpsUri is not null)
        {
            normalizedUrl = httpsUri.AbsoluteUri;
            return true;
        }

        normalizedUrl = string.Empty;
        return false;
    }

    private sealed class LegacyBookmark
    {
        public string Url { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }
}

