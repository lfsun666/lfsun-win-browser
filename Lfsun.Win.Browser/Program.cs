using Lfsun.Win.Browser.Core;
using System.Runtime.InteropServices;

namespace Lfsun.Win.Browser;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            if (IsIgnorableRuntimeException(eventArgs.Exception))
            {
                return;
            }

            HandleFatalException(eventArgs.Exception, "UI线程异常");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                if (IsIgnorableRuntimeException(ex))
                {
                    return;
                }

                HandleFatalException(ex, "应用域未处理异常");
                return;
            }

            HandleFatalException(new Exception(eventArgs.ExceptionObject?.ToString() ?? "未知异常"), "应用域未处理异常");
        };

        try
        {
            ApplicationConfiguration.Initialize();
            var launchOptions = AppLaunchOptions.Parse(args);
            CleanupOrphanPrivateProfiles(launchOptions);
            Application.Run(new MainWindow(BrowserConfig.LoadDefault(), launchOptions));
            CleanupPrivateProfile(launchOptions);
        }
        catch (Exception ex)
        {
            HandleFatalException(ex, "启动异常");
        }
    }

    // 中文注释：无痕窗口退出后删除本次临时 profile 目录，WebView2 释放文件锁可能有延迟，少量重试。
    private static void CleanupPrivateProfile(AppLaunchOptions launchOptions)
    {
        if (!launchOptions.IsPrivateWindow)
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(launchOptions.ProfileStoragePath))
                {
                    return;
                }

                Directory.Delete(launchOptions.ProfileStoragePath, recursive: true);
                return;
            }
            catch
            {
                Thread.Sleep(400);
            }
        }
    }

    // 中文注释：启动时清理历史遗留的无痕目录（异常退出未删除），被其它活动窗口占用的目录删除失败会自动跳过。
    private static void CleanupOrphanPrivateProfiles(AppLaunchOptions launchOptions)
    {
        try
        {
            if (!Directory.Exists(AppLaunchOptions.PrivateProfilesRootPath))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(AppLaunchOptions.PrivateProfilesRootPath))
            {
                if (string.Equals(directory, launchOptions.ProfileStoragePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // 中文注释：目录被其它无痕窗口占用时跳过。
                }
            }
        }
        catch
        {
            // 中文注释：清理失败不影响启动。
        }
    }

    private static bool IsIgnorableRuntimeException(Exception ex)
    {
        return ex is COMException comEx && comEx.HResult == unchecked((int)0x80004004);
    }

    private static void HandleFatalException(Exception ex, string phase)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LfsunBrowser",
                "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "startup-error.log");
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{phase}] {ex}\n";
            File.AppendAllText(logPath, content);
        }
        catch
        {
            // Ignore secondary logging failures.
        }

        MessageBox.Show(
            $"浏览器异常：{ex.Message}\n\n详细日志：%LocalAppData%\\LfsunBrowser\\logs\\startup-error.log",
            "Lfsun浏览器",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

