using Lfsun.Win.Browser.History;
using Lfsun.Win.Browser.UI;
using Lfsun.Win.Browser.UI.Chrome;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public sealed class StabilityRegressionTests
{
    [Fact]
    public void HistoryVisits_DebounceWrites_AndFlushPersistsOnce()
    {
        var writes = new List<IReadOnlyList<HistoryEntry>>();
        var manager = new HistoryManager(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "history.json"),
            _ => { },
            persistOverride: writes.Add);

        manager.AddEntry(new Uri("https://example.com/1"), "第一页");
        manager.AddEntry(new Uri("https://example.com/2"), "第二页");
        Assert.Empty(writes);
        Assert.Equal(2, manager.Entries.Count);

        manager.FlushPendingChanges();
        manager.FlushPendingChanges();
        var snapshot = Assert.Single(writes);
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("https://example.com/2", snapshot[0].Url);
    }

    [Fact]
    public void HistoryClear_PersistsImmediatelyWithoutFlush()
    {
        var writes = new List<IReadOnlyList<HistoryEntry>>();
        var manager = new HistoryManager(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "history.json"),
            _ => { },
            persistOverride: writes.Add);
        manager.AddEntry(new Uri("https://example.com"), "示例");

        manager.Clear();

        var snapshot = Assert.Single(writes);
        Assert.Empty(snapshot);
        Assert.Empty(manager.Entries);
    }

    [Theory]
    [InlineData(0, 6, 180, 180, 500, 0)]
    [InlineData(200, 6, 180, 180, 500, 97)]
    [InlineData(1200, 6, 180, 180, 500, 500)]
    [InlineData(1200, 6, 180, 180, -10, 180)]
    [InlineData(1200, 6, 180, 180, 2000, 1014)]
    [InlineData(400, 6, 520, 260, 680, 262)]
    [InlineData(100, 6, 0, 0, 200, 94)]
    public void SplitDistance_IsAlwaysWithinAvailableClientArea(
        int width, int splitterWidth, int panel1Min, int panel2Min, int desired, int expected)
    {
        var actual = SplitLayoutHelper.CalculateDistance(width, splitterWidth, panel1Min, panel2Min, desired);
        Assert.Equal(expected, actual);
        Assert.InRange(actual, 0, Math.Max(0, width - splitterWidth));
    }

    [Fact]
    public void SplitContainer_NarrowResize_DoesNotThrowOrUseOutdatedDistance()
    {
        RunOnStaThread(() =>
        {
            using var split = new SplitContainer
            {
                Size = new Size(1000, 400),
                SplitterWidth = 6,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            SplitLayoutHelper.ApplyDistance(split, 180, 180, 500);
            split.Size = new Size(200, 400);

            SplitLayoutHelper.ApplyDistance(split, 180, 180, 500);

            Assert.InRange(split.SplitterDistance, 0, split.ClientSize.Width - split.SplitterWidth);
            Assert.Equal(97, split.SplitterDistance);
        });
    }

    [Fact]
    public void LoadingProgress_DoesNotReverseAfterCompletionFade()
    {
        RunOnStaThread(() =>
        {
            using var strip = new LoadingProgressStrip();
            strip.StopLoading();
            strip.StartLoading();
            var initial = strip.CurrentProgress;
            Assert.Equal(0.04F, initial, precision: 4);

            strip.AdvanceFrame();
            Assert.True(strip.CurrentProgress > initial, "新导航进度应继续向前，不能从 100% 回退。");
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
