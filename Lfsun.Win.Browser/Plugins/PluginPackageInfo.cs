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
}

