using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    /// <summary>
    /// SetupComplete.cmd 模板渲染器 —— 设计文档 §6.6 Step 6b。
    /// 由 PE 端 ZS_Agent 在分区/部署/驱动注入后调用，渲染并写入新系统的
    /// C:\Windows\Setup\Scripts\SetupComplete.cmd，新系统首次启动时由 SYSTEM 权限执行，
    /// 完成"静默装软件 + 系统优化 + 可选清理 ZS_Task + 自毁"。
    /// </summary>
    public static class SetupCompleteBuilder
    {
        // 软件目录固定位于新系统的 C:\Windows\Setup\Scripts\software\（已由 §6.6 Step 6a 复制到位）
        private const string SoftwareTargetRoot = @"C:\Windows\Setup\Scripts\software";

        /// <summary>
        /// 渲染 SetupComplete.cmd 内容。
        /// </summary>
        /// <param name="task">解析后的 task.ini 模型（提供 Software 列表 + Optimize 选项 + Meta.FirstBootCleanup）</param>
        /// <param name="taskRoot">ZS_Task 目录绝对路径（用于 FirstBootCleanup 时删除整个 ZS_Task 目录）</param>
        public static string Build(TaskIni task, string taskRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal EnableExtensions");
            sb.AppendLine();
            // 日志固定写到 C:\ProgramData\ZS\first_boot.log
            sb.AppendLine("set LOG=C:\\ProgramData\\ZS\\first_boot.log");
            sb.AppendLine("if not exist \"C:\\ProgramData\\ZS\" md \"C:\\ProgramData\\ZS\"");
            sb.AppendLine("echo %date% %time% ZS SetupComplete 开始 >>%LOG%");
            sb.AppendLine();

            // === 静默安装软件（从 task.ini 逐个生成 start /wait 行）===
            if (task.Software != null && task.Software.Count > 0)
            {
                sb.AppendLine(":: === 静默安装软件 ===");
                foreach (var sw in task.Software)
                {
                    var line = BuildSoftwareLine(sw);
                    if (!string.IsNullOrEmpty(line))
                        sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            // === 系统优化 ===
            sb.AppendLine(":: === 系统优化 ===");
            AppendOptimizeLines(sb, task.Optimize);
            sb.AppendLine();

            // === ZS_Task 清理策略（默认保留）===
            // 设计 §6.6：if exist D:\ZS_Task RD /S /Q D:\ZS_Task
            if (task.Meta != null && task.Meta.FirstBootCleanup
                && !string.IsNullOrEmpty(taskRoot))
            {
                sb.AppendLine(":: === ZS_Task 清理策略（task.ini meta.first_boot_cleanup=yes）===");
                var cleanRoot = taskRoot.TrimEnd('\\');
                sb.AppendLine("if exist \"" + cleanRoot + "\" RD /S /Q \"" + cleanRoot + "\" >>%LOG% 2>&1");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(":: === ZS_Task 保留（task.ini meta.first_boot_cleanup=no）===");
                sb.AppendLine();
            }

            // === 自毁：SetupComplete.cmd 只允许跑一次 ===
            sb.AppendLine(":: === 自毁：SetupComplete.cmd 只允许跑一次 ===");
            sb.AppendLine("echo %date% %time% ZS SetupComplete 完成，自毁。 >>%LOG%");
            sb.AppendLine("del \"%~f0\"");
            sb.AppendLine("endlocal");
            sb.Append("exit /b 0");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// 便捷重载：直接写入到目标路径（通常为 &lt;targetDrive&gt;\Windows\Setup\Scripts\SetupComplete.cmd）。
        /// 返回写入的文件绝对路径。
        /// </summary>
        public static string WriteToSystem(TaskIni task, string targetDrive, string taskRoot)
        {
            var drive = targetDrive.TrimEnd('\\');
            var scriptsDir = Path.Combine(drive + "\\", "Windows", "Setup", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            var path = Path.Combine(scriptsDir, "SetupComplete.cmd");
            File.WriteAllText(path, Build(task, taskRoot), new UTF8Encoding(false));
            return path;
        }

        // —— 单个软件包生成 start /wait 行 ——
        // MSI: start "" /wait msiexec /i "<path>" /qn /norestart >>%LOG% 2>&1
        // EXE: start "" /wait "<path>" <args> >>%LOG% 2>&1
        private static string BuildSoftwareLine(SoftwareEntry sw)
        {
            if (sw == null) return "";
            var installerFile = sw.InstallerFile;
            if (string.IsNullOrEmpty(installerFile)) return "";

            // 软件绝对路径：C:\Windows\Setup\Scripts\software\<Key>\<InstallerFile>
            var keyDir = !string.IsNullOrEmpty(sw.Key) ? sw.Key : "misc";
            var absPath = Path.Combine(SoftwareTargetRoot, keyDir, installerFile);
            var quotedPath = "\"" + absPath + "\"";

            // MSI 走 msiexec，否则直接调 EXE
            if (!string.IsNullOrEmpty(sw.Msi))
            {
                // MSI 静默安装：msiexec /i "<path>" /qn /norestart
                // 若 SoftwareEntry.Args 非空，附加用户指定参数
                var msiArgs = string.IsNullOrEmpty(sw.Args) ? "" : " " + sw.Args;
                return "start \"\" /wait msiexec /i " + quotedPath + " /qn /norestart"
                    + msiArgs + " >>%LOG% 2>&1";
            }
            // EXE 直接调，把用户 Args 拼在后面
            var exeArgs = string.IsNullOrEmpty(sw.Args) ? "" : " " + sw.Args;
            return "start \"\" /wait " + quotedPath + exeArgs + " >>%LOG% 2>&1";
        }

        // —— 系统优化命令 ——
        // 对应设计 §6.6 模板：
        //   powercfg /change standby-timeout-ac 0
        //   powercfg /change standby-timeout-dc 30
        //   powercfg /h off
        // 扩展自 TaskIni.Optimize 字段：
        //   Hibernation / StandbyTimeoutAc / StandbyTimeoutDc / DisableTelemetry / RemoveCortana
        private static void AppendOptimizeLines(StringBuilder sb, OptimizeOptions opt)
        {
            if (opt == null)
            {
                sb.AppendLine(":: optimize 选项为空，跳过系统优化");
                return;
            }

            // 待机超时
            sb.AppendLine("powercfg /change standby-timeout-ac " + opt.StandbyTimeoutAc + " >>%LOG% 2>&1");
            sb.AppendLine("powercfg /change standby-timeout-dc " + opt.StandbyTimeoutDc + " >>%LOG% 2>&1");

            // 关闭休眠（Hibernation=off 时执行）
            if (!string.IsNullOrEmpty(opt.Hibernation)
                && opt.Hibernation.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("powercfg /h off >>%LOG% 2>&1");
            }

            // 禁用遥测
            if (opt.DisableTelemetry)
            {
                sb.AppendLine("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f >>%LOG% 2>&1");
            }

            // 移除 Cortana（Win10 适用，Win11 Cortana 已移除，命令不会报错也不会有效果）
            if (opt.RemoveCortana)
            {
                sb.AppendLine("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f >>%LOG% 2>&1");
            }
        }
    }
}
