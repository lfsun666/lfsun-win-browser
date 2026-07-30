namespace Lfsun.Win.Browser.History;

public sealed record HistoryEntry(
    Guid Id,
    string Title,
    string Url,
    DateTime VisitedAtUtc);

