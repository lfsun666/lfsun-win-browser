using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Lfsun.Win.Browser.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Plugins;

public sealed class PluginManager
{
    // 中文注释：内容脚本注入能力对应的权限名。
    public const string ContentInjectPermission = "content:inject";

    // 中文注释：插件跨域抓取权限名；声明后可通过 chrome.net.fetch 让宿主用 HttpClient 发任意请求，
    // 绕过浏览器的同源/CORS 限制（因为是宿主进程发请求，不受渲染引擎安全策略约束）。
    public const string NetFetchPermission = "net:fetch";

    // 中文注释：跨域抓取响应体积上限（默认 8MB），超出部分会被截断，避免 WebMessage 负载过大。
    private const int MaxFetchBytes = 8 * 1024 * 1024;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    private static readonly HashSet<string> TextContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/", "application/json", "application/xml", "application/javascript",
        "application/svg+xml", "+xml", "+json"
    };

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
    // 中文注释：跨域抓取桥的全局默认参数（来自 BrowserConfig，UpdatePolicy 时刷新）。
    private string _fetchProxyUrl = string.Empty;
    private List<string> _fetchUserAgents = new();
    private int _fetchRetry = 1;
    private int _fetchRetryDelayMs = 500;
    private readonly List<PluginRuntime> _plugins = new();

    // 中文注释：已注册插件桥的 WebView（弱引用），storage 变更时向其他页面广播 onChanged。
    private readonly List<WeakReference<CoreWebView2>> _bridgedWebViews = new();

    // 中文注释：探测“手动运行触发事件”的正则：脚本里 document/window.addEventListener('xxx:fill' 或 'xxx:run')。
    private static readonly Regex RunEventListenerRegex = new(
        @"(?:document|window)\.addEventListener\(\s*['""]([\w.-]+:(?:fill|run))['""]",
        RegexOptions.Compiled);

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
        _fetchProxyUrl = config.FetchProxyUrl ?? string.Empty;
        _fetchUserAgents = config.FetchUserAgents ?? new List<string>();
        _fetchRetry = Math.Clamp(config.FetchRetry, 1, 5);
        _fetchRetryDelayMs = Math.Clamp(config.FetchRetryDelayMs, 0, 5000);
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

                // 中文注释：快捷键/选项页优先取 manifest 声明，缺省回退解析 manifest.chrome.json。
                var (runShortcut, optionsPage) = ResolvePluginExtras(manifest, pluginDirectory);
                _plugins.Add(new PluginRuntime(manifest, scripts, contentScriptGroups,
                    ResolveRunEvents(manifest, scripts, contentScriptGroups),
                    runShortcut, pluginDirectory, optionsPage));
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
                var (runShortcut, optionsPage) = ResolvePluginExtras(manifest, pluginDirectory);
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
                    LoadStatus = status,
                    RunShortcut = runShortcut,
                    OptionsPage = optionsPage
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
            // 中文注释：磁盘写入后立即重新加载插件列表，让内存状态与 manifest.json 同步。
            // 否则启用已禁用插件后，它不会出现在 _plugins 列表，导致注册/注入都无效。
            Reload();
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
                    await webView.ExecuteScriptAsync(BuildChromeScopedScript(plugin.Manifest.Id, scriptBody));
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
        // 中文注释：先注册 chrome.* 兼容桥（storage 持久化/插件快捷键/选项页识别），
        // 内容脚本靠它读取 chrome.storage 配置，必须先于内容脚本执行。
        try
        {
            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(BuildBridgeScript());
            _bridgedWebViews.RemoveAll(reference => !reference.TryGetTarget(out _));
            _bridgedWebViews.Add(new WeakReference<CoreWebView2>(coreWebView));
        }
        catch (Exception ex)
        {
            _log($"[插件桥] 注册失败: {ex.Message}");
        }

        foreach (var plugin in _plugins)
        {
            foreach (var group in plugin.ContentScriptGroups)
            {
                foreach (var scriptBody in group.ScriptBodies)
                {
                    try
                    {
                        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(
                            BuildGuardedContentScript(plugin.Manifest.Id, group.MatchPatternSources, scriptBody));
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
                await webView.ExecuteScriptAsync(BuildChromeScopedScript(plugin.Manifest.Id, scriptBody));
                executed++;
            }

            foreach (var group in plugin.ContentScriptGroups)
            {
                foreach (var scriptBody in group.ScriptBodies)
                {
                    await webView.ExecuteScriptAsync(BuildChromeScopedScript(plugin.Manifest.Id, scriptBody));
                    executed++;
                }
            }

            // 中文注释：内容脚本通常只注册监听器且带防重复守卫，重复执行是空操作；
            // 真正的动作要靠“运行事件”触发：派发声明/探测到的事件 + 通用 lfsun-plugin:run。
            await webView.ExecuteScriptAsync(BuildRunEventDispatchScript(plugin));
        }
        catch (Exception ex)
        {
            _log($"[插件:{pluginId}] 手动执行失败: {ex.Message}");
            return (false, ex.Message);
        }

        return executed > 0 || plugin.RunEvents.Count > 0
            ? (true, string.Empty)
            : (false, "插件没有可执行的脚本");
    }

    // 中文注释：已加载插件的手动运行快捷键绑定（供主窗体在工具栏/地址栏聚焦时匹配触发）。
    // 返回值含 Enabled：未启用的插件其快捷键不会触发，需在快捷键一览中明确标注。
    public IReadOnlyList<(string Id, string Name, string Shortcut, bool Enabled)> GetRunShortcutBindings()
    {
        return _plugins
            .Where(plugin => !string.IsNullOrWhiteSpace(plugin.RunShortcut))
            .Select(plugin => (
                plugin.Manifest.Id,
                string.IsNullOrWhiteSpace(plugin.Manifest.Name) ? plugin.Manifest.Id : plugin.Manifest.Name,
                plugin.RunShortcut,
                plugin.Manifest.Enabled))
            .ToArray();
    }

    // 中文注释：生成“运行事件”派发脚本：先派发插件声明/探测到的事件，再派发通用 lfsun-plugin:run。
    private static string BuildRunEventDispatchScript(PluginRuntime plugin)
    {
        var names = plugin.RunEvents
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        names.Add("lfsun-plugin:run");
        var nameArray = string.Join(", ", names.Select(name => JsonSerializer.Serialize(name)));
        var pluginIdLiteral = JsonSerializer.Serialize(plugin.Manifest.Id);
        return "(function () {\n" +
               $"    var __lpNames = [{nameArray}];\n" +
               $"    var __lpDetail = {{ pluginId: {pluginIdLiteral} }};\n" +
               "    __lpNames.forEach(function (n) {\n" +
               "        try { document.dispatchEvent(new CustomEvent(n, { detail: __lpDetail })); } catch (e) {}\n" +
               "    });\n" +
               "})();";
    }

    // 中文注释：确定插件的运行事件：manifest 显式声明优先，否则扫描脚本源码里的触发监听器。
    private static string[] ResolveRunEvents(
        PluginManifest manifest,
        IEnumerable<string> scriptBodies,
        IEnumerable<PluginContentScriptGroup> contentScriptGroups)
    {
        if (!string.IsNullOrWhiteSpace(manifest.RunEvent))
        {
            return new[] { manifest.RunEvent.Trim() };
        }

        var detected = new List<string>();
        foreach (var body in scriptBodies.Concat(contentScriptGroups.SelectMany(group => group.ScriptBodies)))
        {
            foreach (Match match in RunEventListenerRegex.Matches(body))
            {
                var name = match.Groups[1].Value;
                if (!detected.Contains(name, StringComparer.Ordinal))
                {
                    detected.Add(name);
                }
            }
        }

        return detected.ToArray();
    }

    // 中文注释：生成带地址守卫的包裹脚本；正则源串用 JSON 序列化转 JS 字符串字面量，避免转义问题。
    // 包裹内声明局部 chrome 变量指向本插件专属垫片，让扩展的 chrome.storage 等调用直接可用。
    private static string BuildGuardedContentScript(string pluginId, string[] patternSources, string scriptBody)
    {
        var patternArray = string.Join(", ", patternSources.Select(source => JsonSerializer.Serialize(source)));
        var idLiteral = JsonSerializer.Serialize(pluginId);
        return "(function () {\n" +
               $"    var __lpPatterns = [{patternArray}];\n" +
               "    var __lpHref = window.location.href;\n" +
               "    if (!__lpPatterns.some(function (p) { try { return new RegExp(p, 'i').test(__lpHref); } catch (e) { return false; } })) { return; }\n" +
               $"    var chrome = window.__lfsunChromeFor ? window.__lfsunChromeFor({idLiteral}) : (window.chrome || {{}});\n" +
               scriptBody + "\n" +
               "})();";
    }

    // 中文注释：手动运行/地址匹配注入时的包裹：同样提供本插件专属 chrome 垫片。
    private static string BuildChromeScopedScript(string pluginId, string scriptBody)
    {
        var idLiteral = JsonSerializer.Serialize(pluginId);
        return "(function () {\n" +
               $"    var chrome = window.__lfsunChromeFor ? window.__lfsunChromeFor({idLiteral}) : (window.chrome || {{}});\n" +
               scriptBody + "\n" +
               "})();";
    }

    // 中文注释：chrome.* 兼容桥模板：__BASES__/__BINDINGS__ 由 BuildBridgeScript 注入。
    // 提供 chrome.storage.sync/local（经 WebMessage 持久化到插件目录 .storage.json）、
    // storage.onChanged、runtime.getURL/openOptionsPage、tabs.create，
    // 并处理插件快捷键（页面聚焦时窗体收不到按键，这里直接派发运行事件）与选项页全局 chrome 挂载。
    private const string BridgeScriptTemplate = """
        (function () {
            if (window.__lfsunPluginBridgeInstalled) { return; }
            window.__lfsunPluginBridgeInstalled = true;
            var bases = __BASES__;
            var bindings = __BINDINGS__;
            var host = (window.chrome && window.chrome.webview) ? window.chrome.webview : null;
            var caches = {};
            var pending = {};
            var pendingFetch = {};
            var pendingCapture = {};
            var pendingBody = {};
            var listeners = {};
            var seq = 0;
            function post(msg) { if (host) { try { host.postMessage(msg); } catch (e) {} } }
            if (host) {
                host.addEventListener('message', function (e) {
                    var m = e.data;
                    if (!m || typeof m !== 'object') { return; }
                    if (m.type === 'lfsun-plugin-storage-data') {
                        caches[m.pluginId] = m.data || {};
                        var cb = pending[m.reqId];
                        delete pending[m.reqId];
                        if (cb) { cb(caches[m.pluginId]); }
                    } else if (m.type === 'lfsun-plugin-storage-changed') {
                        caches[m.pluginId] = m.data || {};
                        (listeners[m.pluginId] || []).forEach(function (fn) {
                            try { fn(m.changes || {}, 'sync'); } catch (e2) {}
                        });
                    } else if (m.type === 'lfsun-plugin-network-entries-response') {
                        var pc = pendingCapture[m.reqId];
                        delete pendingCapture[m.reqId];
                        if (pc) { pc.resolve(m.entries || []); }
                    } else if (m.type === 'lfsun-plugin-network-body-response') {
                        var pb = pendingBody[m.reqId];
                        delete pendingBody[m.reqId];
                        if (pb) {
                            if (m.error) { pb.reject(new Error(m.error)); }
                            else { pb.resolve({ body: m.body, base64Encoded: m.base64Encoded }); }
                        }
                    } else if (m.type === 'lfsun-plugin-fetch-response') {
                        // 中文注释：跨域抓取响应：base64 编码的二进制转回 ArrayBuffer，成功 resolve、失败 reject。
                        var pf = pendingFetch[m.reqId];
                        if (!pf) { return; }
                        delete pendingFetch[m.reqId];
                        if (m.encoding === 'base64' && typeof m.body === 'string') {
                            try {
                                var bin = atob(m.body);
                                var ab = new Uint8Array(bin.length);
                                for (var i = 0; i < bin.length; i++) { ab[i] = bin.charCodeAt(i); }
                                m.arrayBuffer = ab.buffer;
                            } catch (e2) {}
                        }
                        if (m.ok === false) {
                            pf.reject(new Error(m.statusText || ('HTTP ' + m.status)));
                        } else {
                            pf.resolve(m);
                        }
                    }
                });
            }
            function load(pluginId, cb) {
                if (caches[pluginId]) { cb(caches[pluginId]); return; }
                if (!host) { caches[pluginId] = {}; cb(caches[pluginId]); return; }
                var reqId = 'r' + (++seq);
                pending[reqId] = cb;
                post({ type: 'lfsun-plugin-storage-get', pluginId: pluginId, reqId: reqId });
            }
            window.__lfsunChromeFor = function (pluginId) {
                function getItems(keys, cb) {
                    load(pluginId, function (data) {
                        var out = {};
                        if (keys === null || keys === undefined) {
                            Object.keys(data).forEach(function (k) { out[k] = data[k]; });
                        } else if (typeof keys === 'string') {
                            if (data[keys] !== undefined) { out[keys] = data[keys]; }
                        } else if (Array.isArray(keys)) {
                            keys.forEach(function (k) { if (data[k] !== undefined) { out[k] = data[k]; } });
                        } else {
                            Object.keys(keys).forEach(function (k) { out[k] = data[k] !== undefined ? data[k] : keys[k]; });
                        }
                        if (cb) { cb(out); }
                    });
                }
                function setItems(items, cb) {
                    load(pluginId, function (data) {
                        var changes = {};
                        Object.keys(items || {}).forEach(function (k) {
                            changes[k] = { oldValue: data[k], newValue: items[k] };
                            data[k] = items[k];
                        });
                        post({ type: 'lfsun-plugin-storage-set', pluginId: pluginId, items: items || {} });
                        (listeners[pluginId] || []).forEach(function (fn) { try { fn(changes, 'sync'); } catch (e) {} });
                        if (cb) { cb(); }
                    });
                }
                function removeItems(keys, cb) {
                    load(pluginId, function (data) {
                        var list = typeof keys === 'string' ? [keys] : (keys || []);
                        list.forEach(function (k) { delete data[k]; });
                        post({ type: 'lfsun-plugin-storage-remove', pluginId: pluginId, keys: list });
                        if (cb) { cb(); }
                    });
                }
                var area = { get: getItems, set: setItems, remove: removeItems };
                function netFetch(url, init) {
                    init = init || {};
                    return new Promise(function (resolve, reject) {
                        var reqId = 'f' + (++seq);
                        var method = (init.method || 'GET').toUpperCase();
                        var headers = init.headers || {};
                        var body = (typeof init.body === 'string') ? init.body : (init.body ? String(init.body) : '');
                        var timeoutMs = init.timeoutMs || 30000;
                        var maxBytes = init.maxBytes || (8 * 1024 * 1024);
                        var retry = init.retry || 1;
                        var retryDelayMs = init.retryDelayMs || 0;
                        var proxy = init.proxy || null;
                        var userAgent = init.userAgent || null;
                        var rotateUA = !!init.rotateUA;
                        pendingFetch[reqId] = { resolve: resolve, reject: reject };
                        post({
                            type: 'lfsun-plugin-fetch',
                            pluginId: pluginId,
                            reqId: reqId,
                            url: url,
                            method: method,
                            headers: headers,
                            body: body,
                            timeoutMs: timeoutMs,
                            maxBytes: maxBytes,
                            retry: retry,
                            retryDelayMs: retryDelayMs,
                            proxy: proxy,
                            userAgent: userAgent,
                            rotateUA: rotateUA
                        });
                    });
                }
                function startCapture(filters) {
                    post({ type: 'lfsun-plugin-network-start', pluginId: pluginId, filters: filters || {} });
                    return Promise.resolve();
                }
                function stopCapture() {
                    post({ type: 'lfsun-plugin-network-stop', pluginId: pluginId });
                    return Promise.resolve();
                }
                function getCaptureEntries(opts) {
                    opts = opts || {};
                    return new Promise(function (resolve, reject) {
                        var reqId = 'c' + (++seq);
                        pendingCapture[reqId] = { resolve: resolve, reject: reject };
                        post({ type: 'lfsun-plugin-network-get', pluginId: pluginId, reqId: reqId, clear: !!opts.clear });
                    });
                }
                function getResponseBody(requestId) {
                    return new Promise(function (resolve, reject) {
                        var reqId = 'b' + (++seq);
                        pendingBody[reqId] = { resolve: resolve, reject: reject };
                        post({ type: 'lfsun-plugin-network-body', pluginId: pluginId, reqId: reqId, requestId: requestId });
                    });
                }
                return {
                    storage: {
                        sync: area,
                        local: area,
                        onChanged: {
                            addListener: function (fn) { (listeners[pluginId] = listeners[pluginId] || []).push(fn); }
                        }
                    },
                    net: {
                        // 中文注释：跨域抓取——宿主用 HttpClient 发请求，绕过同源/CORS 限制。
                        // 返回 Promise<{ ok, status, statusText, headers, body, encoding, truncated, arrayBuffer? }>。
                        fetch: netFetch,
                        // 中文注释：Network 抓包：控制 WebView2 记录当前标签页的网络请求。
                        startCapture: startCapture,
                        getCaptureEntries: getCaptureEntries,
                        stopCapture: stopCapture,
                        // 中文注释：按需读取某条请求的响应体（依赖 CDP Network 域）。
                        getResponseBody: getResponseBody
                    },
                    runtime: {
                        onMessage: { addListener: function () {} },
                        getURL: function (p) { return (bases[pluginId] || '') + (p || ''); },
                        openOptionsPage: function () { post({ type: 'lfsun-plugin-open-options', pluginId: pluginId }); }
                    },
                    tabs: {
                        create: function (opts) { post({ type: 'lfsun-plugin-open-url', pluginId: pluginId, url: (opts && opts.url) || '' }); },
                        query: function (q, cb) { if (cb) { cb([]); } }
                    }
                };
            };
            if (bindings.length > 0) {
                document.addEventListener('keydown', function (e) {
                    for (var i = 0; i < bindings.length; i++) {
                        var b = bindings[i];
                        if ((e.ctrlKey || e.metaKey) === !!b.ctrl && e.shiftKey === !!b.shift && e.altKey === !!b.alt &&
                            (e.key || '').toUpperCase() === b.key) {
                            e.preventDefault();
                            e.stopPropagation();
                            (b.events || []).forEach(function (n) {
                                try { document.dispatchEvent(new CustomEvent(n, { detail: { pluginId: b.id } })); } catch (err) {}
                            });
                            return;
                        }
                    }
                }, true);
            }
            var href = window.location.href.toLowerCase();
            Object.keys(bases).forEach(function (id) {
                    if (bases[id] && href.indexOf(bases[id].toLowerCase()) === 0) {
                        var shim = window.__lfsunChromeFor(id);
                        var c = window.chrome = window.chrome || {};
                        c.storage = shim.storage;
                        c.tabs = shim.tabs;
                        c.net = shim.net;
                        c.runtime = c.runtime || {};
                        c.runtime.onMessage = shim.runtime.onMessage;
                        c.runtime.getURL = shim.runtime.getURL;
                        c.runtime.openOptionsPage = shim.runtime.openOptionsPage;
                    }
            });
        })();
        """;

    // 中文注释：按已加载插件生成桥脚本：file:// 基地址表（选项页识别）+ 快捷键绑定表。
    private string BuildBridgeScript()
    {
        var bases = new JsonObject();
        var bindings = new JsonArray();
        foreach (var plugin in _plugins)
        {
            if (!string.IsNullOrEmpty(plugin.DirectoryPath))
            {
                var normalized = plugin.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                bases[plugin.Manifest.Id] = new Uri(normalized).AbsoluteUri;
            }

            if (TryParseShortcutParts(plugin.RunShortcut, out var ctrl, out var shift, out var alt, out var key))
            {
                var events = new JsonArray();
                foreach (var name in plugin.RunEvents)
                {
                    events.Add(name);
                }

                bindings.Add(new JsonObject
                {
                    ["id"] = plugin.Manifest.Id,
                    ["ctrl"] = ctrl,
                    ["shift"] = shift,
                    ["alt"] = alt,
                    ["key"] = key,
                    ["events"] = events
                });
            }
        }

        return BridgeScriptTemplate
            .Replace("__BASES__", bases.ToJsonString())
            .Replace("__BINDINGS__", bindings.ToJsonString());
    }

    // 中文注释：解析快捷键文本为修饰键 + 主键（大写），供桥脚本按 KeyboardEvent 匹配；
    // 无修饰键的裸键不注册，避免正常打字误触发。
    internal static bool TryParseShortcutParts(string shortcut, out bool ctrl, out bool shift, out bool alt, out string key)
    {
        ctrl = shift = alt = false;
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return false;
        }

        foreach (var token in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" or "MACCTRL" or "COMMAND" or "CMD":
                    ctrl = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                default:
                    key = token.ToUpperInvariant();
                    break;
            }
        }

        return key.Length > 0 && (ctrl || alt);
    }

    // 中文注释：处理页面桥消息（storage 读写/打开选项页或链接）；pluginId 必须命中已加载插件，杜绝路径穿越。
    public async Task HandleBridgeMessageAsync(CoreWebView2 sender, string messageJson, Func<Uri, Task>? openUrlInNewTab)
    {
        try
        {
            if (JsonNode.Parse(messageJson) is not JsonObject message)
            {
                return;
            }

            var type = message["type"]?.GetValue<string>() ?? string.Empty;
            if (!type.StartsWith("lfsun-plugin-", StringComparison.Ordinal))
            {
                return;
            }

            var pluginId = message["pluginId"]?.GetValue<string>() ?? string.Empty;
            var plugin = _plugins.FirstOrDefault(p =>
                string.Equals(p.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            if (plugin is null)
            {
                return;
            }

            switch (type)
            {
                case "lfsun-plugin-storage-get":
                {
                    var reply = new JsonObject
                    {
                        ["type"] = "lfsun-plugin-storage-data",
                        ["pluginId"] = plugin.Manifest.Id,
                        ["reqId"] = message["reqId"]?.GetValue<string>() ?? string.Empty,
                        ["data"] = LoadStorageObject(plugin.Manifest.Id)
                    };
                    sender.PostWebMessageAsJson(reply.ToJsonString());
                    break;
                }

                case "lfsun-plugin-storage-set":
                {
                    if (message["items"] is not JsonObject items)
                    {
                        break;
                    }

                    var (data, changes) = MergeStorage(plugin.Manifest.Id, items);
                    BroadcastStorageChanged(sender, plugin.Manifest.Id, data, changes);
                    break;
                }

                case "lfsun-plugin-storage-remove":
                {
                    if (message["keys"] is not JsonArray keys)
                    {
                        break;
                    }

                    var (data, changes) = RemoveStorageKeys(plugin.Manifest.Id, keys);
                    BroadcastStorageChanged(sender, plugin.Manifest.Id, data, changes);
                    break;
                }

                case "lfsun-plugin-open-options":
                {
                    if (openUrlInNewTab is not null && TryBuildPluginPageUri(plugin, plugin.OptionsPage, out var optionsUri))
                    {
                        await openUrlInNewTab(optionsUri);
                    }

                    break;
                }

                case "lfsun-plugin-open-url":
                {
                    if (openUrlInNewTab is null)
                    {
                        break;
                    }

                    var raw = message["url"]?.GetValue<string>() ?? string.Empty;
                    if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute) && !absolute.IsFile)
                    {
                        await openUrlInNewTab(absolute);
                    }
                    else if (TryBuildPluginPageUri(plugin, raw, out var pageUri))
                    {
                        await openUrlInNewTab(pageUri);
                    }

                    break;
                }

                // 中文注释：跨域抓取桥——插件脚本调用 chrome.net.fetch 时发出的请求。
                // 宿主用 HttpClient 发请求，不受渲染引擎同源/CORS 限制；权限 net:fetch 门控。
                case "lfsun-plugin-fetch":
                {
                    if (!plugin.Manifest.Permissions.Contains(NetFetchPermission, StringComparer.OrdinalIgnoreCase))
                    {
                        ReplyNetFetch(sender, plugin.Manifest.Id, message, ok: false, status: 403,
                            statusText: $"权限拒绝: {NetFetchPermission}", headers: null, body: string.Empty,
                            encoding: "utf8", truncated: false);
                        break;
                    }

                    var url = message["url"]?.GetValue<string>() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var fetchUri))
                    {
                        ReplyNetFetch(sender, plugin.Manifest.Id, message, ok: false, status: 400,
                            statusText: "无效的 URL", headers: null, body: string.Empty,
                            encoding: "utf8", truncated: false);
                        break;
                    }

                    var method = (message["method"]?.GetValue<string>() ?? "GET").ToUpperInvariant();
                    var body = message["body"]?.GetValue<string>();
                    var timeoutMs = Math.Clamp(message["timeoutMs"]?.GetValue<int>() ?? 30000, 1000, 120000);
                    var maxBytes = Math.Clamp(message["maxBytes"]?.GetValue<int>() ?? MaxFetchBytes, 1024, MaxFetchBytes);
                    // 中文注释：重试/代理/UA 选项：优先取插件按请求传入的值，否则回退全局默认。
                    var retry = Math.Clamp(message["retry"]?.GetValue<int>() ?? _fetchRetry, 1, 5);
                    var retryDelayMs = Math.Clamp(message["retryDelayMs"]?.GetValue<int>() ?? _fetchRetryDelayMs, 0, 5000);
                    var proxyUrl = (message["proxy"]?.GetValue<string>() ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(proxyUrl))
                    {
                        proxyUrl = _fetchProxyUrl;
                    }

                    var userAgent = (message["userAgent"]?.GetValue<string>() ?? string.Empty).Trim();
                    var rotateUA = message["rotateUA"]?.GetValue<bool>() ?? false;
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (message["headers"] is JsonObject headerObj)
                    {
                        foreach (var (key, value) in headerObj)
                        {
                            if (!string.IsNullOrWhiteSpace(key) && value is not null)
                            {
                                headers[key] = value.ToString() ?? string.Empty;
                            }
                        }
                    }

                    _ = Task.Run(() => PerformNetFetchAsync(
                        sender, plugin.Manifest.Id, message, fetchUri, method, headers, body,
                        timeoutMs, maxBytes, retry, retryDelayMs, proxyUrl, userAgent, rotateUA, _fetchUserAgents));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _log($"[插件桥] 处理消息失败: {ex.Message}");
        }
    }

    // 中文注释：执行跨域抓取：宿主 HttpClient 发请求，按 maxBytes 截断；
    // 文本原样返回(utf8)，二进制走 base64。因为请求由宿主进程发出，不受渲染引擎同源/CORS 限制。
    // 支持：重试（仅网络层异常）、代理（HttpClientHandler.WebProxy）、UA 轮换（取自配置候选池）。
    private static async Task PerformNetFetchAsync(
        CoreWebView2 sender, string pluginId, JsonObject request, Uri uri, string method,
        Dictionary<string, string> headers, string? body, int timeoutMs, int maxBytes,
        int retry, int retryDelayMs, string? proxyUrl, string? userAgent, bool rotateUA,
        IReadOnlyList<string> userAgents)
    {
        // 中文注释：代理 HttpClient 缓存，按代理地址复用，避免每次请求 new 一个 handler。
        var client = GetFetchHttpClient(proxyUrl);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= retry; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var req = new HttpRequestMessage(new HttpMethod(method), uri);

                // 中文注释：User-Agent 优先级：显式 userAgent > 轮换 UA（取配置候选池）> 默认 UA。
                var effectiveUA = userAgent;
                if (string.IsNullOrWhiteSpace(effectiveUA) && rotateUA && userAgents.Count > 0)
                {
                    var idx = Interlocked.Increment(ref _uaRotationIndex);
                    effectiveUA = userAgents[Math.Abs(idx) % userAgents.Count];
                }

                if (!string.IsNullOrWhiteSpace(effectiveUA))
                {
                    req.Headers.TryAddWithoutValidation("User-Agent", effectiveUA);
                }
                else if (!headers.TryGetValue("User-Agent", out _))
                {
                    req.Headers.TryAddWithoutValidation(
                        "User-Agent", "LfsunBrowser-Scraper/1.0 (+https://gitee.com/pc-app/lfsun-win-browser)");
                }

                foreach (var (key, value) in headers)
                {
                    if (string.Equals(key, "User-Agent", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    req.Headers.TryAddWithoutValidation(key, value);
                }

                if (!string.IsNullOrEmpty(body))
                {
                    var contentType = headers.TryGetValue("Content-Type", out var ct) ? ct : "text/plain; charset=utf-8";
                    req.Content = new StringContent(body, System.Text.Encoding.UTF8);
                    try
                    {
                        req.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
                    }
                    catch
                    {
                        // 中文注释：Content-Type 解析失败时保留 StringContent 的默认类型。
                    }
                }

                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var respHeaders = new JsonObject();
                foreach (var header in resp.Headers)
                {
                    respHeaders[header.Key] = string.Join(", ", header.Value);
                }

                if (resp.Content.Headers.ContentType is not null)
                {
                    foreach (var header in resp.Content.Headers)
                    {
                        respHeaders[header.Key] = string.Join(", ", header.Value);
                    }
                }

                var raw = await resp.Content.ReadAsByteArrayAsync(cts.Token);
                var truncated = raw.Length > maxBytes;
                var take = truncated ? maxBytes : raw.Length;
                var mediaType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var isText = TextContentTypes.Any(t => mediaType.Contains(t, StringComparison.OrdinalIgnoreCase));
                string bodyText;
                string encoding;
                if (isText)
                {
                    var charset = resp.Content.Headers.ContentType?.CharSet;
                    var detected = TryGetEncoding(charset, out var enc) ? enc : System.Text.Encoding.UTF8;
                    bodyText = detected.GetString(raw, 0, take);
                    encoding = "utf8";
                }
                else
                {
                    bodyText = Convert.ToBase64String(raw, 0, take);
                    encoding = "base64";
                }

                ReplyNetFetch(sender, pluginId, request, ok: true, status: (int)resp.StatusCode,
                    statusText: resp.ReasonPhrase ?? string.Empty, headers: respHeaders, body: bodyText,
                    encoding: encoding, truncated: truncated);
                return;
            }
            catch (Exception ex)
            {
                // 中文注释：仅网络层异常（超时/连接失败/DNS）才重试；HTTP 状态码类不在此捕获。
                lastError = ex;
                if (attempt < retry && retryDelayMs > 0)
                {
                    await Task.Delay(retryDelayMs);
                }
            }
        }

        ReplyNetFetch(sender, pluginId, request, ok: false, status: 0,
            statusText: $"抓取失败(重试 {retry} 次): {lastError?.Message}", headers: null, body: string.Empty,
            encoding: "utf8", truncated: false);
    }

    // 中文注释：按代理地址取 HttpClient；无代理用共享实例，有代理按地址缓存一个带 WebProxy 的实例。
    private static HttpClient GetFetchHttpClient(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return SharedHttpClient;
        }

        return ProxyHttpClientCache.GetOrAdd(proxyUrl!, _ =>
        {
            var handler = new HttpClientHandler();
            if (Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri))
            {
                handler.Proxy = new System.Net.WebProxy(proxyUri);
                handler.UseProxy = true;
            }

            return new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        });
    }

    // 中文注释：跨域抓取桥的代理 HttpClient 缓存（按代理地址复用）。
    private static readonly ConcurrentDictionary<string, HttpClient> ProxyHttpClientCache = new(StringComparer.OrdinalIgnoreCase);
    // 中文注释：UA 轮换计数器（线程安全自增）。
    private static int _uaRotationIndex;

    // 中文注释：把抓取结果回传给页面桥（chrome.net.fetch 的 Promise 据此 resolve）。
    private static void ReplyNetFetch(
        CoreWebView2 sender, string pluginId, JsonObject request, bool ok, int status,
        string statusText, JsonObject? headers, string body, string encoding, bool truncated)
    {
        try
        {
            var reply = new JsonObject
            {
                ["type"] = "lfsun-plugin-fetch-response",
                ["pluginId"] = pluginId,
                ["reqId"] = request["reqId"]?.GetValue<string>() ?? string.Empty,
                ["ok"] = ok,
                ["status"] = status,
                ["statusText"] = statusText,
                ["headers"] = headers ?? new JsonObject(),
                ["body"] = body,
                ["encoding"] = encoding,
                ["truncated"] = truncated
            };
            sender.PostWebMessageAsJson(reply.ToJsonString());
        }
        catch
        {
            // 中文注释：回传失败（WebView 可能已释放）忽略。
        }
    }

    // 中文注释：解析响应 Content-Type 的 charset 为 Encoding，失败回退 UTF-8。
    private static bool TryGetEncoding(string? charset, out System.Text.Encoding encoding)
    {
        encoding = System.Text.Encoding.UTF8;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return false;
        }

        var name = charset!.Trim().Trim('"');
        var semi = name.IndexOf(';');
        if (semi >= 0)
        {
            name = name[..semi].Trim();
        }

        try
        {
            encoding = System.Text.Encoding.GetEncoding(name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // 中文注释：把插件内相对路径（可带 #锚点）转换为 file:// 地址；文件必须真实存在于插件目录内。
    private static bool TryBuildPluginPageUri(PluginRuntime plugin, string relativePath, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrEmpty(plugin.DirectoryPath))
        {
            return false;
        }

        var hashIndex = relativePath.IndexOf('#');
        var filePart = hashIndex >= 0 ? relativePath[..hashIndex] : relativePath;
        var fragment = hashIndex >= 0 ? relativePath[hashIndex..] : string.Empty;
        var fullPath = Path.GetFullPath(Path.Combine(plugin.DirectoryPath, filePart));
        if (!fullPath.StartsWith(Path.GetFullPath(plugin.DirectoryPath), StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        uri = new Uri(new Uri(fullPath).AbsoluteUri + fragment);
        return true;
    }

    // 中文注释：插件 chrome.storage 持久化文件（插件目录下 .storage.json，卸载时一并清理）。
    private string GetStorageFilePath(string pluginId) => Path.Combine(_pluginsRoot, pluginId, ".storage.json");

    private JsonObject LoadStorageObject(string pluginId)
    {
        try
        {
            var path = GetStorageFilePath(pluginId);
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject data)
            {
                return data;
            }
        }
        catch (Exception ex)
        {
            _log($"[插件:{pluginId}] 读取存储失败: {ex.Message}");
        }

        return new JsonObject();
    }

    private void SaveStorageObject(string pluginId, JsonObject data)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(_pluginsRoot, pluginId));
            File.WriteAllText(GetStorageFilePath(pluginId), data.ToJsonString(ManifestWriteOptions));
        }
        catch (Exception ex)
        {
            _log($"[插件:{pluginId}] 保存存储失败: {ex.Message}");
        }
    }

    private (JsonObject Data, JsonObject Changes) MergeStorage(string pluginId, JsonObject items)
    {
        var data = LoadStorageObject(pluginId);
        var changes = new JsonObject();
        foreach (var (key, value) in items)
        {
            changes[key] = new JsonObject
            {
                ["oldValue"] = data[key]?.DeepClone(),
                ["newValue"] = value?.DeepClone()
            };
            data[key] = value?.DeepClone();
        }

        SaveStorageObject(pluginId, data);
        return (data, changes);
    }

    private (JsonObject Data, JsonObject Changes) RemoveStorageKeys(string pluginId, JsonArray keys)
    {
        var data = LoadStorageObject(pluginId);
        var changes = new JsonObject();
        foreach (var keyNode in keys)
        {
            var key = keyNode?.GetValue<string>();
            if (string.IsNullOrEmpty(key) || data[key] is null)
            {
                continue;
            }

            changes[key] = new JsonObject { ["oldValue"] = data[key]?.DeepClone() };
            data.Remove(key);
        }

        SaveStorageObject(pluginId, data);
        return (data, changes);
    }

    // 中文注释：storage 变更后广播给其他标签页（发起页已在本地应用并触发过 onChanged）。
    private void BroadcastStorageChanged(CoreWebView2? sender, string pluginId, JsonObject data, JsonObject changes)
    {
        var payload = new JsonObject
        {
            ["type"] = "lfsun-plugin-storage-changed",
            ["pluginId"] = pluginId,
            ["data"] = data.DeepClone(),
            ["changes"] = changes.DeepClone()
        }.ToJsonString();

        _bridgedWebViews.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in _bridgedWebViews)
        {
            if (!reference.TryGetTarget(out var webView) || ReferenceEquals(webView, sender))
            {
                continue;
            }

            try
            {
                webView.PostWebMessageAsJson(payload);
            }
            catch
            {
                // 中文注释：目标 WebView 可能已释放，忽略。
            }
        }
    }

    // 中文注释：快捷键/选项页解析：manifest 声明优先，缺省回退 manifest.chrome.json（转换安装保留的原始清单）；
    // 选项页文件不存在时视为无效，避免菜单出现死链接。
    private static (string RunShortcut, string OptionsPage) ResolvePluginExtras(PluginManifest manifest, string pluginDirectory)
    {
        var shortcut = manifest.RunShortcut?.Trim() ?? string.Empty;
        var optionsPage = manifest.OptionsPage?.Trim() ?? string.Empty;
        if (shortcut.Length == 0 || optionsPage.Length == 0)
        {
            var companionPath = Path.Combine(pluginDirectory, "manifest.chrome.json");
            if (File.Exists(companionPath))
            {
                try
                {
                    if (JsonNode.Parse(File.ReadAllText(companionPath)) is JsonObject chromeRoot)
                    {
                        var (chromeShortcut, chromeOptions) = ParseChromeExtras(chromeRoot);
                        if (shortcut.Length == 0)
                        {
                            shortcut = chromeShortcut;
                        }

                        if (optionsPage.Length == 0)
                        {
                            optionsPage = chromeOptions;
                        }
                    }
                }
                catch
                {
                    // 中文注释：原始清单损坏时按未声明处理。
                }
            }
        }

        if (optionsPage.Length > 0 && !File.Exists(Path.Combine(pluginDirectory, optionsPage)))
        {
            optionsPage = string.Empty;
        }

        return (shortcut, optionsPage);
    }

    // 中文注释：从 Chrome 清单解析快捷键（commands.suggested_key.default，跳过 _execute_action）与选项页。
    private static (string RunShortcut, string OptionsPage) ParseChromeExtras(JsonObject chromeRoot)
    {
        var shortcut = string.Empty;
        var optionsPage = string.Empty;
        try
        {
            if (chromeRoot["commands"] is JsonObject commands)
            {
                foreach (var (commandName, commandNode) in commands)
                {
                    if (string.Equals(commandName, "_execute_action", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(commandName, "_execute_browser_action", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var suggested = commandNode?["suggested_key"]?["default"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(suggested))
                    {
                        shortcut = suggested.Trim();
                        break;
                    }
                }
            }

            optionsPage = chromeRoot["options_ui"]?["page"]?.GetValue<string>()?.Trim()
                          ?? chromeRoot["options_page"]?.GetValue<string>()?.Trim()
                          ?? string.Empty;
        }
        catch
        {
            // 中文注释：清单字段类型异常时按未声明处理。
        }

        return (shortcut, optionsPage);
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

            // 中文注释：从内容脚本源码探测手动运行触发事件，显式写入转换清单便于查看/修改。
            var runEvent = DetectRunEventFromFiles(sourceDirectory, contentScripts);
            // 中文注释：同步转换快捷键（commands）与选项页（options_ui/options_page）声明。
            var (runShortcut, optionsPage) = ParseChromeExtras(root);
            if (optionsPage.Length > 0 && !File.Exists(Path.Combine(sourceDirectory, optionsPage)))
            {
                optionsPage = string.Empty;
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
            if (!string.IsNullOrEmpty(runEvent))
            {
                converted["runEvent"] = runEvent;
            }

            if (!string.IsNullOrEmpty(runShortcut))
            {
                converted["runShortcut"] = runShortcut;
            }

            if (!string.IsNullOrEmpty(optionsPage))
            {
                converted["optionsPage"] = optionsPage;
            }

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

    // 中文注释：扫描待安装扩展的内容脚本文件，探测手动运行触发事件（首个命中即用）。
    private static string DetectRunEventFromFiles(string sourceDirectory, JsonArray contentScripts)
    {
        foreach (var groupNode in contentScripts)
        {
            if (groupNode is not JsonObject group || group["scripts"] is not JsonArray scripts)
            {
                continue;
            }

            foreach (var script in scripts)
            {
                var scriptFile = script?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(scriptFile))
                {
                    continue;
                }

                var path = Path.Combine(sourceDirectory, scriptFile);
                if (!File.Exists(path))
                {
                    continue;
                }

                var match = RunEventListenerRegex.Match(File.ReadAllText(path));
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return string.Empty;
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
