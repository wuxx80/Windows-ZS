using System.Text.Json.Serialization;

namespace WinPE_Client.Models
{
    public class ImageInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = "";

        [JsonPropertyName("file_hash")]
        public string FileHash { get; set; } = "";

        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }

        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "";

        [JsonPropertyName("os_type")]
        public string OsType { get; set; } = "";

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = "";

        [JsonPropertyName("os_architecture")]
        public string OsArchitecture { get; set; } = "";

        [JsonPropertyName("os_language")]
        public string OsLanguage { get; set; } = "";

        [JsonPropertyName("image_count")]
        public int ImageCount { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("tags")]
        public List<TagInfo> Tags { get; set; } = new();

        // ===== 设计文档 §4 卡片展示增强字段（API 未返回时使用默认值）=====
        [JsonPropertyName("install_count")]
        public int InstallCount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";

        [JsonPropertyName("is_recommended")]
        public bool IsRecommended { get; set; }

        [JsonPropertyName("is_new")]
        public bool IsNew { get; set; }

        /// <summary>镜像格式（大写，供筛选匹配）</summary>
        public string FormatUpper => Format.ToUpperInvariant();

        /// <summary>本地缓存状态文本（⬇️ 需下载 / ✅ 已缓存，刷新镜像时由 ViewModel 回填）</summary>
        public string CacheText { get; set; } = "⬇️ 需下载";

        /// <summary>创建日期短文本（取 YYYY-MM-DD）</summary>
        public string DateText => CreatedAt.Length >= 10 ? CreatedAt.Substring(0, 10) : CreatedAt;
    }

    public class TagInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("color")]
        public string Color { get; set; } = "";
    }
}