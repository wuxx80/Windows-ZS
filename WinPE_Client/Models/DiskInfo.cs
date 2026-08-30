using System.Text.Json.Serialization;

namespace WinPE_Client.Models
{
    public class DiskInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";

        [JsonPropertyName("interface_type")]
        public string InterfaceType { get; set; } = "";

        [JsonPropertyName("is_system")]
        public bool IsSystem { get; set; }

        [JsonPropertyName("is_ssd")]
        public bool IsSsd { get; set; }

        [JsonPropertyName("partitions")]
        public List<PartitionInfo> Partitions { get; set; } = new();
    }

    public class PartitionInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("drive_letter")]
        public string DriveLetter { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";

        [JsonPropertyName("used_size")]
        public long UsedSize { get; set; }

        [JsonPropertyName("free_size")]
        public long FreeSize { get; set; }

        [JsonPropertyName("file_system")]
        public string FileSystem { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("is_system")]
        public bool IsSystem { get; set; }

        [JsonPropertyName("is_boot")]
        public bool IsBoot { get; set; }

        [JsonPropertyName("is_esp")]
        public bool IsEsp { get; set; }
    }

    public class PartitionOperation
    {
        public string Operation { get; set; } = ""; // create, delete, format, extend
        public int DiskIndex { get; set; }
        public int? PartitionIndex { get; set; }
        public long? Size { get; set; }
        public string? FileSystem { get; set; } // NTFS, FAT32, exFAT
        public string? DriveLetter { get; set; }
        public string? Label { get; set; }
        public bool? IsEsp { get; set; }
        public bool? IsMsr { get; set; }
    }
}