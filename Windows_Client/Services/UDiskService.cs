using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows_Client.Models;

namespace Windows_Client.Services
{
    /// <summary>U盘制作服务：可移动盘枚举 / 安全校验 / PE 下载 / 格式化写盘</summary>
    public class UDiskService
    {
        /// <summary>枚举可移动 U 盘（过滤系统盘 / 无容量异常盘，附磁盘号）</summary>
        public List<RemovableDisk> GetRemovableDisks()
        {
            var result = new List<RemovableDisk>();
            try
            {
                // 逻辑盘(可移动 DriveType=2) → 磁盘号/型号 关联映射
                var diskByLogical = new Dictionary<string, (int Index, string Model, long Size)>();
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (var disk in searcher.Get())
                    {
                        var devId = disk["DeviceID"]?.ToString() ?? "";
                        var idx = ParseDiskIndex(devId);
                        var model = disk["Model"]?.ToString() ?? "";
                        var size = Convert.ToInt64(disk["Size"] ?? 0);

                        using var partSearcher = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_DiskDrive.DeviceID=\"" + devId + "\"} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                        foreach (var part in partSearcher.Get())
                        {
                            var partDevId = part["DeviceID"]?.ToString() ?? "";
                            using var logSearcher = new ManagementObjectSearcher(
                                "ASSOCIATORS OF {Win32_DiskPartition.DeviceID=\"" + partDevId + "\"} WHERE AssocClass=Win32_LogicalDiskToPartition");
                            foreach (var ld in logSearcher.Get())
                            {
                                var logicalDevId = ld["DeviceID"]?.ToString() ?? "";
                                diskByLogical[logicalDevId] = (idx, model, size);
                            }
                        }
                    }
                }

                var systemDrive = GetSystemDriveLetter();
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=2"))
                {
                    foreach (var ld in searcher.Get())
                    {
                        var deviceId = ld["DeviceID"]?.ToString() ?? "";
                        var size = Convert.ToInt64(ld["Size"] ?? 0);
                        if (size <= 0) continue;

                        var letter = deviceId.Replace(":", "").Trim();
                        (int Index, string Model, long Size) disk = diskByLogical.ContainsKey(deviceId) ? diskByLogical[deviceId] : (-1, "", size);
                        result.Add(new RemovableDisk
                        {
                            Index = disk.Index,
                            Model = disk.Model,
                            Size = size,
                            SizeDisplay = FormatSize(size),
                            DriveLetter = letter,
                            FileSystem = ld["FileSystem"]?.ToString() ?? "",
                            Label = ld["VolumeName"]?.ToString() ?? "",
                            UsedSize = size - Convert.ToInt64(ld["FreeSpace"] ?? 0),
                            FreeSize = Convert.ToInt64(ld["FreeSpace"] ?? 0),
                            FreeSizeDisplay = FormatSize(Convert.ToInt64(ld["FreeSpace"] ?? 0)),
                            IsSystem = letter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase),
                        });
                    }
                }

