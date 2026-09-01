using System;
using System.Collections.Generic;

namespace WinPE_Client.Models
{
    /// <summary>
    /// ZS 装机任务配置（task.ini 明文格式，对应设计文档 §2.1 16 字段）。
    /// 由 Windows_Client 在 P1 阶段生成，由 PE 端 ZS_PE_Agent 在 --auto 模式下解析。
    /// 明文 ini 便于审计：用户可肉眼查看任务到底要装什么、装到哪。
    /// </summary>
    public class TaskIni
    {
        /// <summary>任务元信息（[meta] section）</summary>
        public TaskMeta Meta { get; set; } = new();

        /// <summary>目标磁盘（[target_disk] section）</summary>
        public TargetDisk TargetDisk { get; set; } = new();

        /// <summary>分区方案（[partition_scheme] section）</summary>
        public PartitionScheme PartitionScheme { get; set; } = new();

        /// <summary>系统镜像（[system_image] section）</summary>
        public SystemImage SystemImage { get; set; } = new();

        /// <summary>驱动注入策略（[drivers] section）</summary>
        public DriverOptions Drivers { get; set; } = new();

        /// <summary>软件包列表（[software] section 内 sw1_*/sw2_*/... 动态编号字段）</summary>
        public List<SoftwareEntry> Software { get; set; } = new();

        /// <summary>系统优化项（[optimize] section）</summary>
        public OptimizeOptions Optimize { get; set; } = new();
    }

    public class TaskMeta
    {
        public int Version { get; set; } = 1;
        public string CreatedAt { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string ServerApi { get; set; } = "";
        /// <summary>auto=用 Unattend.xml 全自动跳过 OOBE / manual=保留 OOBE 让用户操作</summary>
        public string OobeMode { get; set; } = "manual";
        /// <summary>yes=首进系统后删 ZS_Task 目录 / no=保留以备下次复用</summary>
        public bool FirstBootCleanup { get; set; } = true;
    }

    public class TargetDisk
    {
        public int DiskIndex { get; set; } = 0;
        /// <summary>clean_whole_disk=整盘重分区 / clean_c_only=只清 C 保留数据盘</summary>
        public string PartitionMode { get; set; } = "clean_whole_disk";
    }

    public class PartitionScheme
    {
        /// <summary>auto=按固件自动判 GPT/MBR / force_gpt / force_mbr</summary>
        public string Table { get; set; } = "auto";
        public int EspSizeMb { get; set; } = 500;
        public int MsrSizeMb { get; set; } = 16;
        public int RecoverySizeMb { get; set; } = 800;
        public string SystemLetter { get; set; } = "C";
        public string SystemLabel { get; set; } = "Windows";
        public string FormatFs { get; set; } = "ntfs";
        public bool QuickFormat { get; set; } = true;
    }

    public class SystemImage
    {
        /// <summary>对应 ZS_Task/ 中的文件名（如 system.esd 或 system.wim）</summary>
        public string File { get; set; } = "";
        /// <summary>WIM/ESD 内的分卷号（6=专业版、4=家庭高级版，依具体镜像）</summary>
        public int Index { get; set; } = 1;
        public string Name { get; set; } = "";
    }

    public class DriverOptions
    {
        public bool Inject { get; set; } = true;
        public bool Recurse { get; set; } = true;
        public bool ForceUnsigned { get; set; } = true;
    }

    /// <summary>单个软件包条目（对应 [software] section 内 sw1_*/sw2_*/... 一组字段）</summary>
    public class SoftwareEntry
    {
        public int Seq { get; set; }
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>MSI 安装包文件名（与 .exe 二选一）</summary>
        public string Msi { get; set; } = "";
        /// <summary>EXE 安装包文件名（与 .msi 二选一）</summary>
        public string Exe { get; set; } = "";
        public string Args { get; set; } = "";
        public int ExpectExit { get; set; } = 0;

        /// <summary>实际使用的安装包文件名（Msi 优先，回退 Exe）</summary>
        public string InstallerFile => !string.IsNullOrWhiteSpace(Msi) ? Msi : Exe;
    }

    public class OptimizeOptions
    {
        /// <summary>off=关闭休眠 / on=保留</summary>
        public string Hibernation { get; set; } = "off";
        public int StandbyTimeoutAc { get; set; } = 0;
        public int StandbyTimeoutDc { get; set; } = 30;
        public bool PagefileAuto { get; set; } = true;
        public bool DisableTelemetry { get; set; } = true;
        public bool RemoveCortana { get; set; } = true;
    }
}
