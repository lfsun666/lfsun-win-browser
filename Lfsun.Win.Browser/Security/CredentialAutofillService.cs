using System.Text.Json;
using Lfsun.Win.Browser.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Lfsun.Win.Browser.Security;

public sealed class CredentialAutofillService
{
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
}

