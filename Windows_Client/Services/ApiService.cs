using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows_Client.Models;

namespace Windows_Client.Services
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

        public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

        public void SetToken(string? token)
        {
            _token = token;
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        }

        public string? GetToken() => _token;

        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(_baseUrl + endpoint);
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions)
                    ?? new ApiResponse<T> { Code = -1, Message = "Parse failed" };
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
                    ?? new ApiResponse<T> { Code = -1, Message = "Parse failed" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Code = -1, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<object>> LoginAsync(string username, string password)
            => await PostAsync<object>("/api/v1/auth/login", new { username, password });

        public async Task<ApiResponse<PaginatedData<ImageInfo>>> GetImagesAsync(int page = 1, int limit = 20)
            => await GetAsync<PaginatedData<ImageInfo>>("/api/v1/images?page=" + page + "&limit=" + limit);

        public async Task<ApiResponse<PaginatedData<SoftwareInfo>>> GetSoftwareAsync(int page = 1, int limit = 20, int? categoryId = null)
        {
            var ep = "/api/v1/software?page=" + page + "&limit=" + limit;
            if (categoryId.HasValue) ep += "&category_id=" + categoryId;
            return await GetAsync<PaginatedData<SoftwareInfo>>(ep);
        }

        public async Task<ApiResponse<List<DiskInfo>>> GetDiskInfoAsync()
            => await GetAsync<List<DiskInfo>>("/api/v1/devices/disks");
    }
}