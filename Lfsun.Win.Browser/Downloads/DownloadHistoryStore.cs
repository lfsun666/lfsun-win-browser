using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.Downloads;

/// <summary>
/// Persists finished download records to %LocalAppData%\LfsunBrowser\downloads.json
/// so the download list survives restarts.
/// </summary>
public sealed class DownloadHistoryStore
{
    private const int MaxEntries = 500;
    private readonly string _storagePath;

    public DownloadHistoryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfsunBrowser",
            "downloads.json"))
    {
    }

    public DownloadHistoryStore(string storagePath)
    {
        _storagePath = storagePath;
    }

    public List<DownloadHistoryEntry> Load()
    {
        var loaded = JsonFileStore.Load<List<DownloadHistoryEntry>>(_storagePath) ?? [];
        return loaded
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FilePath))
            .Take(MaxEntries)
            .ToList();
    }

    public void Save(IEnumerable<DownloadItem> items)
    {
        var entries = items
            .Where(item => item.FinishedUtc is not null)
            .Take(MaxEntries)
            .Select(item => new DownloadHistoryEntry
            {
                Id = item.Id,
                SourceUrl = item.SourceUrl,
                FilePath = item.FilePath,
                StartedUtc = item.StartedUtc,
                FinishedUtc = item.FinishedUtc,
                BytesReceived = item.BytesReceived,
                TotalBytes = item.TotalBytes,
                StatusText = item.StatusText
            })
            .ToList();

        JsonFileStore.Save(_storagePath, entries);
    }
}

public sealed class DownloadHistoryEntry
{
    public Guid Id { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public ulong BytesReceived { get; set; }
    public ulong TotalBytes { get; set; }
    public string StatusText { get; set; } = "已完成";
}
