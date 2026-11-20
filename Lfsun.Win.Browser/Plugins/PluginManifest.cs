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

    // 中文注释：插件版本号（如 "1.2.0"，Chrome 扩展转换时映射其 version 字段）；
    // 留空视为未声明。为将来插件升级/迁移比对打基础。
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

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

    // 中文注释：手动运行（固定按钮/“在当前页面运行”）时向页面派发的 DOM 事件名（如 auto-fill-form:fill）；
    // 留空则加载时自动从脚本源码探测 addEventListener('xxx:fill'/'xxx:run') 兜底。
    [JsonPropertyName("runEvent")]
    public string RunEvent { get; init; } = string.Empty;

    // 中文注释：触发“在当前页面运行”的快捷键文本（如 Ctrl+Shift+V）；
    // 留空则回退读 manifest.chrome.json 里 commands.suggested_key.default。
    [JsonPropertyName("runShortcut")]
    public string RunShortcut { get; init; } = string.Empty;

    // 中文注释：插件选项页相对路径（如 options.html）；
    // 留空则回退读 manifest.chrome.json 里 options_ui.page / options_page。
    [JsonPropertyName("optionsPage")]
    public string OptionsPage { get; init; } = string.Empty;
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
