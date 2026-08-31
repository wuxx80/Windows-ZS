using System.Text.Json.Serialization;

namespace Windows_Client.Models
{
    /// <summary>可移动 U 盘信息（U盘制作 步骤①）</summary>
    public class RemovableDisk
    {
        public int Index { get; set; }
        public string Model { get; set; } = "";
        public long Size { get; set; }
        public string SizeDisplay { get; set; } = "";
        public string DriveLetter { get; set; } = "";
        public string FileSystem { get; set; } = "";
        public string Label { get; set; } = "";
        public long UsedSize { get; set; }
        public long FreeSize { get; set; }
        public string FreeSizeDisplay { get; set; } = "";
        public bool IsSystem { get; set; }

        public string CapacityText => SizeDisplay + (FreeSize > 0 ? " · 可用 " + FreeSizeDisplay : "");

        public string DetailText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(DriveLetter)) parts.Add("盘符 " + DriveLetter + ":");
                if (!string.IsNullOrEmpty(FileSystem)) parts.Add(FileSystem);
                if (!string.IsNullOrEmpty(Label)) parts.Add("卷标 " + Label);
                return parts.Count > 0 ? string.Join("  ", parts) : "无文件系统";
            }
        }
    }

    /// <summary>U盘制作选项（步骤③ + 确认页）</summary>
    public class WritePlan
    {
        public int DiskIndex { get; set; }
        public string DiskDisplay { get; set; } = "";
        public string FileSystem { get; set; } = "exFAT";
        public string BootType { get; set; } = "both";
        public string PartitionScheme { get; set; } = "single";
        public string VolumeLabel { get; set; } = "ZS_PE";
        public string PeSource { get; set; } = "server";
        public PeVersionInfo? PeVersion { get; set; }
        public string PeFilePath { get; set; } = "";
        public string PeDisplay { get; set; } = "";
        public bool IncludeClient { get; set; } = true;
        public bool ApplyCustomize { get; set; }
        public bool IncludeTools { get; set; }
    }

    /// <summary>ISO 镜像构建计划（生成启动 ISO）</summary>
    public class IsoBuildPlan
    {
        public string PeFilePath { get; set; } = "";   // 源 PE：.iso / 目录
        public string OutputPath { get; set; } = "";    // 目标 .iso 完整路径
        public string IsoLabel { get; set; } = "ZS_PE";
        public bool IncludeClient { get; set; } = true;
        public bool IncludeTools { get; set; }
        public string ClientDir { get; set; } = "";     // 客户端发布目录（含 Tools）
    }

    /// <summary>PE 版本信息（服务器来源）</summary>
    public class PeVersionInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
        [JsonPropertyName("arch")]
        public string Arch { get; set; } = "";
        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("file_hash")]
        public string FileHash { get; set; } = "";
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("status")]
        public int Status { get; set; }

        public string DisplayText => Name + "  v" + Version + (string.IsNullOrEmpty(Arch) ? "" : "  " + Arch) + "  " + SizeDisplay;
    }

    /// <summary>写盘执行步骤项（执行页逐条联动）</summary>
    public class UdiskExecStep : ObservableObject
    {
        public string Name { get; set; } = "";
        private string _status = "waiting";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
        }
        private string _detail = "";
        public string Detail
        {
            get => _detail;
            set { _detail = value; OnPropertyChanged(); }
        }
        public string StatusIcon => Status switch
        {
            "running" => "\u23F3",
            "completed" => "\u2705",
            "failed" => "\u274C",
            "canceled" => "\u23F9",
            _ => "\u23F8"
        };
    }
}