using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Lfsun.Win.Browser.Bookmarks;

/// <summary>
/// 现代化书签网站图标（Favicon）与目录图标服务中心。
/// 支持内存缓存、本地磁盘缓存、Base64 数据解析、动态 Fluent 徽章即时生成、
/// 后台非阻塞异步自动补全与 WebView2 运行时图标同步捕获。
/// </summary>
public static class FaviconService
{
    private static readonly ConcurrentDictionary<string, Image> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> _pendingDownloads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim _downloadThrottle = new(4, 4);
    private static readonly HttpClient _httpClient;
    private static readonly string _cacheDirectory;
    private static Image? _folderIcon;

    public static event Action<string, Image, string>? FaviconDownloaded;

    private static readonly Color[] TileColors =
    [
        Color.FromArgb(37, 99, 235),   // Blue
        Color.FromArgb(16, 185, 129),  // Emerald
        Color.FromArgb(245, 158, 11),  // Amber
        Color.FromArgb(239, 68, 68),   // Red
        Color.FromArgb(139, 92, 246),  // Purple
        Color.FromArgb(236, 72, 153),  // Pink
        Color.FromArgb(6, 182, 212),   // Cyan
        Color.FromArgb(249, 115, 22),  // Orange
        Color.FromArgb(20, 184, 166),  // Teal
        Color.FromArgb(99, 102, 241),  // Indigo
        Color.FromArgb(168, 85, 247),  // Violet
        Color.FromArgb(100, 116, 139)  // Slate
    ];

