using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Windows_Client.Services
{
    /// <summary>
    /// BCD bootsequence 一次性启动项注入器（对应设计文档 §4.1 / §4.5）。
    /// 从 {current} 复制启动项（自动继承 UEFI/BIOS 平台参数），
    /// 配置 ramdisk 指向硬盘上的 boot.wim + boot.sdi，
    /// 用 /bootsequence 设为一次性启动（只影响下一次重启，不永久污染启动菜单）。
    /// 需要管理员权限。失败时提供 Rollback 回退。
    /// </summary>
    public class BcdInjector
    {
        /// <summary>注入结果</summary>
        public class BcdResult
        {
            public bool Ok { get; set; }
            public string Error { get; set; } = "";
            public string? PeGuid { get; set; }
            public string? BackupPath { get; set; }
        }

        /// <summary>
        /// 注入 ZS PE 一次性启动项。
        /// taskDriveLetterNoColon = 任务盘盘符（不含冒号，如 "D"）。
        /// </summary>
        public BcdResult Inject(string taskDriveLetterNoColon,
            string wimRelPath = "\\ZS_Task\\boot.wim",
            string sdiRelPath = "\\ZS_Task\\boot.sdi")
        {
            try
            {
                // §4.1.1 备份 BCD（保留 7 天）
                var backupPath = Path.Combine(Path.GetTempPath(),
                    "ZS_BCD_Backup_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bcd");
                var (bk, _) = RunBcdEdit("/export \"" + backupPath + "\"");
                if (!bk) return new BcdResult { Error = "BCD 备份失败" };

                // §4.1.2 从 {current} 复制 → 继承平台参数
                var (copyOk, copyOut) = RunBcdEdit("/copy \"{current}\" /d \"ZS 无人值守 PE\"");
                if (!copyOk) return new BcdResult { Error = "bcdedit /copy 失败: " + copyOut, BackupPath = backupPath };

                var guidMatch = Regex.Match(copyOut, @"\{[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\}");
                if (!guidMatch.Success) return new BcdResult { Error = "无法解析 bcdedit /copy 返回的 GUID", BackupPath = backupPath };
                var peGuid = guidMatch.Value;

                // §4.1.3 配置启动项指向 ramdisk
                var ramdiskDev = "ramdisk=[{" + taskDriveLetterNoColon + ":}]" + wimRelPath + ",{ramdiskoptions}";
                var (d1, _) = RunBcdEdit("/set " + peGuid + " device \"" + ramdiskDev + "\"");
                if (!d1) return Fail(peGuid, backupPath, "bcdedit set device 失败");

                var (d2, _) = RunBcdEdit("/set " + peGuid + " osdevice \"" + ramdiskDev + "\"");
                if (!d2) return Fail(peGuid, backupPath, "bcdedit set osdevice 失败");

                var cmds = new[]
                {
                    "/set " + peGuid + " systemroot \\Windows",
                    "/set " + peGuid + " winpe yes",
                    "/set " + peGuid + " detecthal yes",
                };
                foreach (var c in cmds)
                {
                    var (ok, _) = RunBcdEdit(c);
                    if (!ok) return Fail(peGuid, backupPath, "bcdedit " + c + " 失败");
                }

                // §4.1.4 全局 {ramdiskoptions}：指定 boot.sdi 位置（幂等）
                RunBcdEdit("/set {ramdiskoptions} ramdisksdidevice partition={" + taskDriveLetterNoColon + ":}");
                RunBcdEdit("/set {ramdiskoptions} ramdisksdipath \"" + sdiRelPath + "\"");

                // §4.1.5 设为一次性启动项
                var (bs, bsOut) = RunBcdEdit("/bootsequence " + peGuid);
                if (!bs) return Fail(peGuid, backupPath, "bcdedit /bootsequence 失败: " + bsOut);

                return new BcdResult { Ok = true, PeGuid = peGuid, BackupPath = backupPath };
            }
            catch (Exception ex)
            {
                return new BcdResult { Error = "BCD 注入异常: " + ex.Message };
            }
        }

        /// <summary>回退：删除注入的启动项 + 恢复 BCD 备份</summary>
        public bool Rollback(BcdResult result)
        {
            var ok = true;
            // 删除注入的启动项
            if (!string.IsNullOrEmpty(result.PeGuid))
            {
                var (del, _) = RunBcdEdit("/delete " + result.PeGuid);
                ok = ok && del;
            }
            // 恢复 BCD 备份
            if (!string.IsNullOrEmpty(result.BackupPath) && File.Exists(result.BackupPath))
            {
                var (imp, _) = RunBcdEdit("/import \"" + result.BackupPath + "\"");
                ok = ok && imp;
            }
            return ok;
        }

        private BcdResult Fail(string guid, string backup, string err)
            => new BcdResult { Ok = false, Error = err, PeGuid = guid, BackupPath = backup };

        /// <summary>执行 bcdedit 命令，返回 (是否成功, 输出文本)</summary>
        private (bool Ok, string Output) RunBcdEdit(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("bcdedit.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return (false, "");
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return (proc.ExitCode == 0, output);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
