namespace Lfsun.Win.Browser.Core;

public sealed class RecentAddressCommandLayout
{
    public string CommandId { get; set; } = string.Empty;
    public string ArgumentToken { get; set; } = string.Empty;
    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;
}

