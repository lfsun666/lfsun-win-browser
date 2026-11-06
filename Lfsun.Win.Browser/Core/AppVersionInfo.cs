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
            "0.7.1",
            "2026-11-06",
            "修复双屏分屏面板 SplitContainer 启动边界异常与多窗口布局加固",
            [
                "【启动崩溃修复】修复安装启动时由于 SplitBrowserPanel 默认宽度较小导致 SplitContainer.Panel2MinSize 抛出 'SplitterDistance 必须在 Panel1MinSize 和 Width - Panel2MinSize 之间' 的致命启动异常",
                "【分屏动态适配】重构 SplitBrowserPanel 尺寸自适应机制，初始最小尺寸设为 0，挂载并在窗口实际展开时动态平滑计算两侧最小宽度与均分位置",
                "【全窗体容错加固】全面排查并加固书签管理窗体、会话中心及分屏容器的 SplitContainer 边界校验与异常防线，杜绝任何 DPI 突变或窗口初次加载时的崩溃隐患"
            ]
        ),
        new VersionReleaseEntry(
            "0.7.0",
            "2026-11-06",
            "全局快捷命令面板扩展、下载实时感知提示与垂直标签悬停卡片升级",
            [
                "【全局命令面板】地址栏全局命令 (AddressCommandCatalog) 新增 8 组高频命令：:reader 沉浸阅读、:split 双屏分屏、:vtabs 垂直标签、:pip 画中画、:freemem 休眠后台释放内存、:sidepanel 侧边工具箱、:qrcode 跨端二维码及 :screenshot 网页截图",
                "【实时下载感知】深度打通 WebView2 核心下载引擎，下载开始、下载完成及下载异常自动弹出原生 Fluent 气泡提示，并即时同步状态栏下载计数",
                "【垂直标签悬停卡片】垂直标签栏鼠标悬停卡片全新升级，动态呈现网页完整标题、固定状态、实时音频徽标 (🔊/🔇)、真实 URL 及活跃/休眠状态",
                "【截图快捷封装】统一优化网页整页快照截图链路与快捷键处理，支持 Ctrl+Alt+S 与指令统一调用",
                "【事件安全保障】下载事件订阅安全解耦与跨线程 UI Dispatch，保障长时间大文件下载的系统平稳运行"
            ]
        ),
        new VersionReleaseEntry(
            "0.6.0",
            "2026-11-06",
            "现代化左侧垂直标签栏、后台标签休眠与批量生产力工具箱",
            [
                "【垂直标签栏】新增现代化左侧垂直标签栏 (VerticalTabStrip)，支持完整(220px)与收缩图标(48px)双模式自由切换，自适应暗色/浅色主题",
                "【垂直标签交互】垂直标签栏支持固定标签置顶、音频动态状态徽标 (🔊/🔇)、悬停独立关闭、鼠标中键/双击快捷关闭及新建标签",
                "【快捷键集成】新增标题栏「📑 垂直标签」一键切换按钮与全局 Ctrl+Shift+V 快捷键，自由在传统水平标签与现代垂直标签间切换",
                "【后台内存释放】侧边栏工具箱新增「🧹 释放后台内存」一键休眠所有后台空闲标签，显著降低浏览器内存占用",
                "【全局静音控制】侧边栏工具箱新增「🔇 全局静音切换」，一键全局静音/解除所有标签声音",
                "【标签批量导出】侧边栏工具箱新增一键复制全部标签 Markdown 列表及纯网址到剪贴板，方便跨工具知识沉淀",
                "【智能搜索前缀】地址栏输入支持 '? 关键词' 或 '？ 关键词' 显式强制使用默认搜索引擎检索，避免与网址混淆"
            ]
        ),
        new VersionReleaseEntry(
            "0.5.0",
            "2026-11-06",
            "网页双屏分屏并行浏览、沉浸式阅读模式、画中画与速拨主页全面升级",
            [
                "【双屏分屏】新增网页双屏分屏并行浏览模式 (SplitBrowserPanel)，支持左右同屏浏览、中缝自由拖拽调宽、左右页面一键对调及 Alt+D 快捷切换",
                "【沉浸阅读】新增纯净沉浸式阅读模式 (Reader Mode)，智能提取正文与标题，支持白天/羊皮纸/深色三套主题、多级字号缩放及 Alt+R / Esc 快捷启闭",
                "【视频画中画】新增视频独立置顶画中画浮窗支持 (Picture-in-Picture)，一键将网页内播放视频剥离至系统画中画浮窗",
                "【主页进阶】新标签页速拨面板新增搜索引擎即时切换下拉器（必应/百度/谷歌/GitHub/掘金）与开发者每日灵感寄语轮播",
                "【地址栏缩放】地址栏右侧新增智能缩放指示芯片，当页面缩放偏离 100% 时自动展示，点击一键平滑复原为标准 100% (Ctrl+0)"
            ]
        ),
        new VersionReleaseEntry(
            "0.4.0",
            "2026-11-06",
            "多功能侧边栏、离线二维码、标签音频管理与全局交互优化",
            [
                "【侧边栏】新增右侧多功能抽屉栏 (BrowserSidePanel)，支持页面工具箱、快速书签、快速历史、快速下载及随手便签五大功能面板无缝切换与联动",
                "【网页快照】侧边栏与标签右键新增网页整页截图功能，支持一键复制高清图像到剪贴板或另存为 PNG 文件",
                "【离线二维码】基于纯 C# 算法内置零依赖 ISO/IEC 18004 QR Code Model 2 引擎，支持跨端手机扫码继续浏览，无需外网服务",
                "【阅读护眼】侧边栏工具箱内置 4 种高频视觉滤镜（原样默认、柔和暖色护眼、夜间反色、高对比度），一键切换页面视觉",
                "【随手便签】内置本地即时持久化便签记事本，支持快捷一键插入当前页 Markdown 链接、字数实时统计与复制",
                "【媒体与音频】深度集成 WebView2 音频状态感知，标签页智能动态显示扬声器徽标 (🔊/🔇)，支持 Ctrl+M 单标签静音/解禁",
                "【站点安全】点击地址栏安全盾牌/挂锁徽标弹出 Fluent 站点信息卡片，展示 HTTPS 加密、拦截计数、加载耗时，并提供清除 Cookie、强刷及查看源码",
                "【交互增强】支持双击标签页关闭、鼠标中键关闭标签、双击标签栏空白处新建标签页；地址栏右键支持一键生成二维码及以 Markdown 复制；新增 Alt+S 侧栏与 Ctrl+Alt+S 截图快捷键"
            ]
        ),
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
    /// 获取最近 N 个版本的更新历史（默认展示最近 10 个版本）。
    /// </summary>
    public static IEnumerable<VersionReleaseEntry> GetRecentReleases(int count = 10)
    {
        return ReleaseHistory.Take(Math.Max(1, count));
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
