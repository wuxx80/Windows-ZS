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
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
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
            RefreshDisksCommand.Execute(null);
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
        public string NextText => CurrentStep == 2 ? "下一步" : "下一步";

        public ObservableCollection<StepNav> Steps { get; } = new();

        // ==================== 步骤① U盘 ====================
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

        // ==================== 步骤② PE 源 ====================
        public ObservableCollection<PeSourceItem> PeSources { get; } = new()
        {
            new PeSourceItem { Key = "server", Name = "服务器 PE 版本", Desc = "在线拉取" },
            new PeSourceItem { Key = "local", Name = "本地文件", Desc = "选择 .iso/.wim" },
        };
        private PeSourceItem? _selectedPeSource;
        public PeSourceItem? SelectedPeSource
        {
            get => _selectedPeSource;
            set { _selectedPeSource = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsServerSource)); OnPropertyChanged(nameof(IsLocalSource)); }
        }
        public bool IsServerSource => SelectedPeSource?.Key == "server";
        public bool IsLocalSource => SelectedPeSource?.Key == "local";

        public ObservableCollection<PeVersionInfo> PeVersions { get; } = new();
        private PeVersionInfo? _selectedPeVersion;
        public PeVersionInfo? SelectedPeVersion
        {
            get => _selectedPeVersion;
            set { _selectedPeVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeStatusText)); }
        }
        private string _peFilePath = "";
        public string PeFilePath { get => _peFilePath; set { _peFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeStatusText)); } }
        public string PeStatusText
        {
            get
            {
                if (IsLocalSource) return string.IsNullOrEmpty(PeFilePath) ? "请选择 PE 文件" : "已选: " + PeFilePath;
                return SelectedPeVersion == null ? "请选择 PE 版本" : SelectedPeVersion.DisplayText;
            }
        }
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

        private bool _includeClient = true;
        public bool IncludeClient { get => _includeClient; set { _includeClient = value; OnPropertyChanged(); } }

        private bool _applyCustomize;
        public bool ApplyCustomize { get => _applyCustomize; set { _applyCustomize = value; OnPropertyChanged(); } }

        private bool _includeTools;
        public bool IncludeTools { get => _includeTools; set { _includeTools = value; OnPropertyChanged(); } }

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
        public ObservableCollection<SummaryItem> SummaryItems { get; } = new();
        private string _confirmInput = "";
        public string ConfirmInput { get => _confirmInput; set { _confirmInput = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfirmPassed)); } }
        public bool ConfirmPassed => _confirmInput.Trim() == (SelectedUDisk?.Index.ToString() ?? "-1");
        public string ConfirmPrompt => "请输入目标盘号（磁盘 " + SelectedUDisk?.Index + "）以确认:";

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
        public ICommand SelectSourceCommand { get; private set; } = null!;
        public ICommand BrowsePeCommand { get; private set; } = null!;
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
            SelectSourceCommand = new RelayCommand<PeSourceItem>(s => { SelectedPeSource = s; });
            BrowsePeCommand = new RelayCommand(BrowsePe);
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
            Steps.Add(new StepNav { Index = 0, Name = "选择 U 盘" });
            Steps.Add(new StepNav { Index = 1, Name = "选择 PE" });
            Steps.Add(new StepNav { Index = 2, Name = "制作选项" });
            Steps.Add(new StepNav { Index = 3, Name = "确认执行" });
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
            if (CurrentStep == 0 && SelectedUDisk == null) { StatusText = "请先选择 U 盘"; return; }
            if (CurrentStep == 1)
            {
                if (IsLocalSource && string.IsNullOrEmpty(PeFilePath)) { StatusText = "请选择 PE 文件"; return; }
                if (IsServerSource && SelectedPeVersion == null) { StatusText = "请选择 PE 版本"; return; }
            }
            if (CurrentStep == 2) { BuildSummary(); CurrentStep = 3; return; }
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

        private void BrowsePe()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PE 文件 (*.iso;*.wim)|*.iso;*.wim|ISO 镜像 (*.iso)|*.iso|WIM 镜像 (*.wim)|*.wim",
                Title = "选择 PE 文件"
            };
            if (dlg.ShowDialog() == true)
                PeFilePath = dlg.FileName;
        }

        private void BuildSummary()
        {
            SummaryItems.Clear();
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCBE", Title = "目标盘", Content = SelectedUDisk?.Model + "  " + SelectedUDisk?.CapacityText + "（磁盘 " + SelectedUDisk?.Index + "）" });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDCE6", Title = "PE 源", Content = IsLocalSource ? PeFilePath : (SelectedPeVersion?.DisplayText ?? "") });
            SummaryItems.Add(new SummaryItem { Icon = "\u2699\uFE0F", Title = "文件系统", Content = SelectedFileSystem?.Name + " · " + SelectedBootType?.Name });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83C\uDFF7\uFE0F", Title = "卷标", Content = VolumeLabel });
            SummaryItems.Add(new SummaryItem { Icon = "\uD83D\uDDA5\uFE0F", Title = "附加", Content = (IncludeClient ? "写入装机助手客户端" : "不写入客户端") + (ApplyCustomize ? " · 应用 PE 定制" : "") + (IncludeTools ? " · 写入内置工具" : "") });
        }

        private async Task StartExecution()
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
            if (IsServerSource) AddExecStep("下载 PE 文件");
            ProgressValue = 0;
            Log("开始制作 U 盘，目标磁盘 " + SelectedUDisk.Index + " ...");

            try
            {
                // 下载 PE（服务器源且未缓存）
                var peFile = "";
                if (IsServerSource && SelectedPeVersion != null)
                {
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
                }
                else
                {
                    peFile = PeFilePath;
                }

                var plan = new WritePlan
                {
                    DiskIndex = SelectedUDisk.Index,
                    DiskDisplay = SelectedUDisk.Model + " " + SelectedUDisk.CapacityText,
                    FileSystem = SelectedFileSystem?.Key ?? "exFAT",
                    BootType = SelectedBootType?.Key ?? "both",
                    PartitionScheme = SelectedBootType?.Key == "legacy" ? "single" : (SelectedPartition?.Key ?? "single"),
                    VolumeLabel = SanitizeLabel(VolumeLabel),
                    PeSource = IsLocalSource ? "local" : "server",
                    PeVersion = SelectedPeVersion,
                    PeFilePath = peFile,
                    PeDisplay = IsLocalSource ? Path.GetFileName(peFile) : (SelectedPeVersion?.Name ?? ""),
                    IncludeClient = IncludeClient,
                    ApplyCustomize = ApplyCustomize,
                    IncludeTools = IncludeTools,
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

    /// <summary>PE 来源项</summary>
    public class PeSourceItem
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public override string ToString() => Name;
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