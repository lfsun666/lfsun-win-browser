using System.Text.Json;

namespace Lfsun.Win.Browser.Core;

public sealed class PublicIpInfoService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<string> GetDisplayTextAsync()
    {
        // Try providers in order; each failure falls through to the next.
        var fromIpWhoIs = await TryGetFromIpWhoIsAsync();
        if (fromIpWhoIs is not null)
        {
            return fromIpWhoIs;
        }

        var fromIpApi = await TryGetFromIpApiAsync();
        if (fromIpApi is not null)
        {
            return fromIpApi;
        }

        var fromIpify = await TryGetFromIpifyAsync();
        if (fromIpify is not null)
        {
            return fromIpify;
        }

        return "IP: 不可用";
    }

    private static async Task<string?> TryGetFromIpWhoIsAsync()
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://ipwho.is/");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<IpWhoIsResponse>(json, JsonFileStore.ReadOptions);
            if (payload is null || !payload.Success || string.IsNullOrWhiteSpace(payload.Ip))
            {
                return null;
            }

            return FormatDisplay(payload.Ip, payload.Country, payload.Region, payload.City);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"ipwho.is 查询失败: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> TryGetFromIpApiAsync()
    {
        try
        {
            using var response = await HttpClient.GetAsync("http://ip-api.com/json/?lang=zh-CN");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<IpApiResponse>(json, JsonFileStore.ReadOptions);
            if (payload is null ||
                !string.Equals(payload.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(payload.Query))
            {
                return null;
            }

            return FormatDisplay(payload.Query, payload.Country, payload.RegionName, payload.City);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"ip-api.com 查询失败: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> TryGetFromIpifyAsync()
    {
        try
        {
            var ip = (await HttpClient.GetStringAsync("https://api.ipify.org")).Trim();
            return string.IsNullOrWhiteSpace(ip) ? null : $"IP: {ip}";
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"ipify 查询失败: {ex.Message}");
            return null;
        }
    }

    private static string FormatDisplay(string ip, params string?[] locationParts)
    {
        var location = string.Join(
            " ",
            locationParts
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(location)
            ? $"IP: {ip}"
            : $"IP: {ip} | {location}";
    }

    private sealed class IpWhoIsResponse
    {
        public bool Success { get; init; }
        public string Ip { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
    }

    private sealed class IpApiResponse
    {
        public string Status { get; init; } = string.Empty;
        public string Query { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string RegionName { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
    }
}
