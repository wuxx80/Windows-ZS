using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    /// <summary>
    /// 离线任务服务（方案C）：扫描磁盘中的 zs_task.json，解析本地镜像路径。
    /// PE 环境无网络时，由本服务发现「U盘/ISO 注入」或「Windows 预下载注入」的离线任务。
    /// </summary>
    public class OfflineTaskService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        /// <summary>候选查找位置（相对盘符根目录）：
        ///  · zs_task.json          —— U盘/ISO 制作时注入（方案A，镜像在 ZS_Images\）
        ///  · ZS_Cache\zs_task.json —— Windows 下单时预下载注入（方案B）
        /// 客户端已内置于固定定制 PE，无需再扫描 ZS_Client\ 下的任务文件。</summary>
        private static readonly string[] RelCandidates =
        {
            "zs_task.json",
            "ZS_Cache\\zs_task.json",
        };

        /// <summary>扫描全部本地磁盘（固定盘 + 可移动盘 + 光驱），返回找到的离线任务。
        /// 含 CdRom：方案A 生成「可引导 ISO」时离线任务注入 ISO 根目录，从该 ISO 启动 PE 后
        /// ISO 挂载为光驱盘符，若不扫描 CdRom 将永远无法发现离线任务。</summary>
        public List<OfflineTaskFile> ScanAllDrives()
        {
            var result = new List<OfflineTaskFile>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.DriveType != DriveType.Fixed
                            && drive.DriveType != DriveType.Removable
                            && drive.DriveType != DriveType.CDRom) continue;
                        if (!drive.IsReady) continue;
                        result.AddRange(ScanRoot(drive.RootDirectory.FullName));
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        /// <summary>在指定根目录及候选子目录查找并解析 zs_task.json</summary>
        public List<OfflineTaskFile> ScanRoot(string root)
        {
            var result = new List<OfflineTaskFile>();
            foreach (var rel in RelCandidates)
            {
                try
                {
                    var path = Path.Combine(root, rel);
                    if (!File.Exists(path)) continue;
                    var task = Load(path);
                    if (task != null) result.Add(new OfflineTaskFile(path, task));
                }
                catch { }
            }
            return result;
        }

        /// <summary>解析 zs_task.json；失败返回 null</summary>
        public OfflineTask? Load(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<OfflineTask>(json, JsonOpts);
            }
            catch { return null; }
        }

        /// <summary>
        /// 解析离线任务引用的本地镜像绝对路径。
        /// 优先使用绝对路径；相对路径则基于 zs_task.json 所在目录解析。
        /// 找不到可用的镜像文件返回 null。
        /// </summary>
        public string? ResolveImagePath(string taskFilePath, OfflineTask task)
        {
            try
            {
                var p = task.Image?.FilePath;
                if (string.IsNullOrEmpty(p)) p = task.Image?.FileName;
                if (string.IsNullOrEmpty(p)) return null;
                if (Path.IsPathRooted(p) && File.Exists(p)) return p;
                var dir = Path.GetDirectoryName(taskFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var rel = Path.Combine(dir, p);
                    if (File.Exists(rel)) return rel;
                    // 兼容根目录形态（如 C:\win11.wim 被存为 win11.wim 时）
                    var rootRel = Path.Combine(Path.GetPathRoot(dir) ?? "", p);
                    if (File.Exists(rootRel)) return rootRel;
                }
                return null;
            }
            catch { return null; }
        }

        /// <summary>写入离线任务文件（Windows 预下载 / U盘注入 共用；UTF-8 无 BOM）</summary>
        public static bool Write(string path, OfflineTask task)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(task, JsonOpts);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        /// <summary>生成一个离线任务编号</summary>
        public static string NewTaskNo()
            => "ZS-OFFLINE-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
    }

    /// <summary>离线任务文件（路径 + 解析结果）</summary>
    public class OfflineTaskFile
    {
        public OfflineTaskFile(string path, OfflineTask task)
        {
            Path = path;
            Task = task;
        }
        public string Path { get; }
        public OfflineTask Task { get; }
    }
}
