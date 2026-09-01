using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Windows_Client.Services
{
    /// <summary>
    /// 系统镜像预下载器（对应设计文档 §3.1 步骤 3）。
    /// 在 Windows 端把用户选定的系统镜像（WIM/ESD）下载到 ZS_Task\images\，
    /// 避免进入 PE 后才下载（PE 可能无网卡驱动导致断网）。
    /// task.ini 的 [system_image].file 写相对路径 images/{filename}。
    /// </summary>
    public class SystemImagePreDownloader
    {
        private readonly ApiService _api;

        public SystemImagePreDownloader(ApiService api) => _api = api;

        /// <summary>
        /// 下载系统镜像到 taskRoot\images\{fileName}。
        /// 返回 (是否成功, 相对路径如 images/system.esd, 错误)。
        /// </summary>
        public async Task<(bool Ok, string RelativePath, string Error)> DownloadAsync(
            int imageId, string fileName, string taskRoot,
            IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(fileName))
                return (false, "", "镜像文件名为空");

            var imagesDir = Path.Combine(taskRoot, "images");
            Directory.CreateDirectory(imagesDir);
            var savePath = Path.Combine(imagesDir, fileName);

            // 端点：/api/v1/images/{id}/download
            var url = $"{_api.GetBaseUrl()}/api/v1/images/{imageId}/download";
            var (ok, _, err) = await _api.DownloadFileAsync(url, savePath, progress, ct);
            if (!ok) return (false, "", err);

            var rel = "images/" + fileName.Replace('\\', '/');
            return (true, rel, "");
        }
    }
}
