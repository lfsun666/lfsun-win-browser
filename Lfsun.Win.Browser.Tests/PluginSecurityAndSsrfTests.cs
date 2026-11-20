using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Plugins;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

/// <summary>
/// 中文注释：安全加固回归测试——SSRF 防护、插件 id/脚本路径消毒、权限归一化、
/// 以及“用户收紧白名单后不被静默回填 net:fetch”的配置规则。
/// </summary>
public sealed class PluginSecurityAndSsrfTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lp-sec-tests-{Guid.NewGuid():N}");

    private PluginManager CreateManager() =>
        new(Path.Combine(_tempRoot, "plugins"), new BrowserConfig(), _ => { });

    // ---------- SSRF 防护：地址段判定 ----------

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.5")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("ff02::1")]
    public void SsrfGuard_BlocksPrivateOrReservedAddresses(string literal)
    {
        Assert.True(
            IPAddress.TryParse(literal, out var address),
            $"测试数据应为合法 IP: {literal}");
        Assert.True(
            FetchSsrfGuard.IsPrivateOrReservedAddress(address),
            $"内网/保留地址应被拦截: {literal}");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    public void SsrfGuard_AllowsPublicAddresses(string literal)
    {
        Assert.True(IPAddress.TryParse(literal, out var address));
        Assert.False(FetchSsrfGuard.IsPrivateOrReservedAddress(address), $"公网地址不应被拦截: {literal}");
    }

    [Fact]
    public async Task SsrfGuard_IsBlockedAsync_RejectsLoopbackLocalhostAndNonHttpSchemes()
    {
        // 中文注释：字面量 IP 与 localhost 走同步分支，不依赖 DNS，单测稳定可离线运行。
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("http://127.0.0.1:8090/")));
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("http://[::1]:8090/")));
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("http://localhost:5173/")));
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("http://192.168.1.10/admin")));
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("file:///C:/Windows/win.ini")));
        Assert.True(await FetchSsrfGuard.IsBlockedAsync(new Uri("about:blank")));
        Assert.False(await FetchSsrfGuard.IsBlockedAsync(new Uri("https://8.8.8.8/")));
    }

    // ---------- 插件 id / 脚本路径消毒 ----------

    [Theory]
    [InlineData("night-reader")]
    [InlineData("web.scraper")]
    [InlineData("no-animations")]
    [InlineData("a")]
    [InlineData("Importer_2024")]
    [InlineData("一键填充表单")]
    [InlineData("图片导出工具-v2")]
    public void PluginId_AcceptsSafeIds(string id)
    {
        Assert.True(PluginManager.IsValidPluginId(id));
    }

    [Theory]
    [InlineData("..evil")]
    [InlineData("a/../b")]
    [InlineData("C:evil")]
    [InlineData(".hidden")]
    [InlineData("a..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("id::x")]
    public void PluginId_RejectsTraversalOrUnsafeIds(string id)
    {
        Assert.False(PluginManager.IsValidPluginId(id), $"应拒绝不安全 id: '{id}'");
    }

    [Fact]
    public async Task InstallPluginFromDirectory_RejectsTraversalIdAndWritesNothingOutside()
    {
        var sourceDir = Path.Combine(_tempRoot, "evil-src");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "manifest.json"),
            """{ "id": "..\\..\\evil", "name": "Evil", "enabled": true, "permissions": ["content:dom_read"], "scripts": ["s.js"] }""");
        File.WriteAllText(Path.Combine(sourceDir, "s.js"), "console.log(1);");

        var manager = CreateManager();
        var ok = manager.InstallPluginFromDirectory(sourceDir, out var error);

        Assert.False(ok, "穿越型 id 必须被拒绝安装");
        Assert.Contains("非法字符", error);
        // 中文注释：确认没有在 plugins 目录之外留下任何写入痕迹（parent 下的非常规目录不存在 evil）。
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "plugins", "evil")));
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "evil")));
    }

    [Fact]
    public void Reload_SkipsScriptPathEscapingPluginDirectory()
    {
        var manager = CreateManager();
        var pluginDir = Path.Combine(manager.PluginsRoot, "escape-demo");
        Directory.CreateDirectory(pluginDir);
        // 中文注释：插件目录外的诱饵文件——若路径消毒失效，它会被读入并注入页面。
        var outsideDir = Path.Combine(_tempRoot, "outside");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.js"), "window.__leaked = 'pwned';");
        File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), """
            {
              "id": "escape-demo",
              "name": "Escape",
              "enabled": true,
              "permissions": ["content:dom_read", "content:inject"],
              "scripts": ["../../outside/secret.js"]
            }
            """);

        manager.Reload();

        Assert.Equal(0, manager.PluginCount);
    }

    [Fact]
    public void Reload_LoadsInDirectoryNestedScriptNormally()
    {
        var manager = CreateManager();
        var pluginDir = Path.Combine(manager.PluginsRoot, "nested-ok");
        Directory.CreateDirectory(Path.Combine(pluginDir, "sub"));
        File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), """
            {
              "id": "nested-ok",
              "name": "Nested",
              "enabled": true,
              "permissions": ["content:dom_read"],
              "scripts": ["sub/inner.js"]
            }
            """);
        File.WriteAllText(Path.Combine(pluginDir, "sub", "inner.js"), "console.log('ok');");

        manager.Reload();

        Assert.Equal(1, manager.PluginCount);
    }

    [Fact]
    public void ResolvePluginScriptPath_ClassifiesOkMissingAndEscaped()
    {
        var root = Path.Combine(_tempRoot, "resolve-demo");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ok.js"), "// ok");

        Assert.Equal(PluginManager.PluginScriptPathStatus.Ok,
            PluginManager.ResolvePluginScriptPath(root, "ok.js", out _));
        Assert.Equal(PluginManager.PluginScriptPathStatus.Missing,
            PluginManager.ResolvePluginScriptPath(root, "absent.js", out _));
        Assert.Equal(PluginManager.PluginScriptPathStatus.Escaped,
            PluginManager.ResolvePluginScriptPath(root, "../outside.js", out _));
        Assert.Equal(PluginManager.PluginScriptPathStatus.Escaped,
            PluginManager.ResolvePluginScriptPath(root, "c:\\windows\\system32\\x.js", out _));
    }

    // ---------- 权限归一化与运行时校验口径 ----------

    [Fact]
    public void PluginRuntime_HasPermission_TrimsDeclaredPermissions()
    {
        var manifest = new PluginManifest
        {
            Id = "trim-demo",
            Enabled = true,
            Permissions = [" net:fetch ", "content:dom_read"],
        };
        var runtime = new PluginRuntime(manifest, new[] { "console.log(1);" });

        // 中文注释：回归——运行时 net:fetch 校验曾用未 trim 的 Manifest.Permissions，
        // 声明带空格的插件会“加载成功但 fetch 全部 403”。
        Assert.True(runtime.HasPermission(PluginManager.NetFetchPermission));
        Assert.True(runtime.HasPermission("Content:DOM_Read"));
        Assert.False(runtime.HasPermission("content:inject"));
    }

    // ---------- 配置白名单：用户收紧意图不被回填 ----------

    [Fact]
    public void Normalize_RespectsUserCuratedWhitelistWithoutNetFetch()
    {
        // 中文注释：回归——旧版 Normalize 会把 net:fetch 强制追加回任何非空白名单，
        // 用户主动删掉 net:fetch 收紧安全策略的意图在重启后失效。
        // 注：与旧版默认值完全相同的组合仍会按迁移规则补齐（见下一条测试）。
        var config = new BrowserConfig
        {
            AllowedPluginPermissions = ["content:dom_read", "content:dom_write", "content:inject", "net:capture"],
        };

        var normalized = BrowserConfig.Normalize(config);

        Assert.DoesNotContain(
            BrowserConfig.DefaultNetFetchPermission,
            normalized.AllowedPluginPermissions,
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(4, normalized.AllowedPluginPermissions.Length);
        Assert.Contains("net:capture", normalized.AllowedPluginPermissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_StillUpgradesExactLegacyDefaultWhitelists()
    {
        // 中文注释：旧版默认白名单（仅 dom_read/dom_write 或三件套）应继续自动升级补齐 inject/net:fetch。
        var legacy2 = new BrowserConfig
        {
            AllowedPluginPermissions = ["content:dom_read", "content:dom_write"],
        };
        var normalized2 = BrowserConfig.Normalize(legacy2);
        Assert.Contains(BrowserConfig.DefaultNetFetchPermission, normalized2.AllowedPluginPermissions);

        var legacy3 = new BrowserConfig
        {
            AllowedPluginPermissions = ["content:dom_read", "content:dom_write", "content:inject"],
        };
        var normalized3 = BrowserConfig.Normalize(legacy3);
        Assert.Contains(BrowserConfig.DefaultNetFetchPermission, normalized3.AllowedPluginPermissions);
    }

    [Fact]
    public void Normalize_EmptyWhitelistMeansAllPermissionsOff()
    {
        var config = new BrowserConfig
        {
            AllowedPluginPermissions = [],
        };

        var normalized = BrowserConfig.Normalize(config);

        // 中文注释：空白名单视为用户主动全部关闭，不得回填任何权限（保持旧行为）。
        Assert.Empty(normalized.AllowedPluginPermissions);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // 中文注释：临时目录清理失败不影响测试结果。
        }
    }
}
