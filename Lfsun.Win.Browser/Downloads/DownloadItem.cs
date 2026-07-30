using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lfsun.Win.Browser.Downloads;

public static class DownloadStatusText
{
    public const string InProgress = "下载中";
    public const string Completed = "已完成";
    public const string Canceled = "已取消";
    public const string Failed = "失败";
}

public sealed class DownloadItem : INotifyPropertyChanged
{
    private ulong _bytesReceived;
    private ulong _totalBytes;
    private string _statusText = DownloadStatusText.InProgress;

    public DownloadItem(string sourceUrl, string filePath)
    {
        Id = Guid.NewGuid();
        SourceUrl = sourceUrl;
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        StartedUtc = DateTime.UtcNow;
    }

    // Restores a finished download record loaded from persisted history.
    public DownloadItem(DownloadHistoryEntry entry)
    {
        Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id;
        SourceUrl = entry.SourceUrl;
        FilePath = entry.FilePath;
        FileName = Path.GetFileName(entry.FilePath);
        StartedUtc = entry.StartedUtc;
        FinishedUtc = entry.FinishedUtc;
        _bytesReceived = entry.BytesReceived;
        _totalBytes = entry.TotalBytes;
        _statusText = string.IsNullOrWhiteSpace(entry.StatusText)
            ? DownloadStatusText.Completed
            : entry.StatusText;
    }

    public Guid Id { get; }
    public string SourceUrl { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public DateTime StartedUtc { get; }
    public DateTime? FinishedUtc { get; private set; }
    public ulong BytesReceived
    {
        get => _bytesReceived;
        private set
        {
            _bytesReceived = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressText));
        }
    }
    public ulong TotalBytes
    {
        get => _totalBytes;
        private set
        {
            _totalBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressText));
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText =>
        TotalBytes > 0
            ? $"{BytesReceived / 1024d / 1024d:F1}MB / {TotalBytes / 1024d / 1024d:F1}MB"
            : $"{BytesReceived / 1024d / 1024d:F1}MB";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateProgress(ulong bytesReceived, ulong totalBytes)
    {
        BytesReceived = bytesReceived;
        if (totalBytes > 0)
        {
            TotalBytes = totalBytes;
        }
    }

    public void SetCompleted()
    {
        StatusText = DownloadStatusText.Completed;
        FinishedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(FinishedUtc));
    }

    public void SetCanceled()
    {
        StatusText = DownloadStatusText.Canceled;
        FinishedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(FinishedUtc));
    }

    public void SetInterrupted()
    {
        StatusText = DownloadStatusText.Failed;
        FinishedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(FinishedUtc));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

