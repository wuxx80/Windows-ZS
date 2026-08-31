using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client.ViewModels
{
    /// <summary>绿色软件子窗口 ViewModel：在线软件列表 / 刷新 / 关闭</summary>
    public class SoftwareWindowViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _api;

        public SoftwareWindowViewModel(ApiService api, string serverUrl)
        {
            _api = api;
            _api.SetBaseUrl(serverUrl.TrimEnd('/'));
            RefreshCommand = new RelayCommand(async () => await Load());
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            _ = Load(); // 构造函数中启动异步加载（fire-and-forget）
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public event System.Action? RequestClose;

        public ObservableCollection<SoftwareInfo> SoftwareList { get; } = new();

        private string _statusText = "正在加载软件列表...";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); OnPropertyChanged(nameof(LoadText)); }
        }
        public bool IsNotLoading => !IsLoading;
        public string LoadText => IsLoading ? "刷新中..." : "刷新列表";

        public bool HasNoSoftware => SoftwareList.Count == 0;
        public bool HasSoftware => SoftwareList.Count > 0;

        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }

        private async Task Load()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusText = "正在加载软件列表...";
            try
            {
                var r = await _api.GetSoftwareAsync(1, 200);
                if (!r.IsSuccess || r.Data == null)
                {
                    StatusText = "加载失败: " + r.Message;
                    return;
                }
                SoftwareList.Clear();
                foreach (var sw in r.Data.List)
                    SoftwareList.Add(sw);
                StatusText = "共 " + SoftwareList.Count + " 款软件";
            }
            catch (System.Exception ex)
            {
                StatusText = "加载异常: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoSoftware));
                OnPropertyChanged(nameof(HasSoftware));
            }
        }
    }
}
