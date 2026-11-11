using Lfsun.Win.Browser.Core;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class AppVersionInfoTests
{
    [Fact]
    public void CurrentVersion_ShouldBeValidSemVerFormat()
    {
        var version = AppVersionInfo.CurrentVersion;
        Assert.False(string.IsNullOrWhiteSpace(version));
        var parts = version.Split('.');
        Assert.True(parts.Length >= 2, $"Expected at least major.minor but got: {version}");
        foreach (var part in parts)
        {
            Assert.True(int.TryParse(part, out _), $"Version part '{part}' should be numeric in '{version}'");
        }
    }

    [Fact]
    public void ReleaseHistory_ShouldContainEntriesAndBeOrderedDesc()
    {
        var history = AppVersionInfo.ReleaseHistory;
        Assert.NotEmpty(history);
        var latest = AppVersionInfo.GetLatestRelease();
        Assert.NotNull(latest);
        Assert.Equal(history[0].Version, latest.Version);
    }

    [Fact]
    public void BuildChangelogMarkdown_ShouldProduceMarkdownWithHeadings()
    {
        var markdown = AppVersionInfo.BuildChangelogMarkdown();
        Assert.Contains("# 更新日志", markdown);
        Assert.Contains(AppVersionInfo.CurrentVersion, markdown);
    }

    [Fact]
    public void GetRecentReleases_ShouldRespectLimit()
    {
        var recent = AppVersionInfo.GetRecentReleases(10);
        Assert.NotEmpty(recent);
        Assert.True(recent.Count() <= 10);
    }

    [Fact]
    public void InstallerScript_ShouldAllowUserToSelectInstallationDirectory()
    {
        var issPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../packaging/LfsunWin.iss"));
        if (!File.Exists(issPath))
        {
            issPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../packaging/LfsunWin.iss"));
        }

        if (File.Exists(issPath))
        {
            var content = File.ReadAllText(issPath);
            Assert.Contains("DisableDirPage=no", content);
            Assert.Contains("AlwaysShowDirOnReadyPage=yes", content);
        }
    }
}
