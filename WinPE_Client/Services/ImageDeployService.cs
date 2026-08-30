using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WinPE_Client.Services
{
    public class ImageDeployService
    {
        public event Action<int, string>? ProgressChanged;

        public async Task<bool> DeployWimImage(string wimPath, int imageIndex, string targetDrive, bool formatPartition)
        {
            if (!File.Exists(wimPath))
            {
                ProgressChanged?.Invoke(0, "Error: WIM file not found");
                return false;
            }

            try
            {
                if (formatPartition)
                {
                    ProgressChanged?.Invoke(5, "Formatting partition " + targetDrive + "...");
                    var fmtResult = await RunCommandAsync("format", targetDrive + " /FS:NTFS /Q /Y");
                    if (!fmtResult)
                    {
                        ProgressChanged?.Invoke(0, "Error: Failed to format partition");
                        return false;
                    }
                }

                ProgressChanged?.Invoke(10, "Applying WIM image (index " + imageIndex + ")...");
                var dismResult = await RunDismCommandAsync(
                    "Apply-Image", "/ImageFile:" + wimPath, "/Index:" + imageIndex,
                    "/ApplyDir:" + targetDrive + "\\");

                if (!dismResult)
                {
                    ProgressChanged?.Invoke(0, "Error: Failed to apply image");
                    return false;
                }

                ProgressChanged?.Invoke(90, "Image applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                ProgressChanged?.Invoke(0, "Error: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> RepairBoot(string systemDrive, string? firmwareType = null)
        {
            ProgressChanged?.Invoke(0, "Repairing boot...");
            try
            {
                if (string.IsNullOrEmpty(firmwareType))
                {
                    firmwareType = Environment.GetFolderPath(Environment.SpecialFolder.System).Contains("EFI")
                        ? "EFI" : "BIOS";
                }

                if (firmwareType == "EFI")
                {
                    ProgressChanged?.Invoke(30, "Running BCDBoot for EFI...");
                    await RunCommandAsync("bcdboot", systemDrive + "\\Windows /s S: /f UEFI");
                    ProgressChanged?.Invoke(60, "EFI boot files created");
                }
                else
                {
                    ProgressChanged?.Invoke(30, "Running bootrec for BIOS...");
                    await RunCommandAsync("bootrec", "/FixMbr");
                    await RunCommandAsync("bootrec", "/FixBoot");
                    await RunCommandAsync("bootrec", "/RebuildBcd");
                    ProgressChanged?.Invoke(60, "BIOS boot repaired");
                }

                ProgressChanged?.Invoke(100, "Boot repair completed");
                return true;
            }
            catch (Exception ex)
            {
                ProgressChanged?.Invoke(0, "Error: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> InjectDrivers(string wimMountPath, string driverSourcePath)
        {
            if (!Directory.Exists(driverSourcePath))
            {
                ProgressChanged?.Invoke(0, "Error: Driver source not found");
                return false;
            }

            try
            {
                ProgressChanged?.Invoke(10, "Injecting drivers...");
                var result = await RunDismCommandAsync(
                    "Add-Driver", "/Image:" + wimMountPath,
                    "/Driver:" + driverSourcePath, "/Recurse");

                ProgressChanged?.Invoke(100, "Drivers injected successfully");
                return result;
            }
            catch (Exception ex)
            {
                ProgressChanged?.Invoke(0, "Error: " + ex.Message);
                return false;
            }
        }

        private async Task<bool> RunDismCommandAsync(string command, params string[] args)
        {
            var argStr = string.Join(" ", args);
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/" + command + " " + argStr,
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

        private async Task<bool> RunCommandAsync(string command, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
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