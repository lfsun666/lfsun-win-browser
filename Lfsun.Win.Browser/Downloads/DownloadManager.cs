using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Lfsun.Win.Browser.Downloads;

public sealed class DownloadManager
{
    private string _downloadDirectory;
    private readonly Action<string> _log;
    private readonly DownloadHistoryStore _historyStore;

    public DownloadManager(string downloadDirectory, Action<string> log)
    {
        _downloadDirectory = downloadDirectory;
        _log = log;
        _historyStore = new DownloadHistoryStore();
        Directory.CreateDirectory(_downloadDirectory);
        LoadPersistedHistory();
    }

    public BindingList<DownloadItem> Items { get; } = [];
    public string DownloadDirectory => _downloadDirectory;

    public void AttachTo(CoreWebView2 webView)
    {
        webView.DownloadStarting += (_, args) => Track(args);
    }

    public void OpenDownloadDirectory()
    {
        if (!Directory.Exists(_downloadDirectory))
        {
            Directory.CreateDirectory(_downloadDirectory);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _downloadDirectory,
            UseShellExecute = true
        });
    }

    public bool UpdateDownloadDirectory(string directory, out string error)
    {
        try
        {
            var normalized = directory.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = Core.BrowserConfig.DefaultDownloadDirectory;
            }

            Directory.CreateDirectory(normalized);
            _downloadDirectory = normalized;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public int RemoveByIds(IEnumerable<Guid> ids)
    {
        var idSet = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();
        if (idSet.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (!idSet.Contains(item.Id) || string.Equals(item.StatusText, DownloadStatusText.InProgress, StringComparison.Ordinal))
            {
                continue;
            }

            Items.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            PersistHistory();
        }

        return removed;
    }

    public int RemoveCompletedAndFailed()
    {
        var removed = 0;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (string.Equals(item.StatusText, DownloadStatusText.InProgress, StringComparison.Ordinal))
            {
                continue;
            }

            Items.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            PersistHistory();
        }

        return removed;
    }

    public int ClearAllFinished()
    {
        return RemoveCompletedAndFailed();
    }

    private void LoadPersistedHistory()
    {
        try
        {
            foreach (var entry in _historyStore.Load())
            {
                Items.Add(new DownloadItem(entry));
            }
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warn($"加载下载历史失败: {ex.Message}");
        }
    }

    private void PersistHistory()
    {
        _historyStore.Save(Items);
    }

    private void Track(CoreWebView2DownloadStartingEventArgs args)
    {
        var sourceUri = args.DownloadOperation.Uri;
        var targetPath = BuildTargetPath(sourceUri, args.ResultFilePath);
        args.ResultFilePath = targetPath;

        var item = new DownloadItem(sourceUri, targetPath);
        Items.Insert(0, item);
        _log($"开始下载: {item.FileName}");

        item.UpdateProgress(
            (ulong)Math.Max(0, args.DownloadOperation.BytesReceived),
            args.DownloadOperation.TotalBytesToReceive ?? 0UL);

        args.DownloadOperation.BytesReceivedChanged += (_, _) =>
        {
            item.UpdateProgress(
                (ulong)Math.Max(0, args.DownloadOperation.BytesReceived),
                args.DownloadOperation.TotalBytesToReceive ?? 0UL);
        };

        args.DownloadOperation.StateChanged += (_, _) =>
        {
            switch (args.DownloadOperation.State)
            {
                case CoreWebView2DownloadState.Completed:
                    item.SetCompleted();
                    _log($"下载完成: {item.FileName}");
                    PersistHistory();
                    break;
                case CoreWebView2DownloadState.Interrupted:
                    item.SetInterrupted();
                    _log($"下载失败: {item.FileName}");
                    PersistHistory();
                    break;
                default:
                    if (args.DownloadOperation.InterruptReason ==
                        CoreWebView2DownloadInterruptReason.UserCanceled)
                    {
                        item.SetCanceled();
                        _log($"下载取消: {item.FileName}");
                        PersistHistory();
                    }
                    break;
            }
        };
    }

    private string BuildTargetPath(string sourceUrl, string suggestedPath)
    {
        var fileName = Path.GetFileName(suggestedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            {
                fileName = Path.GetFileName(uri.LocalPath);
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"download-{DateTime.Now:yyyyMMdd-HHmmss}.bin";
        }

        fileName = string.Concat(fileName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"download-{DateTime.Now:yyyyMMdd-HHmmss}.bin";
        }

        var path = Path.Combine(_downloadDirectory, fileName);
        var extension = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        var index = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(_downloadDirectory, $"{stem} ({index}){extension}");
            index++;
        }

        return path;
    }
}

