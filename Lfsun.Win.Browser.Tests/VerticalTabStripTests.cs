using Lfsun.Win.Browser.Browser;
using Lfsun.Win.Browser.UI;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

public class VerticalTabStripTests
{
    [Fact]
    public void VerticalTabStrip_InitialState_ShouldBeExpandedByDefault()
    {
        var strip = new VerticalTabStrip(
            () => Array.Empty<BrowserTab>(),
            () => null,
            _ => { },
            _ => { },
            () => { },
            (_, _) => { });

        Assert.False(strip.IsCollapsed);
        Assert.Equal(220, strip.Width);
    }

    [Fact]
    public void VerticalTabStrip_ToggleCollapse_ShouldUpdateWidthAndRaiseEvent()
    {
        var strip = new VerticalTabStrip(
            () => Array.Empty<BrowserTab>(),
            () => null,
            _ => { },
            _ => { },
            () => { },
            (_, _) => { });

        bool? collapseState = null;
        strip.CollapseChanged += state => collapseState = state;

        strip.IsCollapsed = true;
        Assert.True(strip.IsCollapsed);
        Assert.Equal(48, strip.Width);
        Assert.True(collapseState);

        strip.IsCollapsed = false;
        Assert.False(strip.IsCollapsed);
        Assert.Equal(220, strip.Width);
        Assert.False(collapseState);
    }
}
