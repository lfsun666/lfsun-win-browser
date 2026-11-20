using System.Text.Json;
using Lfsun.Win.Browser.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Security;

public sealed class CredentialAutofillService
{
    // 中文注释：注入到每个页面的登录表单捕获脚本——监听表单 submit 与提交类按钮点击，
    // 提取用户名/密码后 postMessage 回宿主，由 TryAutosaveAsync 决定是否落库（不弹窗）。
    internal const string CredentialAutosaveScript = """
        (() => {
          if (window.__lfsunCredentialAutosaveInstalled) {
            return;
          }
          window.__lfsunCredentialAutosaveInstalled = true;

          const post = payload => {
            try {
              if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(payload);
              }
            } catch (e) {}
          };

          const findHit = () => {
            const forms = document.querySelectorAll('form');
            for (const form of forms) {
              const passwordInputs = Array.prototype.filter.call(
                form.querySelectorAll("input[type='password']"),
                el => !el.disabled && !el.readOnly
              );
              // 中文注释：无密码框、或密码框 ≥3 个（修改密码页：当前/新/确认）时跳过，避免误存。
              if (passwordInputs.length === 0 || passwordInputs.length >= 3) {
                continue;
              }
              const password = passwordInputs[0].value;
              if (!password) {
                continue;
              }
              const usernameSelectors = [
                "input[autocomplete='username']",
                "input[type='email']",
                "input[name*='user' i]",
                "input[name*='mail' i]",
                "input[name*='login' i]",
                "input[name*='account' i]",
                "input[id*='user' i]",
                "input[id*='mail' i]",
                "input[type='text']",
                "input[type='tel']",
                "input:not([type])"
              ];
              let username = '';
              for (const selector of usernameSelectors) {
                const element = form.querySelector(selector);
                if (element && !element.disabled && !element.readOnly && String(element.value || '').trim()) {
                  username = String(element.value).trim();
                  break;
                }
              }
              if (!username) {
                continue;
              }
              return { username, password };
            }
            return null;
          };

          const send = () => {
            const hit = findHit();
            if (!hit) {
              return;
            }
            post(JSON.stringify({ type: 'credentialAutosave', username: hit.username, password: hit.password }));
          };

          // 中文注释：submit 不冒泡，必须在捕获阶段监听；提交后页面可能立即跳转，因此立即发送不做延迟。
          document.addEventListener('submit', send, true);
          // 中文注释：兜底 SPA 登录（按钮点击直接 AJAX、不触发 submit）。
          document.addEventListener('click', event => {
            const target = event.target;
            if (!target || !target.closest || !target.closest('form')) {
              return;
            }
            setTimeout(send, 60);
          }, true);
        })();
        """;

    private readonly BrowserConfig _config;
    private readonly CredentialStore _credentialStore;
    private readonly Action<string> _log;

    public CredentialAutofillService(BrowserConfig config, CredentialStore credentialStore, Action<string> log)
    {
        _config = config;
        _credentialStore = credentialStore;
        _log = log;
    }