    static FaviconService()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(4)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 LfsunBrowser");

        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfsunBrowser",
            "cache",
            "favicons");

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        catch
        {
            // 防御性忽略缓存目录创建失败
        }
    }

    /// <summary>
    /// 获取指定书签节点的 16x16 图标。
    /// 优先级：节点自身 Base64 图标 -> 内存缓存 -> 本地磁盘缓存 -> 异步下载并返回优雅彩色首字母徽章。
    /// </summary>
    public static Image GetIcon(BookmarkNode node)
    {
        if (node.IsFolder)
        {
            return GetFolderIcon();
        }

        // 1. 尝试使用节点已保存的 Base64 Favicon
        if (!string.IsNullOrWhiteSpace(node.Favicon))
        {
            var cacheKey = "base64_" + ComputeHash(node.Favicon);
            if (_memoryCache.TryGetValue(cacheKey, out var cachedImg))
            {
                return cachedImg;
            }

            var parsed = TryParseBase64Image(node.Favicon);
            if (parsed is not null)
            {
                _memoryCache[cacheKey] = parsed;
                return parsed;
            }
        }

        // 2. 根据 URL 规则解析或异步获取
        return GetIcon(node.Url, node.Name);
    }

    /// <summary>
    /// 获取指定 URL 的 16x16 图标。
    /// </summary>
    public static Image GetIcon(string? url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return GetFallbackIcon(url, title);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri is null)
        {
            return GetFallbackIcon(url, title);
        }

        // 专有协议或内部地址
        if (uri.Scheme is "edge" or "chrome" or "about" or "lfsun")
        {
            var internalKey = "internal_" + uri.Host.ToLowerInvariant();
            return _memoryCache.GetOrAdd(internalKey, _ => CreateInternalBadge(uri.Host));
        }

        // 本地文件地址
        if (uri.IsFile)
        {
            return _memoryCache.GetOrAdd("file_glyph", _ => CreateFileBadge());
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            return GetFallbackIcon(url, title);
        }

        // 1. 检查内存缓存
        if (_memoryCache.TryGetValue(host, out var memoryImg))
        {
            return memoryImg;
        }

        // 2. 检查本地磁盘缓存
        var diskPath = GetDiskCachePath(host);
        if (File.Exists(diskPath))
        {
            try
            {
                using var fs = File.OpenRead(diskPath);
                using var orig = Image.FromStream(fs);
                var bmp = ResizeToIconSize(orig);
                _memoryCache[host] = bmp;
                return bmp;
            }
            catch
            {
                // 损坏文件忽略
            }
        }

        // 3. 生成即时优雅彩色徽章（0ms 响应，不阻塞）
        var fallbackBadge = CreateFallbackBadge(url, title);
        _memoryCache[host] = fallbackBadge;

        // 4. 触发后台非阻塞异步获取
        QueueFaviconDownload(host, uri);

        return fallbackBadge;
    }

    /// <summary>
    /// 获取 Win11 Fluent 风格 16x16 文件夹图标。
    /// </summary>
    public static Image GetFolderIcon()
    {
        return _folderIcon ??= CreateFolderBitmap();
    }

    /// <summary>
    /// 主动注册并持久化一个域名对应的图标（如来自 WebView2 FaviconChanged 事件）。
    /// </summary>
    public static void RegisterFavicon(string host, Image image)
    {
        if (string.IsNullOrWhiteSpace(host) || image is null)
        {
            return;
        }

        try
        {
            var normalizedHost = host.Trim().ToLowerInvariant();
            var icon16 = ResizeToIconSize(image);
            _memoryCache[normalizedHost] = icon16;

            var diskPath = GetDiskCachePath(normalizedHost);
            icon16.Save(diskPath, ImageFormat.Png);

            var base64 = ImageToBase64(icon16);
            FaviconDownloaded?.Invoke(normalizedHost, icon16, base64);
        }
        catch
        {
            // 防御性忽略异常
        }
    }

    /// <summary>
    /// 为 TreeView 的 ImageList 准备好指定节点的图标，返回对应的 key。
    /// </summary>
    public static string EnsureTreeIcon(ImageList imageList, BookmarkNode node)
    {
        if (node.IsFolder)
        {
            if (!imageList.Images.ContainsKey("folder"))
            {
                imageList.Images.Add("folder", GetFolderIcon());
            }
            return "folder";
        }

        var key = "link_" + (string.IsNullOrWhiteSpace(node.Favicon)
            ? (node.Url.GetHashCode().ToString())
            : node.Id.ToString());

        if (!imageList.Images.ContainsKey(key))
        {
            var img = GetIcon(node);
            imageList.Images.Add(key, img);
        }

        return key;
    }

    /// <summary>
    /// 后台批量排队抓取并补全书签集合中缺失的网站 Favicon。
    /// </summary>
    public static int QueueBatchFetch(IEnumerable<BookmarkNode> nodes)
    {
        var count = 0;
        void Collect(BookmarkNode parent)
        {
            foreach (var child in parent.Children)
            {
                if (child.IsLink && !string.IsNullOrWhiteSpace(child.Url))
                {
                    if (Uri.TryCreate(child.Url, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        var host = uri.Host.Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(host) && !_memoryCache.ContainsKey(host) && !File.Exists(GetDiskCachePath(host)))
                        {
                            QueueFaviconDownload(host, uri);
                            count++;
                        }
                    }
                }
                if (child.IsFolder)
                {
                    Collect(child);
                }
            }
        }

        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                Collect(node);
            }
            else if (node.IsLink && !string.IsNullOrWhiteSpace(node.Url))
            {
                if (Uri.TryCreate(node.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var host = uri.Host.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(host) && !_memoryCache.ContainsKey(host) && !File.Exists(GetDiskCachePath(host)))
                    {
                        QueueFaviconDownload(host, uri);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 将图像转换为 data:image/png;base64,... 字符串。
    /// </summary>
    public static string ImageToBase64(Image image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// 获取指定 URL 或已存 Favicon 的 Data URL 字符串（如 "data:image/png;base64,..."）。
    /// 若无真实图标则返回 null，供前端使用 Fluent 彩色徽章优雅降级。
    /// </summary>
    public static string? TryGetFaviconDataUrl(string? url, string? existingFavicon = null)
    {
        if (!string.IsNullOrWhiteSpace(existingFavicon))
        {
            var trimmed = existingFavicon.Trim();
            if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
            return $"data:image/png;base64,{trimmed}";
        }

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var diskPath = GetDiskCachePath(host);
        if (File.Exists(diskPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(diskPath);
                if (bytes.Length > 0)
                {
                    return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                }
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    /// <summary>
    /// 获取指定域名或标题对应的统一 Fluent 强调色（16 进制 CSS 颜色字符串）。
    /// </summary>
    public static string ResolveTileCssColor(string? seed)
    {
        var str = string.IsNullOrWhiteSpace(seed) ? "lfsun" : seed.Trim();
        var idx = Math.Abs(str.GetHashCode()) % TileColors.Length;
        var color = TileColors[idx];
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// 缩放并高质量抗锯齿绘制为 16x16 32bpp 像素位图。
    /// </summary>
    public static Bitmap ResizeToIconSize(Image original, int width = 16, int height = 16)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.DrawImage(original, new Rectangle(0, 0, width, height));
        return bmp;
    }

    /// <summary>
    /// 解析 Base64 图片数据为 Bitmap。
    /// </summary>
    public static Bitmap? TryParseBase64Image(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var base64 = raw.Trim();
            var commaIndex = base64.IndexOf(',');
            if (commaIndex >= 0 && base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                base64 = base64[(commaIndex + 1)..];
            }

            var bytes = Convert.FromBase64String(base64.Trim());
            using var ms = new MemoryStream(bytes);
            using var original = Image.FromStream(ms);
            return ResizeToIconSize(original, 16, 16);
        }
        catch
        {
            return null;
        }
    }

    private static Image GetFallbackIcon(string? url, string? title)
    {
        var key = "fallback_" + (url ?? title ?? string.Empty).GetHashCode();
        return _memoryCache.GetOrAdd(key, _ => CreateFallbackBadge(url, title));
    }

    private static Bitmap CreateFolderBitmap()
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 后置文件夹凸耳 (Tab)
        using var tabBrush = new SolidBrush(Color.FromArgb(217, 119, 6)); // #D97706
        using var tabPath = CreateRoundedPath(new RectangleF(1.5f, 2.5f, 6.5f, 3.5f), 1.5f);
        g.FillPath(tabBrush, tabPath);

        // 后置主体
        using var backPath = CreateRoundedPath(new RectangleF(1.5f, 4f, 13f, 9.5f), 1.8f);
        g.FillPath(tabBrush, backPath);

        // 前置文件夹袋面 (渐变明黄)
        using var pocketBrush = new LinearGradientBrush(
            new PointF(0, 5), new PointF(0, 14),
            Color.FromArgb(251, 191, 36),  // #FBBF24
            Color.FromArgb(245, 158, 11)); // #F59E0B
        using var pocketPath = CreateRoundedPath(new RectangleF(1.5f, 5.5f, 13f, 8.5f), 1.8f);
        g.FillPath(pocketBrush, pocketPath);

        // 顶部反光微高光线
        using var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 0.8f);
        g.DrawLine(pen, 2.5f, 6.2f, 13.5f, 6.2f);

        return bmp;
    }

    private static Bitmap CreateInternalBadge(string? name)
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var bgBrush = new SolidBrush(Color.FromArgb(51, 65, 85)); // Slate 700
        using var path = CreateRoundedPath(new RectangleF(1f, 1f, 14f, 14f), 3.5f);
        g.FillPath(bgBrush, path);

        using var font = new Font("Segoe UI", 7.5F, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.FromArgb(224, 231, 255));
        var charToDraw = "⚙";
        if (name?.Contains("history") == true) charToDraw = "◷";
        else if (name?.Contains("download") == true) charToDraw = "↓";
        else if (name?.Contains("bookmark") == true || name?.Contains("favorite") == true) charToDraw = "★";

        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(charToDraw, font, textBrush, new RectangleF(0.5f, 0.5f, 15f, 15f), sf);

        return bmp;
    }

    private static Bitmap CreateFileBadge()
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var bgBrush = new SolidBrush(Color.FromArgb(71, 85, 105)); // Slate 600
        using var path = CreateRoundedPath(new RectangleF(1.5f, 1.5f, 13f, 13f), 3f);
        g.FillPath(bgBrush, path);

        using var font = new Font("Segoe UI", 7F, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("文", font, textBrush, new RectangleF(1f, 1f, 14f, 14f), sf);

        return bmp;
    }

    private static Bitmap CreateFallbackBadge(string? url, string? title)
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var token = ResolveInitialToken(url, title);
        var seedString = !string.IsNullOrWhiteSpace(url) ? url : title ?? "default";
        var colorIndex = Math.Abs(seedString.GetHashCode()) % TileColors.Length;
        var bgColor = TileColors[colorIndex];

        using var bgBrush = new SolidBrush(bgColor);
        using var path = CreateRoundedPath(new RectangleF(1f, 1f, 14f, 14f), 3.5f);
        g.FillPath(bgBrush, path);

        using var font = new Font("Segoe UI", 7.2F, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(token, font, textBrush, new RectangleF(0.5f, 0.5f, 15f, 15f), sf);

        return bmp;
    }

    private static string ResolveInitialToken(string? url, string? title)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            var hostParts = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var domainName = hostParts.Length >= 2 && (hostParts[0] is "www" or "m" or "api")
                ? hostParts[1]
                : hostParts[0];

            var letter = domainName.FirstOrDefault(char.IsLetterOrDigit);
            if (letter != default)
            {
                return char.ToUpperInvariant(letter).ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleLetter = title.Trim().FirstOrDefault(char.IsLetterOrDigit);
            if (titleLetter != default)
            {
                return char.ToUpperInvariant(titleLetter).ToString();
            }
        }

        return "•";
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void QueueFaviconDownload(string host, Uri sampleUri)
    {
        if (!_pendingDownloads.TryAdd(host, 1))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _downloadThrottle.WaitAsync();
                try
                {
                    var icon = await DownloadFaviconAsync(host, sampleUri);
                    if (icon is not null)
                    {
                        RegisterFavicon(host, icon);
                    }
                }
                finally
                {
                    _downloadThrottle.Release();
                    _pendingDownloads.TryRemove(host, out _);
                }
            }
            catch
            {
                // 忽略非关键图标下载异常
            }
        });
    }

    private static async Task<Image?> DownloadFaviconAsync(string host, Uri sampleUri)
    {
        // 候选源清单：公共高可靠 CDN -> 网站根路径
        var candidates = new[]
        {
            $"https://icons.duckduckgo.com/ip3/{host}.ico",
            $"https://favicon.yandex.net/favicon/{host}",
            $"{sampleUri.Scheme}://{host}/favicon.ico"
        };

        foreach (var url in candidates)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var bytes = await _httpClient.GetByteArrayAsync(url, cts.Token);
                if (bytes != null && bytes.Length > 20)
                {
                    using var ms = new MemoryStream(bytes);
                    using var original = Image.FromStream(ms);
                    return ResizeToIconSize(original);
                }
            }
            catch
            {
                // 尝试下一个候选地址
            }
        }

        return null;
    }

    private static string GetDiskCachePath(string host)
    {
        var safeHost = host.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        return Path.Combine(_cacheDirectory, $"{safeHost}.png");
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16];
    }
}
