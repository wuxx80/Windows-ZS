using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WinPE_Client.Services;

namespace WinPE_Client.Services
{
    /// <summary>
    /// 分区脚本尾端验证 —— 设计文档 §6.1。
    /// Agent 执行完 diskpart /s 脚本后，必须解析 stdout 做一次结果验证：
    ///  · GPT 分支：detail partition 输出中必须包含 `Type : System` 或 `EFI` 字样，
    ///              且 list volume 中必须有卷标为 `ESP`、文件系统为 `FAT`/`FAT32` 的一行。
    ///  · MBR 分支：detail partition 输出中必须包含 `Active: Yes`（或本地化后的 `活动: 是` / `Aktiv: Ja`）。
    /// 任何一条不满足 → 即使 diskpart 自己的 ExitCode=0，也要当作失败处理，
    /// 由调用方决定是否 Environment.Exit(3) 回退 F4 救援。
    /// </summary>
    public static class PartitionVerifier
    {
        public enum FirmwareType { Unknown, Gpt, Mbr }

        public sealed class VerifyResult
        {
            /// <summary>是否通过验证（GPT：ESP+FAT32 均满足；MBR：Active=Yes）</summary>
            public bool Pass { get; set; }
            /// <summary>失败原因（Pass=false 时非空）</summary>
            public string Reason { get; set; } = "";
            /// <summary>detail partition 原始输出（用于审计日志）</summary>
            public string DetailOutput { get; set; } = "";
            /// <summary>list volume 原始输出（用于审计日志）</summary>
            public string VolumeOutput { get; set; } = "";
            /// <summary>验证的固件类型（Gpt/Mbr）</summary>
            public FirmwareType VerifiedType { get; set; } = FirmwareType.Unknown;
            /// <summary>ESP/Active 分区卷标（验证命中后非空）</summary>
            public string BootLabel { get; set; } = "";
            /// <summary>ESP 文件系统类型（GPT 分支命中后非空，如 FAT32）</summary>
            public string BootFileSystem { get; set; } = "";
        }

        /// <summary>
        /// 综合验证入口：按 FirmwareType 分流到 GPT 或 MBR 验证。
        /// </summary>
        /// <param name="diskIndex">目标磁盘序号（task.ini.target_disk.index）</param>
        /// <param name="type">固件类型（来自 FirmwareDetector.Detect().Type）</param>
        /// <param name="espPartitionIndex">ESP/Active 分区序号，默认 1（diskpart clean 后第一个 create 的分区）</param>
        public static VerifyResult Verify(int diskIndex, FirmwareDetector.FirmwareType type, int espPartitionIndex = 1)
        {
            if (type == FirmwareDetector.FirmwareType.Uefi)
                return VerifyGpt(diskIndex, espPartitionIndex);
            if (type == FirmwareDetector.FirmwareType.Bios)
                return VerifyMbr(diskIndex, espPartitionIndex);

            return new VerifyResult
            {
                Pass = false,
                Reason = "未知的固件类型，无法选择分区验证策略",
                VerifiedType = FirmwareType.Unknown,
            };
        }

        // —— GPT 分支验证：ESP 分区存在 + 文件系统 FAT/FAT32 ——
        // 设计 §6.1 GPT 尾端验证：
        //   detail partition 输出中必须包含 `Type : System` 或直接出现 `EFI` 字样
        //   list volume 中必须有卷标为 `ESP`、文件系统为 `FAT`/`FAT32` 的一行
        public static VerifyResult VerifyGpt(int diskIndex, int espPartitionIndex = 1)
        {
            var result = RunDiskpartDetail(diskIndex, espPartitionIndex);
            var r = new VerifyResult
            {
                DetailOutput = result.Detail,
                VolumeOutput = result.Volume,
                VerifiedType = FirmwareType.Gpt,
            };

            // 1) detail partition 必须含 "Type : System" 或 "EFI"
            bool detailOk = result.Detail.Contains("Type : System", StringComparison.OrdinalIgnoreCase)
                        || result.Detail.Contains("EFI", StringComparison.OrdinalIgnoreCase);
            if (!detailOk)
            {
                r.Pass = false;
                r.Reason = "detail partition 输出未出现 'Type : System' 或 'EFI'，ESP 分区可能未正确创建";
                return r;
            }

            // 2) list volume 必须有一行卷标为 ESP 且文件系统为 FAT/FAT32
            var volumeLine = result.Volume
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.Contains("ESP", StringComparison.OrdinalIgnoreCase));

