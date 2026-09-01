using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Windows_Client.Services
{
    /// <summary>
    /// 驱动包预下载器（对应设计文档 §3.1 步骤 3 + §2 目录清单 drivers/）。
    /// 下载驱动包到 ZS_Task\drivers\，若是 zip 压缩包则自动解压到 drivers\{name}\，
    /// 供 PE 端 DISM /Add-Driver /recurse 离线注入。
    /// </summary>
    public class DriverPackageDownloader
    {
        private readonly ApiService _api;

        public DriverPackageDownloader(ApiService api) => _api = api;

        /// <summary>
        /// 下载驱动包并（若是 zip）解压到 taskRoot\drivers\{name}\。
        /// 返回 (是否成功, 驱动目录相对路径如 drivers/net, 错误)。
        /// </summary>
        public async Task<(bool Ok, string RelativeDir, string Error)> DownloadAsync(
            int driverId, string packageName, string taskRoot,
            IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(packageName))
                return (false, "", "驱动包名为空");

            var driversDir = Path.Combine(taskRoot, "drivers");
            Directory.CreateDirectory(driversDir);

            var relDir = "drivers/" + packageName;
            var absDir = Path.Combine(driversDir, packageName);

            // 下载到临时文件
            var tmpZip = Path.Combine(driversDir, packageName + ".tmp");
            var url = $"{_api.GetBaseUrl()}/api/v1/drivers/{driverId}/clientDownload";
            var (ok, _, err) = await _api.DownloadFileAsync(url, tmpZip, progress, ct);
            if (!ok) return (false, "", err);

            // 若是 zip 压缩包，解压到目录后删 zip；否则重命名为正常文件
            try
            {
                if (packageName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(absDir);
                    ZipFile.ExtractToDirectory(tmpZip, absDir, overwriteFiles: true);
                    File.Delete(tmpZip);
                }
                else
                {
                    // 非压缩包：直接作为单个文件保留
                    var finalPath = Path.Combine(driversDir, packageName);
                    File.Move(tmpZip, finalPath, true);
                }
            }
            catch (Exception ex)
            {
                return (false, "", "驱动包解压失败: " + ex.Message);
            }

            return (true, relDir, "");
        }
    }
}
