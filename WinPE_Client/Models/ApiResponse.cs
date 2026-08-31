using System.Text.Json.Serialization;

namespace WinPE_Client.Models
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

    public class ApiResponse : ApiResponse<object> { }

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

    /// <summary>登录接口返回：data 含 token 与用户信息（SetToken 只能取 token，不能取整个对象）</summary>
    public class LoginResult
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
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

        [JsonPropertyName("image_id")]
        public int ImageId { get; set; }

        [JsonPropertyName("target_disk_index")]
        public int TargetDiskIndex { get; set; }

        [JsonPropertyName("target_partition")]
        public string TargetPartition { get; set; } = "C:";

        [JsonPropertyName("partition_scheme")]
        public string PartitionScheme { get; set; } = "auto";

        [JsonPropertyName("options")]
        public string Options { get; set; } = "";

        [JsonPropertyName("unattend_template_id")]
        public int? UnattendTemplateId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>任务无人值守应答 XML（GET /tasks/:id/unattend）</summary>
    public class UnattendResult
    {
        [JsonPropertyName("task_id")]
        public int TaskId { get; set; }

        [JsonPropertyName("template_id")]
        public int? TemplateId { get; set; }

        [JsonPropertyName("template_name")]
        public string TemplateName { get; set; } = "";

        [JsonPropertyName("xml")]
        public string Xml { get; set; } = "";
    }

    /// <summary>首次登录脚本（GET /tasks/:id/firstLogon，服务端生成 SetupComplete.cmd）</summary>
    public class FirstLogonResult
    {
        [JsonPropertyName("task_id")]
        public int TaskId { get; set; }

        [JsonPropertyName("task_no")]
        public string TaskNo { get; set; } = "";

        [JsonPropertyName("cmd")]
        public string Cmd { get; set; } = "";
    }

    /// <summary>客户端心跳返回（POST /clients/heartbeat）</summary>
    public class HeartbeatResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("waiting_task_count")]
        public int WaitingTaskCount { get; set; }
    }

    /// <summary>站点品牌信息（GET /api/v1/site/info 公开接口，无需登录）。
    /// 用于客户端首页左上角品牌 + 边框项（版权/版本/联系/关于），由后台系统设置「站点信息」维护。</summary>
    public class SiteInfo
    {
        [JsonPropertyName("site_logo_text")]
        public string LogoText { get; set; } = "ZS";

        [JsonPropertyName("site_title")]
        public string Title { get; set; } = "装机助手 · PE";

        [JsonPropertyName("site_subtitle")]
        public string Subtitle { get; set; } = "ZS Install Assistant | www.zs-install.com";

        [JsonPropertyName("site_tagline")]
        public string Tagline { get; set; } = "简单 · 高效 · 一站式系统维护";

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