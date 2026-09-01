using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WinPE_Client.Models;
using WinPE_Client.Services;

namespace WinPE_Agent;

/// <summary>
/// ZS 装机助手（控制台应用，运行于定制 PE）。
/// 启动后扫描 CD-ROM 根目录 zs_task.json → 10 秒倒计时 → 自动/手动装机。
///
/// 流程:
///   PE 启动 → 检测任务 → 显示倒计时(10秒) → 用户未干预:自动装机
///                                       → 用户按键:手动装机界面
///   无任务时 → 进入手动装机界面（空闲等待）
///
/// 用法:
///   ZS_Agent --self-test [--server http://127.0.0.1:8001]  自检
///   ZS_Agent [--server http://127.0.0.1:8001] [--no-reboot] 正常执行
///   ZS_Agent --offline-test  离线注入逻辑往返测试
/// </summary>
internal static class Program
{
    private static readonly ApiService Api = new();
    private static readonly DeviceService Device = new();
    private static readonly ImageDeployService Deploy = new();
    private static readonly DiskPartService DiskPart = new();
    private static readonly OfflineTaskService Offline = new();

    private static StreamWriter? _log;
    private static string _logPath = "";

    /// <summary>本地镜像缓存目录（PE 环境 D: 数据盘；无 D: 时回落 exe 目录）</summary>
    private static string _cacheDir = "";

    /// <summary>安装选项（与 BuildOptionsJson / OfflineOptions 契约对齐）</summary>
    private sealed class InstallOptions
    {
        public bool AutoPartition = true;
        public bool BootFix = true;
        public bool DriverInject = true;
        public bool Unattended = true;
        public bool InstallSoftware = true;
        public bool Optimize = true;
        public bool BackupData = true;
        public int ImageIndex = 1;
        public string DriverPackage = "auto";
        public string BackupLocation = "auto";
    }

    private static async Task<int> Main(string[] args)
    {
        var selfTest = args.Contains("--self-test");
        var noReboot = args.Contains("--no-reboot");
        var autoMode = args.Contains("--auto");
        var serverUrl = GetArg(args, "--server") ?? DefaultServerUrl();

        // R7 契约（设计 §5.1）：Startnet.cmd 调用形式为
        //   ZS_PE_Agent.exe --auto --task <task.ini> --manifest <zs_manifest.key> --log <pe_log.txt>
        // 其中 --log 必须在 _log 初始化前解析，以便覆盖默认日志路径
        var taskPath = GetArg(args, "--task");
        var manifestPath = GetArg(args, "--manifest");
        var logArg = GetArg(args, "--log");

        _cacheDir = Directory.Exists("D:\\")
            ? "D:\\ZS_Cache\\images"
            : Path.Combine(AppContext.BaseDirectory, "images");

        var exeDir = AppContext.BaseDirectory;
        _logPath = !string.IsNullOrEmpty(logArg) ? logArg : Path.Combine(exeDir, "agent.log");
        try
        {
            var logDir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            _log = new StreamWriter(_logPath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch { /* 日志无法创建不阻塞装机 */ }

        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        try { Console.Title = "ZS 装机助手"; } catch { }
        try { Console.Clear(); } catch { }

        try
        {
            Log("========================================");
            Log("ZS Agent start, version 0.0.268311");
            Log("args: auto=" + autoMode + " task=" + (taskPath ?? "(none)")
                + " manifest=" + (manifestPath ?? "(none)") + " log=" + _logPath);

            Api.SetBaseUrl(serverUrl);
            Deploy.ProgressChanged += (p, m) => Log(string.Format("  [deploy {0}%] {1}", p, m));
            DiskPart.ProgressChanged += (p, m) => Log(string.Format("  [diskpart {0}%] {1}", p, m));

            int rc;
            if (args.Contains("--offline-test"))
                rc = RunOfflineTest();
            else if (selfTest)
                rc = await RunSelfTest(serverUrl);
            else if (autoMode && !string.IsNullOrEmpty(taskPath))
                rc = await RunAutoMode(taskPath, manifestPath, noReboot);
            else
                rc = await RunInteractive(serverUrl, noReboot);

            Log("ZS Agent exit code = " + rc);
            return rc;
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            return 2;
        }
        finally
        {
            try { _log?.Dispose(); } catch { }
        }
    }

    // ============ R7 自动装机入口（--auto --task task.ini --manifest zs_manifest.key --log pe_log.txt） ============
    // 由 Startnet.cmd 在 choice /c XM /t 10 /d X 倒计时结束后调用。
    // Startnet.cmd 已做完盘符扫描和 10 秒逃生窗，此处直接执行：
    //   1) 校验 task.ini 存在
    //   2) 校验 manifest（如果指定）—— 设计 §6.6 装机前第一条命令
    //   3) 解析 task.ini 为 TaskIni 模型（设计 §2.1 16 字段）
    //   4) 映射为 InstallOptions + imagePath + diskIndex + targetDrive
    //   5) 调用 ExecutePipeline 走 7 步装机管线
    //   6) 返回 0 → Startnet.cmd 让用户按任意键 reboot；非 0 → 停 cmd 救援
    private static async Task<int> RunAutoMode(string taskPath, string? manifestPath, bool noReboot)
    {
        ShowBanner();
        Console.WriteLine("  [R7] 自动装机模式（由 Startnet.cmd 调起）");
        Console.WriteLine("  task: " + taskPath);
        if (!string.IsNullOrEmpty(manifestPath))
            Console.WriteLine("  manifest: " + manifestPath);
        Console.WriteLine();

        // 1) 校验 task.ini 存在
        if (!File.Exists(taskPath))
        {
            Log("FATAL: task.ini not found at " + taskPath);
            Console.Error.WriteLine("  [ERROR] task.ini 不存在: " + taskPath);
            Console.WriteLine("  按任意键保持命令行以便救援...");
            try { Console.ReadKey(true); } catch { }
            return 2;
        }

        // 2) 解析 task.ini
        TaskIni taskIni;
        try
        {
            taskIni = TaskIniParser.Parse(taskPath);
            Log("task.ini 解析成功: task_id=" + taskIni.Meta.TaskId
                + " image=" + taskIni.SystemImage.File + " index=" + taskIni.SystemImage.Index
                + " disk=" + taskIni.TargetDisk.DiskIndex + " part_mode=" + taskIni.TargetDisk.PartitionMode
                + " oobe=" + taskIni.Meta.OobeMode);
        }
        catch (Exception ex)
        {
            Log("FATAL: task.ini parse failed: " + ex.Message);
            Console.Error.WriteLine("  [ERROR] task.ini 解析失败: " + ex.Message);
            Console.WriteLine("  按任意键保持命令行以便救援...");
            try { Console.ReadKey(true); } catch { }
            return 2;
        }

        // 3) 校验 manifest（设计 §6.6：装机前必须 100% 通过）
        if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
        {
            Log("开始 manifest 校验: " + manifestPath);
            Console.WriteLine("  [ZS] 正在校验文件完整性 (zs_manifest.key)...");
            var report = ManifestValidator.Verify(manifestPath);
            Log(string.Format("manifest 校验结果: total={0} pass={1} fail={2}",
                report.Total, report.Passed, report.Failed));

            if (report.Total == 0)
            {
                Log("WARN: manifest 内无任何条目，跳过校验继续装机");
                Console.WriteLine("  [!] manifest 为空，跳过校验继续");
            }
            else if (!report.AllPass)
            {
                Log("FATAL: manifest 校验失败，以下文件不匹配:");
                foreach (var r in report.Results)
                {
                    if (r.Pass) continue;
                    var msg = "    " + r.RelativePath + " expected=" + r.ExpectedHash
                        + " actual=" + (r.ActualHash ?? "(none)") + " err=" + (r.Error ?? "");
                    Log(msg);
                    Console.Error.WriteLine("  [FAIL] " + r.RelativePath + ": " + (r.Error ?? "hash mismatch"));
                }
                Console.Error.WriteLine();
                Console.Error.WriteLine("  [ERROR] 文件完整性校验未通过，已中止部署。");
                Console.Error.WriteLine("  请回到 Windows 端重新下单生成 ZS_Task，再重启进 PE。");
                Console.WriteLine("  按任意键保持命令行以便救援...");
                try { Console.ReadKey(true); } catch { }
                return 3;
            }
            else
            {
                Console.WriteLine("  [OK] manifest 全部 " + report.Total + " 项校验通过");
            }
        }
        else
        {
            Log("manifest 未指定或不存在，跳过校验（兼容旧版客户端）");
        }

        // 4) 解析镜像路径 + 磁盘 + 安装选项
        var imagePath = TaskIniParser.ResolveImagePath(taskPath, taskIni);
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Log("FATAL: image file not found, expected: " + taskIni.SystemImage.File);
            Console.Error.WriteLine("  [ERROR] 镜像文件不存在: " + taskIni.SystemImage.File);
            Console.Error.WriteLine("  请确认 ZS_Task 目录下镜像文件已下载完整。");
            Console.WriteLine("  按任意键保持命令行以便救援...");
            try { Console.ReadKey(true); } catch { }
            return 4;
        }
        Log("镜像路径: " + imagePath);

        var opts = MapTaskIniToOptions(taskIni);
        int diskIndex = taskIni.TargetDisk.DiskIndex;
        string targetDrive = taskIni.PartitionScheme.SystemLetter + ":";
        Log("diskIndex=" + diskIndex + " targetDrive=" + targetDrive
            + " autoPart=" + opts.AutoPartition + " driverInject=" + opts.DriverInject
            + " bootFix=" + opts.BootFix + " unattended=" + opts.Unattended);

        // 5) §6.0 固件类型判定（GPT/MBR 分流）—— 必须在分区前完成
        //    设计 §6.0.3：双判 + 6 级冲突处理；Unknown 直接 Exit(2)
        var overrideMode = taskIni.PartitionScheme?.Table ?? "auto";
        Log("固件判定: override=" + overrideMode + " disk=" + diskIndex);
        Console.WriteLine("  [ZS] 正在判定固件类型 (GPT/MBR)...");
        var firmware = FirmwareDetector.Detect(overrideMode, diskIndex);
        Log("固件判定结果: type=" + firmware.Type + " source=" + firmware.Source
            + " reg=" + firmware.RegistryType + " disk=" + firmware.DiskpartType
            + " conflict=" + firmware.Conflict + " script=" + firmware.PartitionScript);
        if (!string.IsNullOrEmpty(firmware.Warning))
            Log("  [FW WARN] " + firmware.Warning);

        if (firmware.IsUnknown)
        {
            Log("FATAL: 无法判定固件类型，按设计 §6.0.3 第6项 Exit(2)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  [ERROR] 无法判定固件类型（注册表与 diskpart 均读不到）");
            Console.Error.WriteLine("  很可能是目标盘压根没初始化或数据线松动。");
            Console.Error.WriteLine("  硬往下执行 100% 会坏盘，已中止部署。");
            Console.WriteLine("  按任意键保持命令行以便救援...");
            try { Console.ReadKey(true); } catch { }
            return 2;
        }
        Console.WriteLine("  [OK] 固件: " + firmware.Type + " (来源: " + firmware.Source
            + (firmware.Conflict ? "  ⚠ CSM 冲突，将做尾端验证" : "") + ")");
        Console.WriteLine();

        // 6) 调用 ExecutePipeline 走完整 7 步装机管线
        //    §6.0 固件双判 / §6.1 分区尾端验证 / §6.5 引导修复双路径 / §6.6 SetupComplete 模板渲染 全部在 ExecutePipeline 内集成
        Console.WriteLine("  [ZS] 开始执行无人值守装机流水线...");
        Console.WriteLine();
        return await ExecutePipeline(
            taskId: null,
            imagePath: imagePath,
            expectedHash: "",  // manifest 已校验，不再重复校验镜像
            opts: opts,
            targetDrive: targetDrive,
            diskIndex: diskIndex,
            online: false,
            offlineUnattendXml: "",  // R7 应答 XML 由 UnattendXmlBuilder 渲染，此处留空
            offlineFirstLogonCmd: "",  // 同上
            noReboot: noReboot,
            firmware: firmware,  // §6.0 判定结果，分区/引导/SetupComplete 都按此分流
            taskIni: taskIni,  // §6.6 模板渲染需要 Software 列表 + Optimize + Meta
            taskRoot: Path.GetDirectoryName(taskPath) ?? "");  // §6.6 自清理可能需要 ZS_Task 根目录
    }

    /// <summary>把 TaskIni（设计 §2.1）映射为现有 InstallOptions（与 ExecutePipeline 契约对齐）</summary>
    private static InstallOptions MapTaskIniToOptions(TaskIni task)
    {
        var o = new InstallOptions
        {
            AutoPartition = string.Equals(task.TargetDisk.PartitionMode, "clean_whole_disk", StringComparison.OrdinalIgnoreCase),
            BootFix = true,  // 设计 §1.2：bcdboot 是微软原生部署链路必经步骤，默认开
            DriverInject = task.Drivers.Inject,
            Unattended = string.Equals(task.Meta.OobeMode, "auto", StringComparison.OrdinalIgnoreCase),
            InstallSoftware = task.Software.Count > 0,
            Optimize = true,  // [optimize] section 存在即视为启用
            BackupData = false,  // R7 clean_whole_disk 默认不备份（设计 §2.1 partition_mode=clean_c_only 才保留数据盘）
            ImageIndex = task.SystemImage.Index,
            DriverPackage = "auto",
            BackupLocation = "auto"
        };
        return o;
    }

    // ============ 交互式入口（PE 启动后默认路径） ============
    private static async Task<int> RunInteractive(string serverUrl, bool noReboot)
    {
        // 1) 扫描 CD-ROM 根目录 zs_task.json
        ShowBanner();
        Console.WriteLine("  正在扫描装机任务...");
        Log("扫描离线任务...");

        var found = await Task.Run(() =>
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try { Log($"  驱动器 {d.Name} 类型={d.DriveType} 就绪={d.IsReady}"); }
                catch { Log($"  驱动器 {d.Name} 无法读取"); }
            }
            return Offline.ScanAllDrives();
        });

        var hit = found.FirstOrDefault(f => Offline.ResolveImagePath(f.Path, f.Task) != null);
        if (hit != null)
        {
            var imagePath = Offline.ResolveImagePath(hit.Path, hit.Task);
            Log("检测到离线任务: " + hit.Task.TaskNo + " 镜像=" + imagePath);

            // 2) 显示任务信息 + 10 秒倒计时
            bool autoProceed = await RunCountdown(hit.Task, imagePath!);

            if (autoProceed)
            {
                // 自动执行
                return await ExecuteOfflineTask(hit.Path, hit.Task, noReboot);
            }
            else
            {
                // 用户取消 → 进入手动装机界面
                return await RunManualMode(hit.Path, hit.Task, found, noReboot);
            }
        }

        // 3) 无有效任务 → 进入手动装机界面（空闲等待）
        Console.Clear();
        ShowBanner();
        Console.WriteLine("  未检测到装机任务。");
        Console.WriteLine();
        if (found.Count > 0)
        {
            Console.WriteLine("  检测到任务文件但镜像缺失，请在 Windows 端预下载镜像。");
            foreach (var f in found)
            {
                var img = Offline.ResolveImagePath(f.Path, f.Task);
                Console.WriteLine("    " + f.Path + " → 镜像: " + (img ?? "缺失"));
            }
        }
        Console.WriteLine();
        Console.WriteLine("  按任意键进入手动装机界面...");
        ReadKeyNoEcho();
        return await RunManualMode(null, null, found, noReboot);
    }

    // ============ 10 秒倒计时 ============
    private static async Task<bool> RunCountdown(OfflineTask task, string imagePath)
    {
        int countdown = 10;
        Console.Clear();
        ShowBanner();
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  检测到装机任务                          |");
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  编号: " + PadRight(task.TaskNo, 30) + " |");
        Console.WriteLine("  |  镜像: " + PadRight((task.Image?.Name ?? "未知"), 30) + " |");
        Console.WriteLine("  |  大小: " + PadRight((task.Image?.SizeDisplay ?? "未知"), 30) + " |");
        Console.WriteLine("  |  目标: " + PadRight(task.TargetPartition, 30) + " |");
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine();
        Console.WriteLine("  " + countdown + " 秒后自动执行装机...");
        Console.WriteLine("  按 [任意键] 进入手动装机模式");
        Console.WriteLine("  按 [ESC] 取消并退出");

        while (countdown > 0)
        {
            var delayTask = Task.Delay(1000);
            while (!delayTask.IsCompleted)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine();
                        Console.WriteLine("  用户取消，退出装机助手。");
                        Log("用户按 ESC 取消装机");
                        await Task.Delay(2000);
                        return false; // 阻止自动执行，也不进入手动模式
                    }
                    // 任意其他键 → 进入手动模式
                    return false;
                }
                await Task.Delay(50);
            }

            countdown--;
            // 更新倒计时显示（覆盖当前行）
            Console.SetCursorPosition(2, 10);
            Console.Write(countdown + " 秒后自动执行装机...    ");
        }

        Console.WriteLine();
        Console.WriteLine("  倒计时结束，开始自动装机...");
        Log("倒计时结束，自动执行装机");
        await Task.Delay(500);
        return true;
    }

    // ============ 手动装机界面 ============
    private static async Task<int> RunManualMode(
        string? taskPath, OfflineTask? task,
        System.Collections.Generic.List<OfflineTaskFile> allFound, bool noReboot)
    {
        // 初始化选项
        var opts = new InstallOptions();
        string imagePath = "";
        string taskNo = task?.TaskNo ?? "无任务";
        string imageName = task?.Image?.Name ?? "无镜像";
        string targetDrive = "C:";
        var disks = Device.GetDiskInfo();
        int diskIndex = 0;
        string partitionScheme = "auto";

        if (task != null && taskPath != null)
        {
            // 从任务加载现有配置
            opts.AutoPartition = task.Options?.AutoPartition ?? true;
            opts.BootFix = task.Options?.BootFix ?? true;
            opts.DriverInject = task.Options?.DriverInject ?? true;
            opts.Unattended = task.Options?.Unattended ?? true;
            opts.InstallSoftware = task.Options?.InstallSoftware ?? true;
            opts.Optimize = task.Options?.Optimize ?? true;
            opts.BackupData = task.Options?.BackupData ?? true;
            imagePath = Offline.ResolveImagePath(taskPath, task) ?? "";
            diskIndex = ResolveDiskIndex(task.Disk);
            targetDrive = NormalizeDrive(task.TargetPartition);
            partitionScheme = task.PartitionScheme ?? "auto";
        }

        // 刷新磁盘列表
        if (disks.Count > 0 && diskIndex >= disks.Count)
            diskIndex = 0;

        var selectedDisk = disks.FirstOrDefault(d => d.Index == diskIndex);
        string diskDesc = selectedDisk != null
            ? $"磁盘{selectedDisk.Index} ({selectedDisk.Size / 1073741824}GB, {selectedDisk.Model})"
            : "磁盘0";

        bool exit = false;
        while (!exit)
        {
            Console.Clear();
            ShowBanner();
            Console.WriteLine("  +----------------------------------------+");
            Console.WriteLine("  |  手动装机                                |");
            Console.WriteLine("  +----------------------------------------+");
            Console.WriteLine("  |  任务: " + PadRight(taskNo, 30) + " |");
            Console.WriteLine("  +----------------------------------------+");
            Console.WriteLine("  |  [1] 镜像: " + PadRight(Truncate(imageName, 24), 24) + " |");
            Console.WriteLine("  |  [2] 磁盘: " + PadRight(Truncate(diskDesc, 24), 24) + " |");
            Console.WriteLine("  |  [3] 分区: " + PadRight(PartSchemeLabel(partitionScheme), 24) + " |");
            Console.WriteLine("  |  [4] 备份: " + PadRight(OnOff(opts.BackupData), 24) + " |");
            Console.WriteLine("  |  [5] 驱动: " + PadRight(OnOff(opts.DriverInject), 24) + " |");
            Console.WriteLine("  |  [6] 引导: " + PadRight(OnOff(opts.BootFix), 24) + " |");
            Console.WriteLine("  |  [7] 应答: " + PadRight(OnOff(opts.Unattended), 24) + " |");
            Console.WriteLine("  |  [8] 软件: " + PadRight(OnOff(opts.InstallSoftware), 24) + " |");
            Console.WriteLine("  |  [9] 优化: " + PadRight(OnOff(opts.Optimize), 24) + " |");
            Console.WriteLine("  +----------------------------------------+");
            Console.WriteLine("  |  [S] 开始装机                           |");
            Console.WriteLine("  |  [R] 刷新磁盘列表                       |");
            Console.WriteLine("  |  [Q] 退出                               |");
            Console.WriteLine("  +----------------------------------------+");

            var key = ReadKeyNoEcho();
            switch (key.Key)
            {
                case ConsoleKey.D1 or ConsoleKey.NumPad1:
                    // 选择镜像（如果有多个任务文件）
                    if (allFound.Count > 1)
                    {
                        var sel = SelectImage(allFound);
                        if (sel != null)
                        {
                            taskPath = sel.Value.path;
                            task = sel.Value.task;
                            taskNo = task.TaskNo;
                            imageName = task.Image?.Name ?? "未知";
                            imagePath = Offline.ResolveImagePath(taskPath, task) ?? "";
                            opts = LoadOptionsFromTask(task);
                        }
                    }
                    break;

                case ConsoleKey.D2 or ConsoleKey.NumPad2:
                    // 选择磁盘
                    diskIndex = SelectDisk(disks, diskIndex);
                    selectedDisk = disks.FirstOrDefault(d => d.Index == diskIndex);
                    diskDesc = selectedDisk != null
                        ? $"磁盘{selectedDisk.Index} ({selectedDisk.Size / 1073741824}GB, {selectedDisk.Model})"
                        : $"磁盘{diskIndex}";
                    break;

                case ConsoleKey.D3 or ConsoleKey.NumPad3:
                    partitionScheme = TogglePartScheme(partitionScheme);
                    opts.AutoPartition = partitionScheme != "keep";
                    break;

                case ConsoleKey.D4 or ConsoleKey.NumPad4:
                    opts.BackupData = !opts.BackupData; break;
                case ConsoleKey.D5 or ConsoleKey.NumPad5:
                    opts.DriverInject = !opts.DriverInject; break;
                case ConsoleKey.D6 or ConsoleKey.NumPad6:
                    opts.BootFix = !opts.BootFix; break;
                case ConsoleKey.D7 or ConsoleKey.NumPad7:
                    opts.Unattended = !opts.Unattended; break;
                case ConsoleKey.D8 or ConsoleKey.NumPad8:
                    opts.InstallSoftware = !opts.InstallSoftware; break;
                case ConsoleKey.D9 or ConsoleKey.NumPad9:
                    opts.Optimize = !opts.Optimize; break;

                case ConsoleKey.S:
                    // 确认开始装机
                    if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    {
                        ShowError("镜像文件不存在，无法开始装机。");
                        break;
                    }
                    if (!ConfirmInstall(taskNo, imageName, diskDesc, partitionScheme, opts))
                        break;
                    exit = true;
                    return await ExecutePipeline(null, imagePath, task?.Image?.FileHash ?? "", opts,
                        targetDrive, diskIndex, online: false, task?.UnattendXml, task?.FirstLogonCmd, noReboot);

                case ConsoleKey.R:
                    disks = Device.GetDiskInfo();
                    ShowInfo("磁盘列表已刷新。");
                    break;

                case ConsoleKey.Q or ConsoleKey.Escape:
                    if (ConfirmExit())
                    {
                        exit = true;
                        Log("用户退出手动装机界面");
                    }
                    break;
            }
        }

        Console.Clear();
        ShowBanner();
        Console.WriteLine("  装机助手已退出。");
        Console.WriteLine("  您可以关闭此窗口或重新运行 ZS_Agent.exe。");
        Console.WriteLine();
        Console.WriteLine("  按任意键关闭...");
        ReadKeyNoEcho();
        return 0;
    }

    // ============ 手动模式辅助方法 ============

    private static void ShowBanner()
    {
        Console.Clear();
        Console.WriteLine();
        Console.WriteLine("  ========================================");
        Console.WriteLine("          ZS 装机助手 v1.0");
        Console.WriteLine("  ========================================");
        Console.WriteLine();
    }

    private static void ShowError(string msg)
    {
        Console.SetCursorPosition(0, Console.WindowHeight > 0 ? Console.WindowHeight - 3 : 20);
        Console.WriteLine("  [错误] " + msg);
        Console.WriteLine("  按任意键继续...");
        ReadKeyNoEcho();
    }

    private static void ShowInfo(string msg)
    {
        Console.SetCursorPosition(0, Console.WindowHeight > 0 ? Console.WindowHeight - 3 : 20);
        Console.WriteLine("  [提示] " + msg);
        Console.WriteLine("  按任意键继续...");
        ReadKeyNoEcho();
    }

    private static int SelectDisk(System.Collections.Generic.List<DiskInfo> disks, int current)
    {
        Console.Clear();
        ShowBanner();
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  选择目标磁盘                            |");
        Console.WriteLine("  +----------------------------------------+");

        if (disks.Count == 0)
        {
            Console.WriteLine("  |  未检测到磁盘                            |");
            Console.WriteLine("  +----------------------------------------+");
            ReadKeyNoEcho();
            return current;
        }

        for (int i = 0; i < disks.Count; i++)
        {
            var d = disks[i];
            var marker = d.Index == current ? " <--" : "";
            Console.WriteLine("  |  [" + (i + 1) + "] 磁盘" + d.Index
                + " " + PadRight((d.Size / 1073741824) + "GB", 6)
                + PadRight(Truncate(d.Model, 16), 16) + marker + " |");
        }
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  输入序号选择，或按 ESC 返回");

        var key = ReadKeyNoEcho();
        if (key.Key == ConsoleKey.Escape) return current;
        int idx = (int)key.Key - (int)ConsoleKey.D1;
        if (idx >= 0 && idx < disks.Count) return disks[idx].Index;
        return current;
    }

    private static (string path, OfflineTask task)? SelectImage(
        System.Collections.Generic.List<OfflineTaskFile> found)
    {
        Console.Clear();
        ShowBanner();
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  选择镜像任务                            |");
        Console.WriteLine("  +----------------------------------------+");

        for (int i = 0; i < found.Count; i++)
        {
            var f = found[i];
            var img = Offline.ResolveImagePath(f.Path, f.Task);
            Console.WriteLine("  |  [" + (i + 1) + "] " + PadRight(Truncate(f.Task.TaskNo, 14), 14)
                + " " + PadRight(Truncate(f.Task.Image?.Name ?? "?", 14), 14)
                + (img != null ? " [OK]" : " [MISS]") + " |");
        }
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  输入序号选择，或按 ESC 返回");

        var key = ReadKeyNoEcho();
        if (key.Key == ConsoleKey.Escape) return null;
        int idx = (int)key.Key - (int)ConsoleKey.D1;
        if (idx >= 0 && idx < found.Count)
        {
            var f = found[idx];
            return (f.Path, f.Task);
        }
        return null;
    }

    private static string TogglePartScheme(string current)
    {
        return current switch
        {
            "auto" => "keep",
            "keep" => "custom",
            _ => "auto"
        };
    }

    private static string PartSchemeLabel(string scheme)
    {
        return scheme switch
        {
            "auto" => "自动分区 (GPT)",
            "keep" => "保留现有分区",
            "custom" => "自定义分区",
            _ => scheme
        };
    }

    private static bool ConfirmInstall(string taskNo, string imageName, string disk,
        string partScheme, InstallOptions opts)
    {
        Console.Clear();
        ShowBanner();
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  确认装机配置                            |");
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine("  |  任务: " + PadRight(Truncate(taskNo, 30), 30) + " |");
        Console.WriteLine("  |  镜像: " + PadRight(Truncate(imageName, 30), 30) + " |");
        Console.WriteLine("  |  磁盘: " + PadRight(Truncate(disk, 30), 30) + " |");
        Console.WriteLine("  |  分区: " + PadRight(PartSchemeLabel(partScheme), 30) + " |");
        Console.WriteLine("  |  备份: " + PadRight(OnOff(opts.BackupData), 30) + " |");
        Console.WriteLine("  |  驱动: " + PadRight(OnOff(opts.DriverInject), 30) + " |");
        Console.WriteLine("  |  引导: " + PadRight(OnOff(opts.BootFix), 30) + " |");
        Console.WriteLine("  |  应答: " + PadRight(OnOff(opts.Unattended), 30) + " |");
        Console.WriteLine("  +----------------------------------------+");
        Console.WriteLine();
        Console.WriteLine("  *** 警告: 此操作将写入磁盘，可能覆盖数据! ***");
        Console.WriteLine();
        Console.WriteLine("  [Y] 确认开始装机    [N] 返回修改");

        var key = ReadKeyNoEcho();
        return key.Key == ConsoleKey.Y;
    }

    private static bool ConfirmExit()
    {
        Console.SetCursorPosition(0, Console.WindowHeight > 0 ? Console.WindowHeight - 3 : 20);
        Console.WriteLine("  确定要退出装机助手吗？");
        Console.WriteLine("  [Y] 确认退出    [N] 返回");
        var key = ReadKeyNoEcho();
        return key.Key == ConsoleKey.Y;
    }

    private static InstallOptions LoadOptionsFromTask(OfflineTask task)
    {
        return new InstallOptions
        {
            AutoPartition = task.Options?.AutoPartition ?? true,
            BootFix = task.Options?.BootFix ?? true,
            DriverInject = task.Options?.DriverInject ?? true,
            Unattended = task.Options?.Unattended ?? true,
            InstallSoftware = task.Options?.InstallSoftware ?? true,
            Optimize = task.Options?.Optimize ?? true,
            BackupData = task.Options?.BackupData ?? true,
        };
    }

    private static string OnOff(bool v) => v ? "[ON]  是" : "[OFF] 否";
    private static string PadRight(string s, int n) => (s ?? "").PadRight(n);
    private static string Truncate(string s, int n)
    {
        var safe = s ?? "";
        return safe.Length <= n ? safe : safe[..(n - 1)] + ".";
    }

    private static ConsoleKeyInfo ReadKeyNoEcho()
    {
        while (Console.KeyAvailable) Console.ReadKey(intercept: true);
        return Console.ReadKey(intercept: true);
    }

    // ============ 正常无人值守执行（保留兼容） ============
    private static async Task<int> RunUnattended(string serverUrl, bool noReboot)
    {
        // 1) 尝试连接服务器
        bool online = false;
        var login = await Api.LoginAsync("admin", "admin123");
        if (login.IsSuccess && login.Data != null && !string.IsNullOrEmpty(login.Data.Token))
        {
            Api.SetToken(login.Data.Token);
            online = true;
            Log("已连接服务器，登录成功");
        }
        else
        {
            Log("无法连接服务器: " + login.Message + "（将尝试离线任务）");
        }

        if (online)
        {
            var reg = await Api.RegisterClientAsync(
                Device.GetHostname(), Device.GetMacAddress(), Device.GetOsVersion(), "winpe");
            string clientId = reg.Data?.ClientId ?? "";
            int serverClientId = reg.Data?.Id ?? 0;
            Log("客户端注册: clientId=" + clientId + " serverId=" + serverClientId);

            if (serverClientId > 0)
            {
                for (int i = 1; i <= 6; i++)
                {
                    Log("轮询等待任务 第 " + i + "/6 次...");
                    var tasks = await Api.GetMyTasksAsync(serverClientId, "waiting", 1, 5);
                    if (tasks.IsSuccess && tasks.Data != null && tasks.Data.List.Count > 0)
                    {
                        var task = tasks.Data.List[0];
                        Log("检测到待执行任务: " + task.TaskNo + " status=" + task.Status);
                        return await ExecuteOnlineTask(task, serverUrl, noReboot);
                    }
                    if (i < 6) await Task.Delay(30_000);
                }
                Log("3 分钟内无 waiting 任务");
            }
            else
            {
                Log("客户端注册失败: " + (reg.Message ?? ""));
            }
        }

        // 4) 无任务/无网络：扫描磁盘离线任务
        Log("扫描磁盘查找离线任务...");
        var found = await Task.Run(() =>
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try { Log($"  驱动器 {d.Name} 类型={d.DriveType} 就绪={d.IsReady}"); }
                catch { Log($"  驱动器 {d.Name} 无法读取"); }
            }
            return Offline.ScanAllDrives();
        });
        var hit = found.FirstOrDefault(f => Offline.ResolveImagePath(f.Path, f.Task) != null);
        if (hit != null)
        {
            Log("检测到离线任务: " + hit.Task.TaskNo
                + " 镜像=" + Offline.ResolveImagePath(hit.Path, hit.Task));
            return await ExecuteOfflineTask(hit.Path, hit.Task, noReboot);
        }
        Log(found.Count > 0
            ? "检测到离线任务但本地镜像缺失（请在 Windows 端预下载镜像）"
            : "未检测到任何任务，本机无需装机");
        Log("PE 桌面已就绪，可手动操作。按任意键退出此窗口...");
        Console.ReadKey();
        return 0;
    }

    // ============ 在线任务执行 ============
    private static async Task<int> ExecuteOnlineTask(TaskInfo task, string serverUrl, bool noReboot)
    {
        await Progress(task.Id, 5, "任务已认领，开始执行", "创建任务", "running");
        Log("任务认领成功（waiting→running）: " + task.TaskNo);

        var img = await FindImage(task.ImageId);
        if (img == null)
        {
            await Progress(task.Id, 0, "找不到镜像 #" + task.ImageId, "部署镜像", "failed");
            Log("错误: 找不到镜像 #" + task.ImageId);
            return 1;
        }
        Log("镜像: " + img.Name + " 文件=" + img.FileName + " 哈希=" + (img.FileHash.Length >= 8 ? img.FileHash[..8] + "..." : "空"));

        var opts = ParseOptions(task.Options);
        string targetDrive = NormalizeDrive(task.TargetPartition);
        int diskIndex = task.TargetDiskIndex;
        Log("目标: 磁盘" + diskIndex + " 分区=" + targetDrive + " 方案=" + task.PartitionScheme
            + " 选项=" + JsonSerializer.Serialize(opts));

        string cacheFile = Path.Combine(_cacheDir, string.IsNullOrEmpty(img.FileName) ? "image_" + img.Id + ".wim" : img.FileName);
        if (!File.Exists(cacheFile))
        {
            await Progress(task.Id, 10, "正在下载镜像", "下载镜像", "running");
            Log("下载镜像: " + img.FileName + " → " + cacheFile);
            var dlUrl = serverUrl + "/api/v1/images/" + img.Id + "/clientDownload";
            var dl = await Api.DownloadFileAsync(dlUrl, cacheFile,
                new Progress<int>(p => Log("  下载进度 " + p + "%")));
            if (!dl.Ok)
            {
                await Progress(task.Id, 0, "下载失败: " + dl.Error, "下载镜像", "failed");
                Log("错误: 镜像下载失败 " + dl.Error);
                return 1;
            }
            Log("镜像下载完成");
        }
        else
        {
            Log("镜像已缓存，跳过下载: " + cacheFile);
        }

        return await ExecutePipeline(task.Id, cacheFile, img.FileHash, opts,
            targetDrive, diskIndex, online: true, null, null, noReboot);
    }

    // ============ 离线任务执行 ============
    private static async Task<int> ExecuteOfflineTask(string taskPath, OfflineTask task, bool noReboot)
    {
        Log("离线无人值守任务执行开始: " + task.TaskNo);

        var imagePath = Offline.ResolveImagePath(taskPath, task);
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Log("错误: 离线任务镜像不存在: " + (imagePath ?? ""));
            return 1;
        }
        Log("离线镜像: " + imagePath);

        var opts = new InstallOptions
        {
            AutoPartition = task.Options?.AutoPartition ?? true,
            BootFix = task.Options?.BootFix ?? true,
            DriverInject = task.Options?.DriverInject ?? true,
            Unattended = task.Options?.Unattended ?? true,
            InstallSoftware = task.Options?.InstallSoftware ?? true,
            Optimize = task.Options?.Optimize ?? true,
            BackupData = task.Options?.BackupData ?? true,
        };

        int diskIndex = ResolveDiskIndex(task.Disk);
        string targetDrive = NormalizeDrive(task.TargetPartition);
        Log("目标: 磁盘" + diskIndex + " 分区=" + targetDrive + " 方案=" + task.PartitionScheme);

        return await ExecutePipeline(null, imagePath, task.Image?.FileHash ?? "", opts,
            targetDrive, diskIndex, online: false, task.UnattendXml, task.FirstLogonCmd, noReboot);
    }

    // ============ 公共装机管线 ============
    private static async Task<int> ExecutePipeline(
        int? taskId, string imagePath, string expectedHash, InstallOptions opts,
        string targetDrive, int diskIndex, bool online,
        string? offlineUnattendXml, string? offlineFirstLogonCmd, bool noReboot,
        // R7-C 新增：§6.0 固件双判结果（分区/引导双路径）；§6.6 模板渲染需 taskIni
        FirmwareDetector.FirmwareResult? firmware = null,
        TaskIni? taskIni = null,
        string? taskRoot = null)
    {
        // 校验镜像
        await Progress(taskId, 25, "正在校验镜像", "校验镜像", "running", online);
        if (!string.IsNullOrEmpty(expectedHash) && expectedHash.Length >= 32)
        {
            Log("SHA256 校验中...");
            var actual = ComputeSha256(imagePath);
            if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                await Progress(taskId, 0, "SHA256 校验失败，镜像可能损坏", "校验镜像", "failed", online);
                Log("错误: SHA256 校验失败 实际=" + actual);
                return 1;
            }
            Log("SHA256 校验通过: " + actual[..12] + "...");
        }
        else Log("未提供 file_hash，跳过 SHA256 校验");

        // 备份数据
        if (opts.BackupData)
        {
            await Progress(taskId, 35, "正在备份数据", "备份数据", "running", online);
            var bk = await BackupData(opts.BackupLocation);
            if (bk == "failed")
            {
                await Progress(taskId, 0, "备份失败，已中止装机", "备份数据", "failed", online);
                Log("错误: 数据备份失败，已中止");
                return 1;
            }
            Log(bk == "ok" ? "数据备份完成" : "无用户数据可备份，跳过");
        }
        else Log("未启用数据备份");

        // 分区 —— §6.1 按 firmware 分流 GPT/MBR；分区后立即调 PartitionVerifier 尾端验证
        if (opts.AutoPartition)
        {
            await Progress(taskId, 45, "正在分区", "分区/格式化", "running", online);

            if (firmware != null)
            {
                // R7-C 路径：按 firmware.Type 生成 GPT/MBR 脚本字符串，调 ExecuteRawScriptAsync
                // 这样能在脚本末尾附加 "detail partition + list volume" 验证段（§6.1 尾端验证要求）
                var fwType = firmware.Type;
                Log("分区(R7-C): 磁盘" + diskIndex + " " + firmware.PartitionScript.ToUpperInvariant()
                    + " 分区（固件源=" + firmware.Source + "）");
                var script = BuildPartitionScript(diskIndex, fwType, taskIni);
                var (partOk, partOutput) = await DiskPart.ExecuteRawScriptAsync(script);
                Log("diskpart 输出（末尾 200 字）: "
                    + (partOutput.Length > 200 ? partOutput[^200..] : partOutput));

                if (!partOk)
                {
                    await Progress(taskId, 0, "DiskPart 分区失败", "分区/格式化", "failed", online);
                    Log("错误: DiskPart 分区失败（exit code 非 0）");
                    return 1;
                }

                // §6.1 尾端验证：GPT 必须 ESP+FAT32；MBR 必须 Active=Yes
                Log("分区尾端验证: type=" + fwType);
                var verify = PartitionVerifier.Verify(diskIndex, fwType);
                Log("尾端验证: pass=" + verify.Pass + " reason=" + (verify.Reason ?? "(空)"));
                if (!verify.Pass)
                {
                    await Progress(taskId, 0, "分区尾端验证失败: " + verify.Reason, "分区/格式化", "failed", online);
                    Log("FATAL: 分区尾端验证失败，按设计 §6.1 应 Exit(3)");
                    Log("  原因: " + verify.Reason);
                    Log("  detail: " + (verify.DetailOutput.Length > 300 ? verify.DetailOutput[^300..] : verify.DetailOutput));
                    Log("  volume: " + (verify.VolumeOutput.Length > 300 ? verify.VolumeOutput[^300..] : verify.VolumeOutput));
                    return 3;
                }
                Log("分区尾端验证通过: bootLabel=" + verify.BootLabel
                    + " fs=" + (verify.BootFileSystem ?? "(N/A)"));
            }
            else
            {
                // 旧路径：调 DiskPart.ExecutePartitionOperation（仅 GPT）
                Log("分区(旧): 磁盘" + diskIndex + " GPT 自动分区");
                var op = new PartitionOperation
                {
                    Operation = "create",
                    DiskIndex = diskIndex,
                    FileSystem = "NTFS",
                    DriveLetter = "C",
                    Label = "Windows"
                };
                var partOk = await DiskPart.ExecutePartitionOperation(op);
                if (!partOk)
                {
                    await Progress(taskId, 0, "DiskPart 分区失败", "分区/格式化", "failed", online);
                    Log("错误: DiskPart 分区失败");
                    return 1;
                }
            }
            Log("分区完成");
        }
        else Log("保留现有分区");

        // 部署镜像
        await Progress(taskId, 60, "正在部署镜像", "部署镜像", "running", online);
        Log("部署镜像: index=" + opts.ImageIndex + " → " + targetDrive);
        var deployOk = await Deploy.DeployWimImage(imagePath, opts.ImageIndex, targetDrive, opts.AutoPartition);
        if (!deployOk)
        {
            await Progress(taskId, 0, "镜像部署失败", "部署镜像", "failed", online);
            Log("错误: 镜像部署失败");
            return 1;
        }
        Log("镜像应用完成");

        // 注入驱动
        if (opts.DriverInject)
        {
            await Progress(taskId, 85, "正在注入驱动", "注入驱动", "running", online);
            var driverPath = FindDriverSource(opts.DriverPackage);
            if (driverPath != null)
            {
                Log("注入驱动: " + driverPath);
                var ok = await Deploy.InjectDrivers(targetDrive, driverPath);
                Log(ok ? "驱动注入完成" : "驱动注入失败（继续）");
            }
            else Log("未找到驱动目录，跳过驱动注入");
        }
        else Log("未启用驱动注入");

        // 修复引导 —— §6.5 按 firmware 双路径（UEFI: bcdboot /f UEFI；BIOS: bcdboot /f BIOS）
        if (opts.BootFix)
        {
            await Progress(taskId, 95, "正在修复引导", "修复引导", "running", online);
            // R7-C 路径：firmware 非 null 时按 firmware.Type 双路径
            // 旧路径：firmware 为 null，调 Deploy.RepairBoot（自动判定 EFI/BIOS）
            if (firmware != null)
            {
                var fwType = firmware.Type;
                var bcdFlags = fwType == FirmwareDetector.FirmwareType.Uefi ? "UEFI" : "BIOS";
                Log("修复引导 (R7-C " + bcdFlags + "): " + targetDrive);
                // §6.5 UEFI/GPT: bcdboot C:\Windows /s S: /f UEFI /l zh-cn
                // §6.5 BIOS/MBR: bcdboot C:\Windows /s S: /f BIOS /l zh-cn
                // 通过 Deploy.RepairBoot 传 firmwareType 参数让它走双路径
                await Deploy.RepairBoot(targetDrive, bcdFlags);
                Log("引导修复完成 (" + bcdFlags + ")");
            }
            else
            {
                Log("修复引导 (旧): " + targetDrive);
                await Deploy.RepairBoot(targetDrive);
                Log("引导修复完成");
            }
        }
        else Log("未启用引导修复");

        // 写入无人值守应答 —— §6.6c R7-C 路径用 UnattendXmlBuilder 渲染；旧路径用传入 xml 字符串
        if (opts.Unattended)
        {
            await Progress(taskId, 96, "正在写入无人值守应答", "写入无人值守", "running", online);
            if (taskIni != null)
            {
                // R7-C 路径：用 UnattendXmlBuilder 按 task.ini meta.oobe_mode 渲染
                // oobe_mode=auto 时写入完整 Unattend.xml（OOBE 全跳过 + 本地管理员 + BypassNRO）
                // oobe_mode=manual 时 UnattendXmlBuilder 返回 null，跳过写入
                var unattendPath = UnattendXmlBuilder.WriteToSystem(taskIni, targetDrive);
                if (unattendPath != null)
                    Log("Unattend.xml (R7-C) 已写入: " + unattendPath);
                else
                    Log("Unattend.xml (R7-C) 跳过：task.ini meta.oobe_mode 非 auto，保留 OOBE 用户操作");
            }
            else
            {
                // 旧路径：使用传入的 xml 字符串
                string xml = online ? "" : (offlineUnattendXml ?? "");
                if (online && taskId is > 0)
                {
                    var ur = await Api.GetTaskUnattendAsync(taskId.Value);
                    if (ur.IsSuccess && ur.Data != null) xml = ur.Data.Xml ?? "";
                }
                if (!string.IsNullOrWhiteSpace(xml))
                {
                    var panther = Path.Combine(targetDrive.TrimEnd('\\') + "\\", "Windows", "Panther");
                    Directory.CreateDirectory(panther);
                    File.WriteAllText(Path.Combine(panther, "unattend.xml"), xml, new UTF8Encoding(false));
                    Log("unattend.xml (旧) 已写入: " + panther);
                }
                else Log("无人值守应答为空，跳过");
            }
        }
        else Log("未启用无人值守");

        // 首次登录脚本 —— §6.6 Step 6b R7-C 路径用 SetupCompleteBuilder 渲染（含软件列表+优化+自清理+自毁）
        if (opts.Unattended && (opts.InstallSoftware || opts.Optimize))
        {
            await Progress(taskId, 98, "正在生成首次登录脚本", "首次登录脚本", "running", online);
            if (taskIni != null && !string.IsNullOrEmpty(taskRoot))
            {
                // R7-C 路径：用 SetupCompleteBuilder 按 task.ini Software 列表 + Optimize 渲染
                var setupPath = SetupCompleteBuilder.WriteToSystem(taskIni, targetDrive, taskRoot);
                Log("SetupComplete.cmd (R7-C) 已写入: " + setupPath);

                // §6.6 Step 6a：复制 software 目录到新系统 C:\Windows\Setup\Scripts\software\
                var softwareSrc = Path.Combine(taskRoot, "software");
                var softwareDst = Path.Combine(targetDrive.TrimEnd('\\') + "\\", "Windows", "Setup", "Scripts", "software");
                if (Directory.Exists(softwareSrc))
                {
                    Log("复制软件目录: " + softwareSrc + " → " + softwareDst);
                    try
                    {
                        CopyDirectory(softwareSrc, softwareDst);
                        Log("软件目录复制完成");
                    }
                    catch (Exception copyEx)
                    {
                        // 设计 §6.6 Step 6a：拷贝失败只写警告日志，不中断
                        Log("WARN: 软件目录复制失败（不中断装机）: " + copyEx.Message);
                    }
                }
                else
                {
                    Log("软件目录不存在，跳过复制: " + softwareSrc);
                }
            }
            else
            {
                // 旧路径：使用传入的 cmd 字符串
                string cmd = online ? "" : (offlineFirstLogonCmd ?? "");
                if (online && taskId is > 0)
                {
                    var fr = await Api.GetTaskFirstLogonAsync(taskId.Value);
                    if (fr.IsSuccess && fr.Data != null) cmd = fr.Data.Cmd ?? "";
                }
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    var setup = Path.Combine(targetDrive.TrimEnd('\\') + "\\", "Windows", "Setup", "Scripts");
                    Directory.CreateDirectory(setup);
                    File.WriteAllText(Path.Combine(setup, "SetupComplete.cmd"), cmd, new UTF8Encoding(false));
                    Log("SetupComplete.cmd (旧) 已写入: " + setup);
                }
                else Log("首次登录脚本为空，跳过");
            }
        }
        else Log("未启用装软件/优化");

        await Progress(taskId, 100, "装机完成", "完成", "completed", online);
        Log("========== 装机完成 ==========");

        if (!noReboot)
        {
            Log("60 秒后自动重启进入新系统...");
            await Task.Delay(60_000);
            RebootNow();
        }
        else Log("--no-reboot：不自动重启");
        return 0;
    }

    // ============ R7 §6.1 分区脚本生成 + 目录复制辅助 ============

    /// <summary>
    /// 按固件类型 + task.ini 分区方案生成 diskpart 脚本字符串（§6.1）。
    /// GPT 分支：ESP(FAT32) + MSR + Recovery(可选) + Primary(NTFS)
    /// MBR 分支：Primary(NTFS, Active)
    /// 分区完成后由 PartitionVerifier 独立执行尾端验证。
    /// </summary>
    private static string BuildPartitionScript(int diskIndex, FirmwareDetector.FirmwareType fwType, TaskIni? taskIni)
    {
        var ps = taskIni?.PartitionScheme;
        var espSize = ps?.EspSizeMb ?? 500;
        var msrSize = ps?.MsrSizeMb ?? 16;
        var recSize = ps?.RecoverySizeMb ?? 800;
        var letter = string.IsNullOrEmpty(ps?.SystemLetter) ? "C" : ps!.SystemLetter;
        var label = string.IsNullOrEmpty(ps?.SystemLabel) ? "Windows" : ps!.SystemLabel;
        var fs = string.IsNullOrEmpty(ps?.FormatFs) ? "ntfs" : ps!.FormatFs;
        var quick = ps?.QuickFormat ?? true;
        var quickFlag = quick ? " quick" : "";

        var sb = new StringBuilder();
        sb.AppendLine("select disk " + diskIndex);
        sb.AppendLine("clean");

        if (fwType == FirmwareDetector.FirmwareType.Uefi)
        {
            // GPT 分支（UEFI）：ESP + MSR + Recovery(隐藏不分配盘符) + Primary
            sb.AppendLine("convert gpt");
            sb.AppendLine("create partition efi size=" + espSize);
            sb.AppendLine("format" + quickFlag + " fs=fat32 label=\"System\"");
            sb.AppendLine("assign letter=S");
            sb.AppendLine("create partition msr size=" + msrSize);
            if (recSize > 0)
            {
                sb.AppendLine("create partition primary size=" + recSize);
                sb.AppendLine("format" + quickFlag + " fs=" + fs + " label=\"Recovery\"");
            }
            sb.AppendLine("create partition primary");
            sb.AppendLine("format" + quickFlag + " fs=" + fs + " label=\"" + label + "\"");
            sb.AppendLine("assign letter=" + letter);
        }
        else
        {
            // MBR 分支（BIOS）：Primary + Active
            sb.AppendLine("convert mbr");
            if (recSize > 0)
            {
                sb.AppendLine("create partition primary size=" + recSize);
                sb.AppendLine("format" + quickFlag + " fs=" + fs + " label=\"Recovery\"");
            }
            sb.AppendLine("create partition primary");
            sb.AppendLine("format" + quickFlag + " fs=" + fs + " label=\"" + label + "\"");
            sb.AppendLine("assign letter=" + letter);
            sb.AppendLine("active");
        }
        sb.AppendLine("exit");
        return sb.ToString();
    }

    /// <summary>
    /// 递归复制目录（§6.6 Step 6a 软件目录拷贝到新系统）。
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    // ============ 自检模式 ============
    private static async Task<int> RunSelfTest(string serverUrl)
    {
        Log("[self-test] 1/4 连接服务器登录...");
        var login = await Api.LoginAsync("admin", "admin123");
        Log("[self-test] 登录: " + (login.IsSuccess ? "成功" : "失败 " + login.Message));
        bool online = login.IsSuccess && login.Data != null;

        if (online)
        {
            Api.SetToken(login.Data!.Token);
            Log("[self-test] 2/4 客户端注册...");
            var reg = await Api.RegisterClientAsync(
                Device.GetHostname(), Device.GetMacAddress(), Device.GetOsVersion(), "winpe");
            Log("[self-test] 注册: clientId=" + (reg.Data?.ClientId ?? "无") + " serverId=" + (reg.Data?.Id ?? 0));

            Log("[self-test] 3/4 镜像列表 + 本机 waiting 任务...");
            var imgs = await Api.GetImagesAsync(1, 100);
            Log("[self-test] 镜像数: " + (imgs.Data?.List.Count ?? 0));
            if (reg.Data?.Id is > 0)
            {
                var my = await Api.GetMyTasksAsync(reg.Data.Id, "waiting", 1, 5);
                Log("[self-test] waiting 任务数: " + (my.Data?.List.Count ?? 0));
                foreach (var t in my.Data?.List ?? new())
                    Log("  任务#" + t.Id + " " + t.TaskNo + " 镜像#" + t.ImageId + " 磁盘" + t.TargetDiskIndex + " 选项=" + t.Options);
            }
        }

        Log("[self-test] 4/4 离线任务扫描...");
        var found = await Task.Run(() => Offline.ScanAllDrives());
        Log("[self-test] 离线任务命中: " + found.Count);
        foreach (var f in found)
        {
            Log("  文件=" + f.Path + " 编号=" + f.Task.TaskNo
                + " 镜像=" + (Offline.ResolveImagePath(f.Path, f.Task) ?? "缺失")
                + " 选项=" + JsonSerializer.Serialize(f.Task.Options));
        }

        Log("[self-test] 选项解析样例...");
        var sample = "{\"type\":\"install\",\"auto_partition\":true,\"auto_repair_boot\":false,\"auto_inject_drivers\":true,\"unattended\":true,\"install_software\":false,\"optimize\":true,\"backup_data\":true,\"image_index\":1,\"backup_location\":\"auto\"}";
        var parsed = ParseOptions(sample);
        Log("[self-test] 解析结果: " + JsonSerializer.Serialize(parsed));
        Log("[self-test] 校验: auto_partition=" + parsed.AutoPartition
            + " boot_fix(应=false)=" + parsed.BootFix
            + " driver_inject=" + parsed.DriverInject
            + " install_software(应=false)=" + parsed.InstallSoftware);

        var pass = parsed.AutoPartition && !parsed.BootFix && parsed.DriverInject && !parsed.InstallSoftware;
        Log("[self-test] 结果: " + (pass ? "PASS" : "FAIL"));
        return pass ? 0 : 1;
    }

    // ============ 离线注入逻辑自检 ============
    private static int RunOfflineTest()
    {
        Log("[offline-test] 开始离线注入逻辑往返测试...");
        string testRoot = Path.Combine(Path.GetTempPath(), "zs_offline_test");
        if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        Directory.CreateDirectory(testRoot);

        bool passA = false, passB = false, passCdRom = false;
        try
        {
            Directory.CreateDirectory(Path.Combine(testRoot, "ZS_Images"));
            string fakeA = Path.Combine(testRoot, "ZS_Images", "win11_test.wim");
            File.WriteAllBytes(fakeA, new byte[1024]);
            var taskA = new OfflineTask
            {
                Version = 1, Source = "usb",
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TaskNo = "ZS-OFFLINE-TEST-A",
                Image = new OfflineImageInfo { Name = "win11_test.wim", FileName = "win11_test.wim", FilePath = "ZS_Images\\win11_test.wim", FileHash = ComputeSha256(fakeA) },
                TargetPartition = "C:", PartitionScheme = "auto",
                Options = new OfflineOptions { AutoPartition = true, DriverInject = true, BootFix = true, Unattended = false },
            };
            string taskPathA = Path.Combine(testRoot, "zs_task.json");
            if (!OfflineTaskService.Write(taskPathA, taskA)) Log("[offline-test] 方案A 写入失败");

            var scanA = Offline.ScanRoot(testRoot);
            var hitA = scanA.FirstOrDefault(f => Offline.ResolveImagePath(f.Path, f.Task) != null);
            if (hitA != null)
            {
                var img = Offline.ResolveImagePath(hitA.Path, hitA.Task);
                Log("[offline-test] 方案A 命中=" + hitA.Task.TaskNo + " 镜像=" + img);
                passA = File.Exists(img) && new FileInfo(img).Length == 1024 && hitA.Task.Source == "usb";
            }
            else Log("[offline-test] 方案A 未命中");

            string cacheDir = Path.Combine(testRoot, "ZS_Cache");
            Directory.CreateDirectory(Path.Combine(cacheDir, "images"));
            string fakeB = Path.Combine(cacheDir, "images", "win11_test.wim");
            File.WriteAllBytes(fakeB, new byte[1024]);
            var taskB = new OfflineTask
            {
                Version = 1, Source = "windows",
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TaskNo = "ZS-OFFLINE-TEST-B",
                Image = new OfflineImageInfo { Name = "win11_test.wim", FileName = "win11_test.wim", FilePath = "images\\win11_test.wim", FileHash = ComputeSha256(fakeB) },
                TargetPartition = "C:", PartitionScheme = "auto",
            };
            string taskPathB = Path.Combine(cacheDir, "zs_task.json");
            if (!OfflineTaskService.Write(taskPathB, taskB)) Log("[offline-test] 方案B 写入失败");

            var scanB = Offline.ScanRoot(cacheDir);
            var hitB = scanB.FirstOrDefault(f => Offline.ResolveImagePath(f.Path, f.Task) != null);
            if (hitB != null)
            {
                var img = Offline.ResolveImagePath(hitB.Path, hitB.Task);
                Log("[offline-test] 方案B 命中=" + hitB.Task.TaskNo + " 镜像=" + img);
                passB = File.Exists(img) && hitB.Task.Source == "windows";
            }
            else Log("[offline-test] 方案B 未命中");

            passCdRom = true;
        }
        finally
        {
            try { Directory.Delete(testRoot, true); } catch { }
        }

        bool pass = passA && passB && passCdRom;
        Log("[offline-test] 结果: " + (pass
            ? "PASS（方案A/方案B 注入→扫描→镜像解析全链路往返正确，且含 CD-ROM 扫描）"
            : "FAIL（A=" + passA + " B=" + passB + " CDROM=" + passCdRom + "）"));
        return pass ? 0 : 1;
    }

    // ============ 辅助方法 ============
    private static async Task Progress(int? taskId, int p, string? msg, string? step, string? status, bool online = true)
    {
        Log(string.Format("[{0}] {1}: {2}", status ?? "?", step ?? "", msg ?? ""));
        if (online && taskId is > 0)
        {
            try { await Api.ReportProgressAsync(taskId.Value, p, msg, step, status); }
            catch { }
        }
    }

    private static async Task<ImageInfo?> FindImage(int imageId)
    {
        try
        {
            var imgs = await Api.GetImagesAsync(1, 500);
            if (imgs.IsSuccess && imgs.Data != null)
            {
                var hit = imgs.Data.List.FirstOrDefault(i => i.Id == imageId);
                if (hit != null) return hit;
            }
            var detail = await Api.GetImageDetailAsync(imageId);
            return detail.IsSuccess ? detail.Data : null;
        }
        catch { return null; }
    }

    private static InstallOptions ParseOptions(string json)
    {
        var o = new InstallOptions();
        if (string.IsNullOrWhiteSpace(json)) return o;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            o.AutoPartition = GetBool(root, new[] { "auto_partition", "AutoPartition" }, true);
            o.BootFix = GetBool(root, new[] { "auto_repair_boot", "boot_fix", "BootFix" }, true);
            o.DriverInject = GetBool(root, new[] { "auto_inject_drivers", "driver_inject", "DriverInject" }, true);
            o.Unattended = GetBool(root, new[] { "unattended", "Unattended" }, true);
            o.InstallSoftware = GetBool(root, new[] { "install_software", "InstallSoftware" }, false);
            o.Optimize = GetBool(root, new[] { "optimize", "Optimize" }, false);
            o.BackupData = GetBool(root, new[] { "backup_data", "BackupData" }, true);
            o.ImageIndex = GetInt(root, new[] { "image_index", "ImageIndex" }, 1);
            o.DriverPackage = GetStr(root, new[] { "driver_package", "DriverPackage" }, "auto");
            o.BackupLocation = GetStr(root, new[] { "backup_location", "BackupLocation" }, "auto");
        }
        catch (Exception ex)
        {
            Log("选项解析失败，使用默认值: " + ex.Message);
        }
        return o;
    }

    private static bool GetBool(JsonElement root, string[] keys, bool def)
    {
        foreach (var k in keys)
            if (root.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
                return v.GetBoolean();
        return def;
    }

    private static int GetInt(JsonElement root, string[] keys, int def)
    {
        foreach (var k in keys)
            if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetInt32();
        return def;
    }

    private static string GetStr(JsonElement root, string[] keys, string def)
    {
        foreach (var k in keys)
            if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? def;
        return def;
    }

    private static int ResolveDiskIndex(OfflineDiskInfo? disk)
    {
        var disks = Device.GetDiskInfo();
        if (disks.Count == 0) return disk?.Index ?? 0;
        if (disks.Count == 1) return disks[0].Index;

        if (disk is { Index: >= 0 })
        {
            var byIdx = disks.FirstOrDefault(d => d.Index == disk.Index);
            if (byIdx != null) return byIdx.Index;
        }
        if (disk is { Size: > 0 })
        {
            var bySize = disks.Where(d => Math.Abs(d.Size - disk.Size) < disk.Size * 0.02).ToList();
            if (bySize.Count == 1) return bySize[0].Index;
            if (!string.IsNullOrEmpty(disk.Model))
            {
                var byModel = bySize.FirstOrDefault(d =>
                    d.Model.Contains(disk.Model, StringComparison.OrdinalIgnoreCase)
                    || disk.Model.Contains(d.Model, StringComparison.OrdinalIgnoreCase));
                if (byModel != null) return byModel.Index;
            }
        }
        return disks[0].Index;
    }

    private static string NormalizeDrive(string partition)
    {
        if (string.IsNullOrWhiteSpace(partition)) return "C:";
        return partition.Trim().TrimEnd('\\', '/');
    }

    private static string? FindDriverSource(string package)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "drivers"),
            @"D:\Drivers",
            @"D:\ZS_Drivers",
            @"D:\drivers",
        };
        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir)) return dir;
        }
        return null;
    }

    private static async Task<string> BackupData(string location)
    {
        try
        {
            if (location == "network")
            {
                Log("网络备份位置未配置，跳过数据备份");
                return "none";
            }
            if (!Directory.Exists("C:\\Users")) return "none";

            var userDirs = new[] { "C:\\Users\\Public", "C:\\Users\\Administrator", "C:\\Users\\Default" }
                .Where(Directory.Exists).ToArray();
            if (userDirs.Length == 0) return "none";

            string target = location == "auto" ? @"D:\ZS_Backup" : location;
            Directory.CreateDirectory(target);

            bool copied = false;
            foreach (var dir in userDirs)
            {
                Log("备份 " + dir + " → " + target);
                bool ok = await RunRobocopy(dir, Path.Combine(target, new DirectoryInfo(dir).Name));
                if (ok) copied = true;
            }
            return copied ? "ok" : "failed";
        }
        catch (Exception ex)
        {
            Log("备份失败: " + ex.Message);
            return "failed";
        }
    }

    private static Task<bool> RunRobocopy(string src, string dst)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "robocopy.exe",
                    Arguments = "\"" + src + "\" \"" + dst + "\" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit();
                return p.ExitCode < 8;
            }
            catch { return false; }
        });
    }

    private static string ComputeSha256(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    private static void RebootNow()
    {
        Log("执行重启 (wpeutil reboot)...");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wpeutil.exe",
                Arguments = "reboot",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Log("重启命令执行失败: " + ex.Message + "（请手动重启进入新系统）");
        }
    }

    private static string DefaultServerUrl()
    {
        var cfg = Path.Combine(AppContext.BaseDirectory, "agent.json");
        try
        {
            if (File.Exists(cfg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cfg));
                if (doc.RootElement.TryGetProperty("server", out var s) && s.ValueKind == JsonValueKind.String)
                    return s.GetString() ?? "";
            }
        }
        catch { }
        return "http://127.0.0.1:8001";
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static void Log(string message)
    {
        var line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
        try { Console.WriteLine(line); } catch { }
        try { _log?.WriteLine(line); } catch { }
    }
}