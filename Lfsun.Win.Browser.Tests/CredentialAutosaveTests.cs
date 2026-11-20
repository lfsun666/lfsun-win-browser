using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Security;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

/// <summary>
/// 中文注释：登录密码自动保存（免弹窗）回归测试——覆盖配置归一化/老配置迁移、
/// 站点规则（域名/IP/端口/IPv6/子域后缀）匹配与自动保存去重/更新逻辑。
/// </summary>
public sealed class CredentialAutosaveTests
{
    private static BrowserConfig CreateConfig(
        bool autosaveEnabled = true,
        IEnumerable<CredentialAutosaveRule>? rules = null)
    {
        return new BrowserConfig
        {
            IsCredentialAutosaveEnabled = autosaveEnabled,
            CredentialAutosaveRules = rules?.ToList() ?? [],
            WebView2PasswordAutosaveMigrated = true,
        };
    }

    private static (CredentialAutofillService Service, CredentialStore Store) CreateService(BrowserConfig config)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lfsun-cred-test-{Guid.NewGuid():N}.json");
        var store = new CredentialStore(path, _ => { });
        return (new CredentialAutofillService(config, store, _ => { }), store);
    }

    // ---------- 配置归一化 ----------

    [Fact]
    public void Normalize_LegacyPasswordAutosave_MigratedToOff()
    {
        var config = new BrowserConfig
        {
            IsPasswordAutosaveEnabled = true,
            WebView2PasswordAutosaveMigrated = false,
        };

        var normalized = BrowserConfig.Normalize(config);

        Assert.False(normalized.IsPasswordAutosaveEnabled);
        Assert.True(normalized.WebView2PasswordAutosaveMigrated);
    }

    [Fact]
    public void Normalize_ExplicitOptInAfterMigration_Respected()
    {
        var config = new BrowserConfig
        {
            IsPasswordAutosaveEnabled = true,
            WebView2PasswordAutosaveMigrated = true,
        };

        var normalized = BrowserConfig.Normalize(config);

        Assert.True(normalized.IsPasswordAutosaveEnabled);
    }

    [Fact]
    public void Normalize_RuleHost_StripsSchemePathAndLowercases()
    {
        var config = new BrowserConfig
        {
            CredentialAutosaveRules =
            [
                new CredentialAutosaveRule { Host = "https://Example.com/path", Mode = CredentialAutosaveRule.ModeBlock },
            ],
        };

        var normalized = BrowserConfig.Normalize(config);

        var rule = Assert.Single(normalized.CredentialAutosaveRules);
        Assert.Equal("example.com", rule.Host);
        Assert.Equal(CredentialAutosaveRule.ModeBlock, rule.Mode);
    }

    [Fact]
    public void Normalize_RuleInvalidMode_BecomesBlock()
    {
        var config = new BrowserConfig
        {
            CredentialAutosaveRules =
            [
                new CredentialAutosaveRule { Host = "example.com", Mode = "Whatever" },
            ],
        };

        var normalized = BrowserConfig.Normalize(config);

        Assert.Equal(CredentialAutosaveRule.ModeBlock, Assert.Single(normalized.CredentialAutosaveRules).Mode);
    }

    [Fact]
    public void Normalize_RuleDuplicateHost_Deduped()
    {
        var config = new BrowserConfig
        {
            CredentialAutosaveRules =
            [
                new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeAllow },
                new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeBlock },
            ],
        };

        var normalized = BrowserConfig.Normalize(config);

        Assert.Single(normalized.CredentialAutosaveRules);
    }

    [Fact]
    public void Normalize_RuleInvalidHost_Dropped()
    {
        var config = new BrowserConfig
        {
            CredentialAutosaveRules =
            [
                new CredentialAutosaveRule { Host = "bad host!!", Mode = CredentialAutosaveRule.ModeAllow },
                new CredentialAutosaveRule { Host = "good.com", Mode = CredentialAutosaveRule.ModeAllow },
            ],
        };

        var normalized = BrowserConfig.Normalize(config);

        var rule = Assert.Single(normalized.CredentialAutosaveRules);
        Assert.Equal("good.com", rule.Host);
    }

    // ---------- 规则匹配 ----------

    [Fact]
    public void IsAutosaveAllowed_GlobalOff_ReturnsFalse()
    {
        var (service, _) = CreateService(CreateConfig(autosaveEnabled: false));
        Assert.False(service.IsAutosaveAllowed(new Uri("https://example.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_NoRule_DefaultTrue()
    {
        var (service, _) = CreateService(CreateConfig());
        Assert.True(service.IsAutosaveAllowed(new Uri("https://example.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_BlockRule_ExactDomain_Blocked()
    {
        var (service, _) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        Assert.False(service.IsAutosaveAllowed(new Uri("https://example.com/login")));
        Assert.True(service.IsAutosaveAllowed(new Uri("https://other.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_AllowRule_GlobalOff_StillBlocked()
    {
        var (service, _) = CreateService(CreateConfig(autosaveEnabled: false, rules:
        [
            new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeAllow },
        ]));

        // 中文注释：全局总闸关闭时规则不生效——总闸语义，避免“关了还在存”的困惑。
        Assert.False(service.IsAutosaveAllowed(new Uri("https://example.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_AllowRule_WithinEnabledGlobal_Allows()
    {
        var (service, _) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeAllow },
            new CredentialAutosaveRule { Host = "blocked.com", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        Assert.True(service.IsAutosaveAllowed(new Uri("https://example.com/login")));
        Assert.False(service.IsAutosaveAllowed(new Uri("https://blocked.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_SubdomainSuffixMatch()
    {
        var (service, _) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        Assert.False(service.IsAutosaveAllowed(new Uri("https://api.example.com/login")));
        // 中文注释：evil-example.com 不是 example.com 的子域，不应命中（防后缀误匹配）。
        Assert.True(service.IsAutosaveAllowed(new Uri("https://evil-example.com/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_IpWithPort()
    {
        var (service, _) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "192.168.1.1:8080", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        Assert.False(service.IsAutosaveAllowed(new Uri("http://192.168.1.1:8080/login")));
        Assert.True(service.IsAutosaveAllowed(new Uri("http://192.168.1.1:9090/login")));
        Assert.True(service.IsAutosaveAllowed(new Uri("http://192.168.1.2:8080/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_Ipv6Host()
    {
        var (service, _) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "[::1]", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        Assert.False(service.IsAutosaveAllowed(new Uri("http://[::1]:8080/login")));
        Assert.False(service.IsAutosaveAllowed(new Uri("http://[::1]/login")));
        Assert.True(service.IsAutosaveAllowed(new Uri("http://[::2]/login")));
    }

    [Fact]
    public void IsAutosaveAllowed_NullUri_ReturnsFalse()
    {
        var (service, _) = CreateService(CreateConfig());
        Assert.False(service.IsAutosaveAllowed(null));
    }

    // ---------- 自动保存行为 ----------

    [Fact]
    public void TryAutosaveAsync_SavesNewEntry()
    {
        var (service, store) = CreateService(CreateConfig());

        var saved = service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1");

        Assert.True(saved);
        var entry = Assert.Single(store.ListEntries());
        Assert.Equal("example.com", entry.Site);
        Assert.Equal("alice", entry.Username);
        Assert.Equal("pw-1", entry.Password);
    }

    [Fact]
    public void TryAutosaveAsync_SamePasswordSkipped()
    {
        var (service, store) = CreateService(CreateConfig());
        Assert.True(service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1"));

        var savedAgain = service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1");

        Assert.False(savedAgain);
        Assert.Single(store.ListEntries());
    }

    [Fact]
    public void TryAutosaveAsync_PasswordChanged_UpdatesSameRecord()
    {
        var (service, store) = CreateService(CreateConfig());
        Assert.True(service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1"));

        var updated = service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-2");

        Assert.True(updated);
        var entry = Assert.Single(store.ListEntries());
        Assert.Equal("pw-2", entry.Password);
    }

    [Fact]
    public void TryAutosaveAsync_BlockedSite_NotSaved()
    {
        var (service, store) = CreateService(CreateConfig(rules:
        [
            new CredentialAutosaveRule { Host = "example.com", Mode = CredentialAutosaveRule.ModeBlock },
        ]));

        var saved = service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1");

        Assert.False(saved);
        Assert.Empty(store.ListEntries());
    }

    [Fact]
    public void TryAutosaveAsync_GlobalOff_NotSaved()
    {
        var (service, store) = CreateService(CreateConfig(autosaveEnabled: false));

        Assert.False(service.TryAutosaveAsync(new Uri("https://example.com/login"), "alice", "pw-1"));
        Assert.Empty(store.ListEntries());
    }
}
