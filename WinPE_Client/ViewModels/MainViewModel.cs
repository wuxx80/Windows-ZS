using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly DiskPartService _diskPart;

        public MainViewModel()
        {
            _api = new ApiService();
            _device = new DeviceService();
            _deploy = new ImageDeployService();
            _diskPart = new DiskPartService();

            _deploy.ProgressChanged += (p, m) =>
            {
                ProgressValue = p;
                StatusMessage = m;
            };
            _diskPart.ProgressChanged += (p, m) =>
            {
                ProgressValue = p;
                StatusMessage = m;
            };

            NavigateCommand = new RelayCommand<string>(Navigate);
            StartInstallCommand = new RelayCommand(async () => await StartInstall(), () => CanStartInstall);
            RefreshDisksCommand = new RelayCommand(async () => await RefreshDisks());
            RefreshImagesCommand = new RelayCommand(async () => await RefreshImages());
            BootRepairCommand = new RelayCommand(async () => await BootRepair());
            InjectDriversCommand = new RelayCommand(async () => await InjectDrivers());
            ConnectCommand = new RelayCommand(async () => await Connect(), () => !IsConnected);
        }

        private string _serverUrl = "http://localhost";
        public string ServerUrl
        {
            get => _serverUrl;
            set { _serverUrl = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotConnected)); }
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
            set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartInstall)); }
        }

        public bool CanStartInstall => !IsInstalling && IsConnected && SelectedImage != null && SelectedDisk != null;

        private string _currentView = "Home";
        public string CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        private ImageInfo? _selectedImage;
        public ImageInfo? SelectedImage
        {
            get => _selectedImage;
            set { _selectedImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartInstall)); }
        }

        private DiskInfo? _selectedDisk;
        public DiskInfo? SelectedDisk
        {
            get => _selectedDisk;
            set { _selectedDisk = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartInstall)); }
        }

        private bool _autoPartition = true;
        public bool AutoPartition
        {
            get => _autoPartition;
            set { _autoPartition = value; OnPropertyChanged(); }
        }

        private bool _autoRepairBoot = true;
        public bool AutoRepairBoot
        {
            get => _autoRepairBoot;
            set { _autoRepairBoot = value; OnPropertyChanged(); }
        }

        private bool _autoInjectDrivers;
        public bool AutoInjectDrivers
        {
            get => _autoInjectDrivers;
            set { _autoInjectDrivers = value; OnPropertyChanged(); }
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

        public ICommand NavigateCommand { get; }
        public ICommand StartInstallCommand { get; }
        public ICommand RefreshDisksCommand { get; }
        public ICommand RefreshImagesCommand { get; }
        public ICommand BootRepairCommand { get; }
        public ICommand InjectDriversCommand { get; }
        public ICommand ConnectCommand { get; }

        public async Task Connect()
        {
            _api.SetBaseUrl(ServerUrl);
            var result = await _api.LoginAsync("admin", "admin123");
            if (result.IsSuccess)
            {
                var token = result.Data?.ToString();
                _api.SetToken(token);
                IsConnected = true;
                StatusMessage = "已连接到服务器";
                await RefreshImages();
                await RefreshDisks();
            }
            else
            {
                StatusMessage = "连接失败: " + result.Message;
            }
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

        private async Task StartInstall()
        {
            if (SelectedImage == null || SelectedDisk == null) return;

            IsInstalling = true;
            StatusMessage = "开始装机...";
            ProgressValue = 0;

            try
            {
                if (AutoPartition)
                {
                    StatusMessage = "正在分区...";
                    var op = new PartitionOperation
                    {
                        Operation = "create",
                        DiskIndex = SelectedDisk.Index,
                        FileSystem = "NTFS",
                        DriveLetter = "C",
                        Label = "Windows"
                    };
                    var partResult = await _diskPart.ExecutePartitionOperation(op);
                    if (!partResult)
                    {
                        StatusMessage = "分区失败，中止装机";
                        IsInstalling = false;
                        return;
                    }
                }

                var deployResult = await _deploy.DeployWimImage(
                    SelectedImage.FilePath, 1, "C:", false);

                if (!deployResult)
                {
                    StatusMessage = "镜像部署失败";
                    IsInstalling = false;
                    return;
                }

                if (AutoRepairBoot)
                {
                    await _deploy.RepairBoot("C:");
                }

                if (AutoInjectDrivers && !string.IsNullOrEmpty(DriverPath))
                {
                    await _deploy.InjectDrivers("C:", DriverPath);
                }

                ProgressValue = 100;
                StatusMessage = "装机完成！";
            }
            catch (Exception ex)
            {
                StatusMessage = "装机失败: " + ex.Message;
            }
            finally
            {
                IsInstalling = false;
            }
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