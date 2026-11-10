using System.Net;
using Lfsun.Win.Browser.UI;

namespace Lfsun.Win.Browser.Core;

/// <summary>
/// 浏览器专有协议与内部地址智能路由器。
/// 拦截并智能解析 edge://、chrome://、about:settings 等专有链接，
/// 无缝映射到本浏览器的原生功能（外观/隐私/书签/历史/下载），避免 WebView2 报 ERR_INVALID_URL 错误。
/// </summary>
public static class InternalUrlRouter
{
    public static bool IsInternalBrowserUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        var trimmed = rawUrl.Trim();
        return trimmed.StartsWith("edge://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("lfsun://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("about:settings", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("about:bookmarks", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("about:favorites", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("about:history", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("about:downloads", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryRoute(
        string? rawUrl,
        Action<int> openSettingsCategory,
        Action openBookmarks,
        Action openHistory,
        Action openDownloads,
        Action<string> showToast,
        out string? redirectHtml)
    {
        redirectHtml = null;
        if (!IsInternalBrowserUrl(rawUrl))
        {
            return false;
        }

        var url = rawUrl!.Trim().ToLowerInvariant();

        // 1. 界面与外观设置（例如 edge://settings/appearance, chrome://settings/appearance）
        if (url.Contains("/appearance") || url.Contains("appearance"))
        {
            openSettingsCategory(1); // 1 = 界面 / 外观
            showToast("已为您打开外观设置");
            return true;
        }

        // 2. 隐私与密码 / 自动填充设置
        if (url.Contains("/privacy") || url.Contains("/passwords") || url.Contains("/autofill") || url.Contains("/security"))
        {
            openSettingsCategory(5); // 5 = 隐私与登录
            showToast("已为您打开隐私与安全设置");
            return true;
        }

        // 3. 搜索与默认引擎
        if (url.Contains("/search") || url.Contains("/searchengines"))
        {
            openSettingsCategory(2); // 2 = 搜索与账号
            showToast("已为您打开搜索引擎设置");
            return true;
        }

        // 4. 下载管理
        if (url.Contains("/downloads") || url.StartsWith("chrome://downloads") || url.StartsWith("edge://downloads"))
        {
            openDownloads();
            showToast("已为您打开下载管理");
            return true;
        }

        // 5. 书签 / 收藏夹管理器
        if (url.Contains("/bookmarks") || url.Contains("/favorites") || url.StartsWith("chrome://bookmarks") || url.StartsWith("edge://favorites"))
        {
            openBookmarks();
            showToast("已为您打开书签管理");
            return true;
        }

        // 6. 历史记录
        if (url.Contains("/history") || url.StartsWith("chrome://history") || url.StartsWith("edge://history"))
        {
            openHistory();
            showToast("已为您打开历史记录");
            return true;
        }

        // 7. 插件扩展管理
        if (url.Contains("/extensions") || url.Contains("/plugins"))
        {
            openSettingsCategory(6); // 6 = 插件
            showToast("已为您打开插件管理");
            return true;
        }

        // 8. 常规全局设置
        if (url.StartsWith("edge://settings") || url.StartsWith("chrome://settings") ||
            url.StartsWith("about:settings") || url.StartsWith("lfsun://settings"))
        {
            openSettingsCategory(0); // 0 = 常规
            showToast("已为您打开全局设置");
            return true;
        }

        // 9. 其它未直接映射的内部地址：生成友好引导交互页面，替代 ERR_INVALID_URL 报错
        redirectHtml = BuildInternalRedirectHtml(rawUrl!);
        return false;
    }

    public static string BuildInternalRedirectHtml(string rawUrl)
    {
        var encodedUrl = WebUtility.HtmlEncode(rawUrl);
        var bg = AppTheme.IsDark ? "#181e29" : "#f4f6fa";
        var cardBg = AppTheme.IsDark ? "#232b3b" : "#ffffff";
        var textPrimary = AppTheme.IsDark ? "#e6edf8" : "#1f2937";
        var textSecondary = AppTheme.IsDark ? "#94a3b8" : "#64748b";
        var border = AppTheme.IsDark ? "#334155" : "#e2e8f0";
        var accent = "#2563eb";

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>内部专有协议地址 - Lfsun浏览器</title>
              <style>
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
                  background-color: {{bg}};
                  color: {{textPrimary}};
                  min-height: 100vh;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  padding: 24px;
                }
                .card {
                  background: {{cardBg}};
                  border: 1px solid {{border}};
                  border-radius: 16px;
                  box-shadow: 0 10px 30px rgba(0,0,0,0.08);
                  max-width: 640px;
                  width: 100%;
                  padding: 36px 32px;
                  text-align: center;
                }
                .icon {
                  font-size: 48px;
                  margin-bottom: 16px;
                }
                h1 {
                  font-size: 20px;
                  font-weight: 700;
                  margin-bottom: 12px;
                }
                p {
                  font-size: 14px;
                  color: {{textSecondary}};
                  line-height: 1.6;
                  margin-bottom: 20px;
                }
                .url-badge {
                  display: inline-block;
                  background: rgba(37, 99, 235, 0.08);
                  color: {{accent}};
                  border: 1px dashed {{border}};
                  border-radius: 8px;
                  padding: 6px 14px;
                  font-family: Consolas, monospace;
                  font-size: 13px;
                  word-break: break-all;
                  margin-bottom: 24px;
                }
                .actions {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
                  gap: 12px;
                  margin-top: 8px;
                }
                .btn {
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                  padding: 10px 16px;
                  border-radius: 10px;
                  font-size: 13.5px;
                  font-weight: 600;
                  cursor: pointer;
                  text-decoration: none;
                  transition: all 0.2s ease;
                  border: 1px solid {{border}};
                  background: {{cardBg}};
                  color: {{textPrimary}};
                }
                .btn:hover {
                  background: rgba(37, 99, 235, 0.08);
                  border-color: {{accent}};
                  color: {{accent}};
                  transform: translateY(-1px);
                }
                .btn-primary {
                  background: {{accent}};
                  color: #ffffff;
                  border-color: {{accent}};
                }
                .btn-primary:hover {
                  background: #1d4ed8;
                  color: #ffffff;
                }
              </style>
            </head>
            <body>
              <div class="card">
                <div class="icon">🧭</div>
                <h1>专有浏览器内部协议链接</h1>
                <p>您访问的地址属于外部浏览器（Chrome / Edge）内置专有地址。Lfsun浏览器已为您智能拦截，请通过下方快捷方式打开对应的浏览器功能：</p>
                <div class="url-badge">{{encodedUrl}}</div>
                <div class="actions">
                  <a class="btn btn-primary" href="javascript:void(0)" onclick="notifyHost('appearance')">🎨 外观与主题</a>
                  <a class="btn" href="javascript:void(0)" onclick="notifyHost('bookmarks')">📑 书签管理</a>
                  <a class="btn" href="javascript:void(0)" onclick="notifyHost('history')">🕒 历史记录</a>
                  <a class="btn" href="javascript:void(0)" onclick="notifyHost('settings')">⚙️ 全局设置</a>
                </div>
              </div>
              <script>
                function notifyHost(action) {
                  try {
                    window.chrome?.webview?.postMessage({ type: 'internalRoute', action: action });
                  } catch (e) {
                    console.error(e);
                  }
                }
              </script>
            </body>
            </html>
            """;
    }
}