    public async Task TryAutofillAsync(WebView2 webView, Uri uri)
    {
        if (!_config.IsCredentialAutofillEnabled)
        {
            return;
        }

        if (!_credentialStore.TryGetForUri(uri, out var entry))
        {
            return;
        }

        var usernameLiteral = JsonSerializer.Serialize(entry.Username);
        var passwordLiteral = JsonSerializer.Serialize(entry.Password);
        var script = $$"""
(() => {
  const username = {{usernameLiteral}};
  const password = {{passwordLiteral}};
  if (!username || !password) {
    return;
  }

  const usernameSelectors = [
    "input[type='email']",
    "input[autocomplete='username']",
    "input[name*='user' i]",
    "input[name*='mail' i]",
    "input[id*='user' i]",
    "input[id*='mail' i]",
    "input[type='text']"
  ];
  const passwordSelectors = [
    "input[type='password']",
    "input[autocomplete='current-password']",
    "input[name*='pass' i]",
    "input[id*='pass' i]"
  ];

  const pickInput = selectors => {
    for (const selector of selectors) {
      const element = document.querySelector(selector);
      if (element && !element.disabled && !element.readOnly) {
        return element;
      }
    }
    return null;
  };

  const fillValue = (input, value) => {
    if (!input || !value || String(input.value || "").trim().length > 0) {
      return;
    }
    input.focus();
    input.value = value;
    input.dispatchEvent(new Event("input", { bubbles: true }));
    input.dispatchEvent(new Event("change", { bubbles: true }));
    input.blur();
  };

  const attemptFill = () => {
    const userInput = pickInput(usernameSelectors);
    const passInput = pickInput(passwordSelectors);
    fillValue(userInput, username);
    fillValue(passInput, password);
  };

  attemptFill();
  setTimeout(attemptFill, 350);
  setTimeout(attemptFill, 1200);
})();
""";

        try
        {
            await webView.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _log($"自动填充失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 中文注释：判断当前站点是否允许自动保存密码——全局开关关闭一律不允许；
    /// 命中站点规则（域名/IP，支持端口）按规则模式；未命中按全局开关（允许）。
    /// </summary>
    public bool IsAutosaveAllowed(Uri? uri)
    {
        if (!_config.IsCredentialAutosaveEnabled)
        {
            return false;
        }

        if (uri is null || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var rule = _config.CredentialAutosaveRules?
            .FirstOrDefault(item => RuleHostMatches(item.Host, uri));
        if (rule is not null)
        {
            return string.Equals(rule.Mode, CredentialAutosaveRule.ModeAllow, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    /// <summary>
    /// 中文注释：登录表单提交后自动保存账号密码到本机凭据库。
    /// 同站点同用户名已存在且密码未变化时跳过写盘；密码变化时更新原记录。
    /// 返回 true 表示本次实际写入了凭据库。
    /// </summary>
    public bool TryAutosaveAsync(Uri? uri, string username, string password)
    {
        try
        {
            if (!IsAutosaveAllowed(uri) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var host = uri!.Host;
            if (_credentialStore.TryFindEntry(host, username, out var existingId, out var existingPassword))
            {
                if (string.Equals(existingPassword, password, StringComparison.Ordinal))
                {
                    return false;
                }

                return _credentialStore.SaveEntry(existingId, host, username, password, out _);
            }

            return _credentialStore.SaveEntry(null, host, username, password, out _);
        }
        catch (Exception ex)
        {
            _log($"自动保存密码失败: {ex.Message}");
            return false;
        }
    }

    // 中文注释：规则主机匹配——支持域名/IPv4/IPv6，可带端口（如 192.168.1.1:8080、[::1]:8080）；
    // 域名按精确或子域后缀匹配（与 CredentialStore 的安全后缀匹配一致，防止 evil-github.com 命中 github.com）。
    private static bool RuleHostMatches(string ruleHost, Uri uri)
    {
        var rule = (ruleHost ?? string.Empty).Trim().ToLowerInvariant();
        if (rule.Length == 0)
        {
            return false;
        }

        var targetName = uri.Host.Trim('[', ']').ToLowerInvariant();
        var ruleName = rule;
        int? rulePort = null;

        if (rule.StartsWith('['))
        {
            // IPv6 形态：[::1] 或 [::1]:8080
            var closeIndex = rule.IndexOf(']');
            if (closeIndex < 0)
            {
                return false;
            }

            ruleName = rule[1..closeIndex];
            if (closeIndex + 1 < rule.Length && rule[closeIndex + 1] == ':')
            {
                rulePort = ParsePort(rule[(closeIndex + 2)..]);
                if (rulePort is null)
                {
                    return false;
                }
            }
        }
        else
        {
            var colonIndex = rule.IndexOf(':');
            if (colonIndex >= 0 && rule.IndexOf(':', colonIndex + 1) < 0)
            {
                // 单个冒号 → IPv4:端口
                rulePort = ParsePort(rule[(colonIndex + 1)..]);
                ruleName = rule[..colonIndex];
                if (rulePort is null)
                {
                    return false;
                }
            }
            // 多个冒号且无方括号 → 纯 IPv6 字面量，整体比较
        }

        if (ruleName.Length == 0)
        {
            return false;
        }

        var nameMatches = string.Equals(ruleName, targetName, StringComparison.OrdinalIgnoreCase) ||
                          targetName.EndsWith("." + ruleName, StringComparison.OrdinalIgnoreCase);
        if (!nameMatches)
        {
            return false;
        }

        return rulePort is null || uri.Port == rulePort.Value;
    }

    private static int? ParsePort(string raw)
    {
        return int.TryParse(raw, out var port) && port is > 0 and <= 65535 ? port : null;
    }
}
