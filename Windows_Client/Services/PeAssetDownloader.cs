using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Windows_Client.Services
{
    /// <summary>
    /// PE 启动资产下载器（对应设计文档 §3.1 步骤 3 + §2 目录清单）。
    /// 从服务器 HTTP 下载 boot.wim、boot.sdi、ZS_PE_Agent.exe 到 ZS_Task 目录。
    /// 这两个文件不是在用户本地构建（需 ADK 6GB），而是运维预打包上传服务器，
    /// 用户端当作普通二进制资源下载。下载期间支持进度上报 + 取消 + 失败回退。
    /// </summary>
    public class PeAssetDownloader
    {
        private readonly ApiService _api;

        public PeAssetDownloader(ApiService api) => _api = api;

        /// <summary>下载 boot.wim 到 taskRoot\boot.wim（使用 PE 版本 ID）</summary>
        public async Task<(bool Ok, string Path, string Error)> DownloadBootWimAsync(
            int peVersionId, string taskRoot, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            var savePath = Path.Combine(taskRoot, "boot.wim");
            var url = $"/api/v1/peVersions/{peVersionId}/bootWim";
            return await DownloadAsync(url, savePath, progress, ct);
        }

        /// <summary>下载 boot.sdi 到 taskRoot\boot.sdi（使用 PE 版本 ID）</summary>
        public async Task<(bool Ok, string Path, string Error)> DownloadBootSdiAsync(
            int peVersionId, string taskRoot, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            var savePath = Path.Combine(taskRoot, "boot.sdi");
            var url = $"/api/v1/peVersions/{peVersionId}/bootSdi";
            return await DownloadAsync(url, savePath, progress, ct);
        }

        /// <summary>下载 ZS_PE_Agent.exe 到 taskRoot\ZS_PE_Agent.exe（使用 PE 版本 ID）</summary>
        public async Task<(bool Ok, string Path, string Error)> DownloadAgentAsync(
            int peVersionId, string taskRoot, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            var savePath = Path.Combine(taskRoot, "ZS_PE_Agent.exe");
            var url = $"/api/v1/peVersions/{peVersionId}/agent";
            return await DownloadAsync(url, savePath, progress, ct);
        }

        /// <summary>统一下载入口：用绝对 URL 或相对端点拼 baseUrl 后调 ApiService.DownloadFileAsync</summary>
        private async Task<(bool Ok, string Path, string Error)> DownloadAsync(
            string urlOrEndpoint, string savePath, IProgress<int>? progress, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                var fullUrl = urlOrEndpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? urlOrEndpoint
                    : _api.GetBaseUrl() + urlOrEndpoint;
                return await _api.DownloadFileAsync(fullUrl, savePath, progress, ct);
            }
            catch (Exception ex)
            {
                return (false, "", "下载失败: " + ex.Message);
            }
        }
    }
}
