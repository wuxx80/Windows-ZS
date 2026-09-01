using System.Text.Json.Serialization;

namespace WinPE_Client.Models
{
    /// <summary>
    /// Agent 项目专用的最小 API 模型。
    /// ApiService 的 GetSoftwareAsync / GetPeVersionsAsync 方法签名引用了
    /// SoftwareInfo / PeVersionInfo；这两个类型在客户端完整模型中与 WPF 的
    /// ObservableObject 基类混在一起，Agent 是纯控制台（无 WPF），
    /// 因此在此处定义最小副本，仅含 Agent 运行所需的字段。
    /// </summary>
    public class SoftwareInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = "";
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

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
    }
}
