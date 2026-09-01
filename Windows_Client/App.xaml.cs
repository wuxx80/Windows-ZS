using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Windows_Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>工作目录路径 D:\MGZS</summary>
    private static readonly string WorkDir = @"D:\MGZS";
    /// <summary>目标 exe 路径</summary>
    private static readonly string TargetExe = Path.Combine(WorkDir, "ZS_Installer.exe");
    /// <summary>启动异常日志</summary>
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "startup_error.log");

    public App()
    {
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

    protected override void OnStartup(StartupEventArgs e)
    {
        // 首次运行：自动部署到 D:\MGZS
        if (!IsRunningFromWorkDir())
        {
            SelfDeploy();
            return; // SelfDeploy 会启动新实例并退出当前进程
        }
        base.OnStartup(e);
    }

    /// <summary>判断是否已从工作目录运行</summary>
    private static bool IsRunningFromWorkDir()
    {
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe)) return false;
        return string.Equals(currentExe, TargetExe, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>自部署：复制文件到 D:\MGZS，创建快捷方式，启动新实例</summary>
    private static void SelfDeploy()
    {
        try
        {
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe))
            {
                MessageBox.Show("无法获取当前程序路径", "部署失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(-1);
                return;
            }

            // 创建 D:\MGZS 目录
            Directory.CreateDirectory(WorkDir);

            // 复制自身到 D:\MGZS\ZS_Installer.exe
            File.Copy(currentExe, TargetExe, overwrite: true);

            // 复制 Data 目录（工具数据等）
            var srcData = Path.Combine(AppContext.BaseDirectory, "Data");
            var dstData = Path.Combine(WorkDir, "Data");
            if (Directory.Exists(srcData))
            {
                if (Directory.Exists(dstData)) Directory.Delete(dstData, recursive: true);
                CopyDirectory(srcData, dstData);
            }

            // 创建桌面快捷方式
            CreateDesktopShortcut();

            // 启动新实例
            var psi = new ProcessStartInfo
            {
                FileName = TargetExe,
                UseShellExecute = true,
                WorkingDirectory = WorkDir
            };
            Process.Start(psi);

            // 退出当前实例
            Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            LogException(ex);
            MessageBox.Show($"部署失败: {ex.Message}\n\n请手动将程序复制到 D:\\MGZS 目录运行。",
                "部署失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown(-1);
        }
    }

    /// <summary>创建桌面快捷方式</summary>
    private static void CreateDesktopShortcut()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "ZS 装机助手.lnk");

        try
        {
            // 使用 WScript.Shell COM 对象创建快捷方式
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            var shell = Activator.CreateInstance(shellType);
            if (shell == null) return;
            var shortcut = shellType.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            if (shortcut == null) { Marshal.ReleaseComObject(shell); return; }

            shellType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { TargetExe });
            shellType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { WorkDir });
            shellType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "ZS 装机助手 - 一键安装系统" });
            shellType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);

            Marshal.ReleaseComObject(shortcut);
            Marshal.ReleaseComObject(shell);
        }
        catch
        {
            // 快捷方式创建失败不影响主流程
        }
    }

    /// <summary>递归复制目录</summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetDirectoryName(dir) ?? "");
            CopyDirectory(dir, dest);
        }
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