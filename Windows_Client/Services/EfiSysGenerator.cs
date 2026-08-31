using System;
using System.IO;
using System.Text;

namespace Windows_Client.Services
{
    /// <summary>
    /// 生成 Windows UEFI 引导所需的 EFISYS.BIN（1.44MB FAT12 盘镜像，内含 EFI\BOOT\BOOTX64.EFI）。
    /// 当下载/解包的 PE 未自带 efisys.bin 时，用该生成器兜底，保证 El Torito UEFI 条目可引导。
    /// 纯 C# 实现，无第三方依赖。
    /// </summary>
    public static class EfiSysGenerator
    {
        private const int BYTES_PER_SECTOR = 512;
        private const int SECTORS_PER_TRACK = 18;
        private const int HEADS = 2;
        private const int CYLINDERS = 80;
        private const int TOTAL_SECTORS = SECTORS_PER_TRACK * HEADS * CYLINDERS; // 2880
        private const int RESERVED_SECTORS = 1;
        private const int NUM_FATS = 2;
        private const int FAT_SECTORS = 9;
        private const int ROOT_ENTRIES = 224;
        private const int ROOT_SECTORS = ROOT_ENTRIES * 32 / BYTES_PER_SECTOR;   // 14
        private const int DATA_START_SECTOR = RESERVED_SECTORS + NUM_FATS * FAT_SECTORS + ROOT_SECTORS; // 33

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
                int fileClusters = (int)((fileSize + BYTES_PER_SECTOR - 1) / BYTES_PER_SECTOR);
                // 保留 cluster2/3 给 EFI、BOOT 目录，文件从 cluster4 开始
                int firstDataCluster = 4;
                int dataClusters = TOTAL_SECTORS - DATA_START_SECTOR;
                if (firstDataCluster + fileClusters > dataClusters)
                {
                    log?.Invoke("BOOTX64.EFI 过大，无法装入 1.44MB efisys.bin");
                    return false;
                }

                var image = new byte[(long)TOTAL_SECTORS * BYTES_PER_SECTOR];
                WriteBootSector(image);

                var fat = new byte[(long)FAT_SECTORS * BYTES_PER_SECTOR];
                fat[0] = 0xF0; fat[1] = 0xFF; fat[2] = 0xFF;   // FAT12 头（media + 前两簇）
                LinkFile(fat, firstDataCluster, fileClusters);
                for (int copy = 0; copy < NUM_FATS; copy++)
                {
                    int start = (RESERVED_SECTORS + copy * FAT_SECTORS) * BYTES_PER_SECTOR;
                    Buffer.BlockCopy(fat, 0, image, start, fat.Length);
                }

                // 根目录：EFI -> cluster 2
                int rootOffset = (RESERVED_SECTORS + NUM_FATS * FAT_SECTORS) * BYTES_PER_SECTOR;
                WriteDirEntry(image, rootOffset, "EFI", "", 0x10, 2, 0);

                // EFI 目录（cluster2, sector33）：BOOT -> cluster3
                int efiDirSector = DATA_START_SECTOR + (2 - 2);
                WriteDirEntry(image, efiDirSector * BYTES_PER_SECTOR, "BOOT", "", 0x10, 3, 0);

                // BOOT 目录（cluster3, sector34）：BOOTX64.EFI -> cluster4
                int bootDirSector = DATA_START_SECTOR + (3 - 2);
                WriteDirEntry(image, bootDirSector * BYTES_PER_SECTOR, "BOOTX64", "EFI", 0x20,
                    firstDataCluster, fileSize);

                // 文件内容写入 cluster4..（sector35..）
                long fileSector = DATA_START_SECTOR + (firstDataCluster - 2);
                using (var fs = File.OpenRead(bootx64Efi))
                {
                    fs.Read(image, (int)(fileSector * BYTES_PER_SECTOR), (int)fileSize);
                }

                var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                File.WriteAllBytes(outputPath, image);
                log?.Invoke("已生成 efisys.bin（FAT12，" + fileClusters + " 簇）");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("生成 efisys.bin 失败: " + ex.Message);
                return false;
            }
        }

        // ==================== 底层写入 ====================

        private static void WriteBootSector(byte[] image)
        {
            byte[] jmp = { 0xEB, 0x3C, 0x90 };
            Buffer.BlockCopy(jmp, 0, image, 0, 3);
            CopyAscii(image, 3, "MSWIN4.1", 8);
            image[0x0B] = (BYTES_PER_SECTOR & 0xFF); image[0x0C] = (BYTES_PER_SECTOR >> 8); // bytes/sector=512
            image[0x0D] = 0x01;                    // sectors/cluster
            image[0x0E] = 0x01; image[0x0F] = 0x00; // reserved=1
            image[0x10] = 0x02;                    // num FATs
            Write16(image, 0x11, ROOT_ENTRIES);    // 224
            Write16(image, 0x13, TOTAL_SECTORS);   // 2880
            image[0x15] = 0xF0;                    // media
            Write16(image, 0x16, FAT_SECTORS);     // 9
            Write16(image, 0x18, SECTORS_PER_TRACK);
            Write16(image, 0x1A, HEADS);
            Write32(image, 0x1C, 0);               // hidden sectors
            Write32(image, 0x20, 0);               // total sectors32
            image[0x24] = 0x00;                    // drive number
            image[0x25] = 0x00;                    // reserved
            image[0x26] = 0x29;                    // extended boot signature
            Write32(image, 0x27, 0x20240101);      // volume id
            CopyAscii(image, 0x2B, "ZS_EFISYS   ", 11);
            CopyAscii(image, 0x36, "FAT12   ", 8);
        }

        /// <summary>将 fileClusters 簇串成链表，起点 startCluster，末尾标记 0xFFF；并标记 EFI/BOOT 目录簇为 EOC。</summary>
        private static void LinkFile(byte[] fat, int startCluster, int fileClusters)
        {
            SetFat(fat, 2, 0xFFF); // EFI 目录
            SetFat(fat, 3, 0xFFF); // BOOT 目录
            for (int i = 0; i < fileClusters; i++)
                SetFat(fat, startCluster + i, i == fileClusters - 1 ? 0xFFF : startCluster + i + 1);
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

        private static void WriteDirEntry(byte[] image, int offset, string name8, string ext3, byte attr, int cluster, long fileSize)
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