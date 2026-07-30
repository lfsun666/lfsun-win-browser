namespace Lfsun.Win.Browser.Core;

public sealed class AppLaunchOptions
{
    public const string SharedProfileId = "shared";

    public string ProfileId { get; init; } = SharedProfileId;
    public bool IsPrivateWindow { get; init; }
    public bool IsIsolatedWindow { get; init; }
    public string? RestoreSessionPayloadPath { get; init; }

    // 中文注释：无痕窗口的 profile 数据放到 %TEMP%，退出时删除，不落地到用户数据目录。
    public string ProfileStoragePath =>
        IsPrivateWindow
            ? Path.Combine(PrivateProfilesRootPath, ProfileId)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LfsunBrowser",
                "profiles",
                ProfileId);

    public static string PrivateProfilesRootPath =>
        Path.Combine(Path.GetTempPath(), "LfsunBrowser", "private-profiles");

    public string WindowModeText =>
        IsPrivateWindow
            ? "无痕"
            : IsIsolatedWindow
                ? "独立"
                : "共享";

    public static AppLaunchOptions Parse(string[] args)
    {
        var profileId = SharedProfileId;
        var isPrivateWindow = false;
        var isIsolatedWindow = false;
        string? restoreSessionPayloadPath = null;

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--private", StringComparison.OrdinalIgnoreCase))
            {
                isPrivateWindow = true;
                isIsolatedWindow = true;
                continue;
            }

            if (string.Equals(arg, "--isolated", StringComparison.OrdinalIgnoreCase))
            {
                isIsolatedWindow = true;
                continue;
            }

            if (arg.StartsWith("--profile-id=", StringComparison.OrdinalIgnoreCase))
            {
                var rawProfileId = arg["--profile-id=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(rawProfileId))
                {
                    profileId = SanitizeProfileId(rawProfileId);
                }

                continue;
            }

            if (arg.StartsWith("--restore-session-payload=", StringComparison.OrdinalIgnoreCase))
            {
                var rawPath = arg["--restore-session-payload=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(rawPath))
                {
                    restoreSessionPayloadPath = Uri.UnescapeDataString(rawPath);
                }
            }
        }

        if (isPrivateWindow && string.Equals(profileId, SharedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            profileId = $"private-{Guid.NewGuid():N}";
        }
        else if (isIsolatedWindow && string.Equals(profileId, SharedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            profileId = CreateIsolatedProfileId();
        }

        return new AppLaunchOptions
        {
            ProfileId = profileId,
            IsPrivateWindow = isPrivateWindow,
            IsIsolatedWindow = isIsolatedWindow,
            RestoreSessionPayloadPath = restoreSessionPayloadPath
        };
    }

    public static string CreateIsolatedProfileId()
    {
        // 中文注释：格式串不能写成 {guid:N[..8]}，Guid 只支持单字符格式，否则抛 FormatException。
        return $"profile-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static string SanitizeProfileId(string rawProfileId)
    {
        var safe = new string(rawProfileId
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? SharedProfileId : safe;
    }
}


