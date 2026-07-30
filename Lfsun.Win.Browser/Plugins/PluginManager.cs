using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Lfsun.Win.Browser.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginManager
{
    // 中文注释：内容脚本注入能力对应的权限名。
    public const string ContentInjectPermission = "content:inject";

    // 中文注释：写 manifest.json 用的序列化选项；.NET 8 起 JsonNode.ToJsonString 传入新建的
    // JsonSerializerOptions 时必须显式指定 TypeInfoResolver，否则报
    // “must specify a TypeInfoResolver setting before being marked as read-only”。
    private static readonly JsonSerializerOptions ManifestWriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly string _pluginsRoot;
    private readonly Action<string> _log;
    private bool _enforcePermissions;
    private HashSet<string> _allowedPermissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PluginRuntime> _plugins = new();

    public PluginManager(string pluginsRoot, BrowserConfig config, Action<string> log)
    {
        _pluginsRoot = pluginsRoot;
        _log = log;
        UpdatePolicy(config);
    }

    public int PluginCount => _plugins.Count;
    public string PluginsRoot => _pluginsRoot;

    public void UpdatePolicy(BrowserConfig config)
    {
        _enforcePermissions = config.EnforcePluginPermissions;
        _allowedPermissions = new HashSet<string>(
            config.AllowedPluginPermissions.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public void Reload()
    {
        _plugins.Clear();

        Directory.CreateDirectory(_pluginsRoot);
        foreach (var pluginDirectory in Directory.GetDirectories(_pluginsRoot))
        {
            var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (manifest is null || !manifest.Enabled || string.IsNullOrWhiteSpace(manifest.Id))
                {
                    continue;
                }

                var requestedPermissions = manifest.Permissions
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (_enforcePermissions)
                {
                    if (requestedPermissions.Length == 0)
                    {
                        _log($"[插件:{manifest.Id}] 已阻止：未声明权限");
                        continue;
                    }

                    var denied = requestedPermissions
                        .Where(permission => !_allowedPermissions.Contains(permission))
                        .ToArray();
                    if (denied.Length > 0)
                    {
                        _log($"[插件:{manifest.Id}] 已阻止：权限不在白名单 {string.Join(", ", denied)}");
                        continue;
                    }
                }

                var scripts = new List<string>();
                foreach (var script in manifest.Scripts)
                {
                    var scriptPath = Path.Combine(pluginDirectory, script);
                    if (!File.Exists(scriptPath))
                    {
                        _log($"[插件:{manifest.Id}] 缺少脚本: {script}");
                        continue;
                    }

                    scripts.Add(File.ReadAllText(scriptPath));
                }

                // 中文注释：内容脚本需声明 content:inject 权限；白名单校验由上方 denied 检查覆盖。
                var contentScriptGroups = new List<PluginContentScriptGroup>();
                if (manifest.ContentScripts.Length > 0)
                {
                    if (!requestedPermissions.Contains(ContentInjectPermission, StringComparer.OrdinalIgnoreCase))
                    {
                        _log($"[插件:{manifest.Id}] 已忽略内容脚本：未声明 {ContentInjectPermission} 权限");
                    }
                    else
                    {
                        foreach (var group in manifest.ContentScripts)
                        {
                            var patternSources = PluginRuntime.BuildPatternSources(group.Matches);
                            var groupScripts = new List<string>();
                            foreach (var script in group.Scripts)
                            {
                                var scriptPath = Path.Combine(pluginDirectory, script);
                                if (!File.Exists(scriptPath))
                                {
                                    _log($"[插件:{manifest.Id}] 缺少内容脚本: {script}");
                                    continue;
                                }

                                groupScripts.Add(File.ReadAllText(scriptPath));
                            }

                            if (patternSources.Length > 0 && groupScripts.Count > 0)
                            {
                                contentScriptGroups.Add(new PluginContentScriptGroup(patternSources, groupScripts));
                            }
                        }
                    }
                }

                if (scripts.Count == 0 && contentScriptGroups.Count == 0)
                {
                    _log($"[插件:{manifest.Id}] 已跳过：无可用脚本");
                    continue;
                }

                _plugins.Add(new PluginRuntime(manifest, scripts, contentScriptGroups));
            }
            catch (Exception ex)
            {
                _log($"[插件] 加载失败 '{pluginDirectory}': {ex.Message}");
            }
        }

        _log($"插件已加载: {_plugins.Count}");
    }

    public IReadOnlyList<PluginPackageInfo> DiscoverPackages()
    {
        var list = new List<PluginPackageInfo>();
        Directory.CreateDirectory(_pluginsRoot);

        foreach (var pluginDirectory in Directory.GetDirectories(_pluginsRoot))
        {
            var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
                {
                    continue;
                }

                var requestedPermissions = manifest.Permissions
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var status = BuildLoadStatus(manifest, requestedPermissions);
                list.Add(new PluginPackageInfo
                {
                    Id = manifest.Id,
                    Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
                    Description = manifest.Description,
                    Enabled = manifest.Enabled,
                    DirectoryPath = pluginDirectory,
                    ManifestPath = manifestPath,
                    Permissions = requestedPermissions,
                    IsLoadable = status == "可加载",
                    LoadStatus = status
                });
            }
            catch (Exception ex)
            {
                _log($"[插件] 读取失败 '{pluginDirectory}': {ex.Message}");
            }
        }

        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool SetPluginEnabled(string pluginId, bool enabled, out string error)
    {
        var info = DiscoverPackages().FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (info is null)
        {
            error = "未找到插件。";
            return false;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(info.ManifestPath)) as JsonObject;
            if (root is null)
            {
                error = "manifest.json 格式无效。";
                return false;
            }

            root["enabled"] = enabled;
            File.WriteAllText(info.ManifestPath, root.ToJsonString(ManifestWriteOptions));
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool UninstallPlugin(string pluginId, out string error)
    {
        var info = DiscoverPackages().FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (info is null)
        {
            error = "未找到插件。";
            return false;
        }

        try
        {
            ForceDeleteDirectory(info.DirectoryPath);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool InstallPluginFromDirectory(string sourceDirectory, out string error)
    {
        var manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            error = "目录中缺少 manifest.json。";
            return false;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                // 中文注释：无 id 时尝试按标准 Chrome/Edge 扩展清单自动转换（仅支持 content_scripts 型扩展）。
                return TryInstallChromeExtension(sourceDirectory, manifestPath, out error);
            }

            var targetDirectory = Path.Combine(_pluginsRoot, manifest.Id.Trim());
            if (!TryClearBrokenPluginDirectory(targetDirectory))
            {
                error = $"插件 '{manifest.Id}' 已存在。";
                return false;
            }

            try
            {
                CopyDirectoryRecursive(sourceDirectory, targetDirectory);
            }
            catch
            {
                // 中文注释：复制中途失败时回滚目标目录，避免留下没有 manifest.json 的残留导致下次安装报“已存在”。
                ForceDeleteDirectory(targetDirectory);
                throw;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool InstallPluginFromZip(string zipPath, out string error)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lp-plugin-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempRoot);
            var manifest = Directory.GetFiles(tempRoot, "manifest.json", SearchOption.AllDirectories).FirstOrDefault();
            if (manifest is null)
            {
                error = "压缩包中缺少 manifest.json。";
                return false;
            }

            var sourceDir = Path.GetDirectoryName(manifest);
            if (string.IsNullOrWhiteSpace(sourceDir))
            {
                error = "压缩包结构无效。";
                return false;
            }

            return InstallPluginFromDirectory(sourceDir, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    public async Task InjectForPageAsync(WebView2 webView, Uri uri)
    {
        foreach (var plugin in _plugins)
        {
            if (!plugin.IsMatch(uri))
            {
                continue;
            }

            foreach (var scriptBody in plugin.ScriptBodies)
            {
                try
                {
                    await webView.ExecuteScriptAsync(scriptBody);
                }
                catch (Exception ex)
                {
                    _log($"[插件:{plugin.Manifest.Id}] 注入失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 中文注释：把已加载插件的内容脚本注册到 WebView2（文档创建时执行）；
    /// 脚本内部先用匹配正则校验当前页面地址，不匹配则直接返回。
    /// </summary>
    public async Task RegisterContentScriptsAsync(CoreWebView2 coreWebView)
    {
        foreach (var plugin in _plugins)
        {
            foreach (var group in plugin.ContentScriptGroups)
            {
                foreach (var scriptBody in group.ScriptBodies)
                {
                    try
                    {
                        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(
                            BuildGuardedContentScript(group.MatchPatternSources, scriptBody));
                    }
                    catch (Exception ex)
                    {
                        _log($"[插件:{plugin.Manifest.Id}] 内容脚本注册失败: {ex.Message}");
                    }
                }
            }
        }
    }

    // 中文注释：手动在当前页面执行指定插件的全部脚本（“在当前页面运行”入口）；
    // 由用户主动触发，不受地址匹配限制，方便“一键填充表单”这类点一下才干活的插件。
    public async Task<(bool Ok, string Error)> RunPluginOnPageAsync(WebView2 webView, string pluginId)
    {
        var plugin = _plugins.FirstOrDefault(p =>
            string.Equals(p.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
        {
            return (false, "插件未加载（可能已禁用或被权限拦截，请先启用/重载）");
        }

        var executed = 0;
        try
        {
            foreach (var scriptBody in plugin.ScriptBodies)
            {
                await webView.ExecuteScriptAsync(scriptBody);
                executed++;
            }

            foreach (var group in plugin.ContentScriptGroups)
            {
                foreach (var scriptBody in group.ScriptBodies)
                {
                    await webView.ExecuteScriptAsync(scriptBody);
                    executed++;
                }
            }
        }
        catch (Exception ex)
        {
            _log($"[插件:{pluginId}] 手动执行失败: {ex.Message}");
            return (false, ex.Message);
        }

        return executed > 0 ? (true, string.Empty) : (false, "插件没有可执行的脚本");
    }

    // 中文注释：生成带地址守卫的包裹脚本；正则源串用 JSON 序列化转 JS 字符串字面量，避免转义问题。
    private static string BuildGuardedContentScript(string[] patternSources, string scriptBody)
    {
        var patternArray = string.Join(", ", patternSources.Select(source => JsonSerializer.Serialize(source)));
        return "(function () {\n" +
               $"    var __lpPatterns = [{patternArray}];\n" +
               "    var __lpHref = window.location.href;\n" +
               "    if (!__lpPatterns.some(function (p) { try { return new RegExp(p, 'i').test(__lpHref); } catch (e) { return false; } })) { return; }\n" +
               scriptBody + "\n" +
               "})();";
    }

    private string BuildLoadStatus(PluginManifest manifest, string[] requestedPermissions)
    {
        if (!manifest.Enabled)
        {
            return "已禁用";
        }

        if (!_enforcePermissions)
        {
            return "可加载";
        }

        if (requestedPermissions.Length == 0)
        {
            return "未声明权限";
        }

        var denied = requestedPermissions.Where(permission => !_allowedPermissions.Contains(permission)).ToArray();
        return denied.Length > 0 ? $"权限拒绝: {string.Join(", ", denied)}" : "可加载";
    }

    // 中文注释：internal 供 MainWindow 启动迁移复用（把程序目录旧插件搬到用户数据目录）。
    internal static void CopyDirectoryRecursive(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
            // 中文注释：清除只读等属性；源目录若是 git 仓库，其对象文件默认只读，复制后会导致覆写/卸载失败。
            File.SetAttributes(targetFile, FileAttributes.Normal);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var directoryName = Path.GetFileName(directory);
            // 中文注释：跳过 .git 等以点开头的元数据目录，插件运行不需要它们，且 git 只读对象文件会卡死卸载。
            if (directoryName.StartsWith('.'))
            {
                continue;
            }

            CopyDirectoryRecursive(directory, Path.Combine(targetDirectory, directoryName));
        }
    }

    // 中文注释：递归清除只读属性后再删除目录；直接 Directory.Delete 遇到只读文件（如 git 对象）会报“拒绝访问”。
    private static void ForceDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }

    // 中文注释：目标目录存在但缺少 manifest.json 时视为历史安装/卸载失败留下的残留，清理后允许重新安装；
    // 返回 false 表示目录内仍有有效插件（真正的“已存在”）。
    private static bool TryClearBrokenPluginDirectory(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return true;
        }

        if (File.Exists(Path.Combine(targetDirectory, "manifest.json")))
        {
            return false;
        }

        ForceDeleteDirectory(targetDirectory);
        return true;
    }

    // 中文注释：把标准 Chrome/Edge 扩展（manifest_version + content_scripts）转换为本浏览器插件格式后安装；
    // background/popup 依赖 Chrome 扩展运行时，无法在 WebView2 中模拟，会明确报不支持。
    private bool TryInstallChromeExtension(string sourceDirectory, string manifestPath, out string error)
    {
        try
        {
            var parsed = JsonNode.Parse(File.ReadAllText(manifestPath), documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (parsed is not JsonObject root || root["manifest_version"] is null)
            {
                error = "manifest.json 缺少有效 id。";
                return false;
            }

            var name = root["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.StartsWith("__MSG_", StringComparison.OrdinalIgnoreCase))
            {
                // 中文注释：i18n 占位名（__MSG_xxx__）不解析 _locales，回退用目录名。
                name = new DirectoryInfo(sourceDirectory).Name;
            }

            var contentScripts = new JsonArray();
            if (root["content_scripts"] is JsonArray chromeGroups)
            {
                foreach (var groupNode in chromeGroups)
                {
                    if (groupNode is not JsonObject group)
                    {
                        continue;
                    }

                    var matches = new JsonArray();
                    foreach (var match in group["matches"] as JsonArray ?? new JsonArray())
                    {
                        var pattern = match?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(pattern))
                        {
                            matches.Add(pattern.Trim());
                        }
                    }

                    var scripts = new JsonArray();
                    foreach (var script in group["js"] as JsonArray ?? new JsonArray())
                    {
                        var scriptFile = script?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(scriptFile))
                        {
                            scripts.Add(scriptFile.Trim());
                        }
                    }

                    if (matches.Count > 0 && scripts.Count > 0)
                    {
                        contentScripts.Add(new JsonObject { ["matches"] = matches, ["scripts"] = scripts });
                    }
                }
            }

            if (contentScripts.Count == 0)
            {
                error = "该扩展没有可转换的内容脚本（content_scripts + js），暂不支持 background/popup 类 Chrome 扩展。";
                return false;
            }

            var pluginId = BuildPluginIdFromName(name);
            var targetDirectory = Path.Combine(_pluginsRoot, pluginId);
            if (!TryClearBrokenPluginDirectory(targetDirectory))
            {
                error = $"插件 '{pluginId}' 已存在。";
                return false;
            }

            var description = root["description"]?.GetValue<string>() is { } desc &&
                              !desc.StartsWith("__MSG_", StringComparison.OrdinalIgnoreCase)
                ? desc
                : "由 Chrome 扩展自动转换";
            var converted = new JsonObject
            {
                ["id"] = pluginId,
                ["name"] = name,
                ["description"] = description,
                ["enabled"] = true,
                ["permissions"] = new JsonArray("content:dom_read", "content:dom_write", ContentInjectPermission),
                ["contentScripts"] = contentScripts
            };

            try
            {
                CopyDirectoryRecursive(sourceDirectory, targetDirectory);
                // 中文注释：原始 Chrome 清单保留为 manifest.chrome.json 便于溯源，manifest.json 写入转换结果。
                File.Move(
                    Path.Combine(targetDirectory, "manifest.json"),
                    Path.Combine(targetDirectory, "manifest.chrome.json"),
                    overwrite: true);
                File.WriteAllText(
                    Path.Combine(targetDirectory, "manifest.json"),
                    converted.ToJsonString(ManifestWriteOptions));
            }
            catch
            {
                // 中文注释：转换安装中途失败时回滚，避免残留半成品目录阻塞下次安装。
                ForceDeleteDirectory(targetDirectory);
                throw;
            }
            _log($"[插件:{pluginId}] 已从 Chrome 扩展清单自动转换安装");
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // 中文注释：从扩展名称派生插件目录 id：小写化，仅保留字母数字，其余折叠为连字符。
    private static string BuildPluginIdFromName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var id = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(id) ? $"imported-{Guid.NewGuid():N}"[..17] : id;
    }
}

