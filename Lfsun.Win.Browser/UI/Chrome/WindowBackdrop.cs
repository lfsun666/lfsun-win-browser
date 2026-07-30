using System.Runtime.InteropServices;

namespace Lfsun.Win.Browser.UI.Chrome;

/// <summary>
/// 中文注释：窗口系统背景效果（Win11 Mica）与沉浸式深色标题栏。
/// Win10 或调用失败时静默回退，保持现有纯色背景不受影响。
/// </summary>
public static class WindowBackdrop
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE：Win10 20H1 前的旧编号为 19，之后为 20。
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    // DWMWA_SYSTEMBACKDROP_TYPE：仅 Win11 22H2+ 支持。
    private const int DwmwaSystemBackdropType = 38;
    // DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW = Mica。
    private const int SystemBackdropMica = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// 中文注释：按当前主题应用 Mica 背景与深色标题栏；任何失败都被忽略。
    /// </summary>
    public static void Apply(IWin32Window window, bool isDark)
    {
        if (window.Handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var darkFlag = isDark ? 1 : 0;
            if (DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkMode, ref darkFlag, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkModeLegacy, ref darkFlag, sizeof(int));
            }

            var backdrop = SystemBackdropMica;
            DwmSetWindowAttribute(window.Handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch
        {
            // 中文注释：Win10 无此属性或 dwmapi 不可用时直接回退纯色背景。
        }
    }
}
