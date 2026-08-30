using System.Text.Json.Serialization;

namespace Windows_Client.Models
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        public bool IsSuccess => Code == 0;
    }

    public class PaginatedData<T>
    {
        [JsonPropertyName("list")]
        public List<T> List { get; set; } = new();
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("page")]
        public int Page { get; set; }
        [JsonPropertyName("limit")]
        public int Limit { get; set; }
        [JsonPropertyName("pages")]
        public int Pages { get; set; }
    }

    public class ImageInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";
        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("format")]
        public string Format { get; set; } = "";
        [JsonPropertyName("os_type")]
        public string OsType { get; set; } = "";
        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

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
        [JsonPropertyName("file_system")]
        public string FileSystem { get; set; } = "";
        [JsonPropertyName("is_system")]
        public bool IsSystem { get; set; }
    }

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

    /// <summary>客户端自注册返回：服务端数据库 ID 与客户端唯一编号</summary>
    public class ClientInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    /// <summary>装机任务信息：创建任务 / 进度上报返回</summary>
    public class TaskInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("task_no")]
        public string TaskNo { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("progress")]
        public int Progress { get; set; }
    }
}