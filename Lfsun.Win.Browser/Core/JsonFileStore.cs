using System.Text.Json;

namespace Lfsun.Win.Browser.Core;

/// <summary>
/// Shared JSON persistence helper. Uses atomic write (tmp + replace) to avoid
/// corrupting files on crash, and keeps a .corrupt backup when a file fails to parse.
/// </summary>
public static class JsonFileStore
{
    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads and deserializes a JSON file. Returns default(T) when the file is
    /// missing or unreadable; a corrupt file is renamed to *.corrupt for diagnosis.
    /// </summary>
    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"读取 JSON 失败: {path}", ex);
            TryBackupCorruptFile(path);
            return null;
        }
    }

    /// <summary>
    /// Serializes and saves atomically: write to a temp file, then replace the target.
    /// </summary>
    public static bool Save<T>(string path, T value, out string error)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(value, WriteOptions);
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"写入 JSON 失败: {path}", ex);
            error = ex.Message;
            return false;
        }
    }

    public static bool Save<T>(string path, T value) => Save(path, value, out _);

    private static void TryBackupCorruptFile(string path)
    {
        try
        {
            File.Copy(path, path + ".corrupt", overwrite: true);
        }
        catch
        {
            // Backup is best-effort only.
        }
    }
}
