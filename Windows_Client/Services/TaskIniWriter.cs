using System.IO;
using System.Text;
using WinPE_Client.Models;

namespace Windows_Client.Services
{
    /// <summary>
    /// task.ini 生成器（对应设计文档 §2.1 / §3.1 步骤 4）。
    /// 由 Windows_Client 在下单阶段把用户在 UI 选的镜像/分区/软件/优化选项
    /// 序列化为明文 ini，供 PE 端 ZS_PE_Agent --auto 模式解析。
    /// 输出 UTF-8 无 BOM（PE 端 TaskIniParser 兼容 BOM 与无 BOM）。
    /// </summary>
    public static class TaskIniWriter
    {
        /// <summary>把 TaskIni 模型写入指定路径（UTF-8 无 BOM）。返回是否成功。</summary>
        public static bool Write(TaskIni task, string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, Build(task), new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        /// <summary>把 TaskIni 模型序列化为 ini 文本（不落盘）</summary>
        public static string Build(TaskIni task)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("; ============================================================");
            sb.AppendLine("; ZS 无人值守装机任务配置（由 Windows_Client 在下单阶段生成）");
            sb.AppendLine("; ============================================================");
            sb.AppendLine();

            // [meta]
            sb.AppendLine("[meta]");
            sb.AppendLine("version=" + task.Meta.Version);
            sb.AppendLine("created_at=" + task.Meta.CreatedAt);
            sb.AppendLine("task_id=" + task.Meta.TaskId);
            sb.AppendLine("server_api=" + task.Meta.ServerApi);
            sb.AppendLine("oobe_mode=" + task.Meta.OobeMode);
            sb.AppendLine("first_boot_cleanup_zs_task=" + YesNo(task.Meta.FirstBootCleanup));
            sb.AppendLine();

            // [target_disk]
            sb.AppendLine("[target_disk]");
            sb.AppendLine("disk_index=" + task.TargetDisk.DiskIndex);
            sb.AppendLine("partition_mode=" + task.TargetDisk.PartitionMode);
            sb.AppendLine();

            // [partition_scheme]
            var ps = task.PartitionScheme;
            sb.AppendLine("[partition_scheme]");
            sb.AppendLine("table=" + ps.Table);
            sb.AppendLine("esp_size_mb=" + ps.EspSizeMb);
            sb.AppendLine("msr_size_mb=" + ps.MsrSizeMb);
            sb.AppendLine("recovery_size_mb=" + ps.RecoverySizeMb);
            sb.AppendLine("system_letter=" + ps.SystemLetter);
            sb.AppendLine("system_label=" + ps.SystemLabel);
            sb.AppendLine("format_fs=" + ps.FormatFs);
            sb.AppendLine("quick_format=" + YesNo(ps.QuickFormat));
            sb.AppendLine();

            // [system_image]
            var si = task.SystemImage;
            sb.AppendLine("[system_image]");
            sb.AppendLine("file=" + si.File);
            sb.AppendLine("index=" + si.Index);
            sb.AppendLine("name=" + si.Name);
            sb.AppendLine();

            // [drivers]
            var dr = task.Drivers;
            sb.AppendLine("[drivers]");
            sb.AppendLine("inject=" + YesNo(dr.Inject));
            sb.AppendLine("recurse=" + YesNo(dr.Recurse));
            sb.AppendLine("force_unsigned=" + YesNo(dr.ForceUnsigned));
            sb.AppendLine();

            // [software]
            sb.AppendLine("[software]");
            sb.AppendLine("count=" + task.Software.Count);
            foreach (var sw in task.Software)
            {
                var p = "sw" + sw.Seq + "_";
                sb.AppendLine(p + "key=" + sw.Key);
                sb.AppendLine(p + "name=" + sw.Name);
                if (!string.IsNullOrEmpty(sw.Msi)) sb.AppendLine(p + "msi=" + sw.Msi);
                if (!string.IsNullOrEmpty(sw.Exe)) sb.AppendLine(p + "exe=" + sw.Exe);
                sb.AppendLine(p + "args=" + sw.Args);
                sb.AppendLine(p + "expect_exit=" + sw.ExpectExit);
            }
            sb.AppendLine();

            // [optimize]
            var op = task.Optimize;
            sb.AppendLine("[optimize]");
            sb.AppendLine("hibernation=" + op.Hibernation);
            sb.AppendLine("standby_timeout_ac=" + op.StandbyTimeoutAc);
            sb.AppendLine("standby_timeout_dc=" + op.StandbyTimeoutDc);
            sb.AppendLine("pagefile_auto=" + YesNo(op.PagefileAuto));
            sb.AppendLine("disable_telemetry=" + YesNo(op.DisableTelemetry));
            sb.AppendLine("remove_cortana=" + YesNo(op.RemoveCortana));

            return sb.ToString();
        }

        private static string YesNo(bool v) => v ? "yes" : "no";
    }
}
