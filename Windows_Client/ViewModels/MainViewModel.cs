using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
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

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand<string>(Navigate);
            OpenInstallWizardCommand = new RelayCommand(OpenInstallWizard);
            LoginCommand = new RelayCommand(async () => await Login(), () => !IsLoggedIn);
            LogoutCommand = new RelayCommand(async () => await Logout(), () => IsLoggedIn);
            RefreshSoftwareCommand = new RelayCommand(async () => await RefreshSoftware());
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

        // 一键装机六步向导（独立子窗口，见 InstallWizardWindow / InstallWizardViewModel）
        public ObservableCollection<SoftwareInfo> SoftwareList { get; } = new();

        public ICommand NavigateCommand { get; }
        public ICommand OpenInstallWizardCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand RefreshSoftwareCommand { get; }

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

        /// <summary>打开一键装机六步向导子窗口（Windows 端仅创建任务，实际装机在 WinPE 执行）</summary>
        private void OpenInstallWizard()
        {
            var vm = new InstallWizardViewModel(_api, new DeviceService(), ServerUrl);
            var win = new InstallWizardWindow { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.Owner = Application.Current.MainWindow;
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