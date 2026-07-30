namespace Lfsun.Win.Browser.Security;

public sealed record CredentialEntry(
    Guid Id,
    string Site,
    string Username,
    string Password,
    DateTime UpdatedUtc);

