using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class JsonFileStoreTests : IDisposable
{
    private sealed class Sample
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private readonly string _tempDir;

    public JsonFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LfsunBrowserTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响测试结果
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValue()
    {
        var path = Path.Combine(_tempDir, "sub", "sample.json");

        var saved = JsonFileStore.Save(path, new Sample { Name = "hello", Count = 42 }, out var error);
        Assert.True(saved, error);

        var loaded = JsonFileStore.Load<Sample>(path);
        Assert.NotNull(loaded);
        Assert.Equal("hello", loaded!.Name);
        Assert.Equal(42, loaded.Count);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var path = Path.Combine(_tempDir, "a", "b", "c", "sample.json");

        Assert.True(JsonFileStore.Save(path, new Sample { Name = "x" }));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "missing.json");

        Assert.Null(JsonFileStore.Load<Sample>(path));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNullAndKeepsCorruptBackup()
    {
        var path = Path.Combine(_tempDir, "broken.json");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(path, "{ this is not valid json !!!");

        var loaded = JsonFileStore.Load<Sample>(path);

        Assert.Null(loaded);
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void Save_DoesNotLeaveTmpFileBehind()
    {
        var path = Path.Combine(_tempDir, "clean.json");

        Assert.True(JsonFileStore.Save(path, new Sample { Name = "y" }));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Load_IsCaseInsensitiveOnPropertyNames()
    {
        var path = Path.Combine(_tempDir, "case.json");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(path, "{\"name\":\"lower\",\"count\":7}");

        var loaded = JsonFileStore.Load<Sample>(path);

        Assert.NotNull(loaded);
        Assert.Equal("lower", loaded!.Name);
        Assert.Equal(7, loaded.Count);
    }
}
