using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Input;
using WinPE_Client.Models;
using WinPE_Client.Services;

namespace WinPE_Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api;
        private readonly DeviceService _device;
        private readonly ImageDeployService _deploy;
        private readonly DispatcherTimer _heartbeatTimer;

        // 客户端注册信息（连接服务器后填充）
        private string _clientId = "";
        private int _serverClientId;

        // 本机待执行任务（首页「待执行任务」卡片 + 立即继续）
        private TaskInfo? _pendingTask;

        public MainViewModel()
        {
            _api = new ApiService();
            _device = new DeviceService();
            _deploy = new ImageDeployService();

            _deploy.ProgressChanged += (p, m) =>
            {
                ProgressValue = p;
                StatusMessage = m;
            };

            // 心跳定时器：每 30 秒上报一次，刷新在线状态并感知本机待执行任务（WinPE 续装闭环）
            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _heartbeatTimer.Tick += async (_, _) => await SendHeartbeat();

            NavigateCommand = new RelayCommand<string>(Navigate);
            OpenInstallWizardCommand = new RelayCommand(async () => await OpenInstallWizard());
            OpenUDiskCommand = new RelayCommand(OpenUDisk);
            OpenToolsCommand = new RelayCommand(OpenTools);
            ContinueTaskCommand = new RelayCommand(async () => await OpenInstallWizardContinuation());
            RefreshDisksCommand = new RelayCommand(async () => await RefreshDisks());
            RefreshImagesCommand = new RelayCommand(async () => await RefreshImages());
            BootRepairCommand = new RelayCommand(async () => await BootRepair());
            InjectDriversCommand = new RelayCommand(async () => await InjectDrivers());
            ConnectCommand = new RelayCommand(async () => await Connect(), () => !IsConnected);
            LoginCommand = new RelayCommand(async () => await Login(), () => !IsConnected);
            LogoutCommand = new RelayCommand(Logout, () => IsConnected);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            OpenContactCommand = new RelayCommand(OpenContact);
            OpenVersionCommand = new RelayCommand(OpenVersion);
            NotifyNotImplementedCommand = new RelayCommand<string>(NotifyNotImplemented);
        }

        private string _serverUrl = "http://127.0.0.1:8001";
        public string ServerUrl
        {
            get => _serverUrl;
            set { _serverUrl = value; OnPropertyChanged(); }
        }

        /// <summary>站点品牌信息（首页左上角 + 边框 版权/版本/联系/关于，来自后台「站点信息」设置）</summary>
        private SiteInfo _site = new();
        public SiteInfo Site
        {
            get => _site;
            set { _site = value; OnPropertyChanged(); }
        }

        /// <summary>登录按钮文本：未连接显示「登录」，已连接显示「退出」（闭环）</summary>
        public string LoginButtonText => IsConnected ? "退出" : "登录";

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotConnected)); OnPropertyChanged(nameof(ServerStatusText)); OnPropertyChanged(nameof(LoginButtonText)); }
        }

        public bool IsNotConnected => !IsConnected;

        private string _statusMessage = "等待连接服务器...";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            set { _isInstalling = value; OnPropertyChanged(); }
        }

        private string _currentView = "Home";
        public string CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        private string _driverPath = "";
        public string DriverPath
        {
            get => _driverPath;
            set { _driverPath = value; OnPropertyChanged(); }
        }

        private string _searchKeyword = "";
        public string SearchKeyword
        {
            get => _searchKeyword;
            set { _searchKeyword = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ImageInfo> Images { get; } = new();
        public ObservableCollection<DiskInfo> Disks { get; } = new();

        // ============ 首页待执行任务（Windows 下单 → PE 续装 闭环）============
        /// <summary>是否存在待执行任务（首页显示「待执行任务」卡片）</summary>
        public bool HasPendingTask => _pendingTask != null;
        public string PendingTaskNo => _pendingTask?.TaskNo ?? "";
        public string PendingTaskStatus => _pendingTask?.Status ?? "";
        /// <summary>待执行任务对应的镜像名（从已加载镜像列表中反查）</summary>
        public string PendingTaskImageName
        {
            get
            {
                if (_pendingTask == null || _pendingTask.ImageId <= 0) return "";
                var img = Images.FirstOrDefault(i => i.Id == _pendingTask.ImageId);
                return img?.Name ?? ("镜像 #" + _pendingTask.ImageId);
            }
        }

        /// <summary>客户端审核状态提示（后台未审核时首页黄色提示）</summary>
        private string _clientStatusText = "";
        public string ClientStatusText
        {
            get => _clientStatusText;
            set { _clientStatusText = value; OnPropertyChanged(); }
        }

        /// <summary>服务器状态文本（顶部状态点 + 连接信息）</summary>
        public string ServerStatusText => IsConnected
            ? (string.IsNullOrEmpty(_clientId) ? "已连接服务器" : "已连接 · " + _clientId)
            : "未连接服务器";

        public ICommand NavigateCommand { get; }
        public ICommand OpenInstallWizardCommand { get; }
        public ICommand OpenUDiskCommand { get; }
        public ICommand OpenToolsCommand { get; }
        public ICommand ContinueTaskCommand { get; }
        public ICommand RefreshDisksCommand { get; }
        public ICommand RefreshImagesCommand { get; }
        public ICommand BootRepairCommand { get; }
        public ICommand InjectDriversCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenAboutCommand { get; }
        public ICommand OpenContactCommand { get; }
        public ICommand OpenVersionCommand { get; }
        public ICommand NotifyNotImplementedCommand { get; }

        /// <summary>窗口加载完成后自动初始化：拉取品牌信息 → 连接服务器 → 自注册 → 检测待执行任务 → 启动心跳</summary>
        public async Task Initialize()
        {
            await LoadSiteInfo();
            if (IsConnected) return;
            await Connect();
            if (IsConnected)
            {
                _heartbeatTimer.Start();
                await CheckWaitingTasks();
            }
        }

        /// <summary>拉取站点品牌信息（公开接口，无需登录；失败时保留默认品牌，首页依然可用）</summary>
        public async Task LoadSiteInfo()
        {
            try
            {
                _api.SetBaseUrl(ServerUrl);
                var r = await _api.GetSiteInfoAsync();
                if (r.IsSuccess && r.Data != null)
                {
                    Site = r.Data;
                }
            }
            catch
            {
                // 服务器不可用：使用默认品牌信息
            }
        }

        /// <summary>登录：连接服务器（WinPE 环境自动以管理员身份登录）</summary>
        private async Task Login()
        {
            await Connect();
        }

        /// <summary>退出登录：断开连接、停止心跳、清空令牌与本地状态（状态闭环）</summary>
        private void Logout()
        {
            _heartbeatTimer.Stop();
            _api.SetToken(null);
            _clientId = "";
            _serverClientId = 0;
            _pendingTask = null;
            IsConnected = false;
            StatusMessage = "已退出连接";
            OnPropertyChanged(nameof(HasPendingTask));
            OnPropertyChanged(nameof(PendingTaskNo));
            OnPropertyChanged(nameof(PendingTaskStatus));
            OnPropertyChanged(nameof(PendingTaskImageName));
        }

        /// <summary>打开设置窗口（服务器地址 / 连接状态 / WinPE 高级工具）</summary>
        private void OpenSettings()
        {
            var win = new SettingsWindow { DataContext = this, Owner = Application.Current.MainWindow };
            win.ShowDialog();
        }

        /// <summary>打开关于窗口（品牌 + 版本 + 关于内容，来自后台「站点信息」设置）</summary>
        private void OpenAbout()
        {
            var win = new AboutWindow { DataContext = this, Owner = Application.Current.MainWindow };
            win.ShowDialog();
        }

        /// <summary>打开联系窗口（联系信息，来自后台「站点信息」设置）</summary>
        private void OpenContact()
        {
            var win = new ContactWindow { DataContext = this, Owner = Application.Current.MainWindow };
            win.ShowDialog();
        }

        /// <summary>打开版本窗口（客户端版本 + 站点版本信息）</summary>
        private void OpenVersion()
        {
            var win = new VersionWindow { DataContext = this, Owner = Application.Current.MainWindow };
            win.ShowDialog();
        }

        public async Task Connect()
        {
            _api.SetBaseUrl(ServerUrl);
            var result = await _api.LoginAsync("admin", "admin123");
            if (result.IsSuccess)
            {
                var token = result.Data?.Token;
                _api.SetToken(token);
                IsConnected = true;
                StatusMessage = "已连接到服务器";
                await RegisterClient();
                await RefreshImages();
                await RefreshDisks();
            }
            else
            {
                StatusMessage = "连接失败: " + result.Message;
            }
        }

        /// <summary>客户端自注册：连接服务器后调用；已有 client_id 时复用（幂等）</summary>
        private async Task RegisterClient()
        {
            try
            {
                var reg = await _api.RegisterClientAsync(
                    _device.GetHostname(), _device.GetMacAddress(), _device.GetOsVersion(),
                    "winpe", string.IsNullOrEmpty(_clientId) ? null : _clientId);
                if (reg.IsSuccess && reg.Data != null)
                {
                    _clientId = reg.Data.ClientId;
                    _serverClientId = reg.Data.Id;
                    ClientStatusText = "";
                    StatusMessage = "已注册客户端: " + _clientId;
                }
                else
                {
                    StatusMessage = "客户端注册失败: " + reg.Message;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "客户端注册异常: " + ex.Message;
            }
        }

        /// <summary>心跳：每 30 秒刷新在线状态，并感知本机待执行任务（后台据此判定在线/离线）</summary>
        private async Task SendHeartbeat()
        {
            if (!IsConnected || string.IsNullOrEmpty(_clientId)) return;
            try
            {
                var hb = await _api.HeartbeatAsync(
                    _clientId, _device.GetMacAddress(), _device.GetHostname(),
                    _device.GetOsVersion(), "winpe");
                if (hb.IsSuccess && hb.Data != null)
                {
                    _serverClientId = hb.Data.Id;
                    ClientStatusText = "";
                    if (hb.Data.WaitingTaskCount > 0) await CheckWaitingTasks();
                }
                else if (hb.Message?.Contains("未注册") == true)
                {
                    // 服务端无记录（如库被重置）→ 重新注册
                    await RegisterClient();
                }
            }
            catch
            {
                // 心跳失败不打断主流程
            }
        }

        /// <summary>检测本机待执行任务（Windows 下单 → PE 续装 的关键闭环）</summary>
        public async Task CheckWaitingTasks()
        {
            if (_serverClientId <= 0) return;
            try
            {
                var r = await _api.GetMyTasksAsync(_serverClientId, "waiting", 1, 5);
                if (r.IsSuccess && r.Data != null && r.Data.List.Count > 0)
                {
                    _pendingTask = r.Data.List[0];
                    StatusMessage = "检测到待执行任务：" + _pendingTask.TaskNo + "，可立即继续装机";
                }
                else
                {
                    _pendingTask = null;
                }
            }
            catch
            {
                _pendingTask = null;
            }
            OnPropertyChanged(nameof(HasPendingTask));
            OnPropertyChanged(nameof(PendingTaskNo));
            OnPropertyChanged(nameof(PendingTaskStatus));
            OnPropertyChanged(nameof(PendingTaskImageName));
        }

        public async Task RefreshImages()
        {
            var result = await _api.GetImagesAsync(1, 100, SearchKeyword);
            if (result.IsSuccess && result.Data != null)
            {
                Images.Clear();
                foreach (var img in result.Data.List)
                    Images.Add(img);
                StatusMessage = "已加载 " + Images.Count + " 个镜像";
            }
        }

        public async Task RefreshDisks()
        {
            var disks = await Task.Run(() => _device.GetDiskInfo());
            Disks.Clear();
            foreach (var disk in disks)
                Disks.Add(disk);
            StatusMessage = "已检测到 " + Disks.Count + " 个磁盘";
        }

        private async Task BootRepair()
        {
            IsInstalling = true;
            try
            {
                await _deploy.RepairBoot("C:");
            }
            finally
            {
                IsInstalling = false;
            }
        }

        private async Task InjectDrivers()
        {
            IsInstalling = true;
            try
            {
                await _deploy.InjectDrivers("C:", DriverPath);
            }
            finally
            {
                IsInstalling = false;
            }
        }

        private void Navigate(string? view)
        {
            CurrentView = view ?? "Home";
        }

        /// <summary>打开一键装机六步向导子窗口（新配置模式）</summary>
        private async Task OpenInstallWizard()
        {
            if (!IsConnected) await Connect();
            var vm = new InstallWizardViewModel(_api, _device, ServerUrl);
            var win = new InstallWizardWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
            await CheckWaitingTasks();
        }

        /// <summary>打开一键装机向导（续装模式）：预填本机待执行任务，跳过配置直接确认执行</summary>
        private async Task OpenInstallWizardContinuation()
        {
            if (!IsConnected) await Connect();
            if (_pendingTask == null) await CheckWaitingTasks();
            if (_pendingTask == null)
            {
                StatusMessage = "没有检测到待执行任务";
                return;
            }
            var vm = new InstallWizardViewModel(_api, _device, ServerUrl);
            await vm.LoadContinuationTask(_pendingTask);
            var win = new InstallWizardWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
            await CheckWaitingTasks();
        }

        /// <summary>打开 U盘制作四步向导子窗口（真实写盘，带安全护栏）</summary>
        private void OpenUDisk()
        {
            var vm = new UDiskViewModel(_api, new UDiskService(), ServerUrl, AppContext.BaseDirectory);
            var win = new UDiskWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        /// <summary>打开工具大全子窗口（53 个维护工具：本地内置 + 服务器同步）</summary>
        private void OpenTools()
        {
            var vm = new ToolsViewModel(_api, ServerUrl, AppContext.BaseDirectory);
            var win = new ToolsWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        /// <summary>占位入口：工具大全 / 绿色软件（设计文档规划中，未实现）</summary>
        private void NotifyNotImplemented(string? feature)
        {
            StatusMessage = "【" + (feature ?? "该功能") + "】正在开发中，敬请期待";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
