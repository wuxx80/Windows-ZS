using System;
using System.IO;
using System.Text;

namespace Windows_Client.Services
{
    /// <summary>
    /// 生成 Windows UEFI 引导所需的 EFISYS.BIN（FAT12 盘镜像，内含 EFI\BOOT\BOOTX64.EFI）。
    ///
    /// 设计要点（严格符合 FAT12 + El Torito 规范）：
    ///  1) 自适应每簇扇区数（SPC=1,2,4,8...）：真实 PE 的 bootx64.efi 通常 2.5MB 左右，
    ///     若按每簇 1 扇区，数据簇数会超过 FAT12 上限（约 4085 簇），导致簇号 ≥4080 的
    ///     FAT 项编码成保留/EOC 值，文件链断裂、UEFI 读不全 bootx64.efi 而引导失败。
    ///     通过增大 SPC 把数据簇数压回合法范围。
    ///  2) 保留扇区数对齐簇边界：数据区起点 = reserved + 2*FAT + 根目录，须是 SPC 的整数倍。
    ///  3) 引导扇区写入 0x55AA 签名（偏移 510-511），与标准 FAT12 引导盘一致。
    ///  4) 镜像尺寸自适应：从 1.44MB 起，容量不足则放大，最大不超过 FAT12 16 位总扇区上限。
    /// 纯 C# 实现，无第三方依赖。仅当下载/解包的 PE 未自带 efisys.bin 时用于兜底。
    /// </summary>
    public static class EfiSysGenerator
    {
        private const int BYTES_PER_SECTOR = 512;
        private const int HEADS = 2;
        private const int CYLINDERS = 80;
        private const int NUM_FATS = 2;
        private const int ROOT_ENTRIES = 224;
        private const int ROOT_SECTORS = ROOT_ENTRIES * 32 / BYTES_PER_SECTOR;   // 14
        // FAT12 数据簇号上限（0xFF0 起为保留/EOC 标记），留安全余量
        private const int MAX_CLUSTERS = 4080;

