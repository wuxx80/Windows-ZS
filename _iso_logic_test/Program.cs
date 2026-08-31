using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ISO 逻辑自测：EfiSysGenerator(FAT12 efisys.bin) + IsoBuilder(ISO9660+Joliet+El Torito 双引导)
class Program
{
    static int pass = 0, fail = 0;
    static void OK(string m) { pass++; Console.WriteLine("  PASS: " + m); }
    static void NG(string m) { fail++; Console.WriteLine("  FAIL: " + m); }

    const int SECTOR = 2048;

    static async Task<int> Main()
    {
        var asm = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Windows_Client.dll"));
        var efiGen = asm.GetType("Windows_Client.Services.EfiSysGenerator")!;
        var isoBuilder = asm.GetType("Windows_Client.Services.IsoBuilder")!;

        var root = Path.Combine(Path.GetTempPath(), "zs_iso_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            // ==================== 1. EfiSysGenerator.TryCreate ====================
            Console.WriteLine("--- 1. EfiSysGenerator(FAT12 efisys.bin) ---");
            var bootx64 = Path.Combine(root, "bootx64.efi");
            var bootBytes = new byte[512 * 300];   // ~150KB 模拟 bootx64.efi
            Random.Shared.NextBytes(bootBytes);
            File.WriteAllBytes(bootx64, bootBytes);
            var hashBoot = SHA256.HashData(bootBytes);

            var efisysOut = Path.Combine(root, "efisys.bin");
            var logs = new List<string>();
            var ok = (bool)efiGen.GetMethod("TryCreate")!.Invoke(null, new object[] { bootx64, efisysOut, (Action<string>)(m => logs.Add(m)) })!;

            if (!ok || !File.Exists(efisysOut)) { NG("TryCreate 失败: " + string.Join(";", logs)); }
            else
            {
                var fi = new FileInfo(efisysOut);
                if (fi.Length == 2880 * 512) OK("efisys.bin 尺寸 = 1.44MB (" + fi.Length + " 字节)");
                else NG("尺寸异常: " + fi.Length);

                var img = File.ReadAllBytes(efisysOut);
                var fs = new Fat12Reader(img);

                bool bootOk = fs.FsType == "FAT12" && fs.BytesPerSector == 512
                              && fs.TotalSectors == 2880 && fs.Media == 0xF0;
                if (bootOk) OK("引导扇区 (FAT12/512B/2880扇区/media=0xF0)");
                else NG("引导扇区异常: fstype=" + fs.FsType + " bps=" + fs.BytesPerSector + " ts=" + fs.TotalSectors + " media=0x" + fs.Media.ToString("X2"));

                var efiCluster = fs.FindDirCluster("EFI");
                var bootCluster = efiCluster > 0 ? fs.FindSubDirCluster(fs.GetDirSector(efiCluster), "BOOT") : -1;
                if (efiCluster == 2) OK("根目录 EFI -> cluster 2");
                else NG("EFI cluster=" + efiCluster);
                if (bootCluster == 3) OK("EFI\\BOOT -> cluster 3");
                else NG("BOOT cluster=" + bootCluster);

                var (fileCluster, fileSize) = bootCluster > 0 ? fs.FindFile(fs.GetDirSector(bootCluster), "BOOTX64", "EFI") : (-1, -1L);
                if (fileCluster == 4 && fileSize == bootBytes.Length) OK("BOOTX64.EFI -> cluster 4, size=" + fileSize);
                else NG("BOOTX64 cluster=" + fileCluster + " size=" + fileSize);

                var data = fs.ReadFile(bootCluster, fileCluster, fileSize);
                var hashRead = SHA256.HashData(data);
                if (Convert.ToHexString(hashRead) == Convert.ToHexString(hashBoot)) OK("BOOTX64.EFI 内容一致（哈希匹配）");
                else
                {
                    NG("BOOTX64.EFI 内容不一致");
                    int firstBad = -1;
                    for (int i = 0; i < data.Length; i++) if (data[i] != bootBytes[i]) { firstBad = i; break; }
                    Console.WriteLine("    首个差异偏移=" + firstBad + " 期望=" + bootBytes[Math.Max(0, firstBad)].ToString("X2") + " 实际=" + data[Math.Max(0, firstBad)].ToString("X2"));
                }

                int lastFileCluster = fileCluster + (int)((fileSize + 511) / 512) - 1;
                var fatEoc = fs.GetFat(2) == 0xFFF && fs.GetFat(3) == 0xFFF && fs.GetFat(lastFileCluster) == 0xFFF;
                if (fatEoc) OK("FAT 链正确（EFI/BOOT=0xFFF，文件末簇=0xFFF）");
                else
                {
                    NG("FAT 链异常");
                    Console.WriteLine("    GetFat(2)=" + fs.GetFat(2).ToString("X3") + " GetFat(3)=" + fs.GetFat(3).ToString("X3") + " GetFat(303)=" + fs.GetFat(303).ToString("X3"));
                    int c = fileCluster; int steps = 0;
                    var seen = new HashSet<int>();
                    while (c >= 2 && c < 0xFF0 && steps < 310)
                    {
                        if (!seen.Add(c)) { Console.WriteLine("    簇 " + c + " 循环"); break; }
                        int nx = fs.GetFat(c);
                        if (steps < 6) Console.WriteLine("    簇 " + c + " -> " + nx.ToString("X3"));
                        c = nx; steps++;
                    }
                    Console.WriteLine("    链长=" + steps + " 末端=" + c.ToString("X3"));
                }
            }

            // ==================== 2. IsoBuilder.BuildAsync ====================
            Console.WriteLine("--- 2. IsoBuilder(ISO9660+Joliet+El Torito) ---");
            var src = Path.Combine(root, "src");
            Directory.CreateDirectory(Path.Combine(src, "SUBDIR"));
            File.WriteAllText(Path.Combine(src, "README.TXT"), "ZS 装机助手 ISO 测试\n第二行中文内容");
            var dataBin = new byte[1024 * 1024 + 123];
            Random.Shared.NextBytes(dataBin);
            File.WriteAllBytes(Path.Combine(src, "SUBDIR", "data.bin"), dataBin);
            var hashData = SHA256.HashData(dataBin);
            var hashReadme = SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(src, "README.TXT"))));

            var efiDir = Path.Combine(src, "EFI", "BOOT");
            Directory.CreateDirectory(efiDir);
            File.Copy(efisysOut, Path.Combine(efiDir, "EFISYS.BIN"), true);
            var legacyDir = Path.Combine(src, "BOOT");
            Directory.CreateDirectory(legacyDir);
            var etfs = new byte[SECTOR * 2];
            Random.Shared.NextBytes(etfs);
            File.WriteAllBytes(Path.Combine(legacyDir, "ETFSYS.COM"), etfs);

            var isoPath = Path.Combine(root, "ZS_PE_TEST.iso");

            var reqType = isoBuilder.GetNestedType("BuildRequest")!;
            var req = Activator.CreateInstance(reqType)!;
            reqType.GetField("OutputPath")!.SetValue(req, isoPath);
            reqType.GetField("SourceDir")!.SetValue(req, src);
            reqType.GetField("Label")!.SetValue(req, "ZS_PE_TEST");
            reqType.GetField("EfiBootRel")!.SetValue(req, "/EFI/BOOT/EFISYS.BIN");
            reqType.GetField("LegacyBootRel")!.SetValue(req, "/BOOT/ETFSYS.COM");

            var buildMethod = isoBuilder.GetMethod("BuildAsync")!;
            try
            {
                var task = (Task)buildMethod.Invoke(null, new object[] { req, null, CancellationToken.None })!;
                await task;
                if (File.Exists(isoPath)) OK("ISO 生成成功: " + new FileInfo(isoPath).Length + " 字节");
                else { NG("ISO 未生成"); return 1; }
            }
            catch (Exception ex)
            {
                NG("BuildAsync 异常: " + ex.Message);
                return 1;
            }

            var iso = File.ReadAllBytes(isoPath);
            var vd = new IsoReader(iso);

            // --- 卷描述符 ---
            if (vd.Pvd.Type == 1 && vd.Pvd.Id == "CD001") OK("PVD 有效 (CD001)");
            else NG("PVD 异常: type=" + vd.Pvd.Type + " id=" + vd.Pvd.Id);
            if (vd.Terminator.Type == 255 && vd.Terminator.Id == "CD001") OK("卷描述符终结符 255");
            else NG("终结符异常: type=" + vd.Terminator.Type);
            if (vd.Svd != null && vd.Svd.Type == 2 && vd.Svd.Id == "CD001" && vd.Svd.Ucs2)
                OK("Joliet SVD 有效 (UCS-2 escape %/@)");
            else NG("Joliet SVD 异常: type=" + (vd.Svd?.Type ?? -1) + " ucs2=" + (vd.Svd?.Ucs2 == true));

            // --- 引导记录 ---
            if (vd.BootRecord != null && vd.BootRecord.Id == "CD001")
                OK("BootRecord 有效: 系统标识=" + vd.BootRecord.SystemId.Trim());
            else NG("BootRecord 缺失");
            if (vd.BootRecordCount == 1) OK("引导记录数量=1（单记录双段，对齐 oscdimg）");
            else NG("引导记录数量=" + vd.BootRecordCount);

            if (vd.BootCatalog != null)
            {
                if (vd.CatalogChecksumValid) OK("引导目录 Validation Entry 校验和正确");
                else NG("引导目录校验和错误");
                if (vd.BootCatalog.Count == 2) OK("双引导条目 (Legacy + EFI)");
                else NG("引导条目数量=" + vd.BootCatalog.Count);
                foreach (var bc in vd.BootCatalog)
                {
                    bool valid = bc.Platform == 0x00 || bc.Platform == 0xEF;
                    bool lbaInRange = bc.LoadRba > 0 && bc.LoadRba < vd.Pvd.VolumeSpaceSize;
                    if (valid && lbaInRange) OK($"引导条目 platform=0x{bc.Platform:X2} lba={bc.LoadRba} 有效");
                    else NG("引导条目异常: platform=0x" + bc.Platform.ToString("X2") + " lba=" + bc.LoadRba);
                }
                var efiEntry = vd.BootCatalog[0].Platform == 0xEF ? vd.BootCatalog[0] : vd.BootCatalog[1];
                var legacyEntry = vd.BootCatalog[0].Platform == 0x00 ? vd.BootCatalog[0] : vd.BootCatalog[1];
                if (efiEntry.SystemType == 0xEF && efiEntry.LoadSegment == 0 && efiEntry.SectorCount == 4)
                    OK("EFI 条目: systemType=0xEF loadSegment=0 sectors=4");
                else NG("EFI 条目参数异常: sys=" + efiEntry.SystemType.ToString("X2") + " seg=" + efiEntry.LoadSegment + " sec=" + efiEntry.SectorCount);
                if (legacyEntry.SystemType == 0 && legacyEntry.LoadSegment == 0x07C0 && legacyEntry.SectorCount == 4)
                    OK("Legacy 条目: systemType=0 loadSegment=0x07C0 sectors=4");
                else NG("Legacy 条目参数异常: sys=" + legacyEntry.SystemType.ToString("X2") + " seg=" + legacyEntry.LoadSegment + " sec=" + legacyEntry.SectorCount);
                var efiImg = File.ReadAllBytes(Path.Combine(efiDir, "EFISYS.BIN"));
                bool efiMatch = CompareAt(iso, (int)efiEntry.LoadRba * SECTOR, efiImg, 512);
                if (efiMatch) OK("EFI 引导条目指向真实 efisys.bin 数据");
                else NG("EFI 引导条目数据不匹配 (lba=" + efiEntry.LoadRba + ")");
                bool legacyMatch = CompareAt(iso, (int)legacyEntry.LoadRba * SECTOR, etfs, 512);
                if (legacyMatch) OK("Legacy 引导条目指向真实 ETFSYS.COM 数据");
                else NG("Legacy 引导条目数据不匹配 (lba=" + legacyEntry.LoadRba + ")");
            }
            else NG("BootCatalog 缺失");

            // --- 路径表 ---
            if (vd.PathTableL.Count > 0 && vd.PathTableL[0].Name == "" && vd.PathTableL[0].Parent == 1)
                OK("L 路径表解析 " + vd.PathTableL.Count + " 项，根目录正确");
            else NG("L 路径表异常: 项数=" + vd.PathTableL.Count);
            bool ptOk = true;
            PathEntryInfo? badPt = null;
            foreach (var e in vd.PathTableL)
                if (e.Extent <= 0 || e.Extent >= vd.Pvd.VolumeSpaceSize) { ptOk = false; badPt = e; break; }
            if (ptOk) OK("L 路径表所有目录 extent 合法");
            else
            {
                NG("L 路径表存在非法 extent: name=" + badPt?.Name + " extent=" + badPt?.Extent + " volsize=" + vd.Pvd.VolumeSpaceSize);
                Console.WriteLine("    PVD PathTableLba=" + vd.Pvd.PathTableLba + " PathTableSize=" + vd.Pvd.PathTableSize + " RootDirExtent(PVD)=" + vd.RootDirExtent);
                long p0 = (long)vd.Pvd.PathTableLba * 2048;
                var sb0 = new StringBuilder();
                for (int i = 0; i < 16; i++) sb0.Append(iso[p0 + i].ToString("X2") + " ");
                Console.WriteLine("    路径表前16字节: " + sb0.ToString().Trim());
                Console.WriteLine("    PathTableL[0]: name='" + vd.PathTableL[0].Name + "' extent=" + vd.PathTableL[0].Extent + " parent=" + vd.PathTableL[0].Parent);
                Console.WriteLine("    PathTableL[1]: name='" + vd.PathTableL[1].Name + "' extent=" + vd.PathTableL[1].Extent + " parent=" + vd.PathTableL[1].Parent);
            }

            // --- 目录记录 + 文件数据 ---
            var rootRecs = vd.ReadDir(vd.RootDirExtent, vd.RootDirSize);
            var names = new List<string>();
            foreach (var r in rootRecs) names.Add(r.Name);
            bool hasReadme = names.Contains("README.TXT");
            bool hasSub = names.Contains("SUBDIR");
            bool hasEfi = names.Contains("EFI");
            bool hasBoot = names.Contains("BOOT");
            if (hasReadme && hasSub && hasEfi && hasBoot) OK("根目录记录: README.TXT/SUBDIR/EFI/BOOT 齐全");
            else NG("根目录记录缺失: " + string.Join(",", names));

            var sub = vd.FindDir(vd.RootDirExtent, vd.RootDirSize, "SUBDIR");
            if (sub != null)
            {
                var subRecs = vd.ReadDir(sub.Extent, sub.Size);
                var subNames = new List<string>();
                foreach (var r in subRecs) subNames.Add(r.Name);
                if (subNames.Contains("DATA.BIN")) OK("SUBDIR 记录含 DATA.BIN");
                else NG("SUBDIR 缺失 DATA.BIN: " + string.Join(",", subNames));
                var dataRec = subRecs.Find(r => r.Name == "DATA.BIN");
                if (dataRec != null)
                {
                    var readBack = new byte[dataRec.Size];
                    Array.Copy(iso, (int)dataRec.Extent * SECTOR, readBack, 0, (int)dataRec.Size);
                    if (Convert.ToHexString(SHA256.HashData(readBack)) == Convert.ToHexString(hashData))
                        OK("DATA.BIN 数据完整性校验通过 (1MB+123)");
                    else NG("DATA.BIN 数据不一致");
                }
            }
            else NG("未找到 SUBDIR 目录记录");

            var readmeRec = rootRecs.Find(r => r.Name == "README.TXT");
            if (readmeRec != null)
            {
                var rb = new byte[readmeRec.Size];
                Array.Copy(iso, (int)readmeRec.Extent * SECTOR, rb, 0, (int)readmeRec.Size);
                if (Convert.ToHexString(SHA256.HashData(rb)) == Convert.ToHexString(hashReadme))
                    OK("README.TXT 内容校验通过");
                else NG("README.TXT 内容不一致");
            }

            // --- Joliet 目录 ---
            var jolRoot = vd.ReadJolietDir(vd.JolietRootExtent, vd.JolietRootSize);
            bool jolOk = jolRoot.Exists(r => r.Name == "README.TXT") && jolRoot.Exists(r => r.Name == "SUBDIR");
            if (jolOk) OK("Joliet 根目录可解析 (README.TXT/SUBDIR)");
            else NG("Joliet 根目录异常: " + string.Join(",", jolRoot.ConvertAll(r => r.Name)));

            // ==================== 3. 在线 PE 拉取 + 生成可引导 ISO（端到端） ====================
            Console.WriteLine("--- 3. 在线 PE 拉取 + 生成可引导 ISO ---");
            try
            {
                var svc = new Windows_Client.Services.UDiskService();
                var cacheDir = Path.Combine(Path.GetTempPath(), "ZS_Cache", "pe");
                const string peUrl = "https://github.com/thedoggybrad/WindowsPEBasic/releases/download/WinPEBasic/WindowsPEBasic.iso";
                Console.WriteLine("    下载 PE: " + peUrl);
                var dl = await svc.DownloadPeUrlAsync(peUrl, cacheDir,
                    new Progress<int>(p => Console.Write("\r    下载进度: " + p + "%  ")));
                Console.WriteLine();
                if (!dl.Ok) NG("在线 PE 拉取失败: " + dl.Error);
                else
                {
                    var fi = new FileInfo(dl.Path);
                    if (fi.Length > 100L * 1024 * 1024) OK("在线 PE 拉取成功: " + fi.Name + "  (" + (fi.Length / 1024 / 1024) + " MB)");
                    else NG("PE 文件过小: " + fi.Length);

                    var outIso = Path.Combine(root, "ZS_PE_from_online.iso");
                    var plan = new Windows_Client.Models.IsoBuildPlan
                    {
                        PeFilePath = dl.Path,
                        OutputPath = outIso,
                        IsoLabel = "ZS_PE_ONLINE",
                        IncludeClient = false,
                        IncludeTools = false,
                        ClientDir = ""
                    };
                    var logs2 = new List<string>();
                    var build = await svc.BuildIsoAsync(plan, m => logs2.Add(m));
                    if (!build.Ok) NG("生成可引导 ISO 失败: " + build.Error);
                    else if (!File.Exists(outIso)) NG("ISO 文件未生成");
                    else
                    {
                        var oi = new FileInfo(outIso);
                        if (oi.Length > 10L * 1024 * 1024) OK("可引导 ISO 生成成功: " + (oi.Length / 1024 / 1024) + " MB");
                        else NG("ISO 过小: " + oi.Length);

                        var iso2 = File.ReadAllBytes(outIso);
                        var vd2 = new IsoReader(iso2);
                        if (vd2.Pvd.Type == 1 && vd2.Pvd.Id == "CD001") OK("在线PE ISO PVD 有效 (CD001)");
                        else NG("在线PE ISO PVD 异常: type=" + vd2.Pvd.Type + " id=" + vd2.Pvd.Id);
                        if (vd2.BootRecord != null && vd2.BootRecord.Id == "CD001")
                            OK("在线PE ISO BootRecord 有效: " + vd2.BootRecord.SystemId.Trim());
                        else NG("在线PE ISO BootRecord 缺失");
                        if (vd2.BootCatalog != null && vd2.BootCatalog.Count >= 1)
                        {
                            if (vd2.CatalogChecksumValid) OK("在线PE ISO 引导目录校验和正确");
                            else NG("在线PE ISO 引导目录校验和错误");
                            var nEfi = vd2.BootCatalog.FindAll(b => b.Platform == 0xEF).Count;
                            var nLeg = vd2.BootCatalog.FindAll(b => b.Platform == 0x00).Count;
                            Console.WriteLine("    引导条目分布: EFI=" + nEfi + " Legacy=" + nLeg);
                            bool allLbaOk = true;
                            foreach (var bc in vd2.BootCatalog)
                                if (!(bc.LoadRba > 0 && bc.LoadRba < vd2.Pvd.VolumeSpaceSize)) allLbaOk = false;
                            if (allLbaOk) OK("在线PE ISO 全部引导条目 lba 有效");
                            else NG("在线PE ISO 存在引导条目 lba 越界");
                        }
                        else NG("在线PE ISO BootCatalog 缺失或为空");
                        var rootRecs2 = vd2.ReadDir(vd2.RootDirExtent, vd2.RootDirSize);
                        if (rootRecs2.Count > 0)
                        {
                            var n0 = rootRecs2.ConvertAll(r => r.Name).GetRange(0, Math.Min(6, rootRecs2.Count));
                            OK("在线PE ISO 根目录可解析: " + string.Join(",", n0) + "...");
                        }
                        else NG("在线PE ISO 根目录不可解析");
                        if (vd2.PathTableL.Count > 0) OK("在线PE ISO L 路径表解析 " + vd2.PathTableL.Count + " 项");
                        else NG("在线PE ISO L 路径表为空");
                        if (vd2.Svd != null && vd2.Svd.Ucs2) OK("在线PE ISO Joliet SVD 有效");
                        else NG("在线PE ISO Joliet SVD 缺失");
                    }
                }
            }
            catch (Exception ex)
            {
                NG("在线 PE 端到端测试异常: " + ex.Message);
            }

            // ==================== 4. 结果汇总 ====================
            Console.WriteLine("========================================");
            Console.WriteLine("PASS=" + pass + "  FAIL=" + fail);
            if (fail == 0) Console.WriteLine("全部通过");
            else Console.WriteLine("存在失败项");
            return fail == 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    static bool CompareAt(byte[] iso, int offset, byte[] img, int n)
    {
        for (int i = 0; i < n; i++)
            if (iso[offset + i] != img[i]) return false;
        return true;
    }

    // ==================== FAT12 读取器 ====================
    class Fat12Reader
    {
        byte[] I;
        public int BytesPerSector, TotalSectors, FatSectors, RootEntries, RootSectors, DataStart;
        public byte Media;
        public string Label, FsType;
        const int RESERVED = 1, NUM_FATS = 2;
        public Fat12Reader(byte[] img)
        {
            I = img;
            BytesPerSector = I[0x0B] | (I[0x0C] << 8);
            TotalSectors = I[0x13] | (I[0x14] << 8);
            Media = I[0x15];
            FatSectors = I[0x16] | (I[0x17] << 8);
            RootEntries = I[0x11] | (I[0x12] << 8);
            RootSectors = RootEntries * 32 / BytesPerSector;
            DataStart = RESERVED + NUM_FATS * FatSectors + RootSectors;
            Label = Encoding.ASCII.GetString(I, 0x2B, 11).Trim();
            FsType = Encoding.ASCII.GetString(I, 0x36, 8).Trim();
        }
        public int GetFat(int cluster)
        {
            // FAT 区起始于保留扇区之后（sector 1 -> 偏移 512），簇项相对 FAT 起始计算
            int fatBase = RESERVED * BytesPerSector;
            int baseOffset = fatBase + (cluster / 2) * 3;
            int v;
            if ((cluster & 1) == 0) v = I[baseOffset] | ((I[baseOffset + 1] & 0x0F) << 8);
            else v = (I[baseOffset + 1] >> 4) | (I[baseOffset + 2] << 4);
            return v;
        }
        int RootOffset => (RESERVED + NUM_FATS * FatSectors) * BytesPerSector;
        int ClusterToSector(int c) => DataStart + (c - 2);
        public int FindDirCluster(string name)
        {
            for (int o = RootOffset; o < RootOffset + RootSectors * BytesPerSector; o += 32)
            {
                var n = Encoding.ASCII.GetString(I, o, 11).Trim();
                if (n == name) return I[o + 26] | (I[o + 27] << 8);
            }
            return -1;
        }
        public int GetDirSector(int cluster) => ClusterToSector(cluster) * BytesPerSector;
        public int FindSubDirCluster(int dirSector, string name)
        {
            for (int o = dirSector; o < dirSector + BytesPerSector * 2; o += 32)
            {
                var n = Encoding.ASCII.GetString(I, o, 11).Trim();
                if (n == name) return I[o + 26] | (I[o + 27] << 8);
            }
            return -1;
        }
        public (int cluster, long size) FindFile(int dirSector, string name, string ext)
        {
            for (int o = dirSector; o < dirSector + BytesPerSector * 2; o += 32)
            {
                var n = Encoding.ASCII.GetString(I, o, 8).Trim();
                var e = Encoding.ASCII.GetString(I, o + 8, 3).Trim();
                if (n == name && e == ext) return (I[o + 26] | (I[o + 27] << 8), BitConverter.ToUInt32(I, o + 28));
            }
            return (-1, -1);
        }
        public byte[] ReadFile(int dirCluster, int startCluster, long size)
        {
            var buf = new byte[size];
            long written = 0;
            int c = startCluster;
            while (c >= 2 && c < 0xFF0 && written < size)
            {
                int sector = ClusterToSector(c) * BytesPerSector;
                if (sector < 0 || sector >= I.Length) break;
                int n = (int)Math.Min(BytesPerSector, Math.Min(size - written, I.Length - sector));
                Array.Copy(I, sector, buf, written, n);
                written += n;
                c = GetFat(c);
            }
            return buf;
        }
    }

    // ==================== ISO9660 读取器 ====================
    class IsoReader
    {
        byte[] I;
        public PvdInfo Pvd; public TermInfo Terminator; public SvdInfo? Svd;
        public BootInfo? BootRecord;
        public int BootRecordCount;
        public bool CatalogChecksumValid;
        public List<BootCatInfo>? BootCatalog;
        public List<PathEntryInfo> PathTableL = new();
        public long RootDirExtent, RootDirSize, JolietRootExtent, JolietRootSize;

        public IsoReader(byte[] iso)
        {
            I = iso;
            int lba = 16;
            for (; lba * 2048 < I.Length; lba++)
            {
                byte t = I[lba * 2048];
                if (t == 0) { BootRecord = ParseBoot(I, lba); BootRecordCount++; }
                else if (t == 1) { Pvd = ParsePvd(I, lba); }
                else if (t == 2) { Svd = ParseSvd(I, lba); }
                else if (t == 255) { Terminator = new TermInfo { Type = 255, Id = Ascii(I, lba * 2048 + 1, 5) }; break; }
            }
            if (Svd != null && Svd.Ucs2)
            {
                JolietRootExtent = Svd.RootExtent; JolietRootSize = Svd.RootSize;
            }
            RootDirExtent = Pvd.RootExtent; RootDirSize = Pvd.RootSize;
            long ptLba = Pvd.PathTableLba;
            long ptSize = Pvd.PathTableSize;
            long off = ptLba * 2048;
            long end = off + ptSize;
            while (off + 8 <= end)
            {
                int len = I[off];
                int extent = BitConverter.ToInt32(I, (int)off + 2);
                int parent = BitConverter.ToUInt16(I, (int)off + 6);
                var name = Encoding.ASCII.GetString(I, (int)off + 8, len);
                PathTableL.Add(new PathEntryInfo { Name = name, Extent = extent, Parent = parent });
                off += 8 + len + (len % 2 == 1 ? 1 : 0);
            }
            if (BootRecord != null)
            {
                BootCatalog = new List<BootCatInfo>();
                long co = (long)BootRecord.CatalogLba * 2048;
                var val = ParseBootCatalogEntry(I, co);
                if (val != null)
                {
                    CatalogChecksumValid = val.ChecksumValid;
                    long p = co + 32;
                    while (p + 32 <= I.Length)
                    {
                        byte ind = I[p];
                        if (ind == 0x88)
                        {
                            // 直接引导条目（默认/Legacy 条目，隐含 x86 平台）
                            var e = ParseBootEntry(I, p);
                            if (e != null) { e.Platform = 0x00; BootCatalog.Add(e); }
                            p += 32;
                        }
                        else if (ind == 0x90 || ind == 0x91)
                        {
                            // 段头：平台 + 条目数，随后跟随条目
                            byte plat = I[p + 1];
                            int count = I[p + 2] | (I[p + 3] << 8);
                            p += 32;
                            for (int i = 0; i < count; i++)
                            {
                                var e = ParseBootEntry(I, p);
                                if (e != null) { e.Platform = plat; BootCatalog.Add(e); }
                                p += 32;
                            }
                            if (count == 0) break;   // 终结段头
                        }
                        else break;
                    }
                }
            }
        }

        public List<DirRecord> ReadDir(long extent, long size)
        {
            var list = new List<DirRecord>();
            long o = extent * 2048;
            long end = o + size;
            while (o < end && o < I.Length)
            {
                int len = I[o];
                if (len == 0) break;
                var r = ParseDirRecord(I, o);
                if (r != null) list.Add(r);
                o += len;
            }
            return list;
        }

        public DirRecord? FindDir(long extent, long size, string name)
            => ReadDir(extent, size).Find(r => r.IsDir && r.Name == name);

        public List<JolName> ReadJolietDir(long extent, long size)
        {
            var list = new List<JolName>();
            long o = extent * 2048;
            long end = o + size;
            while (o < end && o < I.Length)
            {
                int len = I[o];
                if (len == 0) break;
                int idLen = I[o + 32];
                byte[] nameBytes = new byte[idLen];
                Array.Copy(I, o + 33, nameBytes, 0, idLen);
                var sb = new StringBuilder();
                for (int i = 0; i < nameBytes.Length; i += 2)
                {
                    char c = (char)((nameBytes[i] << 8) | nameBytes[i + 1]);
                    sb.Append(c);
                }
                var nm = sb.ToString();
                if (nm == "\u0001") nm = ".";
                else if (nm == "\u0001\u0001") nm = "..";
                list.Add(new JolName { Name = nm, IsDir = (I[o + 25] & 0x02) != 0 });
                o += len;
            }
            return list;
        }

        PvdInfo ParsePvd(byte[] b, int lba)
        {
            int off = lba * 2048;
            return new PvdInfo
            {
                Type = b[off],
                Id = Ascii(b, off + 1, 5),
                VolumeSpaceSize = BitConverter.ToInt32(b, off + 81),
                BlockSize = BitConverter.ToUInt16(b, off + 137),
                PathTableSize = BitConverter.ToInt32(b, off + 141),
                PathTableLba = BitConverter.ToInt32(b, off + 145),
                PathTableMba = (uint)((b[off + 153] << 24) | (b[off + 154] << 16) | (b[off + 155] << 8) | b[off + 156]),
                RootExtent = BitConverter.ToInt32(b, off + 0x9D + 2),
                RootSize = BitConverter.ToInt32(b, off + 0x9D + 10),
            };
        }

        SvdInfo? ParseSvd(byte[] b, int lba)
        {
            int off = lba * 2048;
            bool ucs2 = b[off + 0x58] == 0x25 && b[off + 0x59] == 0x2F && b[off + 0x5A] == 0x40;
            return new SvdInfo
            {
                Type = b[off],
                Id = Ascii(b, off + 1, 5),
                Ucs2 = ucs2,
                RootExtent = BitConverter.ToInt32(b, off + 0x9D + 2),
                RootSize = BitConverter.ToInt32(b, off + 0x9D + 10),
            };
        }

        BootInfo? ParseBoot(byte[] b, int lba)
        {
            int off = lba * 2048;
            var sys = Ascii(b, off + 7, 24);
            if (!sys.StartsWith("EL TORITO")) return null;
            return new BootInfo { Type = b[off], Id = Ascii(b, off + 1, 5), SystemId = sys, CatalogLba = BitConverter.ToInt32(b, off + 71) };
        }

        BootCatInfo? ParseBootCatalogEntry(byte[] b, long off)
        {
            if (b[off] != 0x01) return null;
            return new BootCatInfo
            {
                Platform = b[off + 1],
                SectionCount = b[off + 3] == 0 ? 1 : b[off + 2],
                ChecksumValid = ValidateChecksum(b, off),
                LoadRba = 0,
            };
        }

        bool ValidateChecksum(byte[] b, long off)
        {
            ushort sum = 0;
            for (int i = 0; i <= 0x1E; i += 2)
            {
                if (i == 0x1C) continue;
                ushort w = (ushort)((b[off + i + 1] << 8) | b[off + i]);
                sum = (ushort)((sum + w) & 0xFFFF);
            }
            ushort stored = (ushort)((b[off + 0x1D] << 8) | b[off + 0x1C]);
            ushort calc = (ushort)(0x10000 - sum);
            return stored == calc;
        }

        BootCatInfo? ParseBootEntry(byte[] b, long off)
        {
            if (b[off] != 0x88) return null;
            return new BootCatInfo
            {
                Platform = -1,
                LoadRba = (b[off + 8] | (b[off + 9] << 8) | (b[off + 10] << 16) | (b[off + 11] << 24)),
                SectionCount = 0,
                ChecksumValid = true,
                MediaType = b[off + 1],
                SystemType = b[off + 4],
                SectorCount = b[off + 6] | (b[off + 7] << 8),
                LoadSegment = b[off + 2] | (b[off + 3] << 8),
            };
        }

        DirRecord? ParseDirRecord(byte[] b, long off)
        {
            int len = b[off];
            if (len < 34) return null;
            int idLen = b[off + 32];
            var nm = Encoding.ASCII.GetString(b, (int)(off + 33), idLen);
            if (nm == "\u0000") nm = ".";
            else if (nm == "\u0001") nm = "..";
            return new DirRecord
            {
                Extent = BitConverter.ToInt32(b, (int)off + 2),
                Size = BitConverter.ToInt32(b, (int)off + 10),
                IsDir = (b[off + 25] & 0x02) != 0,
                Name = nm,
            };
        }

        static string Ascii(byte[] b, int off, int n) => Encoding.ASCII.GetString(b, off, n).TrimEnd('\0', ' ');
    }

    class PvdInfo { public byte Type; public string Id = ""; public int VolumeSpaceSize, BlockSize, PathTableSize, PathTableLba, RootExtent, RootSize; public uint PathTableMba; }
    class TermInfo { public byte Type; public string Id = ""; }
    class SvdInfo { public byte Type; public string Id = ""; public bool Ucs2; public int RootExtent, RootSize; }
    class BootInfo { public byte Type; public string Id = ""; public string SystemId = ""; public int CatalogLba; }
    class BootCatInfo { public int Platform = -1; public long LoadRba; public int SectionCount; public bool ChecksumValid; public byte MediaType; public byte SystemType; public int SectorCount; public int LoadSegment; }
    class PathEntryInfo { public string Name = ""; public int Extent; public int Parent; }
    class DirRecord { public long Extent; public long Size; public bool IsDir; public string Name = ""; }
    class JolName { public string Name = ""; public bool IsDir; }
}
