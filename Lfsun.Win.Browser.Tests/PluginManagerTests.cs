using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Plugins;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class PluginManagerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lp-plugin-tests-{Guid.NewGuid():N}");

    private PluginManager CreateManager() =>
        new(Path.Combine(_tempRoot, "plugins"), new BrowserConfig(), _ => { });

    // 中文注释：回归测试——Chrome 扩展转换安装曾因 ToJsonString 传入无 TypeInfoResolver 的
    // JsonSerializerOptions 抛出 “must specify a TypeInfoResolver setting” 异常。
    [Fact]
    public void InstallPluginFromDirectory_ChromeExtension_ConvertsAndInstalls()
    {
        var sourceDir = Path.Combine(_tempRoot, "chrome-ext");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "manifest.json"), """
            {
              "manifest_version": 3,
              "name": "Demo Ext",
              "description": "demo",
              "content_scripts": [
                { "matches": ["https://example.com/*"], "js": ["content.js"] }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(sourceDir, "content.js"), "console.log('hi');");

        var manager = CreateManager();
        var ok = manager.InstallPluginFromDirectory(sourceDir, out var error);

        Assert.True(ok, $"安装应成功，实际错误：{error}");
        var installedManifest = Path.Combine(manager.PluginsRoot, "demo-ext", "manifest.json");
        Assert.True(File.Exists(installedManifest));
        Assert.Contains("content:inject", File.ReadAllText(installedManifest));
    }

    [Fact]
    public void SetPluginEnabled_TogglesManifestWithoutError()
    {
        var manager = CreateManager();
        var pluginDir = Path.Combine(manager.PluginsRoot, "sample");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), """
            { "id": "sample", "name": "Sample", "enabled": true, "permissions": ["content:dom_read"], "scripts": [] }
            """);

        var ok = manager.SetPluginEnabled("sample", false, out var error);

        Assert.True(ok, $"切换应成功，实际错误：{error}");
        Assert.Contains("\"enabled\": false", File.ReadAllText(Path.Combine(pluginDir, "manifest.json")));
    }

    // 中文注释：回归测试——插件目录含只读文件（如 git 对象）时，卸载曾报“拒绝访问”并留下残留目录。
    [Fact]
    public void UninstallPlugin_RemovesDirectoryWithReadOnlyFiles()
    {
        var manager = CreateManager();
        var pluginDir = Path.Combine(manager.PluginsRoot, "readonly-plugin");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), """
            { "id": "readonly-plugin", "name": "RO", "enabled": true, "permissions": ["content:dom_read"], "scripts": [] }
            """);
        var readOnlyFile = Path.Combine(pluginDir, "locked.pack");
        File.WriteAllText(readOnlyFile, "data");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var ok = manager.UninstallPlugin("readonly-plugin", out var error);

        Assert.True(ok, $"卸载应成功，实际错误：{error}");
        Assert.False(Directory.Exists(pluginDir));
    }

    // 中文注释：回归测试——卸载失败留下的无 manifest 残留目录曾导致重新安装报“已存在”。
    [Fact]
    public void InstallPluginFromDirectory_ClearsBrokenLeftoverAndSkipsDotGit()
    {
        var manager = CreateManager();
        // 残留目录：只剩 .git，无 manifest.json。
        var leftoverDir = Path.Combine(manager.PluginsRoot, "demo-ext", ".git");
        Directory.CreateDirectory(leftoverDir);
        File.WriteAllText(Path.Combine(leftoverDir, "stale.pack"), "stale");
        File.SetAttributes(Path.Combine(leftoverDir, "stale.pack"), FileAttributes.ReadOnly);

        // 源目录：带 .git 的 Chrome 扩展。
        var sourceDir = Path.Combine(_tempRoot, "chrome-ext-git");
        Directory.CreateDirectory(Path.Combine(sourceDir, ".git"));
        File.WriteAllText(Path.Combine(sourceDir, ".git", "HEAD"), "ref");
        File.WriteAllText(Path.Combine(sourceDir, "manifest.json"), """
            {
              "manifest_version": 3,
              "name": "Demo Ext",
              "content_scripts": [
                { "matches": ["https://example.com/*"], "js": ["content.js"] }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(sourceDir, "content.js"), "console.log('hi');");

        var ok = manager.InstallPluginFromDirectory(sourceDir, out var error);

        Assert.True(ok, $"安装应成功，实际错误：{error}");
        var installedDir = Path.Combine(manager.PluginsRoot, "demo-ext");
        Assert.True(File.Exists(Path.Combine(installedDir, "manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(installedDir, ".git")), ".git 目录不应被复制到插件目录");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                // 中文注释：测试会创建只读文件，先清除属性再删除。
                foreach (var file in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响测试结果。
        }
    }
}
