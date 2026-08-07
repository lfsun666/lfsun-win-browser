using System.Security.Cryptography;
using System.Text;

namespace Lfsun.Win.Browser.Security;

public sealed class CredentialStore
{
    private readonly string _storagePath;
    private readonly Action<string> _log;
    private readonly List<CredentialStorageEntry> _entries = [];

    public CredentialStore(string storagePath, Action<string> log)
    {
        _storagePath = storagePath;
        _log = log;
        Reload();
    }

    public IReadOnlyList<CredentialEntry> ListEntries()
    {
        return _entries
            .Select(entry => new CredentialEntry(
                entry.Id,
                entry.Site,
                entry.Username,
                DecryptPassword(entry.EncryptedPassword),
                entry.UpdatedUtc))
            .OrderBy(entry => entry.Site, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryGetForUri(Uri uri, out CredentialEntry entry)
    {
        entry = default!;
        if (uri is null || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        var candidates = ListEntries();
        if (candidates.Count == 0)
        {
            return false;
        }

        var exact = candidates
            .Where(item => IsExactHostMatch(item.Site, host))
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault();
        if (exact is not null)
        {
            entry = exact;
            return true;
        }

        var suffix = candidates
            .Where(item => IsSuffixHostMatch(item.Site, host))
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault();
        if (suffix is not null)
        {
            entry = suffix;
            return true;
        }

        return false;
    }

    public bool SaveEntry(Guid? id, string site, string username, string password, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(site))
        {
            error = "站点不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            error = "用户名不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            error = "密码不能为空。";
            return false;
        }

        try
        {
            var normalizedSite = site.Trim();
            var normalizedUsername = username.Trim();
            var encryptedPassword = EncryptPassword(password);
            var nowUtc = DateTime.UtcNow;

            if (id is null || id == Guid.Empty)
            {
                _entries.Add(new CredentialStorageEntry
                {
                    Id = Guid.NewGuid(),
                    Site = normalizedSite,
                    Username = normalizedUsername,
                    EncryptedPassword = encryptedPassword,
                    UpdatedUtc = nowUtc
                });
            }
            else
            {
                var target = _entries.FirstOrDefault(entry => entry.Id == id.Value);
                if (target is null)
                {
                    error = "未找到要更新的账号记录。";
                    return false;
                }

                target.Site = normalizedSite;
                target.Username = normalizedUsername;
                target.EncryptedPassword = encryptedPassword;
                target.UpdatedUtc = nowUtc;
            }

            Persist();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool RemoveEntry(Guid id, out string error)
    {
        error = string.Empty;
        var removed = _entries.RemoveAll(entry => entry.Id == id);
        if (removed == 0)
        {
            error = "未找到要删除的账号记录。";
            return false;
        }

        try
        {
            Persist();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Reload()
    {
        _entries.Clear();
        if (!File.Exists(_storagePath))
        {
            return;
        }

        var loaded = Core.JsonFileStore.Load<List<CredentialStorageEntry>>(_storagePath);
        if (loaded is null)
        {
            _log("读取账号记录失败，已回退空列表");
            return;
        }

        _entries.AddRange(loaded.Where(entry =>
            entry.Id != Guid.Empty &&
            !string.IsNullOrWhiteSpace(entry.Site) &&
            !string.IsNullOrWhiteSpace(entry.Username) &&
            !string.IsNullOrWhiteSpace(entry.EncryptedPassword)));
    }

    private void Persist()
    {
        if (!Core.JsonFileStore.Save(_storagePath, _entries, out var error))
        {
            throw new IOException(error);
        }
    }

    private static string EncryptPassword(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string DecryptPassword(string encryptedBase64)
    {
        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsExactHostMatch(string siteInput, string host)
    {
        var normalized = NormalizeSiteHost(siteInput);
        return string.Equals(normalized, host, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuffixHostMatch(string siteInput, string host)
    {
        var normalized = NormalizeSiteHost(siteInput);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        // 中文注释：要求完整段匹配，避免 evil-github.com 错误匹配 github.com 的安全漏洞。
        // 合法匹配：host == suffix（如 github.com 对 github.com）
        //           || host 是以 "." + suffix 结尾的子域名（如 api.github.com 对 github.com）
        return string.Equals(host, normalized, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSiteHost(string siteInput)
    {
        var raw = siteInput.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = $"https://{raw}";
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri is null)
        {
            return siteInput.Trim().ToLowerInvariant();
        }

        return uri.Host.Trim().ToLowerInvariant();
    }

    private sealed class CredentialStorageEntry
    {
        public Guid Id { get; set; }
        public string Site { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public DateTime UpdatedUtc { get; set; }
    }
}