        /// <summary>将 bootx64.efi 打成一个合法的 FAT12 盘镜像，写入 outputPath。</summary>
        public static bool TryCreate(string bootx64Efi, string outputPath, Action<string>? log = null)
        {
            try
            {
                if (!File.Exists(bootx64Efi))
                {
                    log?.Invoke("缺少 BOOTX64.EFI，无法生成 efisys.bin");
                    return false;
                }
                long fileSize = new FileInfo(bootx64Efi).Length;
                int fileSectors = (int)((fileSize + BYTES_PER_SECTOR - 1) / BYTES_PER_SECTOR);

                // ---- ① 自适应每簇扇区数：把数据簇数压回 FAT12 合法范围 ----
                int spc = 1;
                int fileClusters;
                while (true)
                {
                    fileClusters = (fileSectors + spc - 1) / spc;
                    if (fileClusters + 2 <= MAX_CLUSTERS) break;   // +2 为 EFI/BOOT 目录簇
                    if (spc >= 128)
                    {
                        log?.Invoke("BOOTX64.EFI 过大，无法装入 FAT12 引导镜像");
                        return false;
                    }
                    spc *= 2;
                }

                // ---- ② 确定镜像尺寸与 FAT 扇区数（数据区起点对齐簇边界） ----
                int reserved = 2;
                int totalSectors = Math.Max(2880,
                    RoundUp((fileClusters + 2) * spc + reserved + ROOT_SECTORS, spc));
                int fatSectors = 0;
                int dataStart = 0;
                while (true)
                {
                    int estClusters = (totalSectors - reserved - ROOT_SECTORS) / spc;
                    fatSectors = Math.Max(1, (int)Math.Ceiling(estClusters * 1.5 / BYTES_PER_SECTOR));
                    // 数据区起点 = reserved + 2*fat + root，必须能被 spc 整除 → 增大 fatSectors 直至满足
                    while ((2 * fatSectors + ROOT_SECTORS) % spc != 0) fatSectors++;

                    dataStart = reserved + 2 * fatSectors + ROOT_SECTORS;
                    int totalClusters = (totalSectors - dataStart) / spc;
                    int need = Math.Max(1, (int)Math.Ceiling(totalClusters * 1.5 / BYTES_PER_SECTOR));
                    if (need <= fatSectors && totalClusters >= fileClusters + 2) break;

                    if (totalSectors >= 65535)
                    {
                        log?.Invoke("BOOTX64.EFI 过大，无法装入 FAT12 引导镜像");
                        return false;
                    }
                    totalSectors += spc * 8;
                }

                // ---- ③ 组装镜像 ----
                var image = new byte[(long)totalSectors * BYTES_PER_SECTOR];
                WriteBootSector(image, spc, totalSectors, fatSectors, reserved);

                var fat = new byte[(long)fatSectors * BYTES_PER_SECTOR];
                fat[0] = 0xF0; fat[1] = 0xFF; fat[2] = 0xFF;   // FAT12 头（media + 前两簇）
                LinkFile(fat, fileClusters);
                for (int copy = 0; copy < NUM_FATS; copy++)
                {
                    int start = (reserved + copy * fatSectors) * BYTES_PER_SECTOR;
                    Buffer.BlockCopy(fat, 0, image, start, fat.Length);
                }

                // 根目录：EFI -> cluster 2
                int rootSector = reserved + NUM_FATS * fatSectors;
                WriteDirEntry(image, rootSector, "EFI", "", 0x10, 2, 0);

                // EFI 目录（cluster2）：BOOT -> cluster3
                WriteDirEntry(image, SectorAt(dataStart, 2, spc), "BOOT", "", 0x10, 3, 0);

                // BOOT 目录（cluster3）：BOOTX64.EFI -> cluster4
                WriteDirEntry(image, SectorAt(dataStart, 3, spc), "BOOTX64", "EFI", 0x20, 4, fileSize);

                // 文件内容写入 cluster4..
                long fileSector = dataStart + (4 - 2) * (long)spc;
                using (var fs = File.OpenRead(bootx64Efi))
                {
                    fs.Read(image, (int)(fileSector * BYTES_PER_SECTOR), (int)fileSize);
                }

                var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                File.WriteAllBytes(outputPath, image);
                log?.Invoke($"已生成 efisys.bin（FAT12，SPC={spc}，{fileClusters + 2} 簇 / {totalSectors} 扇区，{totalSectors * BYTES_PER_SECTOR / 1024} KB）");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("生成 efisys.bin 失败: " + ex.Message);
                return false;
            }
        }

        // ==================== 底层写入 ====================

        /// <summary>簇 N 所在扇区号（数据区起点 + 相对簇偏移 × 每簇扇区数）。</summary>
        private static int SectorAt(int dataStart, int cluster, int spc)
        {
            return dataStart + (cluster - 2) * spc;
        }

        private static int RoundUp(int v, int m)
        {
            return (v + m - 1) / m * m;
        }

        private static void WriteBootSector(byte[] image, int spc, int totalSectors, int fatSectors, int reserved)
        {
            byte[] jmp = { 0xEB, 0x3C, 0x90 };
            Buffer.BlockCopy(jmp, 0, image, 0, 3);
            CopyAscii(image, 3, "MSWIN4.1", 8);
            image[0x0B] = (BYTES_PER_SECTOR & 0xFF); image[0x0C] = (BYTES_PER_SECTOR >> 8); // bytes/sector=512
            image[0x0D] = (byte)spc;                // sectors/cluster（自适应）
            image[0x0E] = (byte)(reserved & 0xFF); image[0x0F] = (byte)((reserved >> 8) & 0xFF); // reserved
            image[0x10] = NUM_FATS;                 // num FATs
            Write16(image, 0x11, ROOT_ENTRIES);     // 224
            Write16(image, 0x13, totalSectors);     // 总扇区（16 位，FAT12 上限内）
            image[0x15] = 0xF0;                     // media
            Write16(image, 0x16, fatSectors);       // FAT 扇区数
            int sectorsPerTrack = totalSectors / (HEADS * CYLINDERS);
            if (sectorsPerTrack < 1) sectorsPerTrack = 1;
            Write16(image, 0x18, sectorsPerTrack);  // 每磁道扇区
            Write16(image, 0x1A, HEADS);            // 磁头数
            Write32(image, 0x1C, 0);                // hidden sectors
            Write32(image, 0x20, 0);                // total sectors32
            image[0x24] = 0x00;                     // drive number
            image[0x25] = 0x00;                     // reserved
            image[0x26] = 0x29;                     // extended boot signature
            Write32(image, 0x27, 0x20240101);       // volume id
            CopyAscii(image, 0x2B, "ZS_EFISYS   ", 11);
            CopyAscii(image, 0x36, "FAT12   ", 8);
            // 引导签名（FAT12 引导盘标准）
            image[510] = 0x55;
            image[511] = 0xAA;
        }

