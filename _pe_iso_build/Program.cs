using System;
using System.IO;
using System.Threading.Tasks;
using Windows_Client.Services;

// 定制 PE ISO 构建器：
//   1) 由 staging\EFI\BOOT\bootx64.efi 生成 efisys.bin（FAT12 引导镜像，供 UEFI 引导）
//   2) 用 IsoBuilder 打包为 ISO9660 + Joliet + El Torito 可引导 ISO
// 用法: PeIsoBuild <stagingDir> <outIsoPath>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // verify|dump 子命令：独立 ISO9660 结构校验（由 IsoVerify 处理）
        if (args.Length > 0 && (args[0] == "verify" || args[0] == "dump"))
            return IsoVerify.Main(args);

        var staging = args.Length > 0 ? args[0] : @"D:\Users\Desktop\Windows-ZS\_pe_build\iso_staging";
        var output = args.Length > 1 ? args[1] : @"D:\Users\Desktop\Windows-ZS\publish\ZS_PE_fixed.iso";
        var noJoliet = args.Any(a => a == "--no-joliet");

        try
        {
            // ① UEFI 引导镜像：bootx64.efi 存在且缺 efisys.bin 时自动生成 FAT12 兜底镜像
            var bootx64 = Path.Combine(staging, "EFI", "BOOT", "bootx64.efi");
            var efisys = Path.Combine(staging, "EFI", "BOOT", "efisys.bin");
            if (File.Exists(bootx64) && !File.Exists(efisys))
            {
                Console.WriteLine("生成 efisys.bin（由 bootx64.efi 构造 FAT12 引导镜像）...");
                if (!EfiSysGenerator.TryCreate(bootx64, efisys, m => Console.WriteLine("  " + m)))
                {
                    Console.WriteLine("错误: EFI 引导镜像生成失败");
                    return 1;
                }
            }
            else if (File.Exists(efisys))
            {
                Console.WriteLine("已存在 efisys.bin，跳过生成");
            }
            else
            {
                Console.WriteLine("警告: 未找到 bootx64.efi/efisys.bin，将生成无 UEFI 引导 ISO");
            }

            // ② Legacy BIOS 引导镜像：etfsboot.com（Windows 安装介质标准引导文件）
            var etfsboot = Path.Combine(staging, "Boot", "etfsboot.com");

            // ③ IsoBuilder 打包（UEFI + Legacy 双引导）
            Console.WriteLine("构建 ISO（ISO9660 + Joliet + El Torito 双引导）...");
            var req = new IsoBuilder.BuildRequest
            {
                OutputPath = output,
                Label = "ZS_PE",
                SourceDir = staging,
                EfiBootRel = File.Exists(efisys) ? "/EFI/BOOT/efisys.bin" : null,
                LegacyBootRel = File.Exists(etfsboot) ? "/Boot/etfsboot.com" : null,
                NoJoliet = noJoliet,
            };
            await IsoBuilder.BuildAsync(req,
                new Progress<double>(d => Console.Write($"\r  进度 {d,5:0.0}%")), default);
            Console.WriteLine("\nISO 构建完成: " + output);

            var fi = new FileInfo(output);
            Console.WriteLine("大小: " + fi.Length + " 字节");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("构建失败: " + ex.Message);
            return 1;
        }
    }
}
