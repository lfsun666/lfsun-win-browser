using System.Text.Json.Serialization;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("matches")]
    public string[] Matches { get; init; } = Array.Empty<string>();

    [JsonPropertyName("scripts")]
    public string[] Scripts { get; init; } = Array.Empty<string>();

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; init; } = Array.Empty<string>();

    // 中文注释：内容脚本分组：文档创建时按匹配域名注入，需声明 content:inject 权限。
    [JsonPropertyName("contentScripts")]
    public PluginContentScript[] ContentScripts { get; init; } = Array.Empty<PluginContentScript>();
}

/// <summary>
/// 中文注释：单组内容脚本声明：匹配模式（支持 * 通配符与 &lt;all_urls&gt;）+ js 文件列表。
/// </summary>
public sealed class PluginContentScript
{
    [JsonPropertyName("matches")]
    public string[] Matches { get; init; } = Array.Empty<string>();

    [JsonPropertyName("scripts")]
    public string[] Scripts { get; init; } = Array.Empty<string>();
}

