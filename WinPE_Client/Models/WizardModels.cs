using System.ComponentModel;

namespace WinPE_Client.Models
{
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
}