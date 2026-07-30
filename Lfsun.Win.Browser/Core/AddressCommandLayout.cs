namespace Lfsun.Win.Browser.Core;

public sealed class AddressCommandLayout
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Command { get; set; } = string.Empty;
    public int Order { get; set; }
}

