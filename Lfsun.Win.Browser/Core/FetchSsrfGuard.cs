using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 中文注释：跨域抓取桥（chrome.net.fetch）的 SSRF 防护。
/// 插件让宿主用 HttpClient 发任意请求，等于把渲染进程的同源限制搬到宿主进程；
/// 若不设防，被注入/被诱导的页面可借该通道访问本机服务、内网设备与云元数据端点。
/// 这里按"默认拒绝内网"原则拦截：非 http/https 协议、环回、私网、链路本地、保留段地址；
/// 域名形式的目标会先做 DNS 解析再校验（防公网域名解析进内网的 DNS 重绑定场景）。
/// 需要抓取本机/内网（如本地演示站、局域网设备）时，可在配置里关闭 fetchSsrfGuardEnabled。
/// </summary>
public static class FetchSsrfGuard
{
    // 中文注释：DNS 解析结果按主机名缓存；仅缓存"被拦截"的结论，放行的域名每次仍解析，
    // 避免攻击者先用公网地址通过校验、随后把 DNS 切到内网地址（缓存绕过）。
    private static readonly ConcurrentDictionary<string, byte> BlockedHostCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 中文注释：判断抓取目标是否应被 SSRF 防护拦截。内部使用异步 DNS 解析（域名形式），可安全 await。
    /// </summary>
    public static async Task<bool> IsBlockedAsync(Uri uri)
    {
        if (uri is null)
        {
            return true;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        // 中文注释：字面量 IP 直接判段；IPv4/IPv6 都处理（Uri.Host 已去掉 IPv6 方括号）。
        if (IPAddress.TryParse(host, out var literal))
        {
            return IsPrivateOrReservedAddress(literal);
        }

        if (BlockedHostCache.ContainsKey(host))
        {
            return true;
        }

        // 中文注释：localhost 及常见内网主机名直接拦截，避免依赖本机 hosts 解析结果。
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            BlockedHostCache.TryAdd(host, 1);
            return true;
        }

        // 中文注释：域名先解析再校验；解析失败放行（请求本身也会失败，无需在此拦截）。
        IPAddress[] resolved;
        try
        {
            resolved = await Dns.GetHostAddressesAsync(host);
        }
        catch
        {
            return false;
        }

        if (resolved.Length == 0)
        {
            return false;
        }

        if (resolved.Any(IsPrivateOrReservedAddress))
        {
            BlockedHostCache.TryAdd(host, 1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 中文注释：仅对字面量 IP 做段判断（同步版本，供单元测试与快速预检使用）。
    /// </summary>
    public static bool IsPrivateOrReservedAddress(IPAddress address)
    {
        if (address is null)
        {
            return true;
        }

        // 中文注释：IPv4 映射的 IPv6 地址（::ffff:a.b.c.d）按内嵌 IPv4 再判一次。
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateOrReservedAddress(new IPAddress(address.GetAddressBytes()[^4..]));
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return IsPrivateOrReservedIPv4(bytes);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsPrivateOrReservedIPv6(address);
        }

        return false;
    }

    private static bool IsPrivateOrReservedIPv4(byte[] b)
    {
        // 0.0.0.0/8（未指定）、10/8（私网A）、127/8（环回）。
        if (b[0] == 0 || b[0] == 10 || b[0] == 127)
        {
            return true;
        }

        // 100.64/10（运营商 CGN 共享地址段）。
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
        {
            return true;
        }

        // 169.254/16（链路本地，含云元数据 169.254.169.254）。
        if (b[0] == 169 && b[1] == 254)
        {
            return true;
        }

        // 172.16/12（私网B）。
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        {
            return true;
        }

        // 192.168/16（私网C）。
        if (b[0] == 192 && b[1] == 168)
        {
            return true;
        }

        // 192.0.0/24、192.0.2/24、198.18/15（基准测试段）、198.51.100/24、203.0.113/24（文档示例段）。
        if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2))
        {
            return true;
        }

        if (b[0] == 198 && (b[1] == 18 || b[1] == 19))
        {
            return true;
        }

        if (b[0] == 198 && b[1] == 51 && b[2] == 100)
        {
            return true;
        }

        if (b[0] == 203 && b[1] == 0 && b[2] == 113)
        {
            return true;
        }

        // 224/4（组播）、240/4（保留，含 255.255.255.255 广播）。
        if (b[0] >= 224)
        {
            return true;
        }

        return false;
    }

    private static bool IsPrivateOrReservedIPv6(IPAddress address)
    {
        // :: 与 ::1（环回）。
        if (address.IsIPv6LinkLocal || address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        // fc00::/7（唯一本地地址）与 fe80::/10（链路本地，IsIPv6LinkLocal 已覆盖大半）。
        if ((bytes[0] & 0xfe) == 0xfc)
        {
            return true;
        }

        if ((bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80))
        {
            return true;
        }

        // ff00::/8（组播）。
        if (bytes[0] == 0xff)
        {
            return true;
        }

        // 2001:db8::/32（文档示例段）。
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
        {
            return true;
        }

        return false;
    }
}
