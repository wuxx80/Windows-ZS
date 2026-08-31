using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WinPE_Client.Models;
using WinPE_Client.Services;

namespace WinPE_Client.ViewModels
{
    /// <summary>工具大全 ViewModel：分类 Tab / 搜索过滤 / 运行 / 下载 / 打开目录</summary>
    public class ToolsViewModel : INotifyPropertyChanged
    {
        private readonly ToolService _service;
        private readonly ApiService _api;
        private readonly string _serverUrl;

        public ToolsViewModel(ApiService api, string serverUrl, string baseDir)
        {
            _api = api;
            _serverUrl = serverUrl.TrimEnd('/');
            _api.SetBaseUrl(_serverUrl);
            _service = new ToolService(baseDir);
            InitCommands();
            Load();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ==================== 分类 Tab ====================
        public ObservableCollection<ToolCategory> Categories { get; } = new();
        private ToolCategory? _currentCategory;
        public ToolCategory? CurrentCategory
        {
            get => _currentCategory;
            set { _currentCategory = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // ==================== 工具列表 ====================
        public ObservableCollection<ToolInfo> AllTools { get; } = new();
        public ObservableCollection<ToolInfo> FilteredTools { get; } = new();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set { _isSyncing = value; OnPropertyChanged(); OnPropertyChanged(nameof(SyncText)); OnPropertyChanged(nameof(IsNotSyncing)); }
        }
        public string SyncText => IsSyncing ? "同步中..." : "在线同步";
        public bool IsNotSyncing => !IsSyncing;

        public bool HasNoTools => FilteredTools.Count == 0;
        public bool HasTools => FilteredTools.Count > 0;

        // ==================== 命令 ====================
        public ICommand SelectCategoryCommand { get; private set; } = null!;
        public ICommand RunCommand { get; private set; } = null!;
        public ICommand DownloadCommand { get; private set; } = null!;
        public ICommand OpenDirectoryCommand { get; private set; } = null!;
        public ICommand SyncCommand { get; private set; } = null!;
        public ICommand CloseCommand { get; private set; } = null!;

        private void InitCommands()
        {
            SelectCategoryCommand = new RelayCommand<ToolCategory>(c => CurrentCategory = c);
            RunCommand = new RelayCommand<ToolInfo>(RunTool);
            DownloadCommand = new RelayCommand<ToolInfo>(async t => await DownloadTool(t));
            OpenDirectoryCommand = new RelayCommand<ToolInfo>(t =>
            {
                if (t == null) return;
                var err = _service.OpenDirectory(t);
                if (!string.IsNullOrEmpty(err)) StatusText = err;
            });
            SyncCommand = new RelayCommand(async () => await SyncServerTools(), () => !IsSyncing);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        }

        public event System.Action? RequestClose;

        private void Load()
        {
            var cats = _service.LoadCategories();
            Categories.Add(new ToolCategory { Key = "all", Name = "全部", Icon = "📚" });
            foreach (var c in cats)
                Categories.Add(c);
            CurrentCategory = Categories.FirstOrDefault();

            AllTools.Clear();
            foreach (var t in _service.LoadLocalTools())
                AllTools.Add(t);
            ApplyFilter();
            StatusText = "已加载 " + AllTools.Count + " 个工具（本地清单）";
        }

        private void ApplyFilter()
        {
            FilteredTools.Clear();
            var kw = SearchText.Trim();
            foreach (var t in AllTools)
            {
                var catOk = CurrentCategory == null || CurrentCategory.Key == "all" || t.Category == CurrentCategory.Key;
                var kwOk = string.IsNullOrEmpty(kw)
                    || t.Name.Contains(kw, System.StringComparison.OrdinalIgnoreCase)
                    || t.Description.Contains(kw, System.StringComparison.OrdinalIgnoreCase);
                if (catOk && kwOk)
                    FilteredTools.Add(t);
            }
            OnPropertyChanged(nameof(HasNoTools));
            OnPropertyChanged(nameof(HasTools));
        }

        private void RunTool(ToolInfo? tool)
        {
            if (tool == null) return;
            var err = _service.Run(tool);
            StatusText = string.IsNullOrEmpty(err) ? "已启动: " + tool.Name : err;
        }

        private async Task DownloadTool(ToolInfo? tool)
        {
            if (tool == null || tool.Status != "downloadable") return;
            tool.Status = "downloading";
            StatusText = "正在下载: " + tool.Name + " ...";
            var url = tool.DownloadUrl.StartsWith("http") ? tool.DownloadUrl : _serverUrl + tool.DownloadUrl;
            var (ok, err) = await _service.DownloadAsync(tool, url);
            if (ok)
            {
                tool.Status = "downloaded";
                StatusText = "下载完成: " + tool.Name;
            }
            else
            {
                tool.Status = "downloadable";
                StatusText = err;
            }
        }

        /// <summary>在线同步：拉取服务器软件列表并标记为可下载工具（B4 服务器软件对接）</summary>
        private async Task SyncServerTools()
        {
            if (IsSyncing) return;
            IsSyncing = true;
            StatusText = "正在同步服务器软件列表...";
            try
            {
                var r = await _api.GetSoftwareAsync(1, 200);
                if (!r.IsSuccess || r.Data == null)
                {
                    StatusText = "同步失败: " + r.Message;
                    return;
                }
                var known = new System.Collections.Generic.HashSet<string>(AllTools.Select(t => t.Name));
                foreach (var sw in r.Data.List)
                {
                    if (known.Contains(sw.Name)) continue;
                    AllTools.Add(new ToolInfo
                    {
                        Name = sw.Name,
                        Category = MapCategory(sw.CategoryName),
                        Icon = string.IsNullOrEmpty(sw.Icon) ? "📦" : sw.Icon,
                        Description = sw.Description,
                        Source = "server",
                        Status = "downloadable",
                        SizeDisplay = sw.SizeDisplay,
                        DownloadUrl = "/api/v1/software/" + sw.Id + "/download",
                    });
                }
                ApplyFilter();
                StatusText = "同步完成，共 " + AllTools.Count + " 个工具";
            }
            catch (System.Exception ex)
            {
                StatusText = "同步异常: " + ex.Message;
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private static string MapCategory(string? cat)
        {
            return string.IsNullOrEmpty(cat) ? "comprehensive" : cat switch
            {
                "磁盘" => "disk",
                "系统" => "system",
                "网络" => "network",
                "硬件" => "hardware",
                "备份" => "backup",
                "密码" => "password",
                "恢复" => "recovery",
                "引导" => "boot",
                "PE" => "pe",
                _ => "comprehensive"
            };
        }
    }
}