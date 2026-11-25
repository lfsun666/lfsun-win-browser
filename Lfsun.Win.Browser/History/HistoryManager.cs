namespace Lfsun.Win.Browser.History;

public sealed class HistoryManager
{
    private readonly string _storagePath;
    private readonly Action<string> _log;
    private readonly int _maxEntries;
    private readonly List<HistoryEntry> _entries = [];
    private readonly Action<IReadOnlyList<HistoryEntry>>? _persistOverride;
    private bool _pendingPersist;
    private DateTime _nextPersistRetryUtc;

    public HistoryManager(
        string storagePath,
        Action<string> log,
        int maxEntries = 3000,
        Action<IReadOnlyList<HistoryEntry>>? persistOverride = null)
    {
        _storagePath = storagePath;
        _log = log;
        _maxEntries = Math.Max(200, maxEntries);
        _persistOverride = persistOverride;
        Reload();
    }

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void AddEntry(Uri uri, string title)
    {
        if (uri is null)
        {
            return;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        var absoluteUrl = uri.AbsoluteUri;
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? absoluteUrl : title.Trim();
        var nowUtc = DateTime.UtcNow;

        // Merge consecutive duplicate records within 30 seconds.
        if (_entries.Count > 0)
        {
            var latest = _entries[0];
            if (string.Equals(latest.Url, absoluteUrl, StringComparison.OrdinalIgnoreCase) &&
                nowUtc - latest.VisitedAtUtc <= TimeSpan.FromSeconds(30))
            {
                _entries[0] = latest with { Title = normalizedTitle, VisitedAtUtc = nowUtc };
                SchedulePersist();
                return;
            }
        }

        _entries.Insert(0, new HistoryEntry(Guid.NewGuid(), normalizedTitle, absoluteUrl, nowUtc));
        if (_entries.Count > _maxEntries)
        {
            _entries.RemoveRange(_maxEntries, _entries.Count - _maxEntries);
        }

        SchedulePersist();
    }

    public void Clear()
    {
        _entries.Clear();
        _pendingPersist = true;
        FlushPendingChanges();
    }

    public int RemoveEntriesByIds(IEnumerable<Guid> ids)
    {
        var idSet = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();
        if (idSet.Count == 0)
        {
            return 0;
        }

        var removed = _entries.RemoveAll(entry => idSet.Contains(entry.Id));
        if (removed > 0)
        {
            _pendingPersist = true;
            FlushPendingChanges();
        }

        return removed;
    }

    public int RemoveEntriesByHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return 0;
        }

        var removed = _entries.RemoveAll(entry =>
            Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) &&
            uri is not null &&
            string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            _pendingPersist = true;
            FlushPendingChanges();
        }

        return removed;
    }

    /// <summary>中文注释：浏览记录只标记待保存，由主窗口在空闲时合并写盘，避免每次导航都同步写全量 JSON。</summary>
    private void SchedulePersist()
    {
        _pendingPersist = true;
    }

    /// <summary>中文注释：有待保存变更时立即写入；返回 false 表示写盘失败需要重试。</summary>
    public bool FlushPendingChanges(bool force = false)
    {
        if (!_pendingPersist || (!force && DateTime.UtcNow < _nextPersistRetryUtc))
        {
            return true;
        }

        _pendingPersist = false;
        PersistSilently();
        return !_pendingPersist;
    }

    public void Reload()
    {
        _entries.Clear();
        if (!File.Exists(_storagePath))
        {
            return;
        }

        var loaded = Core.JsonFileStore.Load<List<HistoryEntry>>(_storagePath);
        if (loaded is null)
        {
            _log("读取历史记录失败，已回退空列表");
            return;
        }

        _entries.AddRange(loaded
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Url))
            .OrderByDescending(item => item.VisitedAtUtc)
            .Take(_maxEntries));
    }

    private void PersistSilently()
    {
        if (_persistOverride is not null)
        {
            _persistOverride(_entries.ToArray());
            return;
        }

        if (!Core.JsonFileStore.Save(_storagePath, _entries.ToArray(), out var error))
        {
            _pendingPersist = true;
            _nextPersistRetryUtc = DateTime.UtcNow.AddSeconds(5);
            _log($"保存历史记录失败: {error}");
        }
    }
}

