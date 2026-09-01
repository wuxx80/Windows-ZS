using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinPE_Client.Models;

namespace Windows_Client.Services
{
    /// <summary>
    /// ZS_Task 目录编排器（对应设计文档 §3.1 步骤 1-5 + §3.2 V1 验证）。
    /// Windows 端下单时调用，把 10 项资源全部下载/生成到 {taskDrive}:\ZS_Task\，
    /// 校验通过后返回上下文，供后续 BcdInjector 注入启动项。
    /// 失败时按 §3.3 F1 自动回退删除 ZS_Task 目录（100% 零残留）。
    /// </summary>
    public class ZsTaskBuilder
    {
        private readonly ApiService _api;
        private readonly PeAssetDownloader _peDownloader;
        private readonly SystemImagePreDownloader _imageDownloader;
        private readonly DriverPackageDownloader _driverDownloader;
        private readonly SoftwarePackageDownloader _softwareDownloader;

        public ZsTaskBuilder(ApiService api)
        {
            _api = api;
            _peDownloader = new PeAssetDownloader(api);
            _imageDownloader = new SystemImagePreDownloader(api);
            _driverDownloader = new DriverPackageDownloader(api);
            _softwareDownloader = new SoftwarePackageDownloader(api);
        }

        /// <summary>构建结果</summary>
        public class BuildResult
        {
            public bool Ok { get; set; }
            public string Error { get; set; } = "";
            public string TaskRoot { get; set; } = "";
            public string TaskIniPath { get; set; } = "";
            public string ManifestPath { get; set; } = "";
            public string ImageRelativePath { get; set; } = "";
        }

        /// <summary>构建请求</summary>
        public class BuildRequest
        {
            public string TaskDrive { get; set; } = "D"; // 无冒号
            public string PeVersion { get; set; } = "";
            public int ImageId { get; set; }
            public string ImageFileName { get; set; } = "";
            public int ImageIndex { get; set; } = 1;
            public string ImageName { get; set; } = "";
            public int DiskIndex { get; set; } = 0;
            public string OobeMode { get; set; } = "manual";
            public bool FirstBootCleanup { get; set; } = false;
            public string ServerApi { get; set; } = "";
            public string PartitionTable { get; set; } = "auto"; // auto/force_gpt/force_mbr
            public List<(int Id, string Key, string Installer)> SoftwareList { get; set; } = new();
            public int? DriverId { get; set; }
            public string DriverPackageName { get; set; } = "";
        }

        /// <summary>进度上报</summary>
        public class BuildProgress
        {
            public int Percent { get; set; }
            public string Step { get; set; } = "";
            public string Detail { get; set; } = "";
        }

        /// <summary>
        /// 执行 P1 全流程：创建目录 → 下载资源 → 生成 task.ini/manifest → V1 验证。
        /// 失败时自动回退删除 ZS_Task 目录。
        /// </summary>
        public async Task<BuildResult> BuildAsync(BuildRequest req,
            IProgress<BuildProgress>? progress = null, CancellationToken ct = default)
        {
            var taskRoot = req.TaskDrive + ":\\ZS_Task";
            var result = new BuildResult { TaskRoot = taskRoot };

            try
            {
                // 步骤 1：创建目录 + README（§3.1 步骤 1-2）
                Report(progress, 5, "创建目录", taskRoot);
                Directory.CreateDirectory(taskRoot);
                WriteReadme(taskRoot);

                // 步骤 2：下载 PE 资产（§3.1 步骤 3）
                Report(progress, 10, "下载 PE 资产", "boot.wim");
                var (wimOk, _, wimErr) = await _peDownloader.DownloadBootWimAsync(req.PeVersion, taskRoot, null, ct);
                if (!wimOk) throw new Exception("boot.wim 下载失败: " + wimErr);

                Report(progress, 30, "下载 PE 资产", "boot.sdi");
                var (sdiOk, _, sdiErr) = await _peDownloader.DownloadBootSdiAsync(req.PeVersion, taskRoot, null, ct);
                if (!sdiOk) throw new Exception("boot.sdi 下载失败: " + sdiErr);

                Report(progress, 40, "下载 PE 资产", "ZS_PE_Agent.exe");
                var (agentOk, _, agentErr) = await _peDownloader.DownloadAgentAsync(req.PeVersion, taskRoot, null, ct);
                if (!agentOk) throw new Exception("ZS_PE_Agent.exe 下载失败: " + agentErr);

                // 步骤 3：下载系统镜像（§3.1 步骤 3）
                Report(progress, 50, "下载系统镜像", req.ImageFileName);
                var (imgOk, imgRel, imgErr) = await _imageDownloader.DownloadAsync(
                    req.ImageId, req.ImageFileName, taskRoot, null, ct);
                if (!imgOk) throw new Exception("系统镜像下载失败: " + imgErr);
                result.ImageRelativePath = imgRel;

                // 步骤 4：下载驱动包（可选）
                if (req.DriverId.HasValue && !string.IsNullOrEmpty(req.DriverPackageName))
                {
                    Report(progress, 65, "下载驱动包", req.DriverPackageName);
                    var (drvOk, _, drvErr) = await _driverDownloader.DownloadAsync(
                        req.DriverId.Value, req.DriverPackageName, taskRoot, null, ct);
                    if (!drvOk) throw new Exception("驱动包下载失败: " + drvErr);
                }

                // 步骤 5：下载软件包（可选）
                if (req.SoftwareList.Count > 0)
                {
                    Report(progress, 75, "下载软件包", req.SoftwareList.Count + " 个");
                    var (swOk, _, swErr) = await _softwareDownloader.DownloadAllAsync(
                        req.SoftwareList, taskRoot, null, ct);
                    if (!swOk) throw new Exception("软件包下载失败: " + swErr);
                }

                // 步骤 6：生成 task.ini（§3.1 步骤 4）
                Report(progress, 85, "生成任务配置", "task.ini");
                var taskIni = BuildTaskIni(req, imgRel);
                var iniPath = Path.Combine(taskRoot, "task.ini");
                if (!TaskIniWriter.Write(taskIni, iniPath))
                    throw new Exception("task.ini 写入失败");
                result.TaskIniPath = iniPath;

                // 步骤 7：生成 zs_manifest.key（§3.1 步骤 5）
                Report(progress, 90, "生成校验清单", "zs_manifest.key");
                var manifestPath = Path.Combine(taskRoot, "zs_manifest.key");
                if (!ManifestWriter.Write(taskRoot, manifestPath))
                    throw new Exception("zs_manifest.key 写入失败");
                result.ManifestPath = manifestPath;

                // 步骤 8：V1 验证（§3.2 7 项）
                Report(progress, 95, "验证资源完整性", "V1");
                var v1Err = VerifyV1(taskRoot);
                if (v1Err != null) throw new Exception("V1 验证失败: " + v1Err);

                Report(progress, 100, "完成", "所有资源就绪");
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                // §3.3 F1 回退：删除 ZS_Task 目录，100% 零残留
                try { if (Directory.Exists(taskRoot)) Directory.Delete(taskRoot, true); } catch { }
                result.Ok = false;
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>构造 TaskIni 模型</summary>
        private TaskIni BuildTaskIni(BuildRequest req, string imageRelPath)
        {
            return new TaskIni
            {
                Meta = new TaskMeta
                {
                    Version = 1,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TaskId = "ZS-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                    ServerApi = req.ServerApi,
                    OobeMode = req.OobeMode,
                    FirstBootCleanup = req.FirstBootCleanup,
                },
                TargetDisk = new TargetDisk
                {
                    DiskIndex = req.DiskIndex,
                    PartitionMode = "clean_whole_disk",
                },
                PartitionScheme = new PartitionScheme
                {
                    Table = req.PartitionTable,
                },
                SystemImage = new SystemImage
                {
                    File = imageRelPath,
                    Index = req.ImageIndex,
                    Name = req.ImageName,
                },
            };
        }

        /// <summary>§3.2 V1 验证 7 项</summary>
        private string? VerifyV1(string taskRoot)
        {
            string[] required = { "boot.wim", "boot.sdi", "ZS_PE_Agent.exe", "task.ini", "zs_manifest.key" };
            foreach (var f in required)
                if (!File.Exists(Path.Combine(taskRoot, f)))
                    return "缺少文件: " + f;

            // 镜像文件（images/ 下）
            if (!Directory.Exists(Path.Combine(taskRoot, "images")))
                return "缺少 images 目录";
            var images = Directory.GetFiles(Path.Combine(taskRoot, "images"));
            if (images.Length == 0) return "images 目录为空";

            // manifest 非空
            var info = new FileInfo(Path.Combine(taskRoot, "zs_manifest.key"));
            if (info.Length == 0) return "zs_manifest.key 为空";

            return null;
        }

        private static void WriteReadme(string taskRoot)
        {
            var content = "本目录是 ZS 装机系统的任务目录，删除会导致无人值守失败；"
                + "装机完成如需清理空间可在\"系统优化\"中一键清理。";
            File.WriteAllText(Path.Combine(taskRoot, "README_请勿删除.txt"), content);
        }

        private static void Report(IProgress<BuildProgress>? p, int pct, string step, string detail)
            => p?.Report(new BuildProgress { Percent = pct, Step = step, Detail = detail });
    }
}
