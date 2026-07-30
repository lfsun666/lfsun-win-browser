namespace Lfsun.Win.Browser.Core;

public sealed class SessionLaunchPayloadStore
{
    private readonly string _payloadDirectory;
    private readonly Action<string> _log;

    public SessionLaunchPayloadStore(string payloadDirectory, Action<string> log)
    {
        _payloadDirectory = payloadDirectory;
        _log = log;
    }

    public string? Save(BrowserSessionState session)
    {
        try
        {
            var normalizedTabs = session.Tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (normalizedTabs.Count == 0)
            {
                return null;
            }

            Directory.CreateDirectory(_payloadDirectory);
            CleanupExpiredPayloads();

            var payloadPath = Path.Combine(_payloadDirectory, $"{Guid.NewGuid():N}.json");
            var normalizedSession = new BrowserSessionState
            {
                SavedAtUtc = DateTime.UtcNow,
                ActiveIndex = Math.Clamp(session.ActiveIndex, 0, normalizedTabs.Count - 1),
                ExitedCleanly = true,
                Tabs = normalizedTabs
            };

            if (!JsonFileStore.Save(payloadPath, normalizedSession, out var error))
            {
                _log($"写入启动会话载荷失败: {error}");
                return null;
            }

            return payloadPath;
        }
        catch (Exception ex)
        {
            _log($"写入启动会话载荷失败: {ex.Message}");
            return null;
        }
    }

    public BrowserSessionState? LoadAndDelete(string payloadPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payloadPath) || !File.Exists(payloadPath))
            {
                return null;
            }

            var session = JsonFileStore.Load<BrowserSessionState>(payloadPath);
            File.Delete(payloadPath);
            if (session is null || session.Tabs.Count == 0)
            {
                return null;
            }

            var tabs = session.Tabs
                .Where(tab => Uri.TryCreate(tab.Url, UriKind.Absolute, out _))
                .ToList();
            if (tabs.Count == 0)
            {
                return null;
            }

            return new BrowserSessionState
            {
                SavedAtUtc = session.SavedAtUtc,
                ActiveIndex = Math.Clamp(session.ActiveIndex, 0, tabs.Count - 1),
                ExitedCleanly = true,
                Tabs = tabs
            };
        }
        catch (Exception ex)
        {
            _log($"读取启动会话载荷失败: {ex.Message}");
            return null;
        }
    }

    private void CleanupExpiredPayloads()
    {
        try
        {
            if (!Directory.Exists(_payloadDirectory))
            {
                return;
            }

            var threshold = DateTime.UtcNow.AddHours(-12);
            foreach (var filePath in Directory.EnumerateFiles(_payloadDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(filePath) < threshold)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Ignore stale cleanup failures.
                }
            }
        }
        catch
        {
            // Ignore cleanup failures for launch payloads.
        }
    }
}

