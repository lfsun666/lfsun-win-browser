using System.Text;

namespace Lfsun.Win.Browser.Core;

/// <summary>
/// Unified file logger. Writes daily log files under %LocalAppData%\LfsunBrowser\logs
/// and keeps only the most recent days to avoid unbounded growth.
/// </summary>
public static class AppLogger
{
    private const int RetainedDays = 7;
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LfsunBrowser",
        "logs");
    private static bool _cleanupDone;

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) => Write("ERROR", $"{message} | {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                CleanupOldLogsOnce();
                var path = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private static void CleanupOldLogsOnce()
    {
        if (_cleanupDone)
        {
            return;
        }

        _cleanupDone = true;
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-RetainedDays);
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "app-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Cleanup failures are not important enough to surface.
        }
    }
}
