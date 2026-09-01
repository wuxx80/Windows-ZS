using System.Text.Json.Serialization;

namespace Windows_Client.Models
{
    /// <summary>
    /// 离线无人值守任务（zs_task.json）—— 方案C 双保险的离线注入载体（与 WinPE 端契约完全一致）。
    /// Windows 下单时预下载镜像到数据盘并注入本文件（source = windows）；
    /// U盘/ISO 制作时选择「写入装机镜像」注入（source = usb）。
    /// PE 环境离线时扫描磁盘读取本文件，即可在无网络状态下完成完整装机。
    /// </summary>
    public class OfflineTask
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>来源：usb（U盘/ISO注入） / windows（Windows预下载注入）</summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";

        [JsonPropertyName("task_no")]
        public string TaskNo { get; set; } = "";

        [JsonPropertyName("image")]
        public OfflineImageInfo? Image { get; set; }

        /// <summary>目标磁盘物理特征（PE 环境磁盘序号不稳定，按 size/model 模糊匹配）</summary>
        [JsonPropertyName("disk")]
        public OfflineDiskInfo? Disk { get; set; }

        [JsonPropertyName("target_partition")]
        public string TargetPartition { get; set; } = "C:";

        /// <summary>分区方案：auto（自动） / keep（保留现有） / custom（自定义）</summary>
        [JsonPropertyName("partition_scheme")]
        public string PartitionScheme { get; set; } = "auto";

        [JsonPropertyName("options")]
        public OfflineOptions Options { get; set; } = new();

        /// <summary>无人值守应答 XML（离线直接写入 C:\Windows\Panther\unattend.xml）</summary>
        [JsonPropertyName("unattend_xml")]
        public string UnattendXml { get; set; } = "";

        /// <summary>首次登录脚本（离线直接写入 SetupComplete.cmd）</summary>
        [JsonPropertyName("first_logon_cmd")]
        public string FirstLogonCmd { get; set; } = "";
    }

    /// <summary>离线任务引用的系统镜像</summary>
    public class OfflineImageInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";

        /// <summary>镜像路径：绝对路径，或相对 zs_task.json 所在盘符根目录（如 ZS_Images\win11.wim）</summary>
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = "";

        [JsonPropertyName("file_hash")]
        public string FileHash { get; set; } = "";

        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }

        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
    }

    /// <summary>目标磁盘物理特征（用于 PE 环境模糊匹配）</summary>
    public class OfflineDiskInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; } = -1;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
    }

    /// <summary>离线安装选项（与 BuildOptionsJson 契约对齐）</summary>
    public class OfflineOptions
    {
        [JsonPropertyName("backup_data")]
        public bool BackupData { get; set; } = true;

        [JsonPropertyName("auto_partition")]
        public bool AutoPartition { get; set; } = true;

        [JsonPropertyName("driver_inject")]
        public bool DriverInject { get; set; } = true;

        [JsonPropertyName("boot_fix")]
        public bool BootFix { get; set; } = true;

        [JsonPropertyName("unattended")]
        public bool Unattended { get; set; } = true;

        [JsonPropertyName("install_software")]
        public bool InstallSoftware { get; set; } = true;

        [JsonPropertyName("optimize")]
        public bool Optimize { get; set; } = true;
    }
}