                // 系统盘过滤 + 磁盘号排序
                result = result.Where(r => !r.IsSystem).OrderBy(r => r.Index >= 0 ? 0 : 1).ThenBy(r => r.Index).ToList();
            }
            catch { }
            return result;
        }

        /// <summary>安全校验（写盘前强制，返回错误列表；空列表表示通过）</summary>
        public List<string> ValidateTarget(RemovableDisk? disk)
        {
            var errors = new List<string>();
            if (disk == null) { errors.Add("未选择 U 盘"); return errors; }
            if (disk.IsSystem) errors.Add("目标盘为系统盘，禁止操作");
            if (disk.Size < 512L * 1024 * 1024) errors.Add("U 盘容量不足 512MB");
            if (disk.Index < 0) errors.Add("无法确定目标磁盘号，请重新插入 U 盘后刷新");
            return errors;
        }

        /// <summary>下载 PE 文件到本地缓存（带进度 + 哈希校验）</summary>
        public async Task<(bool Ok, string Path, string Error)> DownloadPeAsync(
            PeVersionInfo pe, string downloadUrl, string cacheDir,
            IProgress<int>? progress = null, CancellationToken ct = default)
        {
            try
            {
                Directory.CreateDirectory(cacheDir);
                var ext = Path.GetExtension(pe.FileName);
                if (string.IsNullOrEmpty(ext)) ext = Path.GetExtension(downloadUrl);
                if (string.IsNullOrEmpty(ext)) ext = ".iso";
                var fileName = SafeFileName(pe.Name + "_" + pe.Version) + ext.ToLowerInvariant();
                var savePath = Path.Combine(cacheDir, fileName);

                if (File.Exists(savePath) && pe.FileHash.Length >= 32 && VerifyHash(savePath, pe.FileHash))
                    return (true, savePath, "");

                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? pe.FileSize;

                var tmp = savePath + ".part";
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var stream = await response.Content.ReadAsStreamAsync(ct))
                {
                    var buffer = new byte[1024 * 256];
                    long written = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                        written += read;
                        progress?.Report(total > 0 ? (int)(written * 100 / total) : 0);
                    }
                }

                if (pe.FileHash.Length >= 32 && !VerifyHash(tmp, pe.FileHash))
                {
                    try { File.Delete(tmp); } catch { }
                    return (false, "", "文件校验失败（哈希不一致），请重新下载");
                }

                File.Move(tmp, savePath, true);
                return (true, savePath, "");
            }
            catch (OperationCanceledException)
            {
                return (false, "", "已取消下载");
            }
            catch (Exception ex)
            {
                return (false, "", "下载失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 生成可引导 ISO：解包 PE（ISO 挂载/目录）到临时目录 → 覆盖写引导镜像 → IsoBuilder 打包。
        /// 返回输出 ISO 路径。支持 UEFI + Legacy 双引导（取决于 PE 自带的 efi 引导镜像）。
        /// </summary>
        public async Task<(bool Ok, string Error, string OutPath)> BuildIsoAsync(
            IsoBuildPlan plan, Action<string>? log, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            void Log(string m) => log?.Invoke(m);
            try
            {
                var staging = Path.Combine(Path.GetTempPath(), "ZS_IsoBuild_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    // ① PE 内容 → staging
                    Log("准备 PE 内容...");
                    var ext = Path.GetExtension(plan.PeFilePath).ToLowerInvariant();
                    if (ext == ".iso")
                    {
                        var mounted = await MountIsoAsync(plan.PeFilePath);
                        if (string.IsNullOrEmpty(mounted))
                            return (false, "无法挂载 PE 镜像（ISO 挂载失败）", "");
                        try { await CopyDirectoryAsync(mounted, staging, log, null, ct); }
                        finally { await DismountIsoAsync(plan.PeFilePath); }
                    }
                    else if (Directory.Exists(plan.PeFilePath))
                    {
                        await CopyDirectoryAsync(plan.PeFilePath, staging, log, null, ct);
                    }
                    else
                    {
                        return (false, "PE 源不是有效的 ISO 或文件夹", "");
                    }
                    progress?.Report(25);

                    // ② 写入装机镜像 + 离线无人值守任务（方案A：PE 无网也可无人值守装机）
                    if (plan.IncludeOfflineImage)
                    {
                        Log("写入装机镜像与离线无人值守任务...");
                        if (string.IsNullOrEmpty(plan.OfflineImagePath) || !File.Exists(plan.OfflineImagePath))
                            return (false, "未选择有效的装机镜像文件", "");
                        if (!InjectOfflineImageAndTask(staging, plan.OfflineImagePath,
                                plan.OfflineUnattendPath, plan.OfflineFirstLogonPath,
                                plan.OfflineAdminPassword, "usb", Log))
                            return (false, "写入装机镜像/离线任务失败", "");
                        progress?.Report(38);
                    }

                    // ④ 检测/兜底生成引导镜像
                    var (efiRel, legacyRel) = DetectAndPrepareBoot(staging, log);

                    // ④ IsoBuilder 打包
                    Log("构建 ISO 镜像...");
                    var req = new IsoBuilder.BuildRequest
                    {
                        OutputPath = plan.OutputPath,
                        Label = string.IsNullOrEmpty(plan.IsoLabel) ? "ZS_PE" : plan.IsoLabel,
                        SourceDir = staging,
                        EfiBootRel = efiRel,
                        LegacyBootRel = legacyRel,
                    };
                    await IsoBuilder.BuildAsync(req,
                        new Progress<double>(d => progress?.Report((int)(35 + d * 65))), ct);
                    Log("ISO 生成完成: " + plan.OutputPath);
                    return (true, "", plan.OutputPath);
                }
                finally
                {
                    try { Directory.Delete(staging, true); } catch { }
                }
            }
            catch (OperationCanceledException)
            {
                return (false, "已取消生成", "");
            }
            catch (Exception ex)
            {
                Log("生成失败: " + ex.Message);
                return (false, "生成失败: " + ex.Message, "");
            }
        }

        /// <summary>从 staging 目录查找 UEFI/Legacy 引导镜像；缺少 efisys.bin 时用 bootx64.efi 生成 FAT12 镜像兜底。</summary>
        private static (string? EfiRel, string? LegacyRel) DetectAndPrepareBoot(string staging, Action<string>? log)
        {
            string? FindFirst(params string[] names)
            {
                foreach (var n in names)
                {
                    var hit = Directory.GetFiles(staging, n, SearchOption.AllDirectories)
                        .FirstOrDefault(f => File.Exists(f));
                    if (hit != null)
                        return "/" + Path.GetRelativePath(staging, hit).Replace('\\', '/');
                }
                return null;
            }

            // UEFI：efisys.bin 原生，其次 bootx64.efi
            var efi = FindFirst("EFISYS.BIN", "BOOTX64.EFI");
            if (efi != null && !efi.EndsWith("/EFISYS.BIN", StringComparison.OrdinalIgnoreCase))
            {
                // 只有 BOOTX64.EFI（无 efisys.bin）：生成 efisys.bin 兜底
                var bootx64 = Path.Combine(staging, efi.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var generated = Path.Combine(staging, "EFI", "BOOT", "efisys.bin");
                if (EfiSysGenerator.TryCreate(bootx64, generated, log))
                    efi = "/EFI/BOOT/efisys.bin";
            }

            var legacy = FindFirst("ETFSYS.COM", "ETFSBOOT.COM", "ETFSYS.BIN");
            log?.Invoke(efi != null ? "已配置 UEFI 引导镜像: " + efi : "未检测到 UEFI 引导镜像（将生成仅 Legacy/无引导 ISO）");
            if (legacy != null) log?.Invoke("已配置 Legacy 引导镜像: " + legacy);

            return (efi, legacy);
        }

        /// <summary>执行写盘：格式化 → 挂载PE拷贝 → 写引导 → 拷入客户端（逐步骤回调）</summary>
        public async Task<(bool Ok, string Error)> WriteAsync(
            WritePlan plan, string peFilePath, string clientDir,
            Action<UdiskExecStep>? stepCallback, Action<string>? log,
            CancellationToken ct, IProgress<int>? progress = null)
        {
            void SetStep(string name, string status, string detail)
                => stepCallback?.Invoke(new UdiskExecStep { Name = name, Status = status, Detail = detail });
            void Log(string msg) => log?.Invoke(msg);

            try
            {
                // ① 格式化 + 分区（DiskPart）
                SetStep("清空并格式化目标盘", "running", "DiskPart 执行中...");
                Log("正在对磁盘 " + plan.DiskIndex + " 执行分区与格式化...");
                if (!await RunDiskPartAsync(BuildDiskPartScript(plan)))
                {
                    SetStep("清空并格式化目标盘", "failed", "DiskPart 执行失败");
                    return (false, "格式化失败，请检查 U 盘是否被占用或权限不足");
                }
                SetStep("清空并格式化目标盘", "completed", "格式化完成");
                Log("格式化完成");
                progress?.Report(10);
                ct.ThrowIfCancellationRequested();

                // 目标盘符（格式化后由 assign 分配，通过卷标反查）
                var targetDrive = await FindDriveByLabel(plan.VolumeLabel, TimeSpan.FromSeconds(15));
                if (string.IsNullOrEmpty(targetDrive))
                {
                    SetStep("定位目标盘符", "failed", "未找到格式化后的盘符");
                    return (false, "格式化后未找到目标盘符");
                }
                SetStep("定位目标盘符", "completed", "盘符 " + targetDrive + ":");
                Log("目标盘符: " + targetDrive + ":");
                progress?.Report(15);

                // ② 拷贝 PE 内容
                SetStep("写入 PE 系统", "running", "正在拷贝...");
                Log("开始拷贝 PE 到 " + targetDrive + ": ...");
                var peProgress = new Progress<int>(p => progress?.Report(15 + p * 30 / 100));
                var peCopy = await CopyPeToDriveAsync(peFilePath, targetDrive, log, peProgress, ct);
                if (!peCopy.Ok)
                {
                    SetStep("写入 PE 系统", "failed", peCopy.Error);
                    return (false, peCopy.Error);
                }
                SetStep("写入 PE 系统", "completed", "PE 拷贝完成");
                Log("PE 拷贝完成");
                progress?.Report(50);
                ct.ThrowIfCancellationRequested();

                // ③ 写引导
                if (plan.BootType == "uefi" || plan.BootType == "both")
                {
                    SetStep("写入 UEFI 引导", "running", "创建 EFI\\BOOT");
                    var uefiOk = EnsureUefiBoot(targetDrive, peCopy.PeRoot);
                    SetStep("写入 UEFI 引导", uefiOk ? "completed" : "failed", uefiOk ? "UEFI 引导就绪" : "未找到 EFI 引导文件（可忽略，尝试 Legacy）");
                    Log(uefiOk ? "UEFI 引导写入完成" : "UEFI 引导文件缺失，跳过");
                }
                ct.ThrowIfCancellationRequested();

                if (plan.BootType == "legacy" || plan.BootType == "both")
                {
                    SetStep("写入 Legacy 引导", "running", "bootsect /nt60");
                    var legacyOk = await WriteLegacyBootAsync(targetDrive);
                    SetStep("写入 Legacy 引导", legacyOk ? "completed" : "failed", legacyOk ? "Legacy 引导就绪" : "bootsect 不可用");
                    Log(legacyOk ? "Legacy 引导写入完成" : "Legacy 引导写入失败（bootsect 不可用）");
                }
                progress?.Report(60);
                ct.ThrowIfCancellationRequested();

                // ④ 写入装机镜像 + 离线无人值守任务（方案A：PE 无网也可无人值守装机）
                if (plan.IncludeOfflineImage)
                {
                    SetStep("写入装机镜像+无人值守", "running", "拷贝镜像到 ZS_Images");
                    Log("写入装机镜像与离线无人值守任务...");
                    if (string.IsNullOrEmpty(plan.OfflineImagePath) || !File.Exists(plan.OfflineImagePath))
                    {
                        SetStep("写入装机镜像+无人值守", "failed", "未选择有效的装机镜像文件");
                        return (false, "未选择有效的装机镜像文件");
                    }
                    var offlineOk = InjectOfflineImageAndTask(targetDrive + ":\\", plan.OfflineImagePath,
                        plan.OfflineUnattendPath, plan.OfflineFirstLogonPath,
                        plan.OfflineAdminPassword, "usb", Log);
                    if (!offlineOk)
                    {
                        SetStep("写入装机镜像+无人值守", "failed", "写入失败");
                        return (false, "写入装机镜像/离线任务失败");
                    }
                    SetStep("写入装机镜像+无人值守", "completed", "镜像与 zs_task.json 已写入");
                    progress?.Report(97);
                    ct.ThrowIfCancellationRequested();
                }

                SetStep("制作完成", "completed", "可以安全拔出 U 盘");
                Log("U盘制作完成，可以安全拔出");
                progress?.Report(100);
                return (true, "");
            }
            catch (OperationCanceledException)
            {
                SetStep("制作已取消", "canceled", "");
                return (false, "已取消");
            }
            catch (Exception ex)
            {
                SetStep("制作失败", "failed", ex.Message);
                return (false, "制作失败: " + ex.Message);
            }
        }

        // ==================== 内部工具方法 ====================

        private static string BuildDiskPartScript(WritePlan plan)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("select disk " + plan.DiskIndex);
            sb.AppendLine("clean");
            if (plan.BootType != "legacy")
                sb.AppendLine("convert gpt");
            if (plan.PartitionScheme == "esp")
            {
                sb.AppendLine("create partition efi size=512");
                sb.AppendLine("format quick fs=fat32 label=\"ESP\"");
                sb.AppendLine("assign letter=S");
                sb.AppendLine("create partition primary");
            }
            else
            {
                sb.AppendLine("create partition primary");
            }
            sb.AppendLine("format quick fs=" + plan.FileSystem + " label=\"" + plan.VolumeLabel + "\"");
            sb.AppendLine("assign");
            return sb.ToString();
        }

        private static async Task<bool> RunDiskPartAsync(string script)
        {
            try
            {
                var temp = Path.Combine(Path.GetTempPath(), "udisk_" + Guid.NewGuid() + ".txt");
                await File.WriteAllTextAsync(temp, script, new System.Text.UTF8Encoding(false));
                var psi = new ProcessStartInfo
                {
                    FileName = "diskpart.exe",
                    Arguments = "/s \"" + temp + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                await p.WaitForExitAsync();
                try { File.Delete(temp); } catch { }
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>按卷标查找盘符（等待格式化分配盘符）</summary>
        private static async Task<string> FindDriveByLabel(string label, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=2");
                    foreach (var ld in searcher.Get())
                    {
                        if ((ld["VolumeName"]?.ToString() ?? "").Equals(label, StringComparison.OrdinalIgnoreCase))
                            return (ld["DeviceID"]?.ToString() ?? "").Replace(":", "");
                    }
                }
                catch { }
                await Task.Delay(500);
            }
            return "";
        }

        private static string GetSystemDriveLetter()
        {
            try { return Environment.GetFolderPath(Environment.SpecialFolder.Windows).Substring(0, 2).Replace(":", ""); }
            catch { return "C"; }
        }

        private static int ParseDiskIndex(string deviceId)
        {
            var idx = deviceId.LastIndexOf('\\');
            if (idx >= 0 && int.TryParse(deviceId.Substring(idx + 1), out var n)) return n;
            return -1;
        }

        /// <summary>拷贝 PE 到 U 盘：ISO 挂载后拷贝 / 目录直接拷贝 / WIM 平铺</summary>
        private static async Task<(bool Ok, string Error, string PeRoot)> CopyPeToDriveAsync(
            string peFilePath, string targetDrive, Action<string>? log, IProgress<int>? progress, CancellationToken ct)
        {
            try
            {
                var ext = Path.GetExtension(peFilePath).ToLowerInvariant();
                if (ext == ".iso")
                {
                    // PowerShell 挂载 ISO
                    var mounted = await MountIsoAsync(peFilePath);
                    if (string.IsNullOrEmpty(mounted)) return (false, "ISO 挂载失败", "");
                    try
                    {
                        await CopyDirectoryAsync(mounted, targetDrive + ":\\", log, progress, ct);
                        return (true, "", mounted);
                    }
                    finally { await DismountIsoAsync(peFilePath); }
                }
                else if (Directory.Exists(peFilePath))
                {
                    await CopyDirectoryAsync(peFilePath, targetDrive + ":\\", log, progress, ct);
                    return (true, "", peFilePath);
                }
                else if (ext == ".wim" || ext == ".esd")
                {
                    // 裸 WIM 无法直接引导，作为数据拷入 PE 目录
                    Directory.CreateDirectory(Path.Combine(targetDrive + ":\\", "PE"));
                    File.Copy(peFilePath, Path.Combine(targetDrive + ":\\PE", Path.GetFileName(peFilePath)), true);
                    return (true, "", "");
                }
                return (false, "不支持的 PE 文件类型: " + ext, "");
            }
            catch (Exception ex)
            {
                return (false, "拷贝 PE 失败: " + ex.Message, "");
            }
        }

        /// <summary>挂载 ISO 并返回盘符（用 -EncodedCommand 规避引号转义；轮询等待盘符分配）</summary>
        private static async Task<string> MountIsoAsync(string isoPath)
        {
            try
            {
                var safePath = isoPath.Replace("'", "''");
                // 单引号路径已转义，避免命令注入；脚本用 UTF-16LE Base64 编码彻底规避 shell 转义问题
                // 挂载后通过 Win32_LogicalDisk(DriveType=5 CD-ROM) 前后对比精确取新盘符，避免误取系统盘/物理光驱
                var script = "$before = @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=5' | ForEach-Object { $_.DeviceID }); " +
                             "$m = Mount-DiskImage -ImagePath '" + safePath + "' -PassThru; " +
                             "if ($m -and $m.Attached) { " +
                             "$drv = $null; " +
                             "for ($i = 0; $i -lt 20 -and -not $drv; $i++) { " +
                             "  $now = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=5' -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.DeviceID }; " +
                             "  if ($now) { $drv = $now | Select-Object -First 1 -ExpandProperty DeviceID } " +
                             "  if (-not $drv) { Start-Sleep -Milliseconds 500 } " +
                             "} " +
                             "if ($drv) { Write-Output $drv.TrimEnd(':') } }";
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi);
                if (p == null) return "";
                var outText = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                return outText?.Trim().TrimEnd('\r', '\n') ?? "";
            }
            catch { return ""; }
        }

        private static async Task DismountIsoAsync(string isoPath)
        {
            try
            {
                var safePath = isoPath.Replace("'", "''");
                var script = "Dismount-DiskImage -ImagePath '" + safePath + "' -ErrorAction SilentlyContinue";
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();
            }
            catch { }
        }

        private static async Task CopyDirectoryAsync(string src, string dst, Action<string>? log, IProgress<int>? progress, CancellationToken ct)
        {
            // 裸盘符（如 "E"）规范化为 "E:\"，避免被当作相对路径
            if (src.Length == 1 && char.IsLetter(src[0])) src = src + ":\\";
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, dir)));
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            var total = files.Length;
            var count = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(src, file);
                var target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
                count++;
                if (count % 20 == 0 || count == total)
                    log?.Invoke("拷贝 " + rel + " (" + count + "/" + total + ")");
                progress?.Report(total > 0 ? (int)(count * 100 / total) : 100);
            }
        }

        private static void CopyDirectoryRecursive(string src, string dst, Action<string>? log, IProgress<int>? progress = null)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, dir)));
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            var count = 0;
            foreach (var file in files)
            {
                var target = Path.Combine(dst, Path.GetRelativePath(src, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
                count++;
                progress?.Report(files.Length > 0 ? (int)(count * 100 / files.Length) : 100);
            }
        }

        /// <summary>写入 UEFI 引导：拷贝 PE 的 EFI\BOOT\bootx64.efi 到 U 盘标准路径</summary>
        private static bool EnsureUefiBoot(string targetDrive, string peRoot)
        {
            try
            {
                var srcEfi = Path.Combine(peRoot, "EFI", "BOOT", "bootx64.efi");
                if (!File.Exists(srcEfi))
                {
                    // 兼容 efi/boot 小写路径
                    srcEfi = Path.Combine(peRoot, "efi", "boot", "bootx64.efi");
                }
                if (!File.Exists(srcEfi)) return false;

                var dst = Path.Combine(targetDrive + ":\\", "EFI", "BOOT");
                Directory.CreateDirectory(dst);
                File.Copy(srcEfi, Path.Combine(dst, "bootx64.efi"), true);

                // 一并拷贝 efi\microsoft（部分 PE 依赖 BCD）
                var srcMs = Path.Combine(peRoot, "EFI", "Microsoft");
                if (Directory.Exists(srcMs))
                    CopyDirectoryRecursive(srcMs, Path.Combine(targetDrive + ":\\", "EFI", "Microsoft"), _ => { });
                return true;
            }
            catch { return false; }
        }

        /// <summary>写入 Legacy 引导：bootsect /nt60（写入 MBR 引导代码）</summary>
        private static async Task<bool> WriteLegacyBootAsync(string targetDrive)
        {
            try
            {
                var bootsect = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Boot", "bootsect.exe");
                if (!File.Exists(bootsect)) return false;
                var psi = new ProcessStartInfo
                {
                    FileName = bootsect,
                    Arguments = "/nt60 " + targetDrive + ": /mbr",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                await p.WaitForExitAsync();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool VerifyHash(string filePath, string expected)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hash = Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
                return hash.Equals(expected.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        /// <summary>当前进程是否以管理员身份运行（U盘制作前置检查）</summary>
        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F1") + " GB";
        }

        // ==================== 方案A：离线无人值守注入 ====================

        /// <summary>
        /// 把装机镜像 + 离线无人值守任务（zs_task.json）写入目标根目录（U盘/ISO staging 共用）。
        /// 镜像拷贝到 ZS_Images\，任务文件写到根目录，PE 端 OfflineTaskService 扫描到后即可无网装机。
        /// 无人值守应答：优先使用用户选择的 .xml；未选时按管理员密码生成默认模板。
        /// </summary>
        public bool InjectOfflineImageAndTask(
            string destRoot, string imagePath, string? unattendPath, string? firstLogonPath,
            string adminPassword, string source, Action<string>? log)
        {
            try
            {
                log?.Invoke("拷贝装机镜像到 ZS_Images...");
                var imageDir = Path.Combine(destRoot, "ZS_Images");
                Directory.CreateDirectory(imageDir);
                var imageName = Path.GetFileName(imagePath);
                var targetImage = Path.Combine(imageDir, imageName);
                File.Copy(imagePath, targetImage, true);

                // 无人值守应答：用户文件优先，否则默认模板（基于管理员密码）
                string unattendXml = "";
                if (!string.IsNullOrEmpty(unattendPath) && File.Exists(unattendPath))
                    unattendXml = File.ReadAllText(unattendPath);
                else if (!string.IsNullOrEmpty(adminPassword))
                    unattendXml = DefaultUnattendXml(adminPassword);

                // 首次登录脚本（可选）
                string firstLogonCmd = "";
                if (!string.IsNullOrEmpty(firstLogonPath) && File.Exists(firstLogonPath))
                    firstLogonCmd = File.ReadAllText(firstLogonPath);

                var info = new FileInfo(targetImage);
                var task = new OfflineTask
                {
                    Version = 1,
                    Source = source,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TaskNo = OfflineTaskService.NewTaskNo(),
                    Image = new OfflineImageInfo
                    {
                        Name = imageName,
                        FileName = imageName,
                        FilePath = "ZS_Images\\" + imageName,
                        FileHash = ComputeSha256(targetImage),
                        FileSize = info.Length,
                        SizeDisplay = FormatSize(info.Length),
                    },
                    Disk = null,
                    TargetPartition = "C:",
                    PartitionScheme = "auto",
                    Options = new OfflineOptions
                    {
                        BackupData = true,
                        AutoPartition = true,
                        DriverInject = true,
                        BootFix = true,
                        Unattended = !string.IsNullOrEmpty(unattendXml),
                        InstallSoftware = false,
                        Optimize = true,
                    },
                    UnattendXml = unattendXml,
                    FirstLogonCmd = firstLogonCmd,
                };

                var taskPath = Path.Combine(destRoot, "zs_task.json");
                if (!OfflineTaskService.Write(taskPath, task))
                {
                    log?.Invoke("写入 zs_task.json 失败");
                    return false;
                }
                log?.Invoke("离线无人值守任务已写入 " + taskPath);
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("离线任务注入失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>生成默认无人值守应答模板（标准 OOBE 跳过 + 本地管理员自动登录）</summary>
        public static string DefaultUnattendXml(string adminPassword)
        {
            var safePwd = (adminPassword ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">\n" +
                "    <settings pass=\"specialize\">\n" +
                "        <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "            <ComputerName>ZS-PC</ComputerName>\n" +
                "            <TimeZone>China Standard Time</TimeZone>\n" +
                "        </component>\n" +
                "    </settings>\n" +
                "    <settings pass=\"oobeSystem\">\n" +
                "        <component name=\"Microsoft-Windows-International-Core\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "            <InputLocale>zh-CN</InputLocale>\n" +
                "            <SystemLocale>zh-CN</SystemLocale>\n" +
                "            <UILanguage>zh-CN</UILanguage>\n" +
                "            <UserLocale>zh-CN</UserLocale>\n" +
                "        </component>\n" +
                "        <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "            <OOBE>\n" +
                "                <HideEULAPage>true</HideEULAPage>\n" +
                "                <HideLocalAccountScreen>true</HideLocalAccountScreen>\n" +
                "                <HideOnlineAccountScreens>true</HideOnlineAccountScreens>\n" +
                "                <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>\n" +
                "                <NetworkLocation>Work</NetworkLocation>\n" +
                "                <ProtectYourPC>3</ProtectYourPC>\n" +
                "                <SkipMachineOOBE>true</SkipMachineOOBE>\n" +
                "                <SkipUserOOBE>true</SkipUserOOBE>\n" +
                "            </OOBE>\n" +
                "            <UserAccounts>\n" +
                "                <LocalAccounts>\n" +
                "                    <LocalAccount wcm:action=\"add\">\n" +
                "                        <Password>\n" +
                "                            <Value>" + safePwd + "</Value>\n" +
                "                            <PlainText>true</PlainText>\n" +
                "                        </Password>\n" +
                "                        <DisplayName>admin</DisplayName>\n" +
                "                        <Group>Administrators</Group>\n" +
                "                        <Name>admin</Name>\n" +
                "                    </LocalAccount>\n" +
                "                </LocalAccounts>\n" +
                "            </UserAccounts>\n" +
                "            <AutoLogon>\n" +
                "                <Password>\n" +
                "                    <Value>" + safePwd + "</Value>\n" +
                "                    <PlainText>true</PlainText>\n" +
                "                </Password>\n" +
                "                <Enabled>true</Enabled>\n" +
                "                <LogonCount>1</LogonCount>\n" +
                "                <Username>admin</Username>\n" +
                "            </AutoLogon>\n" +
                "            <RegisteredOwner>ZS</RegisteredOwner>\n" +
                "            <RegisteredOrganization>ZS</RegisteredOrganization>\n" +
                "        </component>\n" +
                "    </settings>\n" +
                "</unattend>\n";
        }

        private static string ComputeSha256(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            }
            catch { return ""; }
        }
    }
}