using System.Text.RegularExpressions;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginRuntime
{
    private readonly Regex[] _matchRules;

    public PluginRuntime(PluginManifest manifest, IReadOnlyList<string> scriptBodies)
        : this(manifest, scriptBodies, Array.Empty<PluginContentScriptGroup>())
    {
    }

    public PluginRuntime(
        PluginManifest manifest,
        IReadOnlyList<string> scriptBodies,
        IReadOnlyList<PluginContentScriptGroup> contentScriptGroups)
    {
        Manifest = manifest;
        ScriptBodies = scriptBodies;
        ContentScriptGroups = contentScriptGroups;
        _matchRules = BuildRules(manifest.Matches);
    }

    public PluginManifest Manifest { get; }
    public IReadOnlyList<string> ScriptBodies { get; }
    // 中文注释：内容脚本分组（文档创建时注入，需 content:inject 权限）。
    public IReadOnlyList<PluginContentScriptGroup> ContentScriptGroups { get; }

    public bool IsMatch(Uri uri)
    {
        if (_matchRules.Length == 0)
        {
            return false;
        }

        var absoluteUri = uri.AbsoluteUri;
        return _matchRules.Any(rule => rule.IsMatch(absoluteUri));
    }

    private static Regex[] BuildRules(IEnumerable<string> patterns)
    {
        return BuildPatternSources(patterns)
            .Select(source => new Regex(source, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();
    }

    /// <summary>
    /// 中文注释：把通配符匹配模式转为正则源串（.NET 与 JS RegExp 均可用），供内容脚本守卫代码复用。
    /// </summary>
    internal static string[] BuildPatternSources(IEnumerable<string> patterns)
    {
        var sources = new List<string>();

        foreach (var rawPattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(rawPattern))
            {
                continue;
            }

            if (string.Equals(rawPattern, "<all_urls>", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add("^https?://.+");
                continue;
            }

            var escaped = Regex.Escape(rawPattern.Trim()).Replace("\\*", ".*");
            sources.Add($"^{escaped}$");
        }

        return sources.ToArray();
    }
}

/// <summary>
/// 中文注释：已加载的单组内容脚本：匹配正则源串 + 脚本正文列表。
/// </summary>
public sealed class PluginContentScriptGroup
{
    public PluginContentScriptGroup(string[] matchPatternSources, IReadOnlyList<string> scriptBodies)
    {
        MatchPatternSources = matchPatternSources;
        ScriptBodies = scriptBodies;
    }

    public string[] MatchPatternSources { get; }
    public IReadOnlyList<string> ScriptBodies { get; }
}

