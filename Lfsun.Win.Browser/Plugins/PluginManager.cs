using System.Text.Json;
using System.Text.Json.Nodes;
using Lfsun.Win.Browser.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginManager
{
    // 中文注释：内容脚本注入能力对应的权限名。
    public const string ContentInjectPermission = "content:inject";

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
            File.WriteAllText(info.ManifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
            Directory.Delete(info.DirectoryPath, recursive: true);
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
                error = "manifest.json 缺少有效 id。";
                return false;
            }

            var targetDirectory = Path.Combine(_pluginsRoot, manifest.Id.Trim());
            if (Directory.Exists(targetDirectory))
            {
                error = $"插件 '{manifest.Id}' 已存在。";
                return false;
            }

            CopyDirectoryRecursive(sourceDirectory, targetDirectory);
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

    private static void CopyDirectoryRecursive(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var targetSubDirectory = Path.Combine(targetDirectory, Path.GetFileName(directory));
            CopyDirectoryRecursive(directory, targetSubDirectory);
        }
    }
}

