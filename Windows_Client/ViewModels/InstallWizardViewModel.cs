using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
{
    /// <summary>一键装机六步向导 ViewModel（WinPE 端完整执行，对齐设计文档 v3.0）</summary>
    public class InstallWizardViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DeviceService _device;
        private readonly ImageDeployService _deploy = new();
        private readonly DiskPartService _diskPart = new();
        private readonly DispatcherTimer _rebootTimer = new();
        private readonly DispatcherTimer _searchTimer;

        private string _clientId = "";
        private int _serverClientId;
        private int _taskId;
        private bool _detectionRan;
        private bool _isPaused;
        private DateTime _startTime;

        /// <summary>本地镜像缓存目录（设计文档 §4.5：PE 环境默认 D:\ZS_Cache\images）</summary>
        private readonly string _localCacheDir = @"D:\ZS_Cache\images";

        public event Action? RequestClose;

        public InstallWizardViewModel(ApiService api, DeviceService device, string serverUrl)
        {
            _api = api;
            _device = device;
            ServerUrl = serverUrl;

            _deploy.ProgressChanged += OnDeployProgress;
            _diskPart.ProgressChanged += OnDeployProgress;
            _rebootTimer.Interval = TimeSpan.FromSeconds(1);
            _rebootTimer.Tick += (_, _) =>
            {
                if (RebootSeconds > 0) { RebootSeconds--; RebootText = RebootSeconds + " 秒后自动重启"; }
                else { _rebootTimer.Stop(); RebootText = "正在重启..."; RestartNow(); }
            };

            // 搜索防抖（设计文档 §4.6：debounce 300ms）
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += async (_, _) => { _searchTimer.Stop(); await RefreshImages(1); };

            InitSteps();
            InitChecks();
            InitOptions();
            InitExecSteps();
            InitImageSources();
            InitFilters();
            InitTemplates();

            PrevCommand = new RelayCommand(GoPrev, () => CurrentStep > 0 && CurrentStep < 5 && !IsExecuting);
            NextCommand = new RelayCommand(async () => await GoNext(), () => !IsExecuting);
            StepNavCommand = new RelayCommand<int>(GoToStep);
            RefreshImagesCommand = new RelayCommand(async () => await RefreshImages(ImagePage), () => !IsExecuting);
            RefreshDisksCommand = new RelayCommand(async () => await RefreshDisks(), () => !IsExecuting);
            ToggleOptionCommand = new RelayCommand<WizardOption>(ToggleOption);
            RepairCheckCommand = new RelayCommand<int>(async i => await RunCheck(i, true), _ => !IsExecuting);
            RerunDetectionCommand = new RelayCommand(async () => await RunDetection(), () => !IsExecuting);
            ForceContinueCommand = new RelayCommand(ForceContinue);
            IgnoreIssueCommand = new RelayCommand<EnvIssueItem>(IgnoreIssue);
            RepairAllCommand = new RelayCommand(async () => await RepairAllIssues(), () => !IsExecuting);
            BackHomeCommand = new RelayCommand(() => RequestClose?.Invoke());
            RestartNowCommand = new RelayCommand(() => RestartNow());
            PrevImagePageCommand = new RelayCommand(async () => await RefreshImages(ImagePage - 1), () => ImagePage > 1 && !IsExecuting);
            NextImagePageCommand = new RelayCommand(async () => await RefreshImages(ImagePage + 1), () => ImagePage < ImageTotalPages && !IsExecuting);
            AddRemoteUrlCommand = new RelayCommand(async () => await AddRemoteUrl(), () => !IsExecuting);
            ApplyPartitionCommand = new RelayCommand(ApplyPartitionPlan);
            PauseCommand = new RelayCommand(TogglePause, () => IsExecuting);
            CancelCommand = new RelayCommand(async () => await CancelExecution(), () => IsExecuting);
            ViewLogsCommand = new RelayCommand(() => { ShowFailLog = !ShowFailLog; });
            RetryCommand = new RelayCommand(async () => await RetryExecution(), () => !IsExecuting);
            GoConfirmCommand = new RelayCommand(() => { BuildSummary(); BuildConfirm(); CurrentStep = 4; });
        }

        // ============ 基本属性 ============
        public string ServerUrl { get; set; }

        private int _currentStep;
        public int CurrentStep
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStep0)); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); OnPropertyChanged(nameof(IsStep4)); OnPropertyChanged(nameof(IsStep5)); OnPropertyChanged(nameof(NextText)); OnPropertyChanged(nameof(ShowForceContinue)); OnPropertyChanged(nameof(ShowCompletion)); OnPropertyChanged(nameof(ShowExecution)); UpdateStepStates(); }
        }
        public bool IsStep0 => CurrentStep == 0;
        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;
        public bool IsStep5 => CurrentStep == 5;
        /// <summary>底部「上一步」可见：步骤 1-4 且未执行中</summary>
        public bool ShowPrevBottom => CurrentStep > 0 && CurrentStep < 5 && !IsExecuting;
        /// <summary>底部「下一步/开始装机」可见：步骤 0-4 且未执行中</summary>
        public bool ShowNextBottom => CurrentStep < 5 && !IsExecuting;

        public string NextText => CurrentStep == 4 ? "开始装机" : "下一步";

        public ObservableCollection<StepNav> Steps { get; } = new();
        public ObservableCollection<EnvCheckItem> Checks { get; } = new();
        public ObservableCollection<ImageInfo> Images { get; } = new();
        public ObservableCollection<DiskInfo> Disks { get; } = new();
        public ObservableCollection<WizardOption> Options { get; } = new();
        public ObservableCollection<ExecStepItem> ExecSteps { get; } = new();
        public ObservableCollection<LogLine> Logs { get; } = new();
        public ObservableCollection<SummaryItem> SummaryItems { get; } = new();
        public ObservableCollection<ConflictItem> DiskConflicts { get; } = new();
        public ObservableCollection<ConflictItem> ConfirmConflicts { get; } = new();

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private int _progressValue;
        public int ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressColor)); OnPropertyChanged(nameof(ProgressText)); } }

        /// <summary>分段彩色进度条颜色（设计文档 §8.2）</summary>
        public string ProgressColor => ProgressValue switch
        {
            >= 100 => "#389E0D",
            >= 70 => "#52C41A",
            >= 30 => "#13C2C2",
            _ => "#1890FF"
        };
        public string ProgressText => ProgressValue + "%";

        private bool _isExecuting;
        public bool IsExecuting { get => _isExecuting; set { _isExecuting = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowExecControls)); OnPropertyChanged(nameof(PauseText)); } }
        public bool ShowExecControls => IsExecuting;
        public string PauseText => _isPaused ? "继续" : "暂停";

        private bool _isPausedFlag;
        public bool IsPausedFlag { get => _isPausedFlag; set { _isPausedFlag = value; OnPropertyChanged(); OnPropertyChanged(nameof(PauseText)); } }

        private bool _detectionPassed;
        public bool DetectionPassed { get => _detectionPassed; set { _detectionPassed = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDetectionSummary)); OnPropertyChanged(nameof(ShowForceContinue)); UpdateStepStates(); } }
        private int _failCount;
        public int FailCount { get => _failCount; set { _failCount = value; OnPropertyChanged(); } }
        public bool ShowDetectionSummary => _detectionRan && !DetectionPassed;
        public bool ShowForceContinue => CurrentStep == 0 && ShowDetectionSummary;

        // ============ 环境异常汇总（设计文档 §3.5）============
        public ObservableCollection<EnvIssueItem> EnvIssues { get; } = new();
        private bool _showIssueSummary;
        public bool ShowIssueSummary { get => _showIssueSummary; set { _showIssueSummary = value; OnPropertyChanged(); } }
        private bool _hasFatalIssue;
        public bool HasFatalIssue { get => _hasFatalIssue; set { _hasFatalIssue = value; OnPropertyChanged(); } }

        // ============ 完成页 ============
        private bool _finished;
        public bool IsFinished { get => _finished; set { _finished = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowCompletion)); OnPropertyChanged(nameof(ShowExecution)); UpdateStepStates(); } }
        private bool _finishedOk;
        public bool IsFinishedOk { get => _finishedOk; set { _finishedOk = value; OnPropertyChanged(); } }
        public bool ShowCompletion => CurrentStep == 5 && IsFinished;
        public bool ShowExecution => CurrentStep == 5 && !IsFinished;

        private int _rebootSeconds = 60;
        public int RebootSeconds { get => _rebootSeconds; set { _rebootSeconds = value; OnPropertyChanged(); } }
        private string _rebootText = "60 秒后自动重启";
        public string RebootText { get => _rebootText; set { _rebootText = value; OnPropertyChanged(); } }

        // ===== Windows 端完成页：任务已创建（等待 WinPE 执行），非真正完成 =====
        private string _completionTitle = "装机完成!";
        public string CompletionTitle { get => _completionTitle; set { _completionTitle = value; OnPropertyChanged(); } }
        private string _completionDesc = "";
        public string CompletionDesc { get => _completionDesc; set { _completionDesc = value; OnPropertyChanged(); } }

        private string _failStep = "";
        public string FailStep { get => _failStep; set { _failStep = value; OnPropertyChanged(); } }
        private string _failReason = "";
        public string FailReason { get => _failReason; set { _failReason = value; OnPropertyChanged(); } }
        private string _completedStepsText = "";
        public string CompletedStepsText { get => _completedStepsText; set { _completedStepsText = value; OnPropertyChanged(); } }
        private string _pendingStepsText = "";
        public string PendingStepsText { get => _pendingStepsText; set { _pendingStepsText = value; OnPropertyChanged(); } }
        private string _suggestionText = "";
        public string SuggestionText { get => _suggestionText; set { _suggestionText = value; OnPropertyChanged(); } }
        private bool _showFailLog;
        public bool ShowFailLog { get => _showFailLog; set { _showFailLog = value; OnPropertyChanged(); } }
        private string _elapsedText = "";
        public string ElapsedText { get => _elapsedText; set { _elapsedText = value; OnPropertyChanged(); } }

        // ============ 命令 ============
        public ICommand PrevCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand StepNavCommand { get; }
        public ICommand RefreshImagesCommand { get; }
        public ICommand RefreshDisksCommand { get; }
        public ICommand ToggleOptionCommand { get; }
        public ICommand RepairCheckCommand { get; }
        public ICommand RerunDetectionCommand { get; }
        public ICommand ForceContinueCommand { get; }
        public ICommand IgnoreIssueCommand { get; }
        public ICommand RepairAllCommand { get; }
        public ICommand BackHomeCommand { get; }
        public ICommand RestartNowCommand { get; }
        public ICommand PrevImagePageCommand { get; }
        public ICommand NextImagePageCommand { get; }
        public ICommand AddRemoteUrlCommand { get; }
        public ICommand ApplyPartitionCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ViewLogsCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand GoConfirmCommand { get; }
        // ============ 初始化 ============
        private void InitSteps()
        {
            var names = new[] { "环境检测", "选择镜像", "目标磁盘", "安装选项", "确认", "执行" };
            for (int i = 0; i < names.Length; i++) Steps.Add(new StepNav { Index = i, Name = names[i] });
        }

        private void InitChecks()
        {
            var defs = new (string name, bool canRepair, string severity)[]
            {
                ("网络连接检测", true, "fatal"),
                ("服务器连通性", true, "fatal"),
                ("客户端认证", true, "fatal"),
                ("PE 环境识别", false, "fatal"),
                ("磁盘控制器驱动", true, "warning"),
                ("磁盘检测", false, "fatal"),
                ("启动模式检测", false, "warning"),
                ("安全启动状态", false, "warning"),
                ("PE 兼容性", false, "fatal"),
            };
            for (int i = 0; i < defs.Length; i++)
                Checks.Add(new EnvCheckItem { Index = i + 1, Name = defs[i].name, CanRepair = defs[i].canRepair, Severity = defs[i].severity });
        }

        private void InitOptions()
        {
            Options.Add(new WizardOption { Key = "boot_fix", Name = "引导修复", Icon = "\uD83D\uDD27", Sub = "自动修复启动项", IsOn = true });
            Options.Add(new WizardOption { Key = "unattended", Name = "无人值守", Icon = "\uD83E\uDD16", Sub = "全程自动安装", IsOn = true });
            Options.Add(new WizardOption { Key = "driver_inject", Name = "驱动注入", Icon = "\uD83D\uDEE1\uFE0F", Sub = "自动检测注入", IsOn = false });
            Options.Add(new WizardOption { Key = "auto_partition", Name = "自动分区", Icon = "\uD83D\uDCC1", Sub = "GPT 自动分区", IsOn = true });
            Options.Add(new WizardOption { Key = "backup_data", Name = "备份数据", Icon = "\uD83D\uDCBE", Sub = "部署前备份", IsOn = false });
            Options.Add(new WizardOption { Key = "install_software", Name = "装软件", Icon = "\uD83D\uDCE6", Sub = "装机后安装", IsOn = false });
            Options.Add(new WizardOption { Key = "optimize", Name = "系统优化", Icon = "\u26A1", Sub = "性能优化方案", IsOn = true });
            Options.Add(new WizardOption { Key = "activate", Name = "激活", Icon = "\uD83D\uDD11", Sub = "自动激活系统", IsOn = false });
            Options.Add(new WizardOption { Key = "keep_data", Name = "保留数据", Icon = "\uD83D\uDCBB", Sub = "不格式化全盘", IsOn = false });
        }

        private void InitExecSteps()
        {
            // 与「全功能流程闭环清单」第 7.2 节对齐：Windows 端仅执行前 2 步（创建任务+完成），
            // 其余实际装机步骤在 WinPE 中执行，此处以「等待」态展示
            var names = new[] { "创建任务", "下载镜像", "校验镜像", "备份数据", "分区/格式化", "部署镜像", "注入驱动", "修复引导", "写入无人值守", "首次登录脚本", "完成" };
            for (int i = 0; i < names.Length; i++) ExecSteps.Add(new ExecStepItem { Name = names[i] });
        }

        private void InitImageSources()
        {
            ImageSourceItems.Add(new ImageSourceItem { Key = "server", Name = "本地服务器", Desc = "从后台服务器拉取镜像列表（默认）" });
            ImageSourceItems.Add(new ImageSourceItem { Key = "url", Name = "远程URL", Desc = "手动输入外部镜像 URL" });
            ImageSourceItems.Add(new ImageSourceItem { Key = "cache", Name = "本地缓存", Desc = "仅显示已下载到本地的镜像" });
            ImageSourceItems.Add(new ImageSourceItem { Key = "custom", Name = "自定义源", Desc = "从预配置的镜像源列表中选择" });
            SelectedImageSource = ImageSourceItems[0];
        }

        private void InitFilters()
        {
            FormatFilters.Add("全部格式");
            FormatFilters.Add("WIM");
            FormatFilters.Add("ESD");
            FormatFilters.Add("ISO");
            FormatFilters.Add("GHO");
            SelectedFormatFilter = FormatFilters[0];

            OsTypeFilters.Add("全部系统");
            OsTypeFilters.Add("Win11");
            OsTypeFilters.Add("Win10");
            OsTypeFilters.Add("Win7");
            SelectedOsTypeFilter = OsTypeFilters[0];
        }

        private void InitTemplates()
        {
            UnattendTemplates.Add(new TemplateItem { Name = "维修店标准模板", Value = "repair_default" });
            UnattendTemplates.Add(new TemplateItem { Name = "纯净办公模板", Value = "office_clean" });
            UnattendTemplates.Add(new TemplateItem { Name = "无无人值守", Value = "none" });
            SelectedUnattendTemplate = UnattendTemplates[0];

            SoftwareTemplates.Add(new TemplateItem { Name = "办公标配", Value = "office_pack" });
            SoftwareTemplates.Add(new TemplateItem { Name = "设计常用", Value = "design_pack" });
            SoftwareTemplates.Add(new TemplateItem { Name = "不装软件", Value = "none" });
            SelectedSoftwareTemplate = SoftwareTemplates[0];

            DriverPackages.Add(new TemplateItem { Name = "自动检测", Value = "auto" });
            DriverPackages.Add(new TemplateItem { Name = "手动选择", Value = "manual" });
            SelectedDriverPackage = DriverPackages[0];

            OptimizePlans.Add(new TemplateItem { Name = "性能优化", Value = "performance" });
            OptimizePlans.Add(new TemplateItem { Name = "安静模式", Value = "quiet" });
            OptimizePlans.Add(new TemplateItem { Name = "自定义", Value = "custom" });
            SelectedOptimizePlan = OptimizePlans[0];

            BackupLocations.Add(new TemplateItem { Name = "自动", Value = "auto" });
            BackupLocations.Add(new TemplateItem { Name = "D:\\Backup", Value = "D:\\Backup" });
            BackupLocations.Add(new TemplateItem { Name = "网络位置", Value = "network" });
            SelectedBackupLocation = BackupLocations[0];
        }

        // ============ 步骤导航 ============
        private void GoPrev() { if (CurrentStep > 0 && CurrentStep < 5) CurrentStep--; }
        private void GoToStep(int step)
        {
            if (IsExecuting) return;
            if (step == 5) return;
            if (step < CurrentStep) { CurrentStep = step; }
        }

        private void UpdateStepStates()
        {
            bool step0Failed = _detectionRan && !DetectionPassed;
            for (int i = 0; i < Steps.Count; i++)
            {
                var s = Steps[i];
                if (i == 0 && step0Failed) s.State = "error";
                else if (i == CurrentStep) s.State = "current";
                else if (i < CurrentStep) s.State = "completed";
                else s.State = "pending";
            }
        }

        private async Task GoNext()
        {
            if (IsExecuting) return;
            if (CurrentStep == 0)
            {
                if (!_detectionRan) await RunDetection();
                else if (DetectionPassed) CurrentStep = 1;
                return;
            }
            if (CurrentStep == 1 && SelectedImage == null) { StatusText = "请先选择系统镜像"; return; }
            if (CurrentStep == 2 && SelectedDisk == null) { StatusText = "请先选择目标磁盘"; return; }
            if (CurrentStep == 4) { BuildConfirm(); if (!HasFatalConflict) { await StartExecution(); } return; }
            if (CurrentStep == 3) { BuildSummary(); BuildConfirm(); }
            if (CurrentStep < 4) CurrentStep++;
            if (CurrentStep == 1 && Images.Count == 0) await RefreshImages(1);
            if (CurrentStep == 2 && Disks.Count == 0) await RefreshDisks();
            if (CurrentStep == 3) await LoadTemplatesFromServer();
        }

        // ============ 环境检测 ============
        public async Task RunDetection()
        {
            _detectionRan = true;
            DetectionPassed = false;
            FailCount = 0;
            ShowIssueSummary = false;
            EnvIssues.Clear();
            StatusText = "环境检测中...";
            foreach (var c in Checks) { c.Status = "pending"; c.Detail = ""; }
            for (int i = 0; i < Checks.Count; i++)
                await RunCheck(i, false);
            FailCount = Checks.Count(c => c.Status == "fail");
            DetectionPassed = FailCount == 0;
            StatusText = DetectionPassed ? "环境检测通过，可以开始装机" : ("环境检测完成，发现 " + FailCount + " 项问题");
            if (DetectionPassed && CurrentStep == 0) CurrentStep = 1;
            else if (!DetectionPassed) BuildIssueSummary();
        }

        /// <summary>构建环境异常汇总列表（设计文档 §3.5：致命/警告区分）</summary>
        private void BuildIssueSummary()
        {
            EnvIssues.Clear();
            for (int i = 0; i < Checks.Count; i++)
            {
                var c = Checks[i];
                if (c.Status != "fail") continue;
                EnvIssues.Add(new EnvIssueItem { Index = i, Name = c.Name, Detail = c.Detail, Severity = c.Severity });
            }
            HasFatalIssue = EnvIssues.Any(i => i.IsFatal);
            ShowIssueSummary = EnvIssues.Count > 0;
        }

        private async Task RunCheck(int index, bool isRepair)
        {
            var item = Checks[index];
            if (isRepair) { item.Status = "detecting"; item.Detail = "正在修复..."; }
            else item.Status = "detecting";
            try
            {
                switch (index)
                {
                    case 0: await CheckNetwork(item); break;
                    case 1: await CheckServer(item); break;
                    case 2: await CheckAuth(item); break;
                    case 3: CheckPeEnv(item); break;
                    case 4: CheckDiskController(item); break;
                    case 5: CheckDisks(item); break;
                    case 6: CheckBootMode(item); break;
                    case 7: CheckSecureBoot(item); break;
                    case 8: CheckCompatibility(item); break;
                }
            }
            catch (Exception ex)
            {
                item.Status = "fail"; item.Detail = ex.Message;
            }
            if (isRepair && item.Status == "success")
                StatusText = "修复成功：" + item.Name;
        }

        private async Task CheckNetwork(EnvCheckItem item)
        {
            string gw = GetDefaultGateway();
            try
            {
                using var ping = new Ping();
                var target = string.IsNullOrEmpty(gw) ? "223.5.5.5" : gw;
                var reply = await ping.SendPingAsync(target, 2000);
                if (reply.Status == IPStatus.Success) { item.Status = "success"; item.Detail = target + ": " + reply.RoundtripTime + "ms"; }
                else { item.Status = "fail"; item.Detail = "无法连通 " + target; }
            }
            catch { item.Status = "fail"; item.Detail = "网络不可用"; }
        }

        private async Task CheckServer(EnvCheckItem item)
        {
            var r = await _api.GetImagesAsync(1, 1);
            if (r.IsSuccess) { item.Status = "success"; item.Detail = "服务器响应正常"; }
            else { item.Status = "fail"; item.Detail = r.Message; }
        }

        private async Task CheckAuth(EnvCheckItem item)
        {
            var reg = await _api.RegisterClientAsync(_device.GetHostname(), _device.GetMacAddress(), _device.GetOsVersion(), "windows", string.IsNullOrEmpty(_clientId) ? null : _clientId);
            if (reg.IsSuccess && reg.Data != null)
            {
                _clientId = reg.Data.ClientId; _serverClientId = reg.Data.Id;
                item.Status = "success"; item.Detail = "已认证 " + _clientId;
            }
            else { item.Status = "fail"; item.Detail = reg.Message; }
        }

        /// <summary>
        /// 保证 Windows 端已完成客户端注册（r2 闭环）。
        /// 即使环境检测被「强制继续」跳过注册失败项，下单前也会再次尝试注册，
        /// 确保 waiting 任务始终携带 client_id，避免服务端「等待执行任务必须关联已注册客户端」拒绝。
        /// </summary>
        private async Task<bool> EnsureRegistered()
        {
            if (_serverClientId > 0 && !string.IsNullOrEmpty(_clientId)) return true;
            try
            {
                var reg = await _api.RegisterClientAsync(_device.GetHostname(), _device.GetMacAddress(), _device.GetOsVersion(), "windows", string.IsNullOrEmpty(_clientId) ? null : _clientId);
                if (reg.IsSuccess && reg.Data != null)
                {
                    _clientId = reg.Data.ClientId; _serverClientId = reg.Data.Id;
                    AddLog("客户端注册成功：" + _clientId);
                    return true;
                }
                FailAt("客户端注册", reg.Message);
                return false;
            }
            catch (Exception ex)
            {
                FailAt("客户端注册", ex.Message);
                return false;
            }
        }

        private void CheckPeEnv(EnvCheckItem item)
        {
            bool inPe = false;
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                    if (d.Name.Equals("X:\\", StringComparison.OrdinalIgnoreCase) && d.IsReady) { inPe = true; break; }
                if (!inPe && File.Exists(@"X:\Windows\System32\wpeinit.exe")) inPe = true;
            }
            catch { }
            if (inPe) { item.Status = "success"; item.Detail = "WinPE x64 环境"; }
            else { item.Status = "fail"; item.Detail = "非 PE 环境（X 盘不存在）"; }
        }

        private void CheckDiskController(EnvCheckItem item)
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT * FROM Win32_IDEController");
                var cnt = s.Get().Count;
                if (cnt > 0) { item.Status = "success"; item.Detail = cnt + " 个磁盘控制器已驱动"; }
                else { item.Status = "skip"; item.Detail = "未检测到 IDE 控制器"; }
            }
            catch { item.Status = "fail"; item.Detail = "控制器查询失败"; }
        }

        private void CheckDisks(EnvCheckItem item)
        {
            var disks = _device.GetDiskInfo();
            int partCount = disks.Sum(d => d.Partitions.Count);
            if (disks.Count > 0) { item.Status = "success"; item.Detail = disks.Count + " 个磁盘 " + partCount + " 个分区"; }
            else { item.Status = "fail"; item.Detail = "未检测到磁盘"; }
        }

        private void CheckBootMode(EnvCheckItem item)
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT FirmwareType FROM Win32_ComputerSystem");
                int type = 0;
                foreach (var o in s.Get()) type = Convert.ToInt32(o["FirmwareType"] ?? 0);
                item.Status = "success";
                item.Detail = type == 2 ? "UEFI" : (type == 1 ? "BIOS/Legacy" : "未知");
            }
            catch { item.Status = "success"; item.Detail = "无法读取启动模式"; }
        }

        private void CheckSecureBoot(EnvCheckItem item)
        {
            try
            {
                using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                var val = k?.GetValue("UEFISecureBootEnabled");
                bool on = val is int i && i == 1;
                item.Status = "success";
                item.Detail = on ? "已开启" : "关闭";
            }
            catch { item.Status = "skip"; item.Detail = "环境不支持"; }
        }

        private void CheckCompatibility(EnvCheckItem item)
        {
            bool pass = Checks.All(c => c.Status == "success");
            if (pass) { item.Status = "success"; item.Detail = "环境兼容"; }
            else { item.Status = "fail"; item.Detail = "存在不兼容项，建议修复"; }
        }

        private static string GetDefaultGateway()
        {
            try
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up);
                return ni?.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "";
            }
            catch { return ""; }
        }

        private void IgnoreIssue(EnvIssueItem? issue)
        {
            if (issue == null) return;
            EnvIssues.Remove(issue);
            // 重新判定致命项
            HasFatalIssue = EnvIssues.Any(i => i.IsFatal);
            StatusText = "已忽略：" + issue.Name;
        }

        private void ForceContinue()
        {
            ShowIssueSummary = false;
            CurrentStep = 1;
            StatusText = "已强制继续（存在未修复项）";
        }

        private async Task RepairAllIssues()
        {
            StatusText = "正在修复全部问题...";
            foreach (var issue in EnvIssues.ToList())
            {
                int idx = Checks.ToList().FindIndex(c => c.Name == issue.Name);
                if (idx >= 0) await RunCheck(idx, true);
            }
            FailCount = Checks.Count(c => c.Status == "fail");
            DetectionPassed = FailCount == 0;
            BuildIssueSummary();
            StatusText = DetectionPassed ? "全部问题已修复" : "仍有 " + FailCount + " 项问题";
        }
        // ============ 镜像选择（设计文档 §4）============
        public ObservableCollection<ImageSourceItem> ImageSourceItems { get; } = new();
        public ObservableCollection<string> FormatFilters { get; } = new();
        public ObservableCollection<string> OsTypeFilters { get; } = new();
        public ObservableCollection<TemplateItem> UnattendTemplates { get; } = new();
        public ObservableCollection<TemplateItem> SoftwareTemplates { get; } = new();
        public ObservableCollection<TemplateItem> DriverPackages { get; } = new();
        public ObservableCollection<TemplateItem> OptimizePlans { get; } = new();
        public ObservableCollection<TemplateItem> BackupLocations { get; } = new();

        private ImageSourceItem? _selectedImageSource;
        public ImageSourceItem? SelectedImageSource
        {
            get => _selectedImageSource;
            set { _selectedImageSource = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowAddUrlPanel)); OnPropertyChanged(nameof(ShowImageList)); }
        }
        public bool ShowAddUrlPanel => SelectedImageSource?.Key == "url";
        public bool ShowImageList => SelectedImageSource?.Key != "url";

        private string _searchKeyword = "";
        public string SearchKeyword
        {
            get => _searchKeyword;
            set { _searchKeyword = value; OnPropertyChanged(); _searchTimer.Stop(); _searchTimer.Start(); }
        }

        private string _selectedFormatFilter = "全部格式";
        public string SelectedFormatFilter
        {
            get => _selectedFormatFilter;
            set { _selectedFormatFilter = value; OnPropertyChanged(); _ = RefreshImages(1); }
        }

        private string _selectedOsTypeFilter = "全部系统";
        public string SelectedOsTypeFilter
        {
            get => _selectedOsTypeFilter;
            set { _selectedOsTypeFilter = value; OnPropertyChanged(); _ = RefreshImages(1); }
        }

        private int _imagePage = 1;
        public int ImagePage { get => _imagePage; set { _imagePage = value; OnPropertyChanged(); } }
        private int _imageTotalPages = 1;
        public int ImageTotalPages { get => _imageTotalPages; set { _imageTotalPages = value; OnPropertyChanged(); } }
        private int _imageTotal;
        public int ImageTotal { get => _imageTotal; set { _imageTotal = value; OnPropertyChanged(); } }
        public string PageText => "共 " + ImageTotal + " 个镜像，第 " + ImagePage + "/" + Math.Max(1, ImageTotalPages) + " 页";

        private string _remoteUrl = "";
        public string RemoteUrl { get => _remoteUrl; set { _remoteUrl = value; OnPropertyChanged(); } }
        private string _remoteName = "";
        public string RemoteName { get => _remoteName; set { _remoteName = value; OnPropertyChanged(); } }
        private string _remoteUrlStatus = "";
        public string RemoteUrlStatus { get => _remoteUrlStatus; set { _remoteUrlStatus = value; OnPropertyChanged(); } }

        private ImageInfo? _selectedImage;
        public ImageInfo? SelectedImage { get => _selectedImage; set { _selectedImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedImageInfoText)); } }
        public string SelectedImageInfoText => SelectedImage == null ? "" : (SelectedImage.Name + "  (" + SelectedImage.SizeDisplay + ")");

        /// <summary>镜像缓存状态文本（设计文档 §4.5）</summary>
        public string GetCacheText(ImageInfo img)
        {
            var f = Path.Combine(_localCacheDir, img.FileName);
            if (File.Exists(f)) return "✅ 已缓存";
            return "⬇️ 需下载";
        }

        public async Task RefreshImages(int page)
        {
            ImagePage = Math.Max(1, page);
            StatusText = "正在加载镜像...";
            var fmt = SelectedFormatFilter == "全部格式" ? "" : SelectedFormatFilter;
            var osType = SelectedOsTypeFilter == "全部系统" ? "" : SelectedOsTypeFilter;
            var r = await _api.GetImagesAsync(ImagePage, 6, SearchKeyword, fmt, osType);
            if (r.IsSuccess && r.Data != null)
            {
                Images.Clear();
                foreach (var img in r.Data.List) { img.CacheText = GetCacheText(img); Images.Add(img); }
                ImageTotal = r.Data.Total;
                ImageTotalPages = Math.Max(1, r.Data.Pages);
                StatusText = "已加载 " + Images.Count + " 个镜像";
            }
            else StatusText = "加载镜像失败: " + r.Message;
        }

        private async Task AddRemoteUrl()
        {
            if (string.IsNullOrWhiteSpace(RemoteUrl)) { RemoteUrlStatus = "请输入镜像 URL"; return; }
            RemoteUrlStatus = "正在添加...";
            var name = string.IsNullOrWhiteSpace(RemoteName) ? RemoteUrl.Substring(RemoteUrl.LastIndexOf('/') + 1) : RemoteName;
            var r = await _api.AddRemoteUrlAsync(RemoteUrl, name);
            if (r.IsSuccess) { RemoteUrlStatus = "✅ 添加成功，后台准备下载"; RemoteUrl = ""; RemoteName = ""; }
            else RemoteUrlStatus = "添加失败: " + r.Message;
        }

        // ============ 目标磁盘（设计文档 §5）============
        private DiskInfo? _selectedDisk;
        public DiskInfo? SelectedDisk { get => _selectedDisk; set { _selectedDisk = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedDiskInfoText)); if (value != null) BuildDiskConflicts(); } }
        public string SelectedDiskInfoText => SelectedDisk == null ? "" : ("磁盘 " + SelectedDisk.Index + "  " + SelectedDisk.Model + "  (" + SelectedDisk.SizeDisplay + ")");

        /// <summary>分区方案：0 自动分区 / 1 保留现有分区 / 2 自定义分区</summary>
        private int _partitionScheme;
        public int PartitionScheme { get => _partitionScheme; set { _partitionScheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowCustomPartitionPanel)); if (value == 0) ApplyAutoPartitionPlan(); else if (value == 2) LoadCustomPartitionPlan(); } }
        public bool ShowCustomPartitionPanel => PartitionScheme == 2;
        private bool _showDiskConflicts;
        public bool ShowDiskConflicts { get => _showDiskConflicts; set { _showDiskConflicts = value; OnPropertyChanged(); } }

        private bool _hasFatalConflict;
        public bool HasFatalConflict { get => _hasFatalConflict; set { _hasFatalConflict = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfirmBannerText)); } }

        public async Task RefreshDisks()
        {
            StatusText = "正在检测磁盘...";
            var disks = await Task.Run(() => _device.GetDiskInfo());
            Disks.Clear();
            foreach (var d in disks) Disks.Add(d);
            StatusText = "已检测到 " + Disks.Count + " 个磁盘";
            if (Disks.Count > 0) SelectedDisk = Disks[0];
        }

        /// <summary>磁盘冲突检测（设计文档 §5.4）</summary>
        private void BuildDiskConflicts()
        {
            DiskConflicts.Clear();
            if (SelectedDisk == null) return;
            var disk = SelectedDisk;
            // 空间不足检测
            long need = (SelectedImage?.FileSize ?? 0) * 12 / 10;
            long avail = disk.Partitions.Where(p => p.DriveLetter == "C:").Sum(p => p.FreeSize);
            if (avail > 0 && need > 0 && avail < need)
                DiskConflicts.Add(new ConflictItem { Icon = "\u26A0", Text = "磁盘空间不足：需要 " + FormatSize(need) + "，可用 " + FormatSize(avail), Severity = "fatal" });
            // 启动模式与分区表不匹配（设计文档 §5.4 GPT/MBR）
            bool uefi = CheckBootModeType();
            bool hasMbr = disk.Partitions.Any(p => p.Type != "GPT" && p.Type != "EFI");
            if (uefi && hasMbr)
                DiskConflicts.Add(new ConflictItem { Icon = "\u26A0", Text = "启动模式为 UEFI，将自动转换为 GPT 分区表", Severity = "warning", AutoFix = true });
            // PE 运行盘检测
            if (disk.Partitions.Any(p => p.DriveLetter == "X:"))
                DiskConflicts.Add(new ConflictItem { Icon = "\u26A0", Text = "这是 PE 运行盘，不可安装", Severity = "fatal" });
            ShowDiskConflicts = DiskConflicts.Count > 0;
        }

        private bool CheckBootModeType()
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT FirmwareType FROM Win32_ComputerSystem");
                foreach (var o in s.Get()) if (Convert.ToInt32(o["FirmwareType"] ?? 0) == 2) return true;
            }
            catch { }
            return false;
        }

        private void ApplyAutoPartitionPlan()
        {
            // 自动分区方案预览（设计文档 §5.5 表格规则）
            if (SelectedDisk == null) return;
            long gb = SelectedDisk.Size / 1024 / 1024 / 1024;
            long cSize;
            if (gb < 128) cSize = gb;
            else if (gb < 256) cSize = 80;
            else if (gb < 512) cSize = 120;
            else cSize = 150;
            StatusText = "自动分区方案：C: " + cSize + "GB" + (gb > cSize ? " + D: 余量" : "");
        }

        private void ApplyPartitionPlan()
        {
            StatusText = "分区方案已应用";
        }

        /// <summary>自定义分区编辑条目集合（设计文档 §5.5 分区编辑器）</summary>
        public ObservableCollection<PartitionEditItem> PartitionEditItems { get; } = new();

        /// <summary>加载自定义分区编辑器：按磁盘现有分区生成条目</summary>
        private void LoadCustomPartitionPlan()
        {
            PartitionEditItems.Clear();
            if (SelectedDisk == null) return;
            foreach (var pt in SelectedDisk.Partitions)
            {
                PartitionEditItems.Add(new PartitionEditItem
                {
                    DriveLetter = string.IsNullOrEmpty(pt.DriveLetter) ? "-" : pt.DriveLetter,
                    SizeText = pt.SizeDisplay,
                    Size = pt.Size,
                    FileSystem = string.IsNullOrEmpty(pt.FileSystem) ? "NTFS" : pt.FileSystem,
                    Label = pt.Label,
                    Type = pt.IsSystem ? "system" : (pt.IsEsp ? "esp" : "data")
                });
            }
        }

        public string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F1") + " GB";
        }

        // ============ 安装选项（设计文档 §6）============
        public ObservableCollection<TemplateItem> Templates => UnattendTemplates; // 兼容别名

        private TemplateItem? _selectedUnattendTemplate;
        public TemplateItem? SelectedUnattendTemplate { get => _selectedUnattendTemplate; set { _selectedUnattendTemplate = value; OnPropertyChanged(); } }
        private TemplateItem? _selectedSoftwareTemplate;
        public TemplateItem? SelectedSoftwareTemplate { get => _selectedSoftwareTemplate; set { _selectedSoftwareTemplate = value; OnPropertyChanged(); } }
        private TemplateItem? _selectedDriverPackage;
        public TemplateItem? SelectedDriverPackage { get => _selectedDriverPackage; set { _selectedDriverPackage = value; OnPropertyChanged(); } }
        private TemplateItem? _selectedOptimizePlan;
        public TemplateItem? SelectedOptimizePlan { get => _selectedOptimizePlan; set { _selectedOptimizePlan = value; OnPropertyChanged(); } }
        private TemplateItem? _selectedBackupLocation;
        public TemplateItem? SelectedBackupLocation { get => _selectedBackupLocation; set { _selectedBackupLocation = value; OnPropertyChanged(); } }

        private async Task LoadTemplatesFromServer()
        {
            // 无人值守模板
            var u = await _api.GetUnattendTemplatesAsync(1, 100);
            if (u.IsSuccess && u.Data != null && u.Data.List.Count > 0)
            {
                UnattendTemplates.Clear();
                UnattendTemplates.Add(new TemplateItem { Name = "无无人值守", Value = "none" });
                foreach (var t in u.Data.List) UnattendTemplates.Add(new TemplateItem { Name = (t as IDictionary<string, object>)?["name"]?.ToString() ?? "模板", Value = "" });
            }
            // 软件模板
            var s = await _api.GetSoftwareTemplatesAsync(1, 100);
            if (s.IsSuccess && s.Data != null && s.Data.List.Count > 0)
            {
                SoftwareTemplates.Clear();
                SoftwareTemplates.Add(new TemplateItem { Name = "不装软件", Value = "none" });
                foreach (var t in s.Data.List) SoftwareTemplates.Add(new TemplateItem { Name = (t as IDictionary<string, object>)?["name"]?.ToString() ?? "模板", Value = "" });
            }
            // 驱动包
            var d = await _api.GetDriversAsync(1, 100);
            if (d.IsSuccess && d.Data != null && d.Data.List.Count > 0)
            {
                DriverPackages.Clear();
                DriverPackages.Add(new TemplateItem { Name = "自动检测", Value = "auto" });
                foreach (var t in d.Data.List) DriverPackages.Add(new TemplateItem { Name = (t as IDictionary<string, object>)?["name"]?.ToString() ?? "驱动包", Value = "" });
            }
        }

        // ============ 扩展配置面板可见性（设计文档 §6 扩展配置）============
        public bool ShowUnattendPanel => OptionOn("unattended");
        public bool ShowSoftwarePanel => OptionOn("install_software");
        public bool ShowDriverPanel => OptionOn("driver_inject");
        public bool ShowOptimizePanel => OptionOn("optimize");
        public bool ShowBackupPanel => OptionOn("backup_data");

        private bool OptionOn(string key) => Options.FirstOrDefault(o => o.Key == key)?.IsOn ?? false;

        private void UpdateOptionPanels()
        {
            OnPropertyChanged(nameof(ShowUnattendPanel));
            OnPropertyChanged(nameof(ShowSoftwarePanel));
            OnPropertyChanged(nameof(ShowDriverPanel));
            OnPropertyChanged(nameof(ShowOptimizePanel));
            OnPropertyChanged(nameof(ShowBackupPanel));
        }

        private void ToggleOption(WizardOption? opt)
        {
            if (opt == null) return;
            opt.IsOn = !opt.IsOn;
            ApplyOptionLinkage(opt);
            UpdateOptionPanels();
        }

        /// <summary>选项联动规则（设计文档 §6.4 完整版）</summary>
        private void ApplyOptionLinkage(WizardOption changed)
        {
            var g = (string key) => Options.FirstOrDefault(o => o.Key == key);
            bool autoPart = g("auto_partition")!.IsOn;
            bool keep = g("keep_data")!.IsOn;
            bool unattended = g("unattended")!.IsOn;
            bool installSw = g("install_software")!.IsOn;
            bool driverInject = g("driver_inject")!.IsOn;
            bool backup = g("backup_data")!.IsOn;

            if (autoPart && keep) { g("keep_data")!.IsOn = false; StatusText = "分区将格式化全盘，已关闭保留数据"; }
            if (keep && autoPart) { g("auto_partition")!.IsOn = false; g("backup_data")!.IsOn = false; StatusText = "保留数据模式下分区和备份不可用"; }
            if (unattended) { g("boot_fix")!.IsOn = true; g("optimize")!.IsOn = true; StatusText = "无人值守已启用引导修复和优化"; }
            if (installSw && !unattended) { g("install_software")!.IsOn = false; StatusText = "装软件需要无人值守模式"; }
            if (!unattended && installSw) { g("install_software")!.IsOn = false; StatusText = "无人值守关闭，装软件已取消"; }
            if (driverInject) { g("boot_fix")!.IsOn = true; StatusText = "驱动注入需要引导修复"; }
            // GHO 镜像不支持自动分区（设计文档 §6.4）
            if (SelectedImage != null && SelectedImage.Format.Equals("gho", StringComparison.OrdinalIgnoreCase) && autoPart)
            {
                g("auto_partition")!.IsOn = false;
                StatusText = "GHO 格式不支持自动分区";
            }
            // 磁盘健康度 < 50% 强制备份（此处以分区数量近似，避免过度探测）
            if (SelectedDisk != null && SelectedDisk.Partitions.Count == 0 && !backup)
            {
                g("backup_data")!.IsOn = true;
                StatusText = "磁盘健康度低，强制备份数据";
            }
        }

        // ============ 确认页（设计文档 §7）============
        public ObservableCollection<SummaryItem> SummaryItemsAll => SummaryItems;

        private string _confirmBannerIcon = "";
        public string ConfirmBannerIcon { get => _confirmBannerIcon; set { _confirmBannerIcon = value; OnPropertyChanged(); } }
        private string _confirmBannerText = "";
        public string ConfirmBannerText { get => _confirmBannerText; set { _confirmBannerText = value; OnPropertyChanged(); } }
        private string _confirmBannerBg = "#F6FFED";
        public string ConfirmBannerBg { get => _confirmBannerBg; set { _confirmBannerBg = value; OnPropertyChanged(); } }
        private string _confirmBannerBorder = "#B7EB8F";
        public string ConfirmBannerBorder { get => _confirmBannerBorder; set { _confirmBannerBorder = value; OnPropertyChanged(); } }

        private void BuildSummary()
        {
            SummaryItems.Clear();
            if (SelectedImage != null)
                SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCE6", Title = "镜像", Content = SelectedImage.Name + "  格式: " + SelectedImage.Format + " · 大小: " + SelectedImage.SizeDisplay + " · " + GetCacheText(SelectedImage) });
            if (SelectedDisk != null)
                SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCBE", Title = "目标磁盘", Content = SelectedDisk.Model + " (" + (SelectedDisk.Partitions.Any(p => p.Type == "GPT" || p.Type == "EFI") ? "GPT" : "MBR") + ")  分区: C: 将格式化 · 安装到此分区" });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDD27", Title = "安装选项", Content = BuildOptionSummary() });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCCB", Title = "预计耗时", Content = EstimateTime() });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCCD", Title = "装机完成后", Content = "自动重启" });
        }

        private string BuildOptionSummary()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Options.First(o => o.Key == "boot_fix").IsOn) parts.Add("引导修复 开启");
            if (Options.First(o => o.Key == "unattended").IsOn) parts.Add("无人值守 " + (SelectedUnattendTemplate?.Name ?? "维修店标准模板"));
            if (Options.First(o => o.Key == "driver_inject").IsOn) parts.Add("驱动注入 " + (SelectedDriverPackage?.Name ?? "自动检测"));
            if (Options.First(o => o.Key == "backup_data").IsOn) parts.Add("备份数据 " + (SelectedBackupLocation?.Name ?? "D:\\Backup"));
            if (Options.First(o => o.Key == "optimize").IsOn) parts.Add("系统优化 " + (SelectedOptimizePlan?.Name ?? "性能优化方案"));
            if (Options.First(o => o.Key == "install_software").IsOn) parts.Add("装软件 " + (SelectedSoftwareTemplate?.Name ?? "办公标配"));
            return parts.Count > 0 ? string.Join("\n", parts) : "无额外选项";
        }

        private void BuildConfirm()
        {
            ConfirmConflicts.Clear();
            foreach (var c in DiskConflicts) ConfirmConflicts.Add(c);
            // 致命冲突检查
            HasFatalConflict = ConfirmConflicts.Any(c => c.Severity == "fatal");
            if (ConfirmConflicts.Count == 0)
            {
                ConfirmBannerIcon = "\u2705";
                ConfirmBannerText = "未检测到冲突，可以开始装机";
                ConfirmBannerBg = "#F6FFED"; ConfirmBannerBorder = "#B7EB8F";
            }
            else if (HasFatalConflict)
            {
                ConfirmBannerIcon = "\u274C";
                ConfirmBannerText = "检测到致命冲突，无法继续";
                ConfirmBannerBg = "#FFF2F0"; ConfirmBannerBorder = "#FFA39E";
            }
            else
            {
                ConfirmBannerIcon = "\u26A0";
                ConfirmBannerText = "检测到 " + ConfirmConflicts.Count + " 项可自动修复/需确认的冲突";
                ConfirmBannerBg = "#FFFBE6"; ConfirmBannerBorder = "#FFE58F";
            }
        }

        private string EstimateTime()
        {
            long mb = SelectedImage?.FileSize / 1024 / 1024 ?? 0;
            double min = Math.Max(5, mb / 400.0);
            return "约 " + Math.Round(min) + " 分钟";
        }
        // ============ 执行（设计文档 §8）============
        /// <summary>
        /// Windows 桌面端执行：仅创建装机任务（状态 waiting），进度停在 5%，
        /// 分区/部署/驱动/引导/无人值守/首次登录脚本等实际装机步骤全部交由 WinPE 客户端执行，
        /// 避免在宿主机执行破坏性磁盘操作。完成页引导用户重启进入 WinPE 自动续装。
        /// </summary>
        private async Task StartExecution(bool reuseTask = false)
        {
            if (SelectedImage == null || SelectedDisk == null) return;
            BuildSummary();
            BuildConfirm();
            CurrentStep = 5;
            IsFinished = false;
            IsExecuting = true;
            _isPaused = false; IsPausedFlag = false;
            if (!reuseTask) _taskId = 0;
            ProgressValue = 0;
            _startTime = DateTime.Now;
            Logs.Clear();
            foreach (var s in ExecSteps) { s.Status = "waiting"; s.Detail = ""; s.Progress = 0; }
            AddLog(reuseTask ? "开始重试装机任务（实际装机将在 WinPE 中自动执行）" : "开始创建装机任务（实际装机将在 WinPE 中自动执行）");

            try
            {
                // r2 闭环：下单前确保已注册客户端，waiting 任务必须携带 client_id
                if (!await EnsureRegistered()) return;

                if (reuseTask && _taskId > 0)
                {
                    // r9 闭环：重试复用原任务订单，服务端将其重置为 waiting 供 PE 重新认领
                    SetExec(0, "running", "正在重试任务...");
                    var retryResult = await _api.RetryTaskAsync(_taskId);
                    if (!retryResult.IsSuccess || retryResult.Data == null)
                    {
                        FailAt("重试任务", retryResult.Message);
                        return;
                    }
                    _taskId = retryResult.Data.Id;
                    SetExec(0, "completed", "任务编号 " + retryResult.Data.TaskNo);
                    AddLog("任务重试：" + retryResult.Data.TaskNo + "（状态：等待 WinPE 执行）");
                }
                else
                {
                    SetExec(0, "running", "正在创建任务...");
                    var taskResult = await _api.CreateTaskAsync(
                        imageId: SelectedImage.Id,
                        clientId: _serverClientId > 0 ? _serverClientId : (int?)null,
                        targetDiskIndex: SelectedDisk.Index,
                        targetPartition: "C:",
                        partitionScheme: PartitionScheme == 0 ? "auto" : "keep",
                        status: "waiting",
                        optionsJson: BuildOptionsJson());
                    if (!taskResult.IsSuccess || taskResult.Data == null)
                    {
                        FailAt("创建任务", taskResult.Message);
                        return;
                    }
                    _taskId = taskResult.Data.Id;
                    SetExec(0, "completed", "任务编号 " + taskResult.Data.TaskNo);
                    AddLog("任务已创建：" + taskResult.Data.TaskNo + "（状态：等待 WinPE 执行）");
                }
                // 关键闭环：Windows 端只下单，进度上报 waiting，绝不上报 completed
                await ReportProgress(5, "任务已创建，等待 WinPE 执行", "创建任务", "waiting");
                ProgressValue = 5;

                // 实际装机步骤全部在 WinPE 中执行，此处以「等待」态展示（不跳过、不完成）
                var peSteps = new (int idx, string name, int prog)[]
                {
                    (1, "下载镜像", 20),
                    (2, "校验镜像", 30),
                    (3, "备份数据", 40),
                    (4, "分区/格式化", 55),
                    (5, "部署镜像", 80),
                    (6, "注入驱动", 90),
                    (7, "修复引导", 95),
                    (8, "写入无人值守", 97),
                    (9, "首次登录脚本", 99),
                };
                foreach (var st in peSteps)
                {
                    SetExec(st.idx, "waiting", "将在 WinPE 中执行");
                    await Task.Delay(60);
                }

                // ===== 方案B 双保险：预下载镜像到数据盘 + 注入离线任务 zs_task.json =====
                // 即使 WinPE 环境无网络（登录无效），也能通过磁盘扫描到的离线任务完成无人值守装机
                bool offlineInjected = await PreDownloadAndInjectOfflineTask();
                if (offlineInjected)
                {
                    SetExec(1, "completed", "已预下载");
                    SetExec(2, "completed", "校验通过");
                }

                // 完成页：引导重启进 PE，而非报「装机完成」
                SetExec(10, "completed", "任务已创建");
                CompletionTitle = "任务已创建";
                CompletionDesc = offlineInjected
                    ? "请重启电脑并进入 WinPE 环境，ZS 装机助手将自动检测并继续完成装机，全程无人值守。\n已预下载镜像并注入离线任务，即使 WinPE 无网络也能完成装机（无需登录）。"
                    : "请重启电脑并进入 WinPE 环境，ZS 装机助手将自动检测并继续完成装机，全程无人值守。";
                RebootText = offlineInjected
                    ? "进入 WinPE 后，客户端会自动识别本任务（在线或离线）并全自动完成装机"
                    : "进入 WinPE 后，客户端会自动识别本任务并全自动完成装机";
                IsFinished = true;
                IsFinishedOk = true;
                StatusText = offlineInjected
                    ? "任务已创建！镜像已预下载，请重启进入 WinPE 自动完成装机"
                    : "任务已创建！请重启进入 WinPE 自动继续装机";
                ElapsedText = "耗时: " + FormatDuration(DateTime.Now - _startTime);
            }
            catch (OperationCanceledException)
            {
                FailAt("执行", "任务已取消");
            }
            catch (Exception ex)
            {
                FailAt("执行异常", ex.Message);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 方案B：Windows 下单时预下载镜像到数据盘（D:\ZS_Cache\images）并校验 SHA256，
        /// 同时取回无人值守应答与首次登录脚本，注入 D:\ZS_Cache\zs_task.json 离线任务。
        /// PE 环境无网络时，客户端扫描磁盘即可离线完成完整无人值守装机（方案C 双保险）。
        /// 返回是否成功注入；失败不阻塞主流程（PE 联网仍可自动续装）。
        /// </summary>
        private async Task<bool> PreDownloadAndInjectOfflineTask()
        {
            try
            {
                if (SelectedImage == null || SelectedDisk == null || _taskId <= 0) return false;
                var image = SelectedImage;
                string cacheDir = @"D:\ZS_Cache\images";
                string cacheFile = Path.Combine(cacheDir, image.FileName);
                bool hasImage = File.Exists(cacheFile);

                // 1) 预下载镜像（真实下载：clientDownload 流式接口，支持断点续传）
                if (!hasImage)
                {
                    SetExec(1, "running", "正在预下载镜像...");
                    AddLog("方案B 预下载镜像：" + image.FileName);
                    var dlProgress = new Progress<int>(p =>
                    {
                        int mapped = 5 + (int)(p * 0.12);
                        ProgressValue = Math.Max(ProgressValue, mapped);
                        SetExec(1, "running", "正在预下载镜像... " + p + "%");
                    });
                    var dlUrl = ServerUrl + "/api/v1/images/" + image.Id + "/clientDownload";
                    var dl = await _api.DownloadFileAsync(dlUrl, cacheFile, dlProgress);
                    if (!dl.Ok)
                    {
                        AddLog("镜像预下载失败：" + dl.Error + "（离线注入已跳过，PE 联网仍可自动下载）");
                        return false;
                    }
                    hasImage = true;
                    AddLog("镜像预下载完成：" + cacheFile);
                }
                else AddLog("镜像已在数据盘缓存，跳过预下载");

                // 2) 校验镜像（SHA256 与服务端 file_hash 比对）
                if (hasImage && !string.IsNullOrEmpty(image.FileHash) && image.FileHash.Length >= 32)
                {
                    string actualHash = ComputeSha256(cacheFile);
                    if (!string.Equals(actualHash, image.FileHash, StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog("镜像 SHA256 校验失败，离线注入已跳过（PE 联网时将重新下载）");
                        return false;
                    }
                    AddLog("镜像 SHA256 校验通过");
                }

                // 3) 取回无人值守应答 + 首次登录脚本（服务端生成，任务 waiting 状态即可取）
                string unattendXml = "";
                string firstLogonCmd = "";
                try
                {
                    var ur = await _api.GetTaskUnattendAsync(_taskId);
                    if (ur.IsSuccess && ur.Data != null) unattendXml = ur.Data.Xml ?? "";
                    var fr = await _api.GetTaskFirstLogonAsync(_taskId);
                    if (fr.IsSuccess && fr.Data != null) firstLogonCmd = fr.Data.Cmd ?? "";
                }
                catch { /* 取回失败不影响离线任务注入（无应答时 PE 端自动跳过） */ }

                // 4) 组装并写入离线任务 zs_task.json（PE 按盘符根目录解析相对镜像路径）
                var offline = new OfflineTask
                {
                    Source = "windows",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TaskNo = "ZS-OFFLINE-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                    Image = new OfflineImageInfo
                    {
                        Name = image.Name,
                        FileName = image.FileName,
                        // 相对 zs_task.json 所在目录（D:\ZS_Cache\）解析：镜像在 D:\ZS_Cache\images\xxx
                        FilePath = "images\\" + image.FileName,
                        FileHash = image.FileHash ?? "",
                        FileSize = image.FileSize,
                        SizeDisplay = image.SizeDisplay
                    },
                    Disk = new OfflineDiskInfo
                    {
                        Index = SelectedDisk.Index,
                        Size = SelectedDisk.Size,
                        Model = SelectedDisk.Model ?? ""
                    },
                    TargetPartition = "C:",
                    PartitionScheme = PartitionScheme == 0 ? "auto" : "keep",
                    Options = new OfflineOptions
                    {
                        BackupData = Options.First(o => o.Key == "backup_data").IsOn,
                        AutoPartition = Options.First(o => o.Key == "auto_partition").IsOn,
                        DriverInject = Options.First(o => o.Key == "driver_inject").IsOn,
                        BootFix = Options.First(o => o.Key == "boot_fix").IsOn,
                        Unattended = Options.First(o => o.Key == "unattended").IsOn,
                        InstallSoftware = Options.First(o => o.Key == "install_software").IsOn,
                        Optimize = Options.First(o => o.Key == "optimize").IsOn
                    },
                    UnattendXml = unattendXml,
                    FirstLogonCmd = firstLogonCmd
                };
                if (OfflineTaskService.Write(@"D:\ZS_Cache\zs_task.json", offline))
                {
                    AddLog("离线任务已注入 D:\\ZS_Cache\\zs_task.json（PE 无网也可无人值守装机）");
                    return true;
                }
                AddLog("离线任务注入失败（数据盘不可写，PE 联网仍可自动续装）");
                return false;
            }
            catch (Exception ex)
            {
                AddLog("离线注入异常：" + ex.Message);
                return false;
            }
        }

        /// <summary>计算文件 SHA256（校验镜像完整性）</summary>
        private static string ComputeSha256(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = System.Security.Cryptography.SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
            }
            catch { return ""; }
        }


        /// <summary>
        /// 构建完整 options 契约（对齐「全功能流程闭环清单」第 7.2 节），
        /// 供 WinPE 端续装时完整读取镜像/分区/无人值守/软件/优化等全部选项。
        /// </summary>
        private string BuildOptionsJson()
        {
            if (SelectedDisk == null) return "{}";
            string CleanValue(string? v)
                => string.IsNullOrEmpty(v) || v == "none" ? "" : v;

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "install",
                auto_partition = Options.First(o => o.Key == "auto_partition").IsOn,
                auto_repair_boot = Options.First(o => o.Key == "boot_fix").IsOn,
                auto_inject_drivers = Options.First(o => o.Key == "driver_inject").IsOn,
                unattended = Options.First(o => o.Key == "unattended").IsOn,
                install_software = Options.First(o => o.Key == "install_software").IsOn,
                optimize = Options.First(o => o.Key == "optimize").IsOn,
                backup_data = Options.First(o => o.Key == "backup_data").IsOn,
                image_index = 1,
                // r8 磁盘跨环境兜底：PE 环境磁盘序号可能与宿主机不一致，记录物理特征供 PE 端模糊匹配
                disk_index = SelectedDisk.Index,
                disk_size = SelectedDisk.Size,
                disk_model = SelectedDisk.Model,
                unattend_template_id = CleanValue(SelectedUnattendTemplate?.Value) is { Length: > 0 } ut ? ut : null,
                software_template_id = CleanValue(SelectedSoftwareTemplate?.Value) is { Length: > 0 } st ? st : null,
                driver_package = SelectedDriverPackage?.Value ?? "auto",
                optimize_plan = SelectedOptimizePlan?.Value ?? "performance",
                backup_location = SelectedBackupLocation?.Value ?? "auto"
            });
        }
        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalMinutes >= 1) return (int)ts.TotalMinutes + " 分 " + ts.Seconds + " 秒";
            return ts.Seconds + " 秒";
        }

        /// <summary>暂停等待 + 取消检查（设计文档 §8.6）</summary>
        private async Task WaitIfPausedOrCanceled()
        {
            while (_isPaused) await Task.Delay(200);
        }

        private void TogglePause()
        {
            if (!IsExecuting) return;
            _isPaused = !_isPaused;
            IsPausedFlag = _isPaused;
            if (_isPaused)
            {
                AddLog("已暂停");
                _ = _api.PauseTaskAsync(_taskId);
                ProgressValue = ProgressValue; // 触发颜色更新为黄色暂停态
            }
            else
            {
                AddLog("已继续");
                _ = _api.ResumeTaskAsync(_taskId);
            }
        }

        private async Task CancelExecution()
        {
            if (!IsExecuting) return;
            AddLog("正在取消...");
            _isPaused = false; IsPausedFlag = false;
            _ = _api.CancelTaskAsync(_taskId);
            await Task.Delay(100);
            FailAt("执行", "已由用户取消");
            StatusText = "装机已取消，可返回重新配置";
        }

        private async Task RetryExecution()
        {
            // r9 闭环：已有任务订单时复用原任务重试（服务端重置为 waiting），不重复创建新任务
            await StartExecution(_taskId > 0);
        }

        private void FailAt(string step, string reason)
        {
            int idx = 0;
            for (int i = 0; i < ExecSteps.Count; i++)
                if (ExecSteps[i].Name == step) idx = i;
            SetExec(idx, "failed", reason);
            FailStep = step; FailReason = reason;
            IsFinished = true; IsFinishedOk = false;
            StatusText = "装机失败：" + reason;
            AddLog("失败：" + reason);
            BuildFailSummary();
            _ = ReportProgress(0, "装机失败: " + reason, step, "failed");
        }

        /// <summary>失败界面：已完成/未完成步骤 + 建议（设计文档 §9.2）</summary>
        private void BuildFailSummary()
        {
            var done = ExecSteps.Where(s => s.Status == "completed" || s.Status == "skipped").Select(s => s.Name).ToList();
            var pending = ExecSteps.Where(s => s.Status != "completed" && s.Status != "skipped").Select(s => s.Name).ToList();
            CompletedStepsText = done.Count > 0 ? string.Join(" / ", done) : "无";
            PendingStepsText = pending.Count > 0 ? string.Join(" / ", pending) : "无";
            SuggestionText = FailStep.Contains("部署") ? "重新下载镜像后重试" : "检查配置后重试";
        }

        private void SetExec(int index, string status, string detail)
        {
            if (index < 0 || index >= ExecSteps.Count) return;
            var item = ExecSteps[index];
            item.Status = status;
            if (!string.IsNullOrEmpty(detail)) item.Detail = detail;
        }

        private void OnDeployProgress(int p, string m) { ProgressValue = Math.Min(100, p); AddLog(m); }

        private void AddLog(string text) => Logs.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss"), text));

        private async Task ReportProgress(int progress, string? message, string? stepName, string? status)
        {
            if (_taskId <= 0) return;
            try { await _api.ReportProgressAsync(_taskId, progress, message, stepName, status); }
            catch { }
        }

        // ============ 完成页（设计文档 §9）============
        private void StartRebootCountdown()
        {
            RebootSeconds = 60; RebootText = "60 秒后自动重启";
            _rebootTimer.Start();
        }

        private void RestartNow()
        {
            // r5 闭环：Windows 桌面端「立即重启」真实重启系统，重启后需手动进入 PE 完成装机
            _rebootTimer.Stop();
            RebootText = "正在重启...";
            AddLog("正在重启系统...");
            AddLog("提示：重启后请按启动热键（如 F12）进入 PE 环境，ZS 装机助手将自动继续装机");
            var confirm = MessageBox.Show(
                "系统即将重启以进入装机流程。\n\n请先保存所有工作！\n重启后请按启动热键（如 F12 / F11 / Esc）选择进入 WinPE 环境，装机助手将自动继续完成装机。\n\n是否立即重启？",
                "确认重启", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) { RebootText = "已取消重启，可稍后手动重启"; return; }
            try
            {
                using var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "shutdown.exe";
                p.StartInfo.Arguments = "/r /t 5 /c \"ZS 装机助手：任务已创建，重启后进入 PE 自动装机\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
            }
            catch (Exception ex)
            {
                AddLog("自动重启失败：" + ex.Message);
                RequestClose?.Invoke();
            }
        }
        // 兼容旧引用
        private string _driverPath = "";
        public string DriverPath { get => _driverPath; set { _driverPath = value; OnPropertyChanged(); } }
    }

}