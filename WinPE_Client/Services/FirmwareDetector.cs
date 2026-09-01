using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WinPE_Client.Services
{
    /// <summary>
    /// 固件类型判定 —— 设计文档 §6.0 双重判定 + 6 级冲突处理。
    /// PE 环境下注册表 FirmwareType 并不 100% 可靠（部分定制 PE 会清掉，
    /// 少数 BIOS 的 ACPI 表上报也会错）。本服务采用「注册表主判定 + diskpart 回退」两级策略。
    /// </summary>
    public static class FirmwareDetector
    {
        // —— 公开类型 ——
        public enum FirmwareType { Unknown = 0, Bios = 1, Uefi = 2 }

        public enum DetectSource { None, Registry, Diskpart, Override, Both }

        public sealed class FirmwareResult
        {
            /// <summary>最终判定的固件类型（GPT/MBR 分流依据）</summary>
            public FirmwareType Type { get; set; } = FirmwareType.Unknown;
            /// <summary>判定来源：Override（task.ini 强制） / Registry / Diskpart / Both（双判一致）</summary>
            public DetectSource Source { get; set; } = DetectSource.None;
            /// <summary>注册表读取结果（用于冲突诊断日志）</summary>
            public FirmwareType RegistryType { get; set; } = FirmwareType.Unknown;
            /// <summary>diskpart 读取结果（同上）</summary>
            public FirmwareType DiskpartType { get; set; } = FirmwareType.Unknown;
            /// <summary>是否双判冲突（reg=Uefi 但 disk=MBR 或反之）</summary>
            public bool Conflict { get; set; }
            /// <summary>需写入日志的红色警告（冲突时非空）</summary>
            public string Warning { get; set; } = "";
            /// <summary>最终选择的分区脚本：gpt / mbr / unknown</summary>
            public string PartitionScript => Type switch
            {
                FirmwareType.Uefi => "gpt",
                FirmwareType.Bios => "mbr",
                _ => "unknown",
            };

            public bool IsUnknown => Type == FirmwareType.Unknown;
        }

        // —— 6 级冲突处理主入口 ——
        // 设计 §6.0.3：
        //   if task.ini table != auto: 直接按强制选 force_gpt / force_mbr 走（不做自动判定）
        //   reg = DetectByRegistry()
        //   disk = DetectByDiskpart(disk_index_from_task)
        //   判定优先级：
        //     1. reg=Uefi AND disk=Uefi → 走 GPT
        //     2. reg=Bios AND disk=Bios → 走 MBR
        //     3. reg=Unknown AND disk=Uefi → 走 GPT（信 diskpart）
        //     4. reg=Unknown AND disk=Bios → 走 MBR（信 diskpart）
        //     5. 两者冲突 → 以 diskpart 为准 + 写红色警告 + 分区脚本末尾增加尾端验证
        //     6. 两者都 Unknown → 直接 Exit(2)
        public static FirmwareResult Detect(string overrideMode, int diskIndex)
        {
            // 强制模式：task.ini 中 firmware_mode 为 force_gpt / force_mbr 时跳过自动判定
            if (!string.IsNullOrEmpty(overrideMode)
                && overrideMode.Equals("force_gpt", StringComparison.OrdinalIgnoreCase))
            {
                return new FirmwareResult
                {
                    Type = FirmwareType.Uefi,
                    Source = DetectSource.Override,
                    Warning = "固件类型由 task.ini 强制为 force_gpt（跳过自动判定）"
                };
            }
            if (!string.IsNullOrEmpty(overrideMode)
                && overrideMode.Equals("force_mbr", StringComparison.OrdinalIgnoreCase))
            {
                return new FirmwareResult
                {
                    Type = FirmwareType.Bios,
                    Source = DetectSource.Override,
                    Warning = "固件类型由 task.ini 强制为 force_mbr（跳过自动判定）"
                };
            }

            // 自动判定：双判
            var reg = DetectByRegistry();
            var disk = DetectByDiskpart(diskIndex);
            var r = new FirmwareResult { RegistryType = reg, DiskpartType = disk };

            // 1. 双判一致 Uefi
            if (reg == FirmwareType.Uefi && disk == FirmwareType.Uefi)
            {
                r.Type = FirmwareType.Uefi;
                r.Source = DetectSource.Both;
                return r;
            }
            // 2. 双判一致 Bios
            if (reg == FirmwareType.Bios && disk == FirmwareType.Bios)
            {
                r.Type = FirmwareType.Bios;
                r.Source = DetectSource.Both;
                return r;
            }
            // 3. reg Unknown + disk Uefi → 信 diskpart
            if (reg == FirmwareType.Unknown && disk == FirmwareType.Uefi)
            {
                r.Type = FirmwareType.Uefi;
                r.Source = DetectSource.Diskpart;
                return r;
            }
            // 4. reg Unknown + disk Bios → 信 diskpart
            if (reg == FirmwareType.Unknown && disk == FirmwareType.Bios)
            {
                r.Type = FirmwareType.Bios;
                r.Source = DetectSource.Diskpart;
                return r;
            }
            // 5. 双判冲突（reg=Uefi 但 disk=Bios，或反之）
            //    策略：以 diskpart 为准 + 红色警告 + 分区脚本末尾增加尾端验证（PartitionVerifier 负责）
            if ((reg == FirmwareType.Uefi && disk == FirmwareType.Bios)
                || (reg == FirmwareType.Bios && disk == FirmwareType.Uefi))
            {
                r.Conflict = true;
                r.Type = disk; // 以 diskpart 为准
                r.Source = DetectSource.Diskpart;
                r.Warning = "[CSM 冲突警告] 注册表判定=" + reg + " 但 diskpart 读到=" + disk
                    + "；这台机器正跑在 CSM 兼容模式，分区表与固件不一致。"
                    + "按设计 §6.0.3 第5项以 diskpart 为准，分区脚本末尾将增加尾端验证。";
                return r;
            }
            // 5b. reg 命中但 disk Unknown —— 信注册表（diskpart 可能因盘未初始化而读不到）
            if (reg != FirmwareType.Unknown && disk == FirmwareType.Unknown)
            {
                r.Type = reg;
                r.Source = DetectSource.Registry;
                r.Warning = "[注意] diskpart 读不到分区表（disk=" + diskIndex + " 可能未初始化或数据线松动），改用注册表判定。"
                    + "若 diskpart clean 后仍读不到，Agent 会 Exit(2)。";
                return r;
            }

            // 6. 两者都 Unknown → 调用方应直接 Environment.Exit(2)
            r.Type = FirmwareType.Unknown;
            r.Source = DetectSource.None;
            r.Warning = "[致命] 注册表与 diskpart 均无法判定固件类型。"
                + "真实硬件上这种情况极端罕见，出现意味着 diskpart 也读不到分区表，"
                + "很可能是目标盘压根没初始化或数据线松动，硬往下执行 100% 会坏盘。"
                + "Agent 应立即 Environment.Exit(2)。";
            return r;
        }

        // —— §6.0.1 主判定：注册表（快速路径，0 IO）——
        // 优先读 HKLM\System\CurrentControlSet\Control\FirmwareType
        // 0x0 = Unknown, 0x1 = BIOS/MBR, 0x2 = UEFI/GPT（Win8+ 原生 PE 下此键稳定）
        // 备选：环境变量 FirmwareType（部分 PE 下枚举 PE 固件环境变量）
        public static FirmwareType DetectByRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"System\CurrentControlSet\Control", writable: false);
                if (key?.GetValue("FirmwareType") is int v)
                {
                    return v switch
                    {
                        0x1 => FirmwareType.Bios,
                        0x2 => FirmwareType.Uefi,
                        _ => FirmwareType.Unknown,
                    };
                }
            }
            catch { /* 注册表读不到不阻塞，走环境变量回退 */ }

            try
            {
                var raw2 = Environment.GetEnvironmentVariable("FirmwareType");
                if (int.TryParse(raw2, out var v2))
                    return v2 == 2 ? FirmwareType.Uefi : FirmwareType.Bios;
            }
            catch { }

            return FirmwareType.Unknown;
        }

        // —— §6.0.2 回退判定：diskpart 读取磁盘真实分区表（慢速但 100% 可信）——
        // 调用 diskpart.exe /s 执行 "select disk N" + "detail disk"，抓取输出中 "Partition Style" 行
        //   Partition Style: GUID Partition Table   ← GPT，固件必为 UEFI
        //   Partition Style: Master Boot Record     ← MBR，固件为 Legacy BIOS
        public static FirmwareType DetectByDiskpart(int diskIndex)
        {
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), "zs_detail_disk_" + diskIndex + ".txt");
                File.WriteAllText(scriptPath,
                    "select disk " + diskIndex + "\r\ndetail disk\r\nexit\r\n");

                var pi = new ProcessStartInfo("diskpart.exe", "/s \"" + scriptPath + "\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(pi);
                if (proc == null) return FirmwareType.Unknown;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                try { File.Delete(scriptPath); } catch { }

                // 抓 "Partition Style" 行（不同语言系统可能本地化，但 PE 默认 en-US 一般是英文）
                var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.StartsWith("Partition Style", StringComparison.OrdinalIgnoreCase));

                if (line == null) return FirmwareType.Unknown;
                if (line.Contains("GUID", StringComparison.OrdinalIgnoreCase)) return FirmwareType.Uefi;
                if (line.Contains("Master Boot", StringComparison.OrdinalIgnoreCase)) return FirmwareType.Bios;
                return FirmwareType.Unknown;
            }
            catch
            {
                return FirmwareType.Unknown;
            }
        }
    }
}
