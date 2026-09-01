using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    /// <summary>
    /// task.ini 明文解析器（对应设计文档 §2.1 16 字段）。
    /// 支持注释（; 开头）、section（[xxx]）、key=value、value 末尾 ; 注释。
    /// [software] section 内 sw1_*/sw2_*/... 动态编号字段会聚合为 SoftwareEntry 列表。
    /// </summary>
    public static class TaskIniParser
    {
        /// <summary>解析 task.ini 文件为 TaskIni 对象；失败抛异常</summary>
        public static TaskIni Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("task.ini not found", filePath);

            // task.ini 设计要求 UTF-8；兼容 BOM 与无 BOM
            var content = File.ReadAllText(filePath, new UTF8Encoding(false));
            return ParseContent(content);
        }

        /// <summary>解析 ini 文本内容为 TaskIni 对象</summary>
        public static TaskIni ParseContent(string content)
        {
            var ini = new TaskIni();
            var section = "";
            // 软件包动态编号字段缓存：seq → field_dict
            var swBuffer = new Dictionary<int, Dictionary<string, string>>();

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim('\r', '\t', ' ');
                if (line.Length == 0) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;

                // section header
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }

                // key = value（支持 value 后 ; 注释）
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                var val = line.Substring(eq + 1).Trim();
                // strip trailing ; comment
                var semi = val.IndexOf(';');
                if (semi >= 0) val = val.Substring(0, semi).Trim();

                ApplyField(ini, section, key, val, swBuffer);
            }

            // 把软件包编号字段缓存转换为 SoftwareEntry 列表
            foreach (var kv in swBuffer)
            {
                var dict = kv.Value;
                var entry = new SoftwareEntry { Seq = kv.Key };
                if (dict.TryGetValue("key", out var k)) entry.Key = k;
                if (dict.TryGetValue("name", out var n)) entry.Name = n;
                if (dict.TryGetValue("msi", out var m)) entry.Msi = m;
                if (dict.TryGetValue("exe", out var e)) entry.Exe = e;
                if (dict.TryGetValue("args", out var a)) entry.Args = a;
                if (dict.TryGetValue("expect_exit", out var xe) && int.TryParse(xe, out var xi)) entry.ExpectExit = xi;
                ini.Software.Add(entry);
            }
            ini.Software.Sort((a, b) => a.Seq.CompareTo(b.Seq));

            return ini;
        }

        private static void ApplyField(TaskIni ini, string section, string key, string val,
            Dictionary<int, Dictionary<string, string>> swBuffer)
        {
            switch (section)
            {
                case "meta":
                    switch (key)
                    {
                        case "version": if (int.TryParse(val, out var v)) ini.Meta.Version = v; break;
                        case "created_at": ini.Meta.CreatedAt = val; break;
                        case "task_id": ini.Meta.TaskId = val; break;
                        case "server_api": ini.Meta.ServerApi = val; break;
                        case "oobe_mode": ini.Meta.OobeMode = val; break;
                        case "first_boot_cleanup_zjzl": // 兼容旧字段名
                        case "first_boot_cleanup_zs_task":
                            ini.Meta.FirstBootCleanup = ParseYesNo(val, false); break;
                    }
                    break;

                case "target_disk":
                    switch (key)
                    {
                        case "disk_index": if (int.TryParse(val, out var di)) ini.TargetDisk.DiskIndex = di; break;
                        case "partition_mode": ini.TargetDisk.PartitionMode = val; break;
                    }
                    break;

                case "partition_scheme":
                    switch (key)
                    {
                        case "table": ini.PartitionScheme.Table = val; break;
                        case "esp_size_mb": if (int.TryParse(val, out var esp)) ini.PartitionScheme.EspSizeMb = esp; break;
                        case "msr_size_mb": if (int.TryParse(val, out var msr)) ini.PartitionScheme.MsrSizeMb = msr; break;
                        case "recovery_size_mb": if (int.TryParse(val, out var rec)) ini.PartitionScheme.RecoverySizeMb = rec; break;
                        case "system_letter": ini.PartitionScheme.SystemLetter = val; break;
                        case "system_label": ini.PartitionScheme.SystemLabel = val; break;
                        case "format_fs": ini.PartitionScheme.FormatFs = val; break;
                        case "quick_format": ini.PartitionScheme.QuickFormat = ParseYesNo(val, true); break;
                    }
                    break;

                case "system_image":
                    switch (key)
                    {
                        case "file": ini.SystemImage.File = val; break;
                        case "index": if (int.TryParse(val, out var idx)) ini.SystemImage.Index = idx; break;
                        case "name": ini.SystemImage.Name = val; break;
                    }
                    break;

                case "drivers":
                    switch (key)
                    {
                        case "inject": ini.Drivers.Inject = ParseYesNo(val, true); break;
                        case "recurse": ini.Drivers.Recurse = ParseYesNo(val, true); break;
                        case "force_unsigned": ini.Drivers.ForceUnsigned = ParseYesNo(val, true); break;
                    }
                    break;

                case "software":
                    // sw1_key / sw2_args / sw3_msi / ... -> seq=1/2/3, field=key/args/msi
                    if (key.StartsWith("sw") && key.Length > 3 && key[2] == '_')
                    {
                        var numPart = key.Substring(2, key.Length - 3);
                        if (int.TryParse(numPart, out var seq))
                        {
                            var field = key.Substring(key.IndexOf('_') + 1);
                            if (!swBuffer.TryGetValue(seq, out var dict))
                            {
                                dict = new Dictionary<string, string>();
                                swBuffer[seq] = dict;
                            }
                            dict[field] = val;
                        }
                    }
                    break;

                case "optimize":
                    switch (key)
                    {
                        case "hibernation": ini.Optimize.Hibernation = val; break;
                        case "standby_timeout_ac": if (int.TryParse(val, out var sac)) ini.Optimize.StandbyTimeoutAc = sac; break;
                        case "standby_timeout_dc": if (int.TryParse(val, out var sdc)) ini.Optimize.StandbyTimeoutDc = sdc; break;
                        case "pagefile_auto": ini.Optimize.PagefileAuto = ParseYesNo(val, true); break;
                        case "disable_telemetry": ini.Optimize.DisableTelemetry = ParseYesNo(val, true); break;
                        case "remove_cortana": ini.Optimize.RemoveCortana = ParseYesNo(val, true); break;
                    }
                    break;
            }
        }

        /// <summary>yes/no/on/off/1/0/true/false → bool（解析失败返回默认值）</summary>
        public static bool ParseYesNo(string val, bool def)
        {
            if (string.IsNullOrWhiteSpace(val)) return def;
            var v = val.Trim().ToLowerInvariant();
            return v switch
            {
                "yes" or "on" or "1" or "true" => true,
                "no" or "off" or "0" or "false" => false,
                _ => def
            };
        }

        /// <summary>从 task.ini 解析结果 + task.ini 所在目录（即 ZS_Task/ 根目录）解析系统镜像绝对路径</summary>
        public static string? ResolveImagePath(string taskIniPath, TaskIni task)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(task.SystemImage.File)) return null;
                var dir = Path.GetDirectoryName(taskIniPath);
                if (string.IsNullOrEmpty(dir)) return null;
                var abs = Path.Combine(dir, task.SystemImage.File);
                return File.Exists(abs) ? abs : null;
            }
            catch { return null; }
        }

        /// <summary>从 task.ini 所在目录解析 ZS_PE_Agent.exe 绝对路径（PE 端从自身目录回退）</summary>
        public static string ResolveAgentPath(string taskIniPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(taskIniPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var p = Path.Combine(dir, "ZS_PE_Agent.exe");
                    if (File.Exists(p)) return p;
                }
                return AppContext.BaseDirectory;
            }
            catch { return ""; }
        }
    }
}
