using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private string? _token;
        private string _baseUrl = "http://localhost";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ApiService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public void SetBaseUrl(string url)
        {
            _baseUrl = url.TrimEnd('/');
        }

        public void SetToken(string? token)
        {
            _token = token;
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            }
        }

        public string? GetToken() => _token;

        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(_baseUrl + endpoint);
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions) 
                    ?? new ApiResponse<T> { Code = -1, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Code = -1, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data = null)
        {
            try
            {
                var content = data != null 
                    ? new StringContent(JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8, "application/json")
                    : null;
                var response = await _httpClient.PostAsync(_baseUrl + endpoint, content);
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions)
                    ?? new ApiResponse<T> { Code = -1, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Code = -1, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedData<T>>> GetPaginatedAsync<T>(string endpoint, int page = 1, int limit = 20)
        {
            var sep = endpoint.Contains("?") ? "&" : "?";
            return await GetAsync<PaginatedData<T>>(endpoint + sep + "page=" + page + "&limit=" + limit);
        }

        public async Task<ApiResponse<object>> LoginAsync(string username, string password)
        {
            return await PostAsync<object>("/api/v1/auth/login", new { username, password });
        }

        public async Task<ApiResponse<object>> LogoutAsync()
        {
            return await PostAsync<object>("/api/v1/auth/logout");
        }

        public async Task<ApiResponse<PaginatedData<ImageInfo>>> GetImagesAsync(int page = 1, int limit = 20, string? keyword = null)
        {
            var endpoint = "/api/v1/images?page=" + page + "&limit=" + limit;
            if (!string.IsNullOrEmpty(keyword))
                endpoint += "&keyword=" + Uri.EscapeDataString(keyword);
            return await GetAsync<PaginatedData<ImageInfo>>(endpoint);
        }

        public async Task<ApiResponse<ImageInfo>> GetImageDetailAsync(int id)
        {
            return await GetAsync<ImageInfo>("/api/v1/images/" + id);
        }

        public async Task<ApiResponse<List<DiskInfo>>> GetDiskInfoAsync()
        {
            return await GetAsync<List<DiskInfo>>("/api/v1/devices/disks");
        }

        public async Task<ApiResponse<object>> CreateTaskAsync(dynamic data)
        {
            return await PostAsync<object>("/api/v1/tasks", data);
        }

        public async Task<ApiResponse<object>> ReportProgressAsync(int taskId, int progress, string? message = null)
        {
            return await PostAsync<object>("/api/v1/tasks/" + taskId + "/progress", 
                new { progress, message });
        }
    }
}