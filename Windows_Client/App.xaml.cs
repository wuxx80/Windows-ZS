using System;
using System.IO;
using System.Windows;

namespace Windows_Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>启动异常日志（排查启动崩溃时记录到日志文件）</summary>
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "startup_error.log");

    public App()
    {
        // 捕获启动阶段未处理异常，写入日志便于排查
        DispatcherUnhandledException += (_, e) =>
        {
            LogException(e.Exception);
            e.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) LogException(ex);
        };
    }

    private static void LogException(Exception ex)
    {
        try
        {
            File.WriteAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n"
                + ex.ToString());
        }
        catch
        {
            // 日志写入失败不影响主流程
        }
    }
}
