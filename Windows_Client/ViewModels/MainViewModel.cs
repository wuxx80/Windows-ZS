using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api = new();

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand<string>(Navigate);
            LoginCommand = new RelayCommand(async () => await Login(), () => !IsLoggedIn);
            LogoutCommand = new RelayCommand(async () => await Logout(), () => IsLoggedIn);
            RefreshImagesCommand = new RelayCommand(async () => await RefreshImages());
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

        public ObservableCollection<ImageInfo> Images { get; } = new();
        public ObservableCollection<DiskInfo> Disks { get; } = new();
        public ObservableCollection<SoftwareInfo> SoftwareList { get; } = new();

        public ICommand NavigateCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand RefreshImagesCommand { get; }
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
                await RefreshImages();
            }
            else
            {
                StatusMessage = "登录失败: " + result.Message;
            }
        }

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