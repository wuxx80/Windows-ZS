using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinPE_Client.Models;
using WinPE_Client.Services;

namespace WinPE_Client.ViewModels
{
    /// <summary>U盘制作 四步向导 ViewModel（步骤：①选U盘 ②选PE ③选项 ④确认+执行）</summary>
    public class UDiskViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api;
        private readonly UDiskService _udisk;
        private readonly string _clientDir;
        private CancellationTokenSource? _cts;

        public UDiskViewModel(ApiService api, UDiskService udisk, string serverUrl, string clientDir)
        {
            _api = api;
            _udisk = udisk;
            _clientDir = clientDir;
            _api.SetBaseUrl(serverUrl);

            InitSteps();
            InitCommands();
            // 默认选中项（避免首屏空白）
            SelectedOutputMode = OutputModes[0];
            SelectedFileSystem = FileSystems[0];
            SelectedBootType = BootTypes[0];
            SelectedPartition = PartitionSchemes[0];
            RefreshDisksCommand.Execute(null);
            // 固定定制 PE：启动即拉取服务器默认 PE 版本（简化方案，移除本地/URL 三选一）
            _ = RefreshPe();
        }

        public event Action? RequestClose;

        // ==================== 步骤状态 ====================
        private int _currentStep;
        public int CurrentStep
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStep0)); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); UpdateStepStates(); }
        }
        public bool IsStep0 => CurrentStep == 0;
        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool ShowPrevBottom => CurrentStep > 0 && CurrentStep < 3 && !IsExecuting;
        public bool ShowNextBottom => CurrentStep < 3 && !IsExecuting;

        public ObservableCollection<UdiskStepNav> Steps { get; } = new();

        // ==================== 步骤①a 输出方式（写U盘 / 生成ISO） ====================
        public ObservableCollection<RadioOption> OutputModes { get; } = new()
        {
            new RadioOption { Key = "udisk", Name = "写入 U 盘", Sub = "直接制作可启动 U 盘" },
            new RadioOption { Key = "iso", Name = "生成 ISO 镜像", Sub = "打包成可启动镜像" },
        };
        private RadioOption? _selectedOutputMode;
        public RadioOption? SelectedOutputMode
        {
            get => _selectedOutputMode;
            set { _selectedOutputMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUDiskMode)); OnPropertyChanged(nameof(IsIsoMode)); OnPropertyChanged(nameof(ConfirmPrompt)); OnPropertyChanged(nameof(ConfirmPassed)); OnPropertyChanged(nameof(DestructiveWarnText)); OnPropertyChanged(nameof(StartButtonText)); }
        }
        public bool IsUDiskMode => SelectedOutputMode?.Key == "udisk";
        public bool IsIsoMode => SelectedOutputMode?.Key == "iso";
        public string StartButtonText => IsIsoMode ? "开始生成" : "开始制作";

        // ==================== 步骤① U盘 ====================

        // ==================== ISO 输出路径（生成 ISO 模式） ====================
        private string _isoOutputPath = "";
        public string IsoOutputPath
        {
            get => _isoOutputPath;
            set { _isoOutputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsoStatusText)); OnPropertyChanged(nameof(ConfirmPassed)); }
        }
        public string IsoStatusText => string.IsNullOrEmpty(IsoOutputPath) ? "请选择 ISO 输出路径" : "输出到: " + IsoOutputPath;
        public ObservableCollection<RemovableDisk> UDisks { get; } = new();
        private RemovableDisk? _selectedUDisk;
        public RemovableDisk? SelectedUDisk
        {
            get => _selectedUDisk;
            set { _selectedUDisk = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUDisk)); OnPropertyChanged(nameof(UDiskWarnText)); OnPropertyChanged(nameof(UDiskDetailText)); }
        }
        public bool HasUDisk => SelectedUDisk != null;
        public string UDiskWarnText => SelectedUDisk?.IsSystem == true ? "⚠ 系统盘不可选" : (SelectedUDisk != null && SelectedUDisk.Size < 512L * 1024 * 1024 ? "⚠ 容量不足 512MB" : "");
        public string UDiskDetailText => SelectedUDisk?.CapacityText + "  " + SelectedUDisk?.DetailText ?? "";
        public bool HasNoUDisk => UDisks.Count == 0;

        // ==================== 步骤② 固定定制 PE（服务器托管，移除本地/URL 三选一） ====================
        public ObservableCollection<PeVersionInfo> PeVersions { get; } = new();
        private PeVersionInfo? _selectedPeVersion;
        public PeVersionInfo? SelectedPeVersion
        {
            get => _selectedPeVersion;
            set { _selectedPeVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeStatusText)); }
        }
        public string PeStatusText
            => SelectedPeVersion == null ? "请选择 PE 版本" : SelectedPeVersion.DisplayText;
        public bool HasNoPeVersions => PeVersions.Count == 0;

        // ==================== 步骤③ 选项 ====================
        public ObservableCollection<RadioOption> FileSystems { get; } = new()
        {
            new RadioOption { Key = "exFAT", Name = "exFAT", Sub = "推荐，支持大文件" },
            new RadioOption { Key = "FAT32", Name = "FAT32", Sub = "兼容性最好" },
            new RadioOption { Key = "NTFS", Name = "NTFS", Sub = "UEFI 启动不兼容" },
        };
        private RadioOption? _selectedFileSystem;
        public RadioOption? SelectedFileSystem
        {
            get => _selectedFileSystem;
            set { _selectedFileSystem = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConflictText)); }
        }

        public ObservableCollection<RadioOption> BootTypes { get; } = new()
        {
            new RadioOption { Key = "both", Name = "UEFI + Legacy 双引导", Sub = "兼容新老机器" },
            new RadioOption { Key = "uefi", Name = "仅 UEFI", Sub = "新机器" },
            new RadioOption { Key = "legacy", Name = "仅 Legacy", Sub = "老机器" },
        };
        private RadioOption? _selectedBootType;
        public RadioOption? SelectedBootType
        {
            get => _selectedBootType;
            set { _selectedBootType = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConflictText)); OnPropertyChanged(nameof(IsEspDisabled)); }
        }
        public bool IsEspDisabled => SelectedBootType?.Key == "legacy";

        public ObservableCollection<RadioOption> PartitionSchemes { get; } = new()
        {
            new RadioOption { Key = "single", Name = "单分区", Sub = "推荐" },
            new RadioOption { Key = "esp", Name = "UEFI 双分区 (ESP+数据)", Sub = "仅 UEFI" },
        };
        private RadioOption? _selectedPartition;
        public RadioOption? SelectedPartition
        {
            get => _selectedPartition;
            set { _selectedPartition = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConflictText)); }
        }

        private string _volumeLabel = "ZS_PE";
        public string VolumeLabel { get => _volumeLabel; set { _volumeLabel = value; OnPropertyChanged(); } }

        // ==================== 方案A：写入装机镜像 + 离线无人值守任务 ====================
        private bool _includeOfflineImage;
        public bool IncludeOfflineImage
        {
            get => _includeOfflineImage;
            set { _includeOfflineImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(OfflineWarnText)); }
        }
        private string _offlineImagePath = "";
        public string OfflineImagePath
        {
            get => _offlineImagePath;
            set { _offlineImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OfflineImageStatusText)); OnPropertyChanged(nameof(OfflineWarnText)); }
        }
        private string _offlineUnattendPath = "";
        public string OfflineUnattendPath
        {
            get => _offlineUnattendPath;
            set { _offlineUnattendPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OfflineUnattendStatusText)); }
        }
        private string _offlineFirstLogonPath = "";
        public string OfflineFirstLogonPath
        {
            get => _offlineFirstLogonPath;
            set { _offlineFirstLogonPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OfflineFirstLogonStatusText)); }
        }
        private string _offlineAdminPassword = "Zs@12345";
        public string OfflineAdminPassword { get => _offlineAdminPassword; set { _offlineAdminPassword = value; OnPropertyChanged(); } }

        public string OfflineImageStatusText => string.IsNullOrEmpty(OfflineImagePath) ? "请选择装机镜像 (.wim/.esd)" : OfflineImagePath;
        public string OfflineUnattendStatusText => string.IsNullOrEmpty(OfflineUnattendPath) ? "未选择（将使用默认无人值守模板）" : OfflineUnattendPath;
        public string OfflineFirstLogonStatusText => string.IsNullOrEmpty(OfflineFirstLogonPath) ? "未选择（可选）" : OfflineFirstLogonPath;

        /// <summary>镜像注入警告：FAT32 单文件 4GB 上限 / ISO9660 4GB 上限</summary>
        public string OfflineWarnText
        {
            get
            {
                if (!IncludeOfflineImage || string.IsNullOrEmpty(OfflineImagePath) || !File.Exists(OfflineImagePath)) return "";
                try
                {
                    long size = new FileInfo(OfflineImagePath).Length;
                    if (size > 4L * 1024 * 1024 * 1024)
                    {
                        if (IsUDiskMode && SelectedFileSystem?.Key == "FAT32")
                            return "⚠ 装机镜像超过 4GB，FAT32 无法容纳，请改用 exFAT/NTFS";
                        if (IsIsoMode)
                            return "⚠ 装机镜像超过 4GB，ISO9660 单文件上限为 4GB，建议改用「写入 U 盘」模式（exFAT）";
                    }
                }
                catch { }
                return "";
            }
        }

        public string ConflictText
        {
            get
            {
                if (SelectedFileSystem?.Key == "NTFS" && (SelectedBootType?.Key == "both" || SelectedBootType?.Key == "uefi"))
                    return "⚠ NTFS 无法 UEFI 引导，建议使用 exFAT/FAT32";
                if (SelectedBootType?.Key == "legacy" && SelectedPartition?.Key == "esp")
                    return "⚠ 仅 Legacy 引导不支持 ESP 分区，将使用单分区";
                return "";
            }
        }

        // ==================== 步骤④ 确认 + 执行 ====================
        public ObservableCollection<UdiskSummaryItem> SummaryItems { get; } = new();
        private string _confirmInput = "";
        public string ConfirmInput { get => _confirmInput; set { _confirmInput = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfirmPassed)); } }
        public bool ConfirmPassed => IsIsoMode ? !string.IsNullOrEmpty(IsoOutputPath) : _confirmInput.Trim() == (SelectedUDisk?.Index.ToString() ?? "-1");
        public string ConfirmPrompt => IsIsoMode ? "即将基于所选 PE 生成可启动 ISO 镜像（不会格式化任何磁盘）：" : "请输入目标盘号（磁盘 " + SelectedUDisk?.Index + "）以确认:";
        public string DestructiveWarnText => IsIsoMode ? "将解包所选 PE 并打包成可启动 ISO（含装机助手与工具）：" : "以下操作将【清空并格式化】该 U 盘，请确认：";

        public ObservableCollection<UdiskExecStep> ExecSteps { get; } = new();
        public ObservableCollection<LogLine> Logs { get; } = new();
        private int _progressValue;
        public int ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); } }
        public string ProgressText => ProgressValue + "%";
        private bool _isExecuting;
        public bool IsExecuting { get => _isExecuting; set { _isExecuting = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowExecControls)); OnPropertyChanged(nameof(ShowPrevBottom)); OnPropertyChanged(nameof(ShowNextBottom)); OnPropertyChanged(nameof(CanStart)); } }
        public bool ShowExecControls => IsExecuting;
        private bool _finished;
        public bool IsFinished { get => _finished; set { _finished = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowCompletion)); OnPropertyChanged(nameof(ShowExecution)); } }
        private string _resultKind = "";
        public string ResultKind { get => _resultKind; set { _resultKind = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSuccess)); OnPropertyChanged(nameof(IsFailed)); OnPropertyChanged(nameof(IsCanceled)); } }
        public bool IsSuccess => ResultKind == "success";
        public bool IsFailed => ResultKind == "failed";
        public bool IsCanceled => ResultKind == "canceled";
        public bool ShowCompletion => CurrentStep == 3 && IsFinished;
        public bool ShowExecution => CurrentStep == 3 && !IsFinished;
        public bool CanStart => !IsExecuting && ConfirmPassed;

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
        private string _failReason = "";
        public string FailReason { get => _failReason; set { _failReason = value; OnPropertyChanged(); } }

        // ==================== 命令 ====================
        public ICommand PrevCommand { get; private set; } = null!;
        public ICommand NextCommand { get; private set; } = null!;
        public ICommand RefreshDisksCommand { get; private set; } = null!;
        public ICommand RefreshPeCommand { get; private set; } = null!;
        public ICommand BrowseIsoOutputCommand { get; private set; } = null!;
        public ICommand BrowseInstallImageCommand { get; private set; } = null!;
        public ICommand BrowseUnattendCommand { get; private set; } = null!;
        public ICommand BrowseFirstLogonCommand { get; private set; } = null!;
        public ICommand SelectFsCommand { get; private set; } = null!;
        public ICommand SelectBootCommand { get; private set; } = null!;
        public ICommand SelectPartitionCommand { get; private set; } = null!;
        public ICommand StartCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand BackHomeCommand { get; private set; } = null!;
        public ICommand RetryCommand { get; private set; } = null!;

        private void InitCommands()
        {
            PrevCommand = new RelayCommand(GoPrev, () => CurrentStep > 0 && CurrentStep < 3 && !IsExecuting);
            NextCommand = new RelayCommand(async () => await GoNext(), () => CurrentStep < 3 && !IsExecuting);
            RefreshDisksCommand = new RelayCommand(RefreshDisks);
            RefreshPeCommand = new RelayCommand(async () => await RefreshPe());
            BrowseIsoOutputCommand = new RelayCommand(BrowseIsoOutput);
            BrowseInstallImageCommand = new RelayCommand(BrowseInstallImage);
            BrowseUnattendCommand = new RelayCommand(BrowseUnattend);
            BrowseFirstLogonCommand = new RelayCommand(BrowseFirstLogon);
            SelectFsCommand = new RelayCommand<RadioOption>(o => SelectedFileSystem = o);
            SelectBootCommand = new RelayCommand<RadioOption>(o => SelectedBootType = o);
            SelectPartitionCommand = new RelayCommand<RadioOption>(o => SelectedPartition = o);
            StartCommand = new RelayCommand(async () => await StartExecution(), () => CanStart);
            CancelCommand = new RelayCommand(CancelExecution, () => IsExecuting);
            BackHomeCommand = new RelayCommand(() => RequestClose?.Invoke());
            RetryCommand = new RelayCommand(RetryExecution);
        }

        private void InitSteps()
        {
            Steps.Add(new UdiskStepNav { Index = 0, Name = "选择 U 盘" });
            Steps.Add(new UdiskStepNav { Index = 1, Name = "选择 PE" });
            Steps.Add(new UdiskStepNav { Index = 2, Name = "制作选项" });
            Steps.Add(new UdiskStepNav { Index = 3, Name = "确认执行" });
            UpdateStepStates();
        }

        private void UpdateStepStates()
        {
            for (int i = 0; i < Steps.Count; i++)
                Steps[i].State = i == CurrentStep ? "current" : (i < CurrentStep ? "completed" : "pending");
        }

        private void GoPrev()
        {
            if (CurrentStep > 0 && CurrentStep < 3 && !IsExecuting)
                CurrentStep--;
        }

        private async Task GoNext()
        {
            if (CurrentStep == 0)
            {
                if (IsUDiskMode && SelectedUDisk == null) { StatusText = "请先选择 U 盘"; return; }
                if (IsIsoMode && string.IsNullOrEmpty(IsoOutputPath)) { StatusText = "请选择 ISO 输出路径"; return; }
            }
            if (CurrentStep == 1)
            {
                if (SelectedPeVersion == null) { StatusText = "请选择 PE 版本"; return; }
            }
            if (CurrentStep == 2)
            {
                if (IncludeOfflineImage && string.IsNullOrEmpty(OfflineImagePath)) { StatusText = "请选择装机镜像文件"; return; }
                if (!string.IsNullOrEmpty(OfflineWarnText)) { StatusText = OfflineWarnText; return; }
                BuildSummary(); CurrentStep = 3; return;
            }
            if (CurrentStep < 3) CurrentStep++;
        }

        private void RefreshDisks()
        {
            UDisks.Clear();
            foreach (var d in _udisk.GetRemovableDisks())
                UDisks.Add(d);
            OnPropertyChanged(nameof(HasNoUDisk));
            StatusText = UDisks.Count > 0 ? "检测到 " + UDisks.Count + " 个可移动磁盘" : "未检测到 U 盘，请插入后点击刷新";
        }

        private async Task RefreshPe()
        {
            PeVersions.Clear();
            var r = await _api.GetPeVersionsAsync();
            if (r.IsSuccess && r.Data != null)
            {
                foreach (var p in r.Data)
                    PeVersions.Add(p);
            }
            else
            {
                StatusText = "PE 列表获取失败: " + r.Message;
            }
            OnPropertyChanged(nameof(HasNoPeVersions));
        }

        private void BrowseIsoOutput()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "ISO 镜像 (*.iso)|*.iso",
                Title = "选择 ISO 输出路径",
                FileName = "ZS_PE.iso"
            };
            if (dlg.ShowDialog() == true)
                IsoOutputPath = dlg.FileName;
        }

        private void BrowseInstallImage()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "系统镜像 (*.wim;*.esd)|*.wim;*.esd|WIM 镜像 (*.wim)|*.wim|ESD 镜像 (*.esd)|*.esd",
                Title = "选择装机镜像（写入 U盘/ISO，PE 无网可无人值守装机）"
            };
            if (dlg.ShowDialog() == true)
                OfflineImagePath = dlg.FileName;
        }

        private void BrowseUnattend()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "无人值守应答文件 (*.xml)|*.xml",
                Title = "选择无人值守应答文件（可选，不选将使用默认模板）"
            };
            if (dlg.ShowDialog() == true)
                OfflineUnattendPath = dlg.FileName;
        }

        private void BrowseFirstLogon()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "脚本文件 (*.cmd;*.bat)|*.cmd;*.bat",
                Title = "选择首次登录脚本（可选）"
            };
            if (dlg.ShowDialog() == true)
                OfflineFirstLogonPath = dlg.FileName;
        }

        private void BuildSummary()
        {
            SummaryItems.Clear();
            if (IsIsoMode)
            {
                SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCDA", Title = "输出方式", Content = "生成 ISO 镜像" });
                SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCE6", Title = "PE 源", Content = PeDisplayText });
                SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCC1", Title = "输出路径", Content = IsoOutputPath });
                SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDDA5\uFE0F", Title = "附加", Content = (IncludeOfflineImage ? "写入装机镜像+无人值守" : "仅 PE 系统") });
                if (IncludeOfflineImage) SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCE6", Title = "装机镜像", Content = Path.GetFileName(OfflineImagePath) + (string.IsNullOrEmpty(OfflineUnattendPath) ? " · 默认无人值守" : " · 自定义无人值守") });
                return;
            }
            SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCBE", Title = "目标盘", Content = SelectedUDisk?.Model + "  " + SelectedUDisk?.CapacityText + "（磁盘 " + SelectedUDisk?.Index + "）" });
            SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCE6", Title = "PE 源", Content = PeDisplayText });
            SummaryItems.Add(new UdiskSummaryItem { Icon = "\u2699\uFE0F", Title = "文件系统", Content = SelectedFileSystem?.Name + " · " + SelectedBootType?.Name });
            SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83C\uDFF7\uFE0F", Title = "卷标", Content = VolumeLabel });
            SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDDA5\uFE0F", Title = "附加", Content = (IncludeOfflineImage ? "写入装机镜像+无人值守" : "仅 PE 系统") });
            if (IncludeOfflineImage) SummaryItems.Add(new UdiskSummaryItem { Icon = "\uD83D\uDCE6", Title = "装机镜像", Content = Path.GetFileName(OfflineImagePath) + (string.IsNullOrEmpty(OfflineUnattendPath) ? " · 默认无人值守" : " · 自定义无人值守") });
        }

        /// <summary>PE 源显示文本（确认页复用）</summary>
        private string PeDisplayText => SelectedPeVersion?.DisplayText ?? "";

        private async Task StartExecution()
        {
            if (IsIsoMode) { await StartIsoBuild(); return; }
            await StartUdiskWrite();
        }

        /// <summary>生成 ISO 镜像（无需管理员、无需选中 U 盘、无写盘锁）</summary>
        private async Task StartIsoBuild()
        {
            if (string.IsNullOrEmpty(IsoOutputPath)) return;
            _cts = new CancellationTokenSource();

            IsExecuting = true;
            IsFinished = false;
            ResultKind = "";
            Logs.Clear();
            ExecSteps.Clear();
            AddExecStep("下载/准备 PE 文件");
            ProgressValue = 0;
            Log("开始生成 ISO 镜像...");

            try
            {
                // ① 准备固定 PE 文件（服务器托管定制 PE）
                var peFile = "";
                if (SelectedPeVersion == null) { Fail("未选择 PE 版本", "请返回上一步选择 PE 版本"); return; }
                if (string.IsNullOrEmpty(SelectedPeVersion.DownloadUrl))
                {
                    UpdateExecStep(new UdiskExecStep { Name = "下载/准备 PE 文件", Status = "failed", Detail = "服务器未托管文件" });
                    Fail("PE 不可下载", "服务器未托管该 PE 文件，请先在后台上传");
                    return;
                }
                var cacheDir = Path.Combine(Path.GetTempPath(), "ZS_Cache", "pe");
                Log("下载 PE: " + SelectedPeVersion.Name + " ...");
                var dl = await _udisk.DownloadPeAsync(SelectedPeVersion, SelectedPeVersion.DownloadUrl, cacheDir,
                    new Progress<int>(p => ProgressValue = p / 2), _cts.Token);
                if (_cts.IsCancellationRequested) { FinishCanceled(); return; }
                if (!dl.Ok) { UpdateExecStep(new UdiskExecStep { Name = "下载/准备 PE 文件", Status = "failed", Detail = dl.Error }); Fail("PE 下载失败", dl.Error); return; }
                UpdateExecStep(new UdiskExecStep { Name = "下载/准备 PE 文件", Status = "completed", Detail = "下载完成" });
                peFile = dl.Path;
                ProgressValue = 5;

                // ② 校验 PE 源存在
                if (string.IsNullOrEmpty(peFile) || !File.Exists(peFile))
                {
                    UpdateExecStep(new UdiskExecStep { Name = "下载/准备 PE 文件", Status = "failed", Detail = "PE 文件不存在" });
                    Fail("PE 文件无效", "所选 PE 文件不存在或下载失败");
                    return;
                }

                // ③ 构建 ISO
                var plan = new IsoBuildPlan
                {
                    PeFilePath = peFile,
                    OutputPath = IsoOutputPath,
                    IsoLabel = SanitizeLabel(VolumeLabel),
                    IncludeOfflineImage = IncludeOfflineImage,
                    OfflineImagePath = OfflineImagePath,
                    OfflineUnattendPath = OfflineUnattendPath,
                    OfflineFirstLogonPath = OfflineFirstLogonPath,
                    OfflineAdminPassword = OfflineAdminPassword,
                };
                AddExecStep("解包 PE 并准备引导");
                if (IncludeOfflineImage) AddExecStep("写入装机镜像+无人值守任务");
                AddExecStep("构建 ISO 镜像");
                var build = await _udisk.BuildIsoAsync(plan, Log,
                    new Progress<int>(p => ProgressValue = 5 + p * 90 / 100), _cts.Token);
                if (_cts.IsCancellationRequested) { FinishCanceled(); return; }
                if (!build.Ok)
                {
                    UpdateExecStep(new UdiskExecStep { Name = "构建 ISO 镜像", Status = "failed", Detail = build.Error });
                    Fail("ISO 生成失败", build.Error);
                    return;
                }
                UpdateExecStep(new UdiskExecStep { Name = "解包 PE 并准备引导", Status = "completed", Detail = "完成" });
                if (IncludeOfflineImage) UpdateExecStep(new UdiskExecStep { Name = "写入装机镜像+无人值守任务", Status = "completed", Detail = "已写入 ZS_Images + zs_task.json" });
                UpdateExecStep(new UdiskExecStep { Name = "构建 ISO 镜像", Status = "completed", Detail = "生成完成" });

                ResultKind = "success";
                IsFinished = true;
                ProgressValue = 100;
                StatusText = "ISO 镜像生成完成";
            }
            catch (OperationCanceledException)
            {
                FinishCanceled();
            }
            catch (Exception ex)
            {
                Fail("生成异常", ex.Message);
            }
            finally
            {
                IsExecuting = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>写入 U 盘（管理员权限预检 + 全局写锁 + 四步执行）</summary>
        private async Task StartUdiskWrite()
        {
            if (SelectedUDisk == null) return;
            _cts = new CancellationTokenSource();
            var errors = _udisk.ValidateTarget(SelectedUDisk);
            if (errors.Count > 0) { FailReason = string.Join("；", errors); StatusText = FailReason; return; }

            // 管理员权限预检（分区/格式化/挂载ISO/写引导均需管理员）
            if (!UDiskService.IsAdministrator())
            {
                var choice = MessageBox.Show("制作 U 盘需要管理员权限（分区、格式化、写引导）。是否以管理员身份重新启动？",
                    "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (choice == MessageBoxResult.Yes) RestartElevated();
                return;
            }

            // 全局写锁：防止多个实例同时对 U 盘写盘
            using var writeLock = new Mutex(true, @"Local\ZS_UDiskWrite", out var createdNew);
            if (!createdNew)
            {
                StatusText = "另一个 U 盘制作任务正在进行，请稍后再试";
                _cts.Cancel(); _cts.Dispose(); _cts = null;
                return;
            }

            IsExecuting = true;
            IsFinished = false;
            ResultKind = "";
            Logs.Clear();
            ExecSteps.Clear();
            AddExecStep("下载 PE 文件");
            ProgressValue = 0;
            Log("开始制作 U 盘，目标磁盘 " + SelectedUDisk.Index + " ...");

            try
            {
                // 固定服务器定制 PE：始终从服务器下载所选 PE 版本
                var peFile = "";
                if (SelectedPeVersion == null)
                {
                    UpdateExecStep(new UdiskExecStep { Name = "下载 PE 文件", Status = "failed", Detail = "未选择 PE 版本" });
                    Fail("PE 不可下载", "请返回上一步选择 PE 版本");
                    return;
                }
                if (string.IsNullOrEmpty(SelectedPeVersion.DownloadUrl))
                {
                    UpdateExecStep(new UdiskExecStep { Name = "下载 PE 文件", Status = "failed", Detail = "服务器未托管文件" });
                    Fail("PE 不可下载", "服务器未托管该 PE 文件，请先在后台上传");
                    return;
                }
                var cacheDir = Path.Combine(Path.GetTempPath(), "ZS_Cache", "pe");
                Log("下载 PE: " + SelectedPeVersion.Name + " ...");
                var dl = await _udisk.DownloadPeAsync(SelectedPeVersion, SelectedPeVersion.DownloadUrl, cacheDir,
                    new Progress<int>(p => ProgressValue = p / 2), _cts.Token);
                if (_cts.IsCancellationRequested) { FinishCanceled(); return; }
                if (!dl.Ok)
                {
                    UpdateExecStep(new UdiskExecStep { Name = "下载 PE 文件", Status = "failed", Detail = dl.Error });
                    Fail("PE 下载失败", dl.Error);
                    return;
                }
                UpdateExecStep(new UdiskExecStep { Name = "下载 PE 文件", Status = "completed", Detail = "下载完成" });
                peFile = dl.Path;

                var plan = new WritePlan
                {
                    DiskIndex = SelectedUDisk.Index,
                    DiskDisplay = SelectedUDisk.Model + " " + SelectedUDisk.CapacityText,
                    FileSystem = SelectedFileSystem?.Key ?? "exFAT",
                    BootType = SelectedBootType?.Key ?? "both",
                    PartitionScheme = SelectedBootType?.Key == "legacy" ? "single" : (SelectedPartition?.Key ?? "single"),
                    VolumeLabel = SanitizeLabel(VolumeLabel),
                    PeSource = "server",
                    PeVersion = SelectedPeVersion,
                    PeFilePath = peFile,
                    PeDisplay = Path.GetFileName(peFile),
                    IncludeOfflineImage = IncludeOfflineImage,
                    OfflineImagePath = OfflineImagePath,
                    OfflineUnattendPath = OfflineUnattendPath,
                    OfflineFirstLogonPath = OfflineFirstLogonPath,
                    OfflineAdminPassword = OfflineAdminPassword,
                };

                var stepCb = new Action<UdiskExecStep>(s => UpdateExecStep(s));
                // 写盘进度 0-100 → 总进度 50-100（下载阶段占 0-50）
                var result = await _udisk.WriteAsync(plan, peFile, _clientDir, stepCb, Log, _cts.Token,
                    new Progress<int>(p => ProgressValue = 50 + p / 2));

                if (_cts.IsCancellationRequested) { FinishCanceled(); return; }
                if (result.Ok)
                {
                    ResultKind = "success";
                    IsFinished = true;
                    ProgressValue = 100;
                    StatusText = "U盘制作完成";
                }
                else
                {
                    Fail("制作失败", result.Error);
                }
            }
            catch (OperationCanceledException)
            {
                FinishCanceled();
            }
            catch (Exception ex)
            {
                Fail("制作异常", ex.Message);
            }
            finally
            {
                IsExecuting = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void CancelExecution()
        {
            try { _cts?.Cancel(); } catch { }
            Log("正在取消...");
        }

        /// <summary>失败结果（红色 ❌ + 失败原因 + 返回重试）</summary>
        private void Fail(string title, string reason)
        {
            ResultKind = "failed";
            FailReason = title + ": " + reason;
            StatusText = title;
            IsFinished = true;
            Log(title + ": " + reason);
        }

        /// <summary>取消结果（独立状态，允许返回重试）</summary>
        private void FinishCanceled()
        {
            ResultKind = "canceled";
            StatusText = "已取消制作";
            FailReason = "制作已取消";
            IsFinished = true;
            Log("制作已取消");
        }

        /// <summary>失败/取消后返回执行页，可调整后重新制作</summary>
        private void RetryExecution()
        {
            IsExecuting = false;
            IsFinished = false;
            ResultKind = "";
            FailReason = "";
            StatusText = "";
            CurrentStep = 3;
        }

        /// <summary>以管理员身份重启本程序（用户确认后）</summary>
        private static void RestartElevated()
        {
            try
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(exe)) return;
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true, Verb = "runas" });
                Application.Current.Shutdown();
            }
            catch
            {
                MessageBox.Show("未获得管理员权限，U盘制作无法进行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>卷标消毒：去除非法字符并按 FAT/exFAT 上限 11 字符截断</summary>
        private static string SanitizeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return "ZS_PE";
            label = label.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                label = label.Replace(c, '_');
            if (label.Length > 11) label = label.Substring(0, 11);
            return string.IsNullOrWhiteSpace(label) ? "ZS_PE" : label;
        }

        private void AddExecStep(string name)
            => ExecSteps.Add(new UdiskExecStep { Name = name });

        private void UpdateExecStep(UdiskExecStep step)
        {
            var exist = ExecSteps.FirstOrDefault(s => s.Name == step.Name);
            if (exist != null)
            {
                exist.Status = step.Status;
                exist.Detail = step.Detail;
            }
            else
            {
                ExecSteps.Add(step);
            }
        }

        private void Log(string text)
        {
            Logs.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss"), text));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>单选项（文件系统/引导/分区）</summary>
    public class RadioOption
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Sub { get; set; } = "";
        public override string ToString() => Name;
    }
}