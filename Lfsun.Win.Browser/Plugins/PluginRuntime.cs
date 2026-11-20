using System.Text.RegularExpressions;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginRuntime
{
    private readonly Regex[] _matchRules;
    // 中文注释：加载时归一化（去空格去重）的权限集合，运行时校验统一走它，
    // 与加载侧白名单校验口径一致（manifest 声明里带空格也能正常命中）。
    private readonly HashSet<string> _permissions;

    public PluginRuntime(PluginManifest manifest, IReadOnlyList<string> scriptBodies)
        : this(manifest, scriptBodies, Array.Empty<PluginContentScriptGroup>())
    {
    }

    public PluginRuntime(
        PluginManifest manifest,
        IReadOnlyList<string> scriptBodies,
        IReadOnlyList<PluginContentScriptGroup> contentScriptGroups,
        IReadOnlyList<string>? runEvents = null,
        string runShortcut = "",
        string directoryPath = "",
        string optionsPage = "")
    {
        Manifest = manifest;
        ScriptBodies = scriptBodies;
        ContentScriptGroups = contentScriptGroups;
        RunEvents = runEvents ?? Array.Empty<string>();
        RunShortcut = runShortcut;
        DirectoryPath = directoryPath;
        OptionsPage = optionsPage;
        _matchRules = BuildRules(manifest.Matches);
        _permissions = new HashSet<string>(
            manifest.Permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public PluginManifest Manifest { get; }
    public IReadOnlyList<string> ScriptBodies { get; }
    // 中文注释：内容脚本分组（文档创建时注入，需 content:inject 权限）。
    public IReadOnlyList<PluginContentScriptGroup> ContentScriptGroups { get; }

    // 中文注释：判断插件是否声明了某权限（与加载侧白名单校验同口径）。
    public bool HasPermission(string permission) => _permissions.Contains(permission);

    // 中文注释：手动运行时要派发的 DOM 事件名（manifest 声明优先，否则从脚本源码自动探测）。
    public IReadOnlyList<string> RunEvents { get; }

    // 中文注释：触发手动运行的快捷键文本（如 Ctrl+Shift+V），来自 manifest 或 Chrome 清单 commands。
    public string RunShortcut { get; }

    // 中文注释：插件安装目录绝对路径，供 chrome.* 垫片计算 file:// 基地址（选项页识别）。
    public string DirectoryPath { get; }

    // 中文注释：插件选项页相对路径（options_ui/options_page 转换而来），空表示无选项页。
    public string OptionsPage { get; }

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
