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
        public string Version { get; set; } = "";

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("tags")]
        public List<TagInfo> Tags { get; set; } = new();
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