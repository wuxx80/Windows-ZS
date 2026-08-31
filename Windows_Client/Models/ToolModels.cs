using System.Text.Json.Serialization;

namespace Windows_Client.Models
{
    /// <summary>工具分类（工具大全 Tab）</summary>
    public class ToolCategory
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";
        public override string ToString() => Name;
    }

    /// <summary>工具条目（本地内置 / 服务器来源）</summary>
    public class ToolInfo : ObservableObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";
        [JsonPropertyName("exe")]
        public string ExePath { get; set; } = "";
        [JsonPropertyName("args")]
        public string Args { get; set; } = "";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        [JsonPropertyName("need_admin")]
        public bool NeedAdmin { get; set; }

        /// <summary>来源：local 本地内置 / server 服务器</summary>
        public string Source { get; set; } = "local";

        // 状态：ready 可运行 / missing 缺文件 / downloadable 可下载 / downloading 下载中 / downloaded 已下载
        private string _status = "missing";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(ShowRun)); OnPropertyChanged(nameof(ShowDownload)); OnPropertyChanged(nameof(ShowOpenFolder)); OnPropertyChanged(nameof(ShowDownloading)); OnPropertyChanged(nameof(SourceBadge)); }
        }

        public string SizeDisplay { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string FullPath { get; set; } = "";

        public string StatusText => Status switch
        {
            "ready" => "运行",
            "missing" => "缺文件",
            "downloadable" => "下载",
            "downloading" => "下载中",
            "downloaded" => "运行",
            _ => "运行"
        };

        public bool ShowRun => Status == "ready" || Status == "downloaded";
        public bool ShowDownload => Status == "downloadable";
        public bool ShowDownloading => Status == "downloading";
        public bool ShowOpenFolder => Status == "missing";

        public string SourceBadge => Source == "server" ? "服务器" : "本地";

        public string SourceBadgeColor => Source == "server" ? "#E67E22" : "#1976D2";
    }
}