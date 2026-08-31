using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Windows_Client.Models
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        public bool IsSuccess => Code == 0;
    }

    public class PaginatedData<T>
    {
        [JsonPropertyName("list")]
        public List<T> List { get; set; } = new();
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("page")]
        public int Page { get; set; }
        [JsonPropertyName("limit")]
        public int Limit { get; set; }
        [JsonPropertyName("pages")]
        public int Pages { get; set; }
    }

    public class ImageInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = "";
        [JsonPropertyName("file_hash")]
        public string FileHash { get; set; } = "";
        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("format")]
        public string Format { get; set; } = "";
        [JsonPropertyName("os_type")]
        public string OsType { get; set; } = "";
        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = "";
        [JsonPropertyName("os_architecture")]
        public string OsArchitecture { get; set; } = "";
        [JsonPropertyName("os_language")]
        public string OsLanguage { get; set; } = "";
        [JsonPropertyName("image_count")]
        public int ImageCount { get; set; }
        [JsonPropertyName("version")]
        public int Version { get; set; }
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("tags")]
        public List<TagInfo> Tags { get; set; } = new();

        // ===== 设计文档 §4 卡片展示增强字段（API 未返回时使用默认值）=====
        [JsonPropertyName("install_count")]
        public int InstallCount { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
        [JsonPropertyName("is_recommended")]
        public bool IsRecommended { get; set; }
        [JsonPropertyName("is_new")]
        public bool IsNew { get; set; }

        /// <summary>镜像格式（大写，供筛选匹配）</summary>
        public string FormatUpper => Format.ToUpperInvariant();

        /// <summary>本地缓存状态文本（⬇️ 需下载 / ✅ 已缓存，刷新镜像时由 ViewModel 回填）</summary>
        public string CacheText { get; set; } = "⬇️ 需下载";

        /// <summary>创建日期短文本（取 YYYY-MM-DD）</summary>
        public string DateText => CreatedAt.Length >= 10 ? CreatedAt.Substring(0, 10) : CreatedAt;
    }

    public class TagInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("color")]
        public string Color { get; set; } = "";
    }

    public class DiskInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("interface_type")]
        public string InterfaceType { get; set; } = "";
        [JsonPropertyName("is_system")]
        public bool IsSystem { get; set; }
        [JsonPropertyName("is_ssd")]
        public bool IsSsd { get; set; }
        [JsonPropertyName("partitions")]
        public List<PartitionInfo> Partitions { get; set; } = new();

        /// <summary>分区表类型判断（设计文档 §5.4 GPT/MBR）</summary>
        public bool HasGpt => Partitions.Any(p => p.Type == "GPT" || p.Type == "EFI");
        public bool HasMbr => Partitions.Count > 0 && !HasGpt;
        public string PartitionTable => HasGpt ? "GPT" : (Partitions.Count > 0 ? "MBR" : "");
    }

    public class PartitionInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        [JsonPropertyName("drive_letter")]
        public string DriveLetter { get; set; } = "";
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("used_size")]
        public long UsedSize { get; set; }
        [JsonPropertyName("free_size")]
        public long FreeSize { get; set; }
        [JsonPropertyName("file_system")]
        public string FileSystem { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("is_system")]
        public bool IsSystem { get; set; }
        [JsonPropertyName("is_boot")]
        public bool IsBoot { get; set; }
        [JsonPropertyName("is_esp")]
        public bool IsEsp { get; set; }
    }

    /// <summary>分区操作参数（设计文档 §5.5 分区编辑器）</summary>
    public class PartitionOperation
    {
        public string Operation { get; set; } = ""; // create, delete, format, extend
        public int DiskIndex { get; set; }
        public int? PartitionIndex { get; set; }
        public long? Size { get; set; }
        public string? FileSystem { get; set; } // NTFS, FAT32, exFAT
        public string? DriveLetter { get; set; }
        public string? Label { get; set; }
        public bool? IsEsp { get; set; }
        public bool? IsMsr { get; set; }
    }

    public class SoftwareInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = "";
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
        [JsonPropertyName("size_display")]
        public string SizeDisplay { get; set; } = "";
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    /// <summary>客户端自注册返回：服务端数据库 ID 与客户端唯一编号</summary>
    public class ClientInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = "";
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    /// <summary>登录/注册接口返回：data 含 token 与用户信息（SetToken 只能取 token，不能取整个对象）</summary>
    public class LoginResult
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("user")]
        public UserProfile? User { get; set; }
    }

    /// <summary>登录用户信息（客户端显示用户名/昵称/角色）</summary>
    public class UserProfile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = "";

        [JsonPropertyName("role_code")]
        public string RoleCode { get; set; } = "";

        [JsonPropertyName("is_super")]
        public int IsSuper { get; set; }
    }

    /// <summary>装机任务信息：创建任务 / 进度上报返回</summary>
    public class TaskInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("task_no")]
        public string TaskNo { get; set; } = "";
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("progress")]
        public int Progress { get; set; }
    }

    // ===================== 一键装机六步向导模型（对齐设计文档） =====================

    /// <summary>可观察对象基类</summary>
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>环境检测项（步骤①，逐项实时联动）</summary>
    public class EnvCheckItem : ObservableObject
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";

        /// <summary>严重级别：fatal 致命 / warning 警告（设计文档 §3.5）</summary>
        public string Severity { get; set; } = "warning";

        /// <summary>检测序号显示文本：检测项 1/9</summary>
        public string IndexText => "检测项 " + Index + "/9";

        private string _status = "pending";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(ShowRepair)); OnPropertyChanged(nameof(ShowIgnore)); OnPropertyChanged(nameof(RowBg)); }
        }
        private string _detail = "";
        public string Detail
        {
            get => _detail;
            set { _detail = value; OnPropertyChanged(); }
        }
        public bool CanRepair { get; set; }
        public bool ShowRepair => Status == "fail" && CanRepair;
        public bool ShowSeverityWarning => Severity == "warning";
        public bool ShowSeverityFatal => Severity == "fatal";
        public bool ShowIgnore => Status == "fail" && Severity == "warning";
        public string RowBg => (Index % 2 == 1) ? "#FAFAFA" : "#FFFFFF";

        public string StatusIcon => Status switch
        {
            "detecting" => "\u23F3",
            "success" => "\u2705",
            "fail" => "\u274C",
            "skip" => "\u23ED",
            _ => "\u25CB"
        };
        public string StatusText => Status switch
        {
            "detecting" => "正在检测...",
            "success" => "通过",
            "fail" => "失败",
            "skip" => "跳过",
            _ => "待检测"
        };
    }

    /// <summary>环境异常汇总项（设计文档 §3.5：致命/警告区分）</summary>
    public class EnvIssueItem : ObservableObject
    {
        public int Index { get; set; } = -1;
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Severity { get; set; } = "warning"; // fatal / warning
        public string Icon => Severity == "fatal" ? "\uD83D\uDEAB" : "\u26A0";
        public string SeverityText => Severity == "fatal" ? "致命" : "警告";
        public bool IsFatal => Severity == "fatal";
        public bool ShowIgnore => Severity == "warning";
        public string Bg => Severity == "fatal" ? "#FFF1F0" : "#FFFBE6";
        public string Border => Severity == "fatal" ? "#FFA39E" : "#FFE58F";
    }

    /// <summary>安装选项药丸（步骤④）</summary>
    public class WizardOption : ObservableObject
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Sub { get; set; } = "";
        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set { _isOn = value; OnPropertyChanged(); OnPropertyChanged(nameof(OnText)); OnPropertyChanged(nameof(ToggleText)); OnPropertyChanged(nameof(IsOff)); }
        }
        public bool IsOff => !IsOn;
        public string OnText => IsOn ? "开启" : "关闭";
        public string ToggleText => IsOn ? "\u25CF" : "\u25CB";
    }

    /// <summary>扩展配置模板下拉项（无人值守/软件/驱动/优化/备份）</summary>
    public class TemplateItem
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public override string ToString() => Name;
    }

    /// <summary>执行步骤清单项（步骤⑥）</summary>
    public class ExecStepItem : ObservableObject
    {
        public string Name { get; set; } = "";
        private string _status = "waiting";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
        }
        private string _detail = "";
        public string Detail
        {
            get => _detail;
            set { _detail = value; OnPropertyChanged(); }
        }
        private int _progress;
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }
        public string StatusIcon => Status switch
        {
            "running" => "\u23F3",
            "completed" => "\u2705",
            "failed" => "\u274C",
            "canceled" => "\u23F9",
            "skipped" => "\u26A0",
            "paused" => "\u23F8",
            _ => "\u23F8"
        };
    }

    /// <summary>实时日志行</summary>
    public class LogLine
    {
        public string Time { get; set; } = "";
        public string Text { get; set; } = "";
        public LogLine(string time, string text) { Time = time; Text = text; }
    }

    /// <summary>镜像来源项（设计文档 §4.4）</summary>
    public class ImageSourceItem
    {
        public string Key { get; set; } = "server"; // server/url/cache/custom
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public override string ToString() => Name;
    }

    /// <summary>镜像缓存状态（设计文档 §4.5）</summary>
    public enum ImageCacheState
    {
        NotCached,   // 需下载 ⬇
        Downloading, // 下载中 ⏳
        Cached,      // 已缓存 ✅
        Corrupted,   // 已损坏 ❌
        Expired      // 需更新 ⚠
    }

    /// <summary>冲突检测项（设计文档 §5.4 / §7.2）</summary>
    public class ConflictItem : ObservableObject
    {
        public string Icon { get; set; } = "\u26A0";
        public string Text { get; set; } = "";
        public string Severity { get; set; } = "warning"; // pass/info/warning/fatal
        public bool AutoFix { get; set; }
        public string SeverityText => Severity switch
        {
            "fatal" => "致命",
            "warning" => "警告",
            "info" => "提示",
            _ => ""
        };
        public string Bg => Severity switch
        {
            "fatal" => "#FFF2F0",
            "warning" => "#FFFBE6",
            "info" => "#E6F7FF",
            _ => "#F6FFED"
        };
        public string Border => Severity switch
        {
            "fatal" => "#FFA39E",
            "warning" => "#FFE58F",
            "info" => "#91D5FF",
            _ => "#B7EB8F"
        };
    }

    /// <summary>分区编辑条目（设计文档 §5.5 分区编辑器）</summary>
    public class PartitionEditItem : ObservableObject
    {
        public string DriveLetter { get; set; } = "";
        public string SizeText { get; set; } = "";
        public long Size { get; set; }
        public string FileSystem { get; set; } = "NTFS";
        public string Label { get; set; } = "";
        public string Type { get; set; } = ""; // system/data/esp/msr
        public bool IsSystem => Type == "system";
    }

    /// <summary>步骤导航项（状态: current/completed/pending/error）</summary>
    public class StepNav : ObservableObject
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        private string _state = "pending";
        public string State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(Icon)); OnPropertyChanged(nameof(IsCurrent)); OnPropertyChanged(nameof(IsCompleted)); OnPropertyChanged(nameof(IsError)); }
        }
        public string Icon => State switch
        {
            "completed" => "\u2713",
            "error" => "\u2715",
            "current" => "\u25CF",
            _ => "\u25CB"
        };
        public bool IsCurrent => State == "current";
        public bool IsCompleted => State == "completed";
        public bool IsError => State == "error";
    }

    /// <summary>确认页汇总项</summary>
    public class SummaryItem
    {
        public string Icon { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    /// <summary>站点品牌信息（GET /api/v1/site/info 公开接口，无需登录）。
    /// 用于客户端首页左上角品牌 + 边框项（版权/版本/联系/关于），由后台系统设置「站点信息」维护。</summary>
    public class SiteInfo
    {
        [JsonPropertyName("site_logo_text")]
        public string LogoText { get; set; } = "ZS";

        [JsonPropertyName("site_title")]
        public string Title { get; set; } = "装机助手";

        [JsonPropertyName("site_subtitle")]
        public string Subtitle { get; set; } = "ZS Install Assistant | www.zs-install.com";

        [JsonPropertyName("site_tagline")]
        public string Tagline { get; set; } = "— 简单 · 高效 · 一站式系统维护 —";

        [JsonPropertyName("site_website")]
        public string Website { get; set; } = "www.zs-install.com";

        [JsonPropertyName("site_copyright")]
        public string Copyright { get; set; } = "© 2026 ZS 装机助手 版权所有";

        [JsonPropertyName("site_version")]
        public string Version { get; set; } = "v0.0.268311";

        [JsonPropertyName("site_contact")]
        public string Contact { get; set; } = "";

        [JsonPropertyName("site_about")]
        public string About { get; set; } = "";
    }
}