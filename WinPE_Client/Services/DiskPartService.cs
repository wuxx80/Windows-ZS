using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    public class DiskPartService
    {
        public event Action<int, string>? ProgressChanged;

        public async Task<bool> ExecutePartitionOperation(PartitionOperation operation)
        {
            var script = BuildDiskPartScript(operation);
            if (string.IsNullOrEmpty(script))
            {
                ProgressChanged?.Invoke(0, "Error: Invalid operation");
                return false;
            }

            try
            {
                var tempScript = Path.Combine(Path.GetTempPath(), "diskpart_" + Guid.NewGuid() + ".txt");
                await File.WriteAllTextAsync(tempScript, script);

                ProgressChanged?.Invoke(30, "Executing DiskPart...");
                var result = await RunDiskPartAsync(tempScript);

                try { File.Delete(tempScript); } catch { }

                if (result)
                    ProgressChanged?.Invoke(100, "Operation completed");
                else
                    ProgressChanged?.Invoke(0, "Error: DiskPart operation failed");

                return result;
            }
            catch (Exception ex)
            {
                ProgressChanged?.Invoke(0, "Error: " + ex.Message);
                return false;
            }
        }

        private string BuildDiskPartScript(PartitionOperation op)
        {
            var script = "select disk " + op.DiskIndex + Environment.NewLine;

            switch (op.Operation.ToLower())
            {
                case "create":
                    script += "clean" + Environment.NewLine;
                    script += "convert gpt" + Environment.NewLine;
                    script += "create partition efi size=100" + Environment.NewLine;
                    script += "format quick fs=fat32 label=\"System\"" + Environment.NewLine;
                    script += "assign letter=S" + Environment.NewLine;
                    script += "create partition msr size=16" + Environment.NewLine;
                    if (op.Size.HasValue && op.Size.Value > 0)
                        script += "create partition primary size=" + (op.Size.Value / 1024 / 1024) + Environment.NewLine;
                    else
                        script += "create partition primary" + Environment.NewLine;
                    script += "format quick fs=" + (op.FileSystem ?? "NTFS") + " label=\"" + (op.Label ?? "Windows") + "\"" + Environment.NewLine;
                    if (!string.IsNullOrEmpty(op.DriveLetter))
                        script += "assign letter=" + op.DriveLetter + Environment.NewLine;
                    break;

                case "delete":
                    if (op.PartitionIndex.HasValue)
                    {
                        script += "select partition " + op.PartitionIndex.Value + Environment.NewLine;
                        script += "delete partition override" + Environment.NewLine;
                    }
                    break;

                case "format":
                    if (op.PartitionIndex.HasValue)
                    {
                        script += "select partition " + op.PartitionIndex.Value + Environment.NewLine;
                        script += "format quick fs=" + (op.FileSystem ?? "NTFS") + " label=\"" + (op.Label ?? "") + "\"" + Environment.NewLine;
                    }
                    break;

                default:
                    return "";
            }

            return script;
        }

        /// <summary>
        /// 执行自定义 diskpart 脚本字符串（R7 §6.1 GPT/MBR 分支脚本生成）。
        /// 调用方按 FirmwareDetector 判定结果生成对应脚本字符串，本方法负责落盘 + 调起 diskpart + 返回 ExitCode。
        /// </summary>
        public async Task<(bool Success, string Output)> ExecuteRawScriptAsync(string scriptContent)
        {
            if (string.IsNullOrEmpty(scriptContent))
            {
                ProgressChanged?.Invoke(0, "Error: empty diskpart script");
                return (false, "");
            }

            var tempScript = Path.Combine(Path.GetTempPath(), "diskpart_raw_" + Guid.NewGuid() + ".txt");
            try
            {
                await File.WriteAllTextAsync(tempScript, scriptContent);
                ProgressChanged?.Invoke(30, "Executing DiskPart (raw)...");
                var psi = new ProcessStartInfo
                {
                    FileName = "diskpart.exe",
                    Arguments = "/s \"" + tempScript + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var process = Process.Start(psi);
                if (process == null)
                {
                    ProgressChanged?.Invoke(0, "Error: failed to start diskpart");
                    return (false, "");
                }
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                var ok = process.ExitCode == 0;
                ProgressChanged?.Invoke(ok ? 100 : 0, ok ? "DiskPart raw completed" : "DiskPart raw failed");
                return (ok, output);
            }
            catch (Exception ex)
            {
                ProgressChanged?.Invoke(0, "Error: " + ex.Message);
                return (false, "");
            }
            finally
            {
                try { File.Delete(tempScript); } catch { }
            }
        }

        private async Task<bool> RunDiskPartAsync(string scriptPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = "/s \"" + scriptPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
    }
}