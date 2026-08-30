using System.Text.Json.Serialization;

namespace WinPE_Client.Models
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

    public class ApiResponse : ApiResponse<object> { }

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