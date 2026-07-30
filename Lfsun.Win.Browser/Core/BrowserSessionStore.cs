namespace Lfsun.Win.Browser.Core;

public sealed class BrowserSessionStore
{
    private readonly string _storagePath;
    private readonly Action<string> _log;

    public BrowserSessionStore(string storagePath, Action<string> log)
    {
        _storagePath = storagePath;
        _log = log;
    }

    public BrowserSessionState? Load()
    {
        if (!File.Exists(_storagePath))
        {
            return null;
        }

        try
        {
            var loaded = JsonFileStore.Load<BrowserSessionState>(_storagePath);
            if (loaded is null || loaded.Tabs.Count == 0)
            {
                return null;
            }

            var tabs = loaded.Tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (tabs.Count == 0)
            {
                return null;
            }

            var activeIndex = Math.Clamp(loaded.ActiveIndex, 0, tabs.Count - 1);
            return new BrowserSessionState
            {
                SavedAtUtc = loaded.SavedAtUtc,
                ActiveIndex = activeIndex,
                ExitedCleanly = loaded.ExitedCleanly,
                Tabs = tabs
            };
        }
        catch (Exception ex)
        {
            _log($"读取会话失败: {ex.Message}");
            return null;
        }
    }

    public void Save(IEnumerable<BrowserSessionTabState> tabs, int activeIndex, bool exitedCleanly)
    {
        try
        {
            var normalizedTabs = tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (normalizedTabs.Count == 0)
            {
                Clear();
                return;
            }

            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var session = new BrowserSessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(activeIndex, 0, normalizedTabs.Count - 1),
                ExitedCleanly = exitedCleanly,
                Tabs = normalizedTabs
            };
            if (!JsonFileStore.Save(_storagePath, session, out var error))
            {
                _log($"保存会话失败: {error}");
            }
        }
        catch (Exception ex)
        {
            _log($"保存会话失败: {ex.Message}");
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
        }
        catch (Exception ex)
        {
            _log($"清理会话失败: {ex.Message}");
        }
    }
}

public sealed class BrowserSessionState
{
    public DateTime SavedAtUtc { get; init; } = DateTime.UtcNow;
    public int ActiveIndex { get; init; }
    public bool ExitedCleanly { get; init; }
    public List<BrowserSessionTabState> Tabs { get; init; } = [];
}

public sealed class BrowserSessionTabState
{
    public string Url { get; init; } = "about:blank";
    public string Title { get; init; } = string.Empty;
    public string CustomTitle { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public string TabColorKey { get; init; } = string.Empty;
}

