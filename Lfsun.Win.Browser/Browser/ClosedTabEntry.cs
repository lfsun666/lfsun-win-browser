namespace Lfsun.Win.Browser.Browser;

public sealed record ClosedTabEntry(
    string Title,
    string Url,
    DateTime ClosedAtUtc);

