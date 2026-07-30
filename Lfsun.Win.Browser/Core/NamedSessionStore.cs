namespace Lfsun.Win.Browser.Core;

public sealed class NamedSessionStore
{
    private readonly string _sessionsDirectory;
    private readonly Action<string> _log;

    public NamedSessionStore(string sessionsDirectory, Action<string> log)
    {
        _sessionsDirectory = sessionsDirectory;
        _log = log;
    }

    public IReadOnlyList<NamedSessionDescriptor> ListSessions()
    {
        try
        {
            if (!Directory.Exists(_sessionsDirectory))
            {
                return [];
            }

            var sessions = new List<NamedSessionDescriptor>();
            foreach (var filePath in Directory.EnumerateFiles(_sessionsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var session = JsonFileStore.Load<NamedBrowserSessionState>(filePath);
                    if (session is null || string.IsNullOrWhiteSpace(session.Name) || session.Tabs.Count == 0)
                    {
                        continue;
                    }

                    sessions.Add(new NamedSessionDescriptor(
                        session.Name.Trim(),
                        session.SavedAtUtc,
                        session.Tabs.Count));
                }
                catch
                {
                    // Ignore bad single-session files and keep the rest usable.
                }
            }

            return sessions
                .OrderByDescending(item => item.SavedAtUtc)
                .ToArray();
        }
        catch (Exception ex)
        {
            _log($"读取命名会话列表失败: {ex.Message}");
            return [];
        }
    }

    public bool Save(string name, BrowserSessionState session, out string error)
    {
        try
        {
            var normalizedName = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                error = "会话名称不能为空。";
                return false;
            }

            if (session.Tabs.Count == 0)
            {
                error = "当前没有可保存的标签页。";
                return false;
            }

            Directory.CreateDirectory(_sessionsDirectory);
            var filePath = GetSessionFilePath(normalizedName);
            var namedSession = new NamedBrowserSessionState
            {
                Name = normalizedName,
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(session.ActiveIndex, 0, session.Tabs.Count - 1),
                ExitedCleanly = true,
                Tabs = session.Tabs
                    .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                    .ToList()
            };
            if (!JsonFileStore.Save(filePath, namedSession, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log($"保存命名会话失败: {ex.Message}");
            return false;
        }
    }

    public NamedBrowserSessionState? Load(string name)
    {
        try
        {
            var filePath = GetSessionFilePath(name);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var session = JsonFileStore.Load<NamedBrowserSessionState>(filePath);
            if (session is null || session.Tabs.Count == 0)
            {
                return null;
            }

            session.Tabs = session.Tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (session.Tabs.Count == 0)
            {
                return null;
            }

            session.ActiveIndex = Math.Clamp(session.ActiveIndex, 0, session.Tabs.Count - 1);
            return session;
        }
        catch (Exception ex)
        {
            _log($"读取命名会话失败: {ex.Message}");
            return null;
        }
    }

    public bool Delete(string name, out string error)
    {
        try
        {
            var filePath = GetSessionFilePath(name);
            if (!File.Exists(filePath))
            {
                error = "未找到会话文件。";
                return false;
            }

            File.Delete(filePath);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log($"删除命名会话失败: {ex.Message}");
            return false;
        }
    }

    public bool Rename(string oldName, string newName, out string error)
    {
        try
        {
            var existing = Load(oldName);
            if (existing is null)
            {
                error = "未找到原命名会话。";
                return false;
            }

            var normalizedNewName = NormalizeName(newName);
            if (string.IsNullOrWhiteSpace(normalizedNewName))
            {
                error = "新会话名称不能为空。";
                return false;
            }

            var oldPath = GetSessionFilePath(oldName);
            var newPath = GetSessionFilePath(normalizedNewName);
            Directory.CreateDirectory(_sessionsDirectory);

            var renamed = new NamedBrowserSessionState
            {
                Name = normalizedNewName,
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = existing.ActiveIndex,
                ExitedCleanly = true,
                Tabs = existing.Tabs
            };

            if (!JsonFileStore.Save(newPath, renamed, out error))
            {
                return false;
            }

            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _log($"重命名命名会话失败: {ex.Message}");
            return false;
        }
    }

    private string GetSessionFilePath(string name)
    {
        return Path.Combine(_sessionsDirectory, $"{NormalizeName(name)}.json");
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var safeChars = trimmed
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(safeChars).Trim();
    }
}

public sealed record NamedSessionDescriptor(
    string Name,
    DateTime SavedAtUtc,
    int TabCount);

public sealed class NamedBrowserSessionState
{
    public string Name { get; init; } = string.Empty;
    public DateTime SavedAtUtc { get; init; } = DateTime.UtcNow;
    public int ActiveIndex { get; set; }
    public bool ExitedCleanly { get; init; }
    public List<BrowserSessionTabState> Tabs { get; set; } = [];
}

