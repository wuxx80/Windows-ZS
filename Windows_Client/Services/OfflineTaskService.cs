using System;
using System.IO;
using System.Text.Json;
using Windows_Client.Models;

namespace Windows_Client.Services
{
    /// <summary>
    /// 离线任务写入服务（方案B）：Windows 下单时预下载镜像到数据盘并注入 zs_task.json，
    /// 供 PE 环境无网络时离线无人值守装机使用。JSON 契约与 WinPE 端 OfflineTaskService 完全一致。
    /// </summary>
    public static class OfflineTaskService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>生成一个离线任务编号</summary>
        public static string NewTaskNo()
            => "ZS-OFFLINE-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

        /// <summary>写入离线任务文件（UTF-8 无 BOM，避免响应头 BOM 干扰 PE 端解析）</summary>
        public static bool Write(string path, OfflineTask task)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(task, JsonOpts);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }
    }
}