            if (volumeLine == null)
            {
                r.Pass = false;
                r.Reason = "list volume 中未找到卷标为 'ESP' 的卷，ESP 分区可能未分配盘符或卷标错误";
                return r;
            }

            // 文件系统字段：FAT / FAT32（兼容大小写）
            bool fsOk = volumeLine.Contains("FAT", StringComparison.OrdinalIgnoreCase);
            if (!fsOk)
            {
                r.Pass = false;
                r.Reason = "ESP 卷文件系统不是 FAT/FAT32（实际行: " + volumeLine.Trim() + "），UEFI 引导会失败";
                return r;
            }

            r.Pass = true;
            r.BootLabel = "ESP";
            r.BootFileSystem = volumeLine.Contains("FAT32", StringComparison.OrdinalIgnoreCase) ? "FAT32" : "FAT";
            return r;
        }

        // —— MBR 分支验证：分区1被标记为 active ——
        // 设计 §6.1 MBR 尾端验证：
        //   detail partition 输出中必须包含 `Active: Yes`
        //   （或本地化后的 `活动: 是` / `Aktiv: Ja`）
        public static VerifyResult VerifyMbr(int diskIndex, int activePartitionIndex = 1)
        {
            var result = RunDiskpartDetail(diskIndex, activePartitionIndex);
            var r = new VerifyResult
            {
                DetailOutput = result.Detail,
                VolumeOutput = result.Volume,
                VerifiedType = FirmwareType.Mbr,
            };

            // 支持多语言：Active: Yes / 活动: 是 / Aktiv: Ja
            bool activeOk = result.Detail.Contains("Active: Yes", StringComparison.OrdinalIgnoreCase)
                        || result.Detail.Contains("Active:  Yes", StringComparison.OrdinalIgnoreCase) // 双空格容错
                        || result.Detail.Contains("活动: 是", StringComparison.Ordinal)
                        || result.Detail.Contains("活动:是", StringComparison.Ordinal)
                        || result.Detail.Contains("Aktiv: Ja", StringComparison.OrdinalIgnoreCase);

            if (!activeOk)
            {
                r.Pass = false;
                r.Reason = "detail partition 输出未出现 'Active: Yes'（或本地化 '活动: 是'），分区未被标记为活动，BIOS 无法引导";
                return r;
            }

            r.Pass = true;
            r.BootLabel = "System Reserved";
            return r;
        }

        // —— diskpart detail partition + list volume 一次性执行 ——
        private static (string Detail, string Volume) RunDiskpartDetail(int diskIndex, int partitionIndex)
        {
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), "zs_verify_part_" + diskIndex + "_" + partitionIndex + ".txt");
                File.WriteAllText(scriptPath,
                    "select disk " + diskIndex + "\r\n" +
                    "select partition " + partitionIndex + "\r\n" +
                    "detail partition\r\n" +
                    "list volume\r\n" +
                    "exit\r\n");

                var pi = new ProcessStartInfo("diskpart.exe", "/s \"" + scriptPath + "\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(pi);
                if (proc == null)
                {
                    try { File.Delete(scriptPath); } catch { }
                    return ("", "");
                }
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                try { File.Delete(scriptPath); } catch { }

                // detail partition 和 list volume 在同一输出中，按 "list volume" 关键字分割
                var splitIdx = output.IndexOf("list volume", StringComparison.OrdinalIgnoreCase);
                if (splitIdx < 0)
                    return (output, "");

                var detail = output[..splitIdx];
                var volume = output[splitIdx..];
                return (detail, volume);
            }
            catch
            {
                return ("", "");
            }
        }
    }
}
