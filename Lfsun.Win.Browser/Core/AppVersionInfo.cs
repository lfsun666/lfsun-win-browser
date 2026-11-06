using System.Reflection;

namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 应用程序版本与更新说明统一管理服务。
/// 动态获取程序集版本，记录每一次版本迭代的核心特性与修复，
/// 供应用内关于窗口、设置面板及发布脚本读取和展示。
/// </summary>
public static class AppVersionInfo
{
    public sealed record VersionReleaseEntry(
        string Version,
        string ReleaseDate,
        string Title,
        IReadOnlyList<string> Highlights
    );

    /// <summary>
    /// 当前版本号字符串（如 "0.3.0"）。
    /// </summary>
    public static string CurrentVersion { get; } = ResolveCurrentVersion();

    /// <summary>
    /// 产品全名。
    /// </summary>
    public const string ProductName = "LfsunBrowser";

    /// <summary>
    /// 产品定位标语。
    /// </summary>
    public const string Slogan = "面向开发者的现代化 Windows 浏览器";

    /// <summary>
    /// 项目主页。
    /// </summary>
    public const string RepositoryUrl = "https://gitee.com/pc-app/lfsun-win-browser";

    /// <summary>
    /// 完整版本历史列表（按版本降序排列）。
    /// </summary>
    public static IReadOnlyList<VersionReleaseEntry> ReleaseHistory { get; } =
    [
        new VersionReleaseEntry(
            "0.3.0",
            "2026-11-06",
            "全局 UI/布局重构与自动化发布交付系统",
            [
                "【UI/布局】顶部栏全新 Fluent 扁平设计，优化标题栏(36px)、工具栏(44px)与书签栏(34px)高度比例，消除无效背景色块堆叠",
                "【UI/布局】标题栏标签页卡片化精细渲染，支持悬停高亮、平滑圆角、底部强调色状态线及独立关闭按钮命中反馈",
                "【UI/布局】窗口控制按钮（最小化/最大化/关闭）重构，支持 Segoe Fluent/MDL2 原生矢量字形，关闭按钮提供标准 Windows 红悬停",
                "【UI/布局】地址栏流线型圆角聚焦环与发光强调效果，等宽字体清晰排版，安全站点徽标视觉融合",
                "【UI/布局】状态栏全面芯片胶囊化，耗时、缩放、IP、内存与广告拦截计数采用圆角微芯片与状态语义着色",
                "【UI/暗色】修正书签设置、下载管理、历史记录、会话管理等全部二级子窗口的硬编码颜色，完美适配暗色/浅色/系统主题",
                "【新标签页】速拨面板现代化玻璃拟态升级，新增快捷功能直达芯片，提升视觉精致度",
                "【发布自动化】新增 scripts/release.ps1 一键自动化发布工具：支持语义化版本号自动自增、代码质量门禁测试、便携包与安装包自动产出到 release/ 目录",
                "【版本记录】新增应用内「关于与更新说明」现代化弹窗，并在设置面板中内置「关于」选项卡，随时查阅完整版本履历与运行环境"
            ]
        ),
        new VersionReleaseEntry(
            "0.2.0",
            "2026-08-07",
            "可视化网页爬虫插件与跨域抓取能力",
            [
                "【插件系统】新增可视化网页爬虫插件（web-scraper），无需编程即可点击 DOM 提取列表与字段数据",
                "【抓取引擎】新增 chrome.net.fetch 跨域抓取桥接口，由浏览器宿主进程发包，彻底绕过 CORS 限制",
                "【稳定性】重写爬虫选点逻辑，采用 DOM 层级自动识别列表容器，解决 nth-child 导致的单条匹配限制",
                "【核心安全】修复 8 项内存泄漏与沙箱权限校验边界，增强长时间运行的资源回收机制"
            ]
        ),
        new VersionReleaseEntry(
            "0.1.0",
            "2026-08-04",
            "LfsunBrowser 初始发布",
            [
                "【核心浏览】基于 .NET 8 + WinForms + WebView2，多标签智能管理与空闲标签页休眠策略",
                "【开发者向】一键 DevTools、Ctrl+U 查看源码、等宽地址栏、页面导航耗时芯片（⏱ ms）",
                "【数据管理】树形书签管理、下载管理、历史记录持久化、命名会话与启动会话恢复",
                "【安全隐私】无痕多开窗口、本机用户加密的凭据自动填充、广告过滤拦截服务",
                "【个性定制】明暗主题与系统跟随、Mica 质感、多种强调色选择、自定义搜索引擎与快捷键一览"
            ]
        )
    ];

    private static string ResolveCurrentVersion()
    {
        try
        {
            var assembly = typeof(AppVersionInfo).Assembly;
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var plusIndex = informationalVersion.IndexOf('+');
                return plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
            }

            var version = assembly.GetName().Version;
            if (version is not null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch
        {
            // Fallback
        }

        return "0.3.0";
    }

    /// <summary>
    /// 获取当前最新版本的更新说明摘要。
    /// </summary>
    public static VersionReleaseEntry? GetLatestRelease()
    {
        return ReleaseHistory.FirstOrDefault();
    }

    /// <summary>
    /// 生成 Markdown 格式的更新说明文本。
    /// </summary>
    public static string BuildChangelogMarkdown()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# 更新日志 (Changelog)");
        builder.AppendLine();
        foreach (var release in ReleaseHistory)
        {
            builder.AppendLine($"## [{release.Version}] - {release.ReleaseDate}");
            builder.AppendLine($"### {release.Title}");
            builder.AppendLine();
            foreach (var item in release.Highlights)
            {
                builder.AppendLine($"- {item}");
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
