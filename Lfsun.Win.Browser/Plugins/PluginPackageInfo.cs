namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginPackageInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string DirectoryPath { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public string[] Permissions { get; init; } = [];
    public bool IsLoadable { get; init; }
    public string LoadStatus { get; init; } = "未知";

    // 中文注释：手动运行快捷键文本（如 Ctrl+Shift+V），来自 manifest 或 Chrome 清单 commands。
    public string RunShortcut { get; init; } = string.Empty;

    // 中文注释：插件选项页相对路径（如 options.html），存在时插件下拉展示“插件选项”入口。
    public string OptionsPage { get; init; } = string.Empty;
}
