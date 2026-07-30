namespace Lfsun.Win.Browser.Bookmarks;

public enum BookmarkNodeType
{
    Folder = 0,
    Link = 1
}

public sealed class BookmarkNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public BookmarkNodeType Type { get; set; } = BookmarkNodeType.Folder;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public List<BookmarkNode> Children { get; init; } = [];

    public bool IsFolder => Type == BookmarkNodeType.Folder;
    public bool IsLink => Type == BookmarkNodeType.Link;
}