        /// <summary>将文件簇（4..4+fileClusters-1）串成链表；EFI/BOOT 目录簇标记为 EOC。</summary>
        private static void LinkFile(byte[] fat, int fileClusters)
        {
            SetFat(fat, 2, 0xFFF); // EFI 目录
            SetFat(fat, 3, 0xFFF); // BOOT 目录
            for (int i = 0; i < fileClusters; i++)
                SetFat(fat, 4 + i, i == fileClusters - 1 ? 0xFFF : 4 + i + 1);
        }

        /// <summary>按 FAT12 标准写入簇项：偶簇占 [base, base+1]，奇簇占 [base+1, base+2]（base=(cluster/2)*3）。</summary>
        private static void SetFat(byte[] fat, int cluster, int value)
        {
            int baseOffset = (cluster / 2) * 3;
            if ((cluster & 1) == 0)
            {
                // 偶簇：低位字节在 base，高位 4bit 在 base+1 的低半字节
                fat[baseOffset] = (byte)(value & 0xFF);
                fat[baseOffset + 1] = (byte)((fat[baseOffset + 1] & 0xF0) | ((value >> 8) & 0x0F));
            }
            else
            {
                // 奇簇：低位 4bit 在 base+1 的高半字节，高位字节在 base+2
                fat[baseOffset + 1] = (byte)(((value & 0x0F) << 4) | (fat[baseOffset + 1] & 0x0F));
                fat[baseOffset + 2] = (byte)((value >> 4) & 0xFF);
            }
        }

        private static void WriteDirEntry(byte[] image, int sector, string name8, string ext3, byte attr, int cluster, long fileSize)
        {
            var entry = new byte[32];
            CopyAscii(entry, 0, name8, 8);
            CopyAscii(entry, 8, ext3, 3);
            entry[11] = attr;
            int date = 22561; // 2024-01-01
            Write16(entry, 0x0E, 0x0000); // create time
            Write16(entry, 0x10, date);   // create date
            Write16(entry, 0x12, 0x0000); // last access
            Write16(entry, 0x14, 0);      // high cluster
            Write16(entry, 0x16, 0x0000); // write time
            Write16(entry, 0x18, date);   // write date
            Write16(entry, 0x1A, (ushort)cluster);
            Write32(entry, 0x1C, (uint)fileSize);
            int offset = sector * BYTES_PER_SECTOR;
            Buffer.BlockCopy(entry, 0, image, offset, 32);
        }

        private static void CopyAscii(byte[] b, int offset, string s, int len)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            for (int i = 0; i < len; i++)
                b[offset + i] = i < bytes.Length ? bytes[i] : (byte)0x20;
        }

        private static void Write16(byte[] b, int offset, int v)
        {
            b[offset] = (byte)(v & 0xFF);
            b[offset + 1] = (byte)((v >> 8) & 0xFF);
        }

        private static void Write32(byte[] b, int offset, uint v)
        {
            b[offset] = (byte)(v & 0xFF);
            b[offset + 1] = (byte)((v >> 8) & 0xFF);
            b[offset + 2] = (byte)((v >> 16) & 0xFF);
            b[offset + 3] = (byte)((v >> 24) & 0xFF);
        }
    }
}
