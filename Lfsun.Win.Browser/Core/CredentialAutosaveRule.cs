namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 中文注释：登录密码自动保存的站点规则。
/// Host 为域名或 IP（可带端口，如 example.com、192.168.1.1、192.168.1.1:8080）；
/// Mode 为 Allow（允许自动保存）或 Block（不自动保存）。命中规则优先于全局开关。
/// </summary>
public sealed class CredentialAutosaveRule
{
    public const string ModeAllow = "Allow";
    public const string ModeBlock = "Block";

    public string Host { get; set; } = string.Empty;
    public string Mode { get; set; } = ModeAllow;
}
