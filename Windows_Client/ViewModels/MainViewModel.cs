using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api = new();

        // 客户端注册信息（登录成功后填充）
        private string _clientId = "";
        private int _serverClientId;

        // 当前装机任务（创建后保存，用于进度上报）
        private int _taskId;

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand<string>(Navigate);
            LoginCommand = new RelayCommand(async () => await Login(), () => !IsLoggedIn);
            LogoutCommand = new RelayCommand(async () => await Logout(), () => IsLoggedIn);
            RefreshImagesCommand = new RelayCommand(async () => await RefreshImages());
            RefreshSoftwareCommand = new RelayCommand(async () => await RefreshSoftware());
            StartInstallCommand = new RelayCommand(async () => await StartInstall(), () => CanStartInstall);
        }

        private string _serverUrl = "http://localhost";
        public string ServerUrl
        {
            get => _serverUrl;
            set { _serverUrl = value; OnPropertyChanged(); }
        }

        private string _username = "admin";
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoggedIn)); }
        }

        public bool IsNotLoggedIn => !IsLoggedIn;

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

        // 一键装机
        private ImageInfo? _selectedImage;
        public ImageInfo? SelectedImage
        {
            get => _selectedImage;
            set { _selectedImage = value; OnPropertyChanged(); }
        }

        private DiskInfo? _selectedDisk;
        public DiskInfo? SelectedDisk
        {
            get => _selectedDisk;
            set { _selectedDisk = value; OnPropertyChanged(); }
        }

        private bool _autoPartition = true;
        public bool AutoPartition { get => _autoPartition; set { _autoPartition = value; OnPropertyChanged(); } }

        private bool _autoRepairBoot = true;
        public bool AutoRepairBoot { get => _autoRepairBoot; set { _autoRepairBoot = value; OnPropertyChanged(); } }

        private bool _isInstalling;
        public bool IsInstalling
        {
            get => _isInstalling;
            set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartInstall)); }
        }

        public bool CanStartInstall => !IsInstalling && IsLoggedIn && SelectedImage != null;

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ImageInfo> Images { get; } = new();
        public ObservableCollection<DiskInfo> Disks { get; } = new();
        public ObservableCollection<SoftwareInfo> SoftwareList { get; } = new();

        public ICommand NavigateCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand RefreshImagesCommand { get; }
        public ICommand RefreshSoftwareCommand { get; }
        public ICommand StartInstallCommand { get; }

        private async Task Login()
        {
            _api.SetBaseUrl(ServerUrl);
            var result = await _api.LoginAsync(Username, "admin123");
            if (result.IsSuccess)
            {
                var token = result.Data?.ToString();
                _api.SetToken(token);
                IsLoggedIn = true;
                StatusMessage = "登录成功 - 已连接到服务器";
                await RegisterClient();
                await RefreshImages();
            }
            else
            {
                StatusMessage = "登录失败: " + result.Message;
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

        /// <summary>一键装机：创建装机任务并通过 API 上报进度</summary>
        private async Task StartInstall()
        {
            if (SelectedImage == null) return;

            IsInstalling = true;
            StatusMessage = "开始创建装机任务...";
            ProgressValue = 0;
            _taskId = 0;

            try
            {
                // 创建装机任务（服务端记录，客户端后续上报进度）
                var taskResult = await _api.CreateTaskAsync(
                    imageId: SelectedImage.Id,
                    clientId: _serverClientId > 0 ? _serverClientId : (int?)null,
                    targetDiskIndex: 0,
                    targetPartition: "C:",
                    partitionScheme: AutoPartition ? "auto" : "keep",
                    optionsJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        auto_partition = AutoPartition,
                        auto_repair_boot = AutoRepairBoot,
                        image_index = 1
                    }));
                if (!taskResult.IsSuccess || taskResult.Data == null)
                {
                    StatusMessage = "创建任务失败: " + taskResult.Message;
                    IsInstalling = false;
                    return;
                }
                _taskId = taskResult.Data.Id;

                // 上报任务已创建（保持 running 状态，等待重启进入 WinPE 后由 PE 端继续执行装机）
                await ReportProgress(5, "任务已创建，等待进入 WinPE 执行装机", "创建任务", "running");

                ProgressValue = 5;
                StatusMessage = "装机任务已创建：任务编号 " + taskResult.Data.TaskNo + "，请在 PE 环境继续执行";
            }
            catch (Exception ex)
            {
                StatusMessage = "创建任务异常: " + ex.Message;
                await ReportProgress(0, "创建任务异常: " + ex.Message, "异常", "failed");
            }
            finally
            {
                IsInstalling = false;
            }
        }

        /// <summary>上报任务进度（任务未创建成功时静默跳过）</summary>
        private async Task ReportProgress(int progress, string? message, string? stepName, string? status)
        {
            if (_taskId <= 0) return;
            try
            {
                await _api.ReportProgressAsync(_taskId, progress, message, stepName, status);
            }
            catch
            {
                // 进度上报失败不影响主流程
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
            IsLoggedIn = false;
            _api.SetToken(null);
            StatusMessage = "已退出登录";
            Images.Clear();
        }

        private async Task RefreshImages()
        {
            var result = await _api.GetImagesAsync();
            if (result.IsSuccess && result.Data != null)
            {
                Images.Clear();
                foreach (var img in result.Data.List)
                    Images.Add(img);
                StatusMessage = "已加载 " + Images.Count + " 个镜像";
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