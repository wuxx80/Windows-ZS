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
}