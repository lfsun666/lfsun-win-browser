namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 网页加载性能与诊断分析数据模型。
/// 基于 W3C Navigation Timing 与 Resource Timing API 采集。
/// </summary>
public sealed record PagePerformanceReport
{
    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Protocol { get; init; } = "HTTP/2";
    public double TotalDurationMs { get; init; }
    public double DnsDurationMs { get; init; }
    public double TcpDurationMs { get; init; }
    public double TtfbMs { get; init; }
    public double DomReadyMs { get; init; }
    public double LoadCompleteMs { get; init; }
    public long TransferSizeBytes { get; init; }
    public long DecodedBodySizeBytes { get; init; }
    public int ResourceCount { get; init; }
    public int ScriptCount { get; init; }
    public int StyleCount { get; init; }
    public int ImageCount { get; init; }
    public int DomElementCount { get; init; }

    public string ToMarkdown()
    {
        var transferFormatted = FormatBytes(TransferSizeBytes);
        var decodedFormatted = FormatBytes(DecodedBodySizeBytes);
        return $"""
            ### ⚡ 网页性能与诊断报告
            - **页面标题**: {Title}
            - **网址**: {Url}
            - **传输协议**: `{Protocol}`
            
            #### ⏱ 耗时阶段分析 (Navigation Timing)
            - 🌐 **DNS 查询**: {DnsDurationMs:F0} ms
            - 🤝 **TCP/TLS 握手**: {TcpDurationMs:F0} ms
            - 🚀 **首包到达 (TTFB)**: {TtfbMs:F0} ms
            - 📄 **DOM Ready (DOMContentLoaded)**: {DomReadyMs:F0} ms
            - 🏁 **页面完全加载 (Load)**: {LoadCompleteMs:F0} ms
            - ⏱ **导航总耗时**: {TotalDurationMs:F0} ms
            
            #### 📊 资源与体积分布 (Resource Timing)
            - **网络传输体积**: {transferFormatted}
            - **解压后体积**: {decodedFormatted}
            - **网络请求总数**: {ResourceCount} 项
            - **JavaScript 脚本**: {ScriptCount} 项
            - **CSS 样式表**: {StyleCount} 项
            - **图片与媒体**: {ImageCount} 项
            - **DOM 节点总数**: {DomElementCount} 个
            """;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
