using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Windows_Client.Services
{
    /// <summary>
    /// 软件包预下载器（对应设计文档 §3.1 步骤 3 + §2 目录清单 software/）。
    /// 下载软件安装包到 ZS_Task\software\{key}\，供 PE 端 SetupComplete.cmd 首次启动时静默安装。
    /// </summary>
    public class SoftwarePackageDownloader
    {
        private readonly ApiService _api;

        public SoftwarePackageDownloader(ApiService api) => _api = api;

        /// <summary>
        /// 下载单个软件包到 taskRoot\software\{key}\{installer}。
        /// 返回 (是否成功, 安装文件相对路径如 software/7z/7z1900-x64.msi, 错误)。
        /// </summary>
        public async Task<(bool Ok, string RelativePath, string Error)> DownloadAsync(
            int softwareId, string key, string installerFile, string taskRoot,
            IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(installerFile))
                return (false, "", "软件 key 或安装包名为空");

            var swDir = Path.Combine(taskRoot, "software", key);
            Directory.CreateDirectory(swDir);
            var savePath = Path.Combine(swDir, installerFile);

            var url = $"{_api.GetBaseUrl()}/api/v1/software/{softwareId}/clientDownload";
            var (ok, _, err) = await _api.DownloadFileAsync(url, savePath, progress, ct);
            if (!ok) return (false, "", err);

            var rel = "software/" + key + "/" + installerFile;
            return (true, rel.Replace('\\', '/'), "");
        }

        /// <summary>
        /// 批量下载软件包列表。返回是否全部成功 + 失败列表。
        /// </summary>
        public async Task<(bool AllOk, List<string> FailedKeys, string Error)> DownloadAllAsync(
            IEnumerable<(int Id, string Key, string Installer)> items, string taskRoot,
            IProgress<(int Current, int Total, string Key)>? progress = null, CancellationToken ct = default)
        {
            var list = new List<(int Id, string Key, string Installer)>(items);
            var failed = new List<string>();
            var total = list.Count;
            var current = 0;

            foreach (var item in list)
            {
                ct.ThrowIfCancellationRequested();
                current++;
                progress?.Report((current, total, item.Key));

                var (ok, _, err) = await DownloadAsync(item.Id, item.Key, item.Installer, taskRoot, null, ct);
                if (!ok) failed.Add(item.Key + ": " + err);
            }

            return (failed.Count == 0, failed, failed.Count == 0 ? "" : string.Join("; ", failed));
        }
    }
}
