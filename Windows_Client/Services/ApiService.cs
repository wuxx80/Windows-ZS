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

        public async Task<ApiResponse<PaginatedData<T>>> GetPaginatedAsync<T>(string endpoint, int page = 1, int limit = 20)
        {
            var sep = endpoint.Contains("?") ? "&" : "?";
            return await GetAsync<PaginatedData<T>>(endpoint + sep + "page=" + page + "&limit=" + limit);
        }

        public async Task<ApiResponse<PaginatedData<ImageInfo>>> GetImagesAsync(int page = 1, int limit = 20, string? keyword = null, string? format = null, string? osType = null)
        {
            var endpoint = "/api/v1/images?page=" + page + "&limit=" + limit;
            if (!string.IsNullOrEmpty(keyword))
                endpoint += "&keyword=" + Uri.EscapeDataString(keyword);
            if (!string.IsNullOrEmpty(format))
                endpoint += "&format=" + Uri.EscapeDataString(format);
            if (!string.IsNullOrEmpty(osType))
                endpoint += "&os_type=" + Uri.EscapeDataString(osType);
            return await GetAsync<PaginatedData<ImageInfo>>(endpoint);
        }

        /// <summary>添加远程镜像 URL（镜像来源-远程URL模式）</summary>
        public async Task<ApiResponse<object>> AddRemoteUrlAsync(string url, string name)
            => await PostAsync<object>("/api/v1/images/addRemoteUrl", new { url, name });

        /// <summary>获取无人值守模板列表（扩展配置-无人值守）</summary>
        public async Task<ApiResponse<PaginatedData<object>>> GetUnattendTemplatesAsync(int page = 1, int limit = 200)
            => await GetPaginatedAsync<object>("/api/v1/unattendTemplates", page, limit);

        /// <summary>获取软件模板列表（扩展配置-软件包）</summary>
        public async Task<ApiResponse<PaginatedData<object>>> GetSoftwareTemplatesAsync(int page = 1, int limit = 200)
            => await GetPaginatedAsync<object>("/api/v1/softwareTemplates", page, limit);

        /// <summary>获取驱动包列表（扩展配置-驱动注入）</summary>
        public async Task<ApiResponse<PaginatedData<object>>> GetDriversAsync(int page = 1, int limit = 200)
            => await GetPaginatedAsync<object>("/api/v1/drivers", page, limit);

        /// <summary>暂停任务</summary>
        public async Task<ApiResponse<object>> PauseTaskAsync(int taskId)
            => await PostAsync<object>("/api/v1/tasks/" + taskId + "/pause");

        /// <summary>恢复任务</summary>
        public async Task<ApiResponse<object>> ResumeTaskAsync(int taskId)
            => await PostAsync<object>("/api/v1/tasks/" + taskId + "/resume");

        /// <summary>取消任务</summary>
        public async Task<ApiResponse<object>> CancelTaskAsync(int taskId)
            => await PostAsync<object>("/api/v1/tasks/" + taskId + "/cancel");

        public async Task<ApiResponse<PaginatedData<SoftwareInfo>>> GetSoftwareAsync(int page = 1, int limit = 20, int? categoryId = null)
        {
            var ep = "/api/v1/software?page=" + page + "&limit=" + limit;
            if (categoryId.HasValue) ep += "&category_id=" + categoryId;
            return await GetAsync<PaginatedData<SoftwareInfo>>(ep);
        }

        public async Task<ApiResponse<List<DiskInfo>>> GetDiskInfoAsync()
            => await GetAsync<List<DiskInfo>>("/api/v1/devices/disks");

        /// <summary>
        /// 客户端自注册（幂等）：首次注册由服务端生成 client_id；
        /// 再次调用时传入已有 clientId，服务端仅刷新心跳并返回已有记录。
        /// </summary>
        public async Task<ApiResponse<ClientInfo>> RegisterClientAsync(
            string hostname, string macAddress, string osVersion,
            string clientType = "windows", string? clientId = null)
        {
            var data = new Dictionary<string, object?>
            {
                ["hostname"] = hostname,
                ["mac_address"] = macAddress,
                ["os_version"] = osVersion,
                ["client_version"] = "0.0.268311",
                ["client_type"] = clientType,
            };
            if (!string.IsNullOrEmpty(clientId))
                data["client_id"] = clientId;

            return await PostAsync<ClientInfo>("/api/v1/clients/register", data);
        }

        /// <summary>创建装机任务</summary>
        public async Task<ApiResponse<TaskInfo>> CreateTaskAsync(
            int imageId, int? clientId = null, int targetDiskIndex = 0,
            string targetPartition = "C:", string partitionScheme = "auto",
            string? optionsJson = null)
        {
            var data = new Dictionary<string, object?>
            {
                ["image_id"] = imageId,
                ["client_id"] = clientId,
                ["target_disk_index"] = targetDiskIndex,
                ["target_partition"] = targetPartition,
                ["partition_scheme"] = partitionScheme,
            };
            if (!string.IsNullOrEmpty(optionsJson))
                data["options"] = optionsJson;

            return await PostAsync<TaskInfo>("/api/v1/tasks", data);
        }

        /// <summary>上报任务进度（支持状态闭环：running/completed/failed）</summary>
        public async Task<ApiResponse<object>> ReportProgressAsync(
            int taskId, int progress, string? message = null, string? stepName = null, string? status = null)
        {
            return await PostAsync<object>("/api/v1/tasks/" + taskId + "/progress", new
            {
                progress,
                message,
                step_name = stepName,
                status
            });
        }
    }
}
