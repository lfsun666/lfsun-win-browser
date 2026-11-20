using System.Windows.Forms;
using Lfsun.Win.Browser.Core;

namespace Lfsun.Win.Browser.Shortcuts;

/// <summary>
/// 中文注释：快捷键命令标识（表驱动派发的命令键）。
/// 固定命令与可自定义命令统一编号；命名带 Custom 后缀的表示“同一动作的可自定义版本”
/// （与固定绑定并存：固定绑定在前优先命中，自定义重绑后才会命中 Custom 项）。
/// </summary>
internal static class ShortcutCommands
{
    public const string ToggleMute = "toggleMute";
    public const string ToggleSidePanel = "toggleSidePanel";
    public const string ToggleSplitView = "toggleSplitView";
    public const string ToggleReaderMode = "toggleReaderMode";
    public const string CaptureScreenshot = "captureScreenshot";
    public const string ToggleVerticalTabs = "toggleVerticalTabs";
    public const string OpenCommandPalette = "openCommandPalette";
    public const string OpenTabQuickSwitchPalette = "openTabQuickSwitchPalette";
    public const string ToggleZenMode = "toggleZenMode";
    public const string SaveWorkspaceSnapshot = "saveWorkspaceSnapshot";
    public const string OpenWorkspaceSessions = "openWorkspaceSessions";
    public const string ReopenClosedTab = "reopenClosedTab";
    public const string NewBlankTab = "newBlankTab";
    public const string CloseActiveTab = "closeActiveTab";
    public const string CloseOtherTabs = "closeOtherTabs";
    public const string CloseLeftTabs = "closeLeftTabs";
    public const string CloseRightTabs = "closeRightTabs";
    public const string ReloadActiveTab = "reloadActiveTab";
    public const string GoBack = "goBack";
    public const string GoForward = "goForward";
    public const string MoveActiveTabLeft = "moveActiveTabLeft";
    public const string MoveActiveTabRight = "moveActiveTabRight";
    public const string CycleTabSelection = "cycleTabSelection";
    public const string SelectTabByIndex = "selectTabByIndex";
    public const string FocusAddressBar = "focusAddressBar";
    public const string AddBookmark = "addBookmark";
    public const string NewConfigurableTab = "newConfigurableTab";
    public const string CloseConfigurableTab = "closeConfigurableTab";
    public const string FocusAddressBarCustom = "focusAddressBarCustom";
    public const string RefreshCustom = "refreshCustom";
    public const string OpenDevTools = "openDevTools";
    public const string ToggleBookmarksBar = "toggleBookmarksBar";
    public const string RestoreClosedTabCustom = "restoreClosedTabCustom";
    public const string OpenHistory = "openHistory";
    public const string ShowFindBar = "showFindBar";
    public const string NewPrivateWindow = "newPrivateWindow";
    public const string NewDualWindow = "newDualWindow";
    public const string ViewSource = "viewSource";
    public const string ZoomIn = "zoomIn";
    public const string ZoomOut = "zoomOut";
    public const string ZoomReset = "zoomReset";
}

/// <summary>
/// 中文注释：一条快捷键绑定。固定绑定提供 Matches 谓词（与原 if 分支逐字对应，
/// 保留“某些分支不校验 Alt”等既有宽松语义）；可自定义绑定提供配置取值器，
/// 由路由按 IsShortcutTriggered 语义匹配（开关关闭或配置不可解析时不命中）。
/// </summary>
internal sealed record ShortcutBinding(
    string Command,
    Func<KeyEventArgs, bool>? FixedMatch = null,
    Func<BrowserConfig, bool>? ConfigEnabled = null,
    Func<BrowserConfig, string>? ConfigShortcutValue = null)
{
    public bool IsConfigurable => ConfigShortcutValue is not null;
}

/// <summary>
/// 中文注释：快捷键命令路由表——把 MainWindow 里 366 行 if-else 快捷键链路收敛为有序绑定表。
/// 绑定顺序与原代码分支顺序逐一对齐：首个命中的命令先派发，处理器返回 false（如无活动标签、
/// 切签失败）时继续向后匹配，与原“内层条件不满足则落到下一分支”的行为一致。
/// 已知的顺序性冲突（同键多义，靠顺序消解，改动顺序前先看注释）：
/// - Ctrl+Shift+R：阅读模式在前，“关闭右侧标签”永远不可达（历史遗留，保持原行为）；
/// - Alt+D：分屏在前，地址栏聚焦的 Alt+D 分支不可达；
/// - Ctrl+T/Ctrl+W/Ctrl+L 等：固定绑定先于同默认值的自定义绑定，用户重绑后 Custom 项才生效。
/// </summary>
internal static class ShortcutCommandRouter
{
    private static readonly ShortcutBinding[] Bindings =
    [
        new(ShortcutCommands.ToggleMute, e => e.Control && !e.Shift && !e.Alt && e.KeyCode == Keys.M),
        new(ShortcutCommands.ToggleSidePanel, e => (e.Control && e.Shift && e.KeyCode == Keys.E) || (e.Alt && e.KeyCode == Keys.S)),
        new(ShortcutCommands.ToggleSplitView, e => (e.Alt && e.KeyCode == Keys.D) || (e.Control && e.Alt && e.KeyCode == Keys.D)),
        new(ShortcutCommands.ToggleReaderMode, e => (e.Alt && e.KeyCode == Keys.R) || (e.Control && e.Shift && e.KeyCode == Keys.R)),
        new(ShortcutCommands.CaptureScreenshot, e => e.Control && e.Alt && e.KeyCode == Keys.S),
        new(ShortcutCommands.ToggleVerticalTabs, e => e.Control && e.Shift && e.KeyCode == Keys.V),
        new(ShortcutCommands.OpenCommandPalette, e => e.Control && e.Shift && e.KeyCode == Keys.P),
        new(ShortcutCommands.OpenTabQuickSwitchPalette, e => e.Control && !e.Shift && e.KeyCode == Keys.K),
        new(ShortcutCommands.ToggleZenMode, e => e.Control && e.Shift && e.KeyCode == Keys.Z),
        new(ShortcutCommands.SaveWorkspaceSnapshot, e => e.Control && e.Shift && e.KeyCode == Keys.S),
        new(ShortcutCommands.OpenWorkspaceSessions, e => e.Control && e.Shift && e.KeyCode == Keys.O),
        new(ShortcutCommands.ReopenClosedTab, e => e.Control && e.Shift && e.KeyCode == Keys.T),
        new(ShortcutCommands.NewBlankTab, e => e.Control && !e.Shift && e.KeyCode == Keys.T),
        new(ShortcutCommands.CloseActiveTab, e => e.Control && !e.Shift && e.KeyCode == Keys.W),
        new(ShortcutCommands.CloseOtherTabs, e => e.Control && e.Shift && e.KeyCode == Keys.W),
        new(ShortcutCommands.CloseLeftTabs, e => e.Control && e.Shift && e.KeyCode == Keys.L),
        // 中文注释：此绑定在原代码中位于阅读模式分支之后，Ctrl+Shift+R 永远被前者截获（保持原行为，勿调整顺序）。
        new(ShortcutCommands.CloseRightTabs, e => e.Control && e.Shift && e.KeyCode == Keys.R),
        new(ShortcutCommands.ReloadActiveTab, e => (e.Control && !e.Shift && e.KeyCode == Keys.R) || e.KeyCode == Keys.F5),
        new(ShortcutCommands.GoBack, e => e.Alt && e.KeyCode == Keys.Left),
        new(ShortcutCommands.GoForward, e => e.Alt && e.KeyCode == Keys.Right),
        new(ShortcutCommands.MoveActiveTabLeft, e => e.Control && e.Shift && (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.Left)),
        new(ShortcutCommands.MoveActiveTabRight, e => e.Control && e.Shift && (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Right)),
        // 中文注释：Ctrl+Tab 切签：方向由 Shift 决定；处理器切签失败时返回 false 继续向后匹配。
        new(ShortcutCommands.CycleTabSelection, e => e.Control && e.KeyCode == Keys.Tab),
        // 中文注释：Ctrl+数字（1~9，含小键盘）切签；原分支不校验 Alt，保留该宽松语义。
        new(ShortcutCommands.SelectTabByIndex, e => e.Control && !e.Shift && GetDigitTabIndex(e) >= 0),
        // 中文注释：Alt+D 在原代码中已被分屏分支截获，此处 Alt+D 不可达（保持原行为）。
        new(ShortcutCommands.FocusAddressBar, e => (e.Control && e.KeyCode == Keys.L) || (e.Alt && e.KeyCode == Keys.D)),
        // ---- 可自定义快捷键（配置开关 + 快捷键串）----
        new(ShortcutCommands.AddBookmark,
            ConfigEnabled: c => c.ShortcutAddBookmarkEnabled,
            ConfigShortcutValue: c => c.ShortcutAddBookmark),
        new(ShortcutCommands.NewConfigurableTab,
            ConfigEnabled: c => c.ShortcutNewTabEnabled,
            ConfigShortcutValue: c => c.ShortcutNewTab),
        new(ShortcutCommands.CloseConfigurableTab,
            ConfigEnabled: c => c.ShortcutCloseTabEnabled,
            ConfigShortcutValue: c => c.ShortcutCloseTab),
        new(ShortcutCommands.FocusAddressBarCustom,
            ConfigEnabled: c => c.ShortcutFocusAddressEnabled,
            ConfigShortcutValue: c => c.ShortcutFocusAddress),
        new(ShortcutCommands.RefreshCustom,
            ConfigEnabled: c => c.ShortcutRefreshEnabled,
            ConfigShortcutValue: c => c.ShortcutRefresh),
        new(ShortcutCommands.OpenDevTools,
            ConfigEnabled: c => c.ShortcutDevToolsEnabled,
            ConfigShortcutValue: c => c.ShortcutDevTools),
        new(ShortcutCommands.ToggleBookmarksBar,
            ConfigEnabled: c => c.ShortcutToggleBookmarksBarEnabled,
            ConfigShortcutValue: c => c.ShortcutToggleBookmarksBar),
        new(ShortcutCommands.RestoreClosedTabCustom,
            ConfigEnabled: c => c.ShortcutRestoreClosedTabEnabled,
            ConfigShortcutValue: c => c.ShortcutRestoreClosedTab),
        new(ShortcutCommands.OpenHistory,
            ConfigEnabled: c => c.ShortcutOpenHistoryEnabled,
            ConfigShortcutValue: c => c.ShortcutOpenHistory),
        new(ShortcutCommands.ShowFindBar,
            ConfigEnabled: c => c.ShortcutFindInPageEnabled,
            ConfigShortcutValue: c => c.ShortcutFindInPage),
        // ---- 固定快捷键（后段）----
        new(ShortcutCommands.NewPrivateWindow, e => e.Control && e.Shift && !e.Alt && e.KeyCode == Keys.N),
        new(ShortcutCommands.NewDualWindow, e => e.Control && e.Shift && !e.Alt && e.KeyCode == Keys.M),
        new(ShortcutCommands.ViewSource, e => e.Control && !e.Shift && !e.Alt && e.KeyCode == Keys.U),
        new(ShortcutCommands.ZoomIn, e => e.Control && !e.Alt && !e.Shift && e.KeyCode is Keys.Oemplus or Keys.Add),
        new(ShortcutCommands.ZoomOut, e => e.Control && !e.Alt && !e.Shift && e.KeyCode is Keys.OemMinus or Keys.Subtract),
        new(ShortcutCommands.ZoomReset, e => e.Control && !e.Alt && !e.Shift && e.KeyCode is Keys.D0 or Keys.NumPad0),
    ];

    /// <summary>
    /// 中文注释：按绑定顺序产出命中的命令序列（可能多个，交由调用方逐个执行，
    /// 处理器返回 false 时继续尝试后续命中项——对应原代码“内层条件不满足落到下一分支”）。
    /// </summary>
    public static IEnumerable<string> MatchCommands(BrowserConfig config, KeyEventArgs eventArgs)
    {
        foreach (var binding in Bindings)
        {
            if (binding.FixedMatch is not null)
            {
                if (binding.FixedMatch(eventArgs))
                {
                    yield return binding.Command;
                }
            }
            else if (binding.ConfigEnabled is not null &&
                     binding.ConfigShortcutValue is not null &&
                     IsShortcutTriggered(binding.ConfigEnabled(config), binding.ConfigShortcutValue(config), eventArgs))
            {
                yield return binding.Command;
            }
        }
    }

    /// <summary>
    /// 中文注释：Ctrl+1~9（含小键盘）数字键映射到标签索引（0~8），非数字返回 -1。
    /// </summary>
    public static int GetDigitTabIndex(KeyEventArgs eventArgs)
    {
        return eventArgs.KeyCode switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            Keys.D4 or Keys.NumPad4 => 3,
            Keys.D5 or Keys.NumPad5 => 4,
            Keys.D6 or Keys.NumPad6 => 5,
            Keys.D7 or Keys.NumPad7 => 6,
            Keys.D8 or Keys.NumPad8 => 7,
            Keys.D9 or Keys.NumPad9 => 8,
            _ => -1
        };
    }

    /// <summary>
    /// 中文注释：可自定义快捷键命中判定（自 MainWindow 迁移）：开关开启、配置可解析且与按键完全一致。
    /// </summary>
    internal static bool IsShortcutTriggered(bool enabled, string configuredShortcut, KeyEventArgs eventArgs)
    {
        if (!enabled)
        {
            return false;
        }

        if (!ShortcutParser.TryParse(configuredShortcut, out var expected))
        {
            return false;
        }

        return eventArgs.KeyData == expected;
    }
}
