using System.Windows.Forms;
using Lfsun.Win.Browser.Core;
using Lfsun.Win.Browser.Shortcuts;
using Xunit;

namespace Lfsun.Win.Browser.Tests;

/// <summary>
/// 中文注释：快捷键命令路由表回归测试——锁死绑定顺序语义（同键多义靠顺序消解）与
/// 可自定义快捷键的开关/解析规则；重构 HandleShortcutAsync if-else 链时靠这些用例守住行为不漂移。
/// </summary>
public sealed class ShortcutCommandTableTests
{
    private static BrowserConfig CreateConfig() => new();

    private static List<string> Match(Keys keyData, BrowserConfig? config = null)
    {
        var args = new KeyEventArgs(keyData);
        return ShortcutCommandRouter.MatchCommands(config ?? CreateConfig(), args).ToList();
    }

    [Fact]
    public void CtrlShiftR_RoutesToReaderMode_BeforeCloseRightTabs()
    {
        // 中文注释：历史遗留顺序冲突——阅读模式分支在前，关闭右侧标签的 Ctrl+Shift+R 永远不可达。
        var commands = Match(Keys.Control | Keys.Shift | Keys.R);

        Assert.Equal(ShortcutCommands.ToggleReaderMode, commands[0]);
        Assert.Contains(ShortcutCommands.CloseRightTabs, commands); // 命中但排在后面，永远不会执行到。
    }

    [Fact]
    public void AltD_RoutesToSplitView_BeforeFocusAddressBar()
    {
        var commands = Match(Keys.Alt | Keys.D);

        Assert.Equal(ShortcutCommands.ToggleSplitView, commands[0]);
        Assert.Contains(ShortcutCommands.FocusAddressBar, commands);
    }

    [Fact]
    public void CtrlT_FixedNewBlankTab_WinsOverConfigurableDefault()
    {
        // 中文注释：默认配置下 Ctrl+T 同时命中固定“空白新标签”与自定义“新标签（启动页）”，固定项在前。
        var commands = Match(Keys.Control | Keys.T);

        Assert.Equal(ShortcutCommands.NewBlankTab, commands[0]);
        Assert.Contains(ShortcutCommands.NewConfigurableTab, commands);
    }

    [Fact]
    public void RebindableShortcut_TakesEffectWhenDefaultRebound()
    {
        // 中文注释：用户把“新建标签”重绑到 F9 后，Ctrl+T 只剩固定绑定，F9 命中自定义绑定。
        var config = CreateConfig();
        config.ShortcutNewTab = "F9";

        Assert.Contains(ShortcutCommands.NewConfigurableTab, Match(Keys.F9, config));
        Assert.DoesNotContain(ShortcutCommands.NewConfigurableTab, Match(Keys.Control | Keys.T, config));
    }

    [Theory]
    [InlineData(Keys.Control | Keys.M, ShortcutCommands.ToggleMute)]
    [InlineData(Keys.Control | Keys.Shift | Keys.E, ShortcutCommands.ToggleSidePanel)]
    [InlineData(Keys.Alt | Keys.S, ShortcutCommands.ToggleSidePanel)]
    [InlineData(Keys.Control | Keys.Alt | Keys.D, ShortcutCommands.ToggleSplitView)]
    [InlineData(Keys.Alt | Keys.R, ShortcutCommands.ToggleReaderMode)]
    [InlineData(Keys.Control | Keys.Shift | Keys.V, ShortcutCommands.ToggleVerticalTabs)]
    [InlineData(Keys.Control | Keys.Shift | Keys.P, ShortcutCommands.OpenCommandPalette)]
    [InlineData(Keys.Control | Keys.K, ShortcutCommands.OpenTabQuickSwitchPalette)]
    [InlineData(Keys.Control | Keys.Shift | Keys.Z, ShortcutCommands.ToggleZenMode)]
    [InlineData(Keys.Control | Keys.Shift | Keys.S, ShortcutCommands.SaveWorkspaceSnapshot)]
    [InlineData(Keys.Control | Keys.Shift | Keys.O, ShortcutCommands.OpenWorkspaceSessions)]
    [InlineData(Keys.Control | Keys.Shift | Keys.T, ShortcutCommands.ReopenClosedTab)]
    [InlineData(Keys.Control | Keys.W, ShortcutCommands.CloseActiveTab)]
    [InlineData(Keys.Control | Keys.Shift | Keys.W, ShortcutCommands.CloseOtherTabs)]
    [InlineData(Keys.Control | Keys.Shift | Keys.L, ShortcutCommands.CloseLeftTabs)]
    [InlineData(Keys.Control | Keys.R, ShortcutCommands.ReloadActiveTab)]
    [InlineData(Keys.F5, ShortcutCommands.ReloadActiveTab)]
    [InlineData(Keys.Alt | Keys.Left, ShortcutCommands.GoBack)]
    [InlineData(Keys.Alt | Keys.Right, ShortcutCommands.GoForward)]
    [InlineData(Keys.Control | Keys.Shift | Keys.PageUp, ShortcutCommands.MoveActiveTabLeft)]
    [InlineData(Keys.Control | Keys.Shift | Keys.Left, ShortcutCommands.MoveActiveTabLeft)]
    [InlineData(Keys.Control | Keys.Shift | Keys.PageDown, ShortcutCommands.MoveActiveTabRight)]
    [InlineData(Keys.Control | Keys.Shift | Keys.Right, ShortcutCommands.MoveActiveTabRight)]
    [InlineData(Keys.Control | Keys.Tab, ShortcutCommands.CycleTabSelection)]
    [InlineData(Keys.Control | Keys.L, ShortcutCommands.FocusAddressBar)]
    [InlineData(Keys.Control | Keys.Shift | Keys.N, ShortcutCommands.NewPrivateWindow)]
    [InlineData(Keys.Control | Keys.Shift | Keys.M, ShortcutCommands.NewDualWindow)]
    [InlineData(Keys.Control | Keys.U, ShortcutCommands.ViewSource)]
    [InlineData(Keys.Control | Keys.Oemplus, ShortcutCommands.ZoomIn)]
    [InlineData(Keys.Control | Keys.Add, ShortcutCommands.ZoomIn)]
    [InlineData(Keys.Control | Keys.OemMinus, ShortcutCommands.ZoomOut)]
    [InlineData(Keys.Control | Keys.Subtract, ShortcutCommands.ZoomOut)]
    [InlineData(Keys.Control | Keys.D0, ShortcutCommands.ZoomReset)]
    [InlineData(Keys.Control | Keys.NumPad0, ShortcutCommands.ZoomReset)]
    public void FixedBindings_RouteToExpectedCommands(Keys keyData, string expectedCommand)
    {
        var commands = Match(keyData);
        Assert.Equal(expectedCommand, commands[0]);
    }

    [Fact]
    public void CtrlAltS_RoutesToSidePanel_BeforeScreenshotAndSnapshot()
    {
        // 中文注释：原实现顺序——侧边栏分支（Alt+S，不校验 Ctrl）先于截图（Ctrl+Alt+S）与快照（Ctrl+Shift+S），
        // 因此带任意附加修饰键的 S 都先命中侧边栏；表驱动忠实保留该优先级。
        var commands = Match(Keys.Control | Keys.Alt | Keys.S);

        Assert.Equal(ShortcutCommands.ToggleSidePanel, commands[0]);
        Assert.Contains(ShortcutCommands.CaptureScreenshot, commands);

        var withShift = Match(Keys.Control | Keys.Shift | Keys.Alt | Keys.S);
        Assert.Equal(ShortcutCommands.ToggleSidePanel, withShift[0]);
        Assert.Contains(ShortcutCommands.CaptureScreenshot, withShift);
        Assert.Contains(ShortcutCommands.SaveWorkspaceSnapshot, withShift);
    }

    [Fact]
    public void CtrlDigit_RoutesToSelectTabByIndex()
    {
        // 中文注释：Ctrl+数字（含小键盘）切签；原分支不校验 Alt。
        Assert.Equal(ShortcutCommands.SelectTabByIndex, Match(Keys.Control | Keys.D3)[0]);
        Assert.Equal(ShortcutCommands.SelectTabByIndex, Match(Keys.Control | Keys.NumPad9)[0]);
        Assert.Equal(ShortcutCommands.SelectTabByIndex, Match(Keys.Control | Keys.Alt | Keys.D1)[0]);
    }

    [Fact]
    public void GetDigitTabIndex_MapsDigitsAndRejectsOthers()
    {
        var d3 = new KeyEventArgs(Keys.Control | Keys.D3);
        var numpad9 = new KeyEventArgs(Keys.Control | Keys.NumPad9);
        var letterA = new KeyEventArgs(Keys.Control | Keys.A);

        Assert.Equal(2, ShortcutCommandRouter.GetDigitTabIndex(d3));
        Assert.Equal(8, ShortcutCommandRouter.GetDigitTabIndex(numpad9));
        Assert.Equal(-1, ShortcutCommandRouter.GetDigitTabIndex(letterA));
    }

    [Fact]
    public void ConfigurableShortcut_DisabledOrUnparsable_DoesNotMatch()
    {
        var config = CreateConfig();
        config.ShortcutOpenHistoryEnabled = false;

        Assert.DoesNotContain(ShortcutCommands.OpenHistory, Match(Keys.Control | Keys.H, config));

        var config2 = CreateConfig();
        config2.ShortcutOpenHistory = "not a valid key!!";
        Assert.DoesNotContain(ShortcutCommands.OpenHistory, Match(Keys.Control | Keys.H, config2));
    }

    [Fact]
    public void ConfigurableShortcut_EnabledWithValidBinding_Matches()
    {
        var config = CreateConfig();
        Assert.Contains(ShortcutCommands.OpenHistory, Match(Keys.Control | Keys.H, config));
        Assert.Contains(ShortcutCommands.ShowFindBar, Match(Keys.Control | Keys.F, config));
    }

    [Fact]
    public void FixedReload_WinsEvenWhenRefreshCustomDisabled()
    {
        // 中文注释：F5 是固定刷新绑定，即使自定义刷新开关关闭也命中（与原实现一致）。
        var config = CreateConfig();
        config.ShortcutRefreshEnabled = false;

        Assert.Equal(ShortcutCommands.ReloadActiveTab, Match(Keys.F5, config)[0]);
    }

    [Fact]
    public void UnrelatedKey_ProducesNoCommands()
    {
        Assert.Empty(Match(Keys.A));
        Assert.Empty(Match(Keys.F13));
    }
}
