using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api = new();
        private readonly DispatcherTimer _heartbeatTimer;

        // 客户端注册信息（登录成功后填充）
        private string _clientId = "";
        private int _serverClientId;

        public MainViewModel()
        {
            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _heartbeatTimer.Tick += async (_, _) => await SendHeartbeat();

            NavigateCommand = new RelayCommand<string>(Navigate);
            OpenInstallWizardCommand = new RelayCommand(OpenInstallWizard);
            OpenUDiskCommand = new RelayCommand(OpenUDisk);
            OpenToolsCommand = new RelayCommand(OpenTools);
            OpenSoftwareCommand = new RelayCommand(OpenSoftware);
            LoginCommand = new RelayCommand(async () => await Login(), () => !IsLoggedIn);
            LogoutCommand = new RelayCommand(async () => await Logout(), () => IsLoggedIn);
            RefreshSoftwareCommand = new RelayCommand(async () => await RefreshSoftware());
            RefreshMyTasksCommand = new RelayCommand(async () => await LoadMyTasks());
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            OpenContactCommand = new RelayCommand(OpenContact);
            OpenVersionCommand = new RelayCommand(OpenVersion);
        }

        private string _serverUrl = "http://127.0.0.1:8001";
        public string ServerUrl
        {
            get => _serverUrl;
            set { _serverUrl = value; OnPropertyChanged(); }
        }

        private string _username = "";
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        /// <summary>站点品牌信息（首页左上角 + 边框 版权/版本/联系/关于，来自后台「站点信息」设置）</summary>
        private SiteInfo _site = new();
        public SiteInfo Site
        {
            get => _site;
            set { _site = value; OnPropertyChanged(); }
        }

        /// <summary>登录按钮文本：未连接显示「登录」，已连接显示「退出」（闭环）</summary>
        public string LoginButtonText => IsLoggedIn ? "退出" : "登录";

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoggedIn)); OnPropertyChanged(nameof(LoginButtonText)); OnPropertyChanged(nameof(ServerStatusText)); }
        }

        public bool IsNotLoggedIn => !IsLoggedIn;

        /// <summary>服务器状态文本（顶部状态点 + 连接信息）</summary>
        public string ServerStatusText => IsLoggedIn
            ? (string.IsNullOrEmpty(_clientId) ? "已连接服务器" : "已连接 · " + _clientId)
            : "未连接服务器";

        /// <summary>客户端审核状态提示（后台未审核时设置页黄色提示）</summary>
        private string _clientStatusText = "";
        public string ClientStatusText
        {
            get => _clientStatusText;
            set { _clientStatusText = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "欢迎使用 ZS 装机助手";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _currentView = "Home";
        public string CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // 一键装机六步向导（独立子窗口，见 InstallWizardWindow / InstallWizardViewModel）
        public ObservableCollection<SoftwareInfo> SoftwareList { get; } = new();

        // 最近任务（首页卡片：下单后显示 waiting，提示进 PE 继续）
        public ObservableCollection<TaskInfo> MyTasks { get; } = new();

        private TaskInfo? _recentTask;
        public bool HasRecentTask => _recentTask != null;
        public string RecentTaskNo => _recentTask?.TaskNo ?? "";
        public string RecentTaskStatusText => _recentTask?.Status switch
        {
            "waiting" => "等待 WinPE 执行",
            "running" => "执行中 " + (_recentTask?.Progress ?? 0) + "%",
            "completed" => "已完成",
            "failed" => "失败",
            "cancelled" => "已取消",
            "paused" => "已暂停",
            _ => _recentTask?.Status ?? ""
        };
        public bool ShowRecentContinue => _recentTask?.Status == "waiting";

        public ICommand NavigateCommand { get; }
        public ICommand OpenInstallWizardCommand { get; }
        public ICommand OpenUDiskCommand { get; }
        public ICommand OpenToolsCommand { get; }
        public ICommand OpenSoftwareCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand RefreshSoftwareCommand { get; }
        public ICommand RefreshMyTasksCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenAboutCommand { get; }
        public ICommand OpenContactCommand { get; }
        public ICommand OpenVersionCommand { get; }

        /// <summary>窗口加载完成后自动初始化：拉取品牌信息 → 检测本地已保存登录 → 恢复会话或等待用户登录</summary>
        public async Task Initialize()
        {
            await LoadSiteInfo();
            // 不再自动 admin 登录：改为用户注册/登录（用户名密码），登录成功后才连接服务器
            if (!IsLoggedIn && TryRestoreSession())
            {
                await RegisterClient();
                await LoadMyTasks();
                _heartbeatTimer.Start();
                StatusMessage = "已恢复登录会话";
            }
        }

        /// <summary>尝试从本地恢复上次登录会话（保存 token 与用户名，免重复登录）</summary>
        private bool TryRestoreSession()
        {
            var savedToken = SessionStore.LoadToken();
            var savedUsername = SessionStore.LoadUsername();
            if (string.IsNullOrEmpty(savedToken) || string.IsNullOrEmpty(savedUsername))
                return false;
            _api.SetBaseUrl(ServerUrl);
            _api.SetToken(savedToken);
            Username = savedUsername;
            IsLoggedIn = true;
            return true;
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

        /// <summary>打开用户注册/登录对话框：登录/注册成功后建立会话（token + 用户信息），连接服务器</summary>
        private async Task Login()
        {
            _api.SetBaseUrl(ServerUrl);
            var win = new LoginWindow(_api, ServerUrl) { Owner = Application.Current.MainWindow };
            if (win.ShowDialog() == true && win.Result != null)
            {
                var token = win.Result.Token;
                _api.SetToken(token);
                Username = win.Result.User?.Username ?? "";
                SessionStore.Save(token, Username);
                IsLoggedIn = true;
                StatusMessage = "登录成功 - 已连接到服务器";
                await RegisterClient();
                await LoadMyTasks();
                _heartbeatTimer.Start();
            }
            else
            {
                StatusMessage = "已取消登录";
            }
        }

        /// <summary>客户端自注册：登录成功后调用；已有 client_id 时复用（幂等）</summary>
        private async Task RegisterClient()
        {
            try
            {
                var reg = await _api.RegisterClientAsync(
                    GetHostname(), GetMacAddress(), GetOsVersion(),
                    "windows", string.IsNullOrEmpty(_clientId) ? null : _clientId);
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

        private static string GetHostname() => Environment.MachineName;

        private static string GetMacAddress()
        {
            try
            {
                var mac = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && n.OperationalStatus == OperationalStatus.Up)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .FirstOrDefault(m => !string.IsNullOrEmpty(m) && m != "000000000000");
                return mac ?? "00-00-00-00-00-00";
            }
            catch
            {
                return "00-00-00-00-00-00";
            }
        }

        private static string GetOsVersion() => Environment.OSVersion.VersionString;

        private async Task Logout()
        {
            _heartbeatTimer.Stop();
            IsLoggedIn = false;
            _api.SetToken(null);
            SessionStore.Clear();
            StatusMessage = "已退出登录";
        }

        /// <summary>心跳：每 30 秒刷新在线状态（后台据此判定在线/离线）</summary>
        private async Task SendHeartbeat()
        {
            if (!IsLoggedIn || string.IsNullOrEmpty(_clientId)) return;
            try
            {
                var hb = await _api.HeartbeatAsync(
                    _clientId, GetMacAddress(), GetHostname(), GetOsVersion(), "windows");
                if (hb.IsSuccess && hb.Data != null)
                    _serverClientId = hb.Data.Id;
            }
            catch
            {
                // 心跳失败不打断主流程
            }
        }

        private async Task RefreshSoftware()
        {
            var result = await _api.GetSoftwareAsync();
            if (result.IsSuccess && result.Data != null)
            {
                SoftwareList.Clear();
                foreach (var sw in result.Data.List)
                    SoftwareList.Add(sw);
                StatusMessage = "已加载 " + SoftwareList.Count + " 款软件";
            }
        }

        private void Navigate(string? view)
        {
            CurrentView = view ?? "Home";
        }

        /// <summary>加载本机最近任务（首页卡片展示；下单后返回首页自动刷新）</summary>
        private async Task LoadMyTasks()
        {
            if (!IsLoggedIn || _serverClientId <= 0) return;
            try
            {
                var r = await _api.GetMyTasksAsync(_serverClientId, null, 1, 5);
                if (r.IsSuccess && r.Data != null)
                {
                    MyTasks.Clear();
                    foreach (var t in r.Data.List)
                        MyTasks.Add(t);
                    _recentTask = r.Data.List.FirstOrDefault();
                    StatusMessage = _recentTask?.Status switch
                    {
                        "waiting" => "有任务等待 WinPE 执行：" + _recentTask.TaskNo + "，请重启进入 PE 自动继续",
                        "running" => "任务正在执行中：" + _recentTask.TaskNo,
                        _ => StatusMessage
                    };
                }
                else
                {
                    _recentTask = null;
                }
            }
            catch
            {
                _recentTask = null;
            }
            OnPropertyChanged(nameof(HasRecentTask));
            OnPropertyChanged(nameof(RecentTaskNo));
            OnPropertyChanged(nameof(RecentTaskStatusText));
            OnPropertyChanged(nameof(ShowRecentContinue));
        }

        /// <summary>打开一键装机六步向导子窗口（Windows 端仅创建任务，实际装机在 WinPE 执行；需登录）</summary>
        private async void OpenInstallWizard()
        {
            if (!await EnsureLoggedIn()) return;
            var vm = new InstallWizardViewModel(_api, new DeviceService(), ServerUrl);
            var win = new InstallWizardWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
            await LoadMyTasks();
        }

        /// <summary>打开 U盘制作四步向导子窗口（真实写盘，带安全护栏；需登录）</summary>
        private async void OpenUDisk()
        {
            if (!await EnsureLoggedIn()) return;
            var vm = new UDiskViewModel(_api, new UDiskService(), ServerUrl, AppContext.BaseDirectory);
            var win = new UDiskWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        /// <summary>打开工具大全子窗口（53 个维护工具：本地内置 + 服务器同步；需登录）</summary>
        private async void OpenTools()
        {
            if (!await EnsureLoggedIn()) return;
            var vm = new ToolsViewModel(_api, ServerUrl, AppContext.BaseDirectory);
            var win = new ToolsWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        /// <summary>打开绿色软件子窗口（在线软件列表；需登录）</summary>
        private async void OpenSoftware()
        {
            if (!await EnsureLoggedIn()) return;
            var vm = new SoftwareWindowViewModel(_api, ServerUrl);
            var win = new SoftwareWindow { DataContext = vm };
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        /// <summary>核心功能登录门槛：未登录时先弹出注册/登录对话框，登录成功才放行（状态闭环）</summary>
        private async Task<bool> EnsureLoggedIn()
        {
            if (IsLoggedIn) return true;
            StatusMessage = "请先登录后再使用该功能";
            await Login();
            return IsLoggedIn;
        }

        /// <summary>打开设置窗口（服务器地址 / 连接状态 / 版本信息）</summary>
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged
        { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged
        { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}