using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinPE_Client.Services
{
    /// <summary>
    /// 纯 C# 可引导 ISO 生成器：ISO9660 (Level 2) + Joliet (UCS-2) + 双路径表 + El Torito 双引导（Legacy/UEFI）。
    /// 不依赖 oscdimg/ADK/第三方库，无需管理员权限，可在 Windows 与 WinPE 环境运行。
    /// 兼容 Windows 挂载、Ventoy、UltraISO、PowerISO 及 UEFI/Legacy 固件引导。
    /// </summary>
    public static class IsoBuilder
    {
        private const int SECTOR = 2048;
        private const int VOLSYS_AREAS = 16;
        private const byte VD_BOOT = 0, VD_PRIMARY = 1, VD_SUPPLEMENTARY = 2, VD_TERMINATOR = 255;
        private const byte FLAG_DIR = 0x02;
        private const byte MEDIA_NO_EMU = 0;
        private const byte PLATFORM_X86 = 0, PLATFORM_EFI = 0xEF;
        private const byte BOOTABLE = 0x88;

        // ==================== 公共接口 ====================

        /// <summary>构建请求：扫描 SourceDir 下全部文件生成 ISO，并按相对路径指定引导镜像。</summary>
        public sealed class BuildRequest
        {
            /// <summary>输出 ISO 文件名（绝对/相对路径）。</summary>
            public string OutputPath = "";

            /// <summary>卷标（ISO-9660，自动清洗）。</summary>
            public string? Label;

            /// <summary>待打包的源目录（递归扫描全部文件）。</summary>
            public string SourceDir = "";

            /// <summary>Legacy BIOS 引导镜像相对 ISO 根路径，须位于 SourceDir 内（如 "BOOT/ETFSYS.COM"）。为空则跳过。</summary>
            public string? LegacyBootRel;

            /// <summary>UEFI 引导镜像相对 ISO 根路径，须位于 SourceDir 内（如 "EFI/BOOT/EFISYS.BIN"）。为空则跳过。</summary>
            public string? EfiBootRel;
        }

        public static async Task BuildAsync(
            BuildRequest request,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(request.SourceDir) || !Directory.Exists(request.SourceDir))
                throw new DirectoryNotFoundException("ISO 源目录不存在: " + request.SourceDir);

            var files = new List<IsoFile>();
            var root = new IsoNode { Path = "/" };
            ScanDir(request.SourceDir, root, files, cancellationToken);
            if (files.Count == 0)
                throw new InvalidOperationException("ISO 源目录为空，无可打包文件");

            // 引导镜像必须存在，否则报错防止生成不可引导镜像
            var bootLegacy = ResolveBoot(files, request.LegacyBootRel);
            var bootEfi = ResolveBoot(files, request.EfiBootRel);

            string label = request.Label ?? Path.GetFileNameWithoutExtension(request.OutputPath);
            var layout = new Layout(Label19(label), root, files, bootLegacy, bootEfi);
            layout.Allocate();

            var outDir = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            long total = layout.TotalBytes;
            double done = 0;
            using (var fs = new FileStream(request.OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                fs.SetLength(total);

                WriteAt(fs, layout.LbaPvd * SECTOR, BuildPvd(layout));
                if (layout.LbaBrv >= 0) WriteAt(fs, layout.LbaBrv * SECTOR, BuildBootRecord(layout));
                WriteAt(fs, layout.LbaSvd * SECTOR, BuildJolietPvd(layout));
                WriteAt(fs, layout.LbaTerminator * SECTOR, BuildTerminator());

                WriteAt(fs, layout.LbaTypeL * SECTOR, PathTableL(layout));
                WriteAt(fs, layout.LbaTypeM * SECTOR, PathTableM(layout));
                if (layout.LbaCat >= 0) WriteAt(fs, layout.LbaCat * SECTOR, BuildBootCatalog(layout));

                foreach (var n in layout.IsoDirs) WriteAt(fs, n.IsoLba * SECTOR, BuildIsoBlock(n, layout));
                foreach (var n in layout.JolDirs) WriteAt(fs, n.JolLba * SECTOR, BuildJolBlock(n, layout));

                foreach (var f in layout.FilesSorted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fs.Position = f.Extent * SECTOR;
                    using (var sf = new FileStream(f.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await sf.CopyToAsync(fs, 1024 * 1024, cancellationToken).ConfigureAwait(false);
                    }
                    long pad = RoundUp(f.Size, SECTOR) - f.Size;
                    if (pad > 0) fs.Write(new byte[pad], 0, (int)pad);
                    done += RoundUp(f.Size, SECTOR);
                    progress?.Report(Math.Min(1, done / total));
                }
                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static void WriteAt(Stream s, long offset, byte[] block)
        {
            s.Position = offset;
            s.Write(block, 0, block.Length);
        }

        private static string? ResolveBoot(List<IsoFile> files, string? rel)
        {
            if (string.IsNullOrEmpty(rel)) return null;
            var norm = rel!.Replace('\\', '/');
            if (!norm.StartsWith("/", StringComparison.Ordinal)) norm = "/" + norm;
            if (!files.Any(f => f.IsoPath.Equals(norm, StringComparison.OrdinalIgnoreCase)))
                throw new FileNotFoundException("引导镜像未找到: " + norm);
            return norm;
        }

        // ==================== 文件树构建 ====================

        private sealed class IsoFile
        {
            public string SourcePath = "";
            public string IsoPath = "";      // 带原始大小写的虚拟路径，用于树定位/去重
            public string IsoName = "";      // 清洗后的 ISO-9660 文件名
            public string JolietName = "";   // Joliet 原始文件名
            public long Size;
            public long Extent;
        }

        private sealed class IsoNode
        {
            public string Path = "/";
            public IsoNode? Parent;
            public List<IsoNode> Children = new();
            public List<IsoFile> Files = new();
            public long IsoLba, IsoSize;
            public long JolLba, JolSize;
            public int DirNum;
            public string Name => Path == "/" ? "" : Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private static void ScanDir(string dir, IsoNode node, List<IsoFile> files, CancellationToken ct)
        {
            foreach (var d in Directory.GetDirectories(dir))
            {
                ct.ThrowIfCancellationRequested();
                var child = new IsoNode
                {
                    Path = (node.Path == "/" ? "/" : node.Path + "/") + Path.GetFileName(d),
                    Parent = node
                };
                ScanDir(d, child, files, ct);
                node.Children.Add(child); // 保留空目录
            }
            foreach (var f in Directory.GetFiles(dir))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(f);
                var ifile = new IsoFile
                {
                    SourcePath = f,
                    IsoPath = (node.Path == "/" ? "/" : node.Path + "/") + name,
                    IsoName = SanitizeIsoName(name),
                    JolietName = name,
                    Size = new FileInfo(f).Length
                };
                files.Add(ifile);
                node.Files.Add(ifile);
            }
        }

        // ==================== 布局分配 ====================

        private sealed class PathEntry
        {
            public string Name = "";
            public int DirNum, ParentNum;
            public long Extent;
        }

        private sealed class Layout
        {
            public Layout(string label, IsoNode root, List<IsoFile> files, string? bootLegacy, string? bootEfi)
            {
                Label = label; Root = root; Files = files;
                BootLegacy = bootLegacy; BootEfi = bootEfi;
                BuildPathTable();
            }

            public string Label;
            public readonly IsoNode Root;
            public readonly List<IsoFile> Files;
            public string? BootLegacy, BootEfi;

            public long LbaPvd, LbaSvd, LbaTerminator;
            public long LbaBrv = -1, LbaCat = -1;
            public long LbaTypeL, LbaTypeM, PathTableBytes;
            public List<IsoNode> IsoDirs = new();
            public List<IsoNode> JolDirs = new();
            public List<IsoFile> FilesSorted = new();
            public List<PathEntry> PathEntries = new();
            public long TotalBytes;

            private void BuildPathTable()
            {
                var queue = new Queue<IsoNode>();
                int num = 1;
                Root.DirNum = num++;
                queue.Enqueue(Root);
                PathEntries.Add(new PathEntry { Name = "", DirNum = 1, ParentNum = 1, Extent = 0 });
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    foreach (var c in node.Children)
                    {
                        c.DirNum = num++;
                        var pn = c.Parent == null ? c.DirNum : c.Parent.DirNum;
                        PathEntries.Add(new PathEntry { Name = ShortAscii(c.Name, 8), DirNum = c.DirNum, ParentNum = pn, Extent = 0 });
                        queue.Enqueue(c);
                    }
                }
                long bytes = 0;
                foreach (var e in PathEntries)
                {
                    int idLen = Encoding.ASCII.GetByteCount(e.Name);
                    bytes += 8 + idLen + (idLen % 2 == 1 ? 1 : 0);
                }
                // PVD 的 PathTableSize 必须是路径表实际字节数（存储时才按扇区对齐），
                // 否则严格读取器会把对齐零填充误解析为多余空条目。
                PathTableBytes = bytes;
            }

            public void Allocate()
            {
                long next = VOLSYS_AREAS;
                LbaPvd = next++;
                bool anyBoot = !string.IsNullOrEmpty(BootLegacy) || !string.IsNullOrEmpty(BootEfi);
                if (anyBoot) LbaBrv = next++;
                LbaSvd = next++;
                LbaTerminator = next++;
                LbaTypeL = next; next += RoundUp(PathTableBytes, SECTOR) / SECTOR;
                LbaTypeM = next; next += RoundUp(PathTableBytes, SECTOR) / SECTOR;
                if (anyBoot) LbaCat = next++;

                var iso = new List<IsoNode>();
                var jol = new List<IsoNode>();
                void Traverse(IsoNode n)
                {
                    n.IsoSize = ComputeDirSize(n, false);
                    n.JolSize = ComputeDirSize(n, true);
                    foreach (var c in n.Children) Traverse(c);
                    iso.Add(n); jol.Add(n);
                }
                Traverse(Root);

                foreach (var n in iso) { n.IsoLba = next; next += RoundUp(n.IsoSize, SECTOR) / SECTOR; }
                foreach (var n in jol) { n.JolLba = next; next += RoundUp(n.JolSize, SECTOR) / SECTOR; }

                foreach (var e in PathEntries)
                    e.Extent = FindNode(e.DirNum).IsoLba;

                FilesSorted = Files.OrderBy(f => f.IsoName, StringComparer.Ordinal).ToList();
                foreach (var f in FilesSorted) { f.Extent = next; next += RoundUp(f.Size, SECTOR) / SECTOR; }

                IsoDirs = iso; JolDirs = jol;
                TotalBytes = next * SECTOR;
            }

            private IsoNode FindNode(int dirNum)
            {
                var q = new Queue<IsoNode>(); q.Enqueue(Root);
                while (q.Count > 0)
                {
                    var n = q.Dequeue();
                    if (n.DirNum == dirNum) return n;
                    foreach (var c in n.Children) q.Enqueue(c);
                }
                throw new InvalidOperationException("未找到目录 " + dirNum);
            }

            public (long Lba, long Size) GetBootImage(bool legacy)
            {
                var rel = legacy ? BootLegacy : BootEfi;
                if (string.IsNullOrEmpty(rel)) return (0, 0);
                var f = FilesSorted.FirstOrDefault(x => x.IsoPath.Equals(rel, StringComparison.OrdinalIgnoreCase));
                return (f?.Extent ?? 0, f?.Size ?? 0);
            }
        }

        // ==================== 目录尺寸与记录 ====================

        private static long ComputeDirSize(IsoNode node, bool joliet)
        {
            long size = RecLen(joliet ? DotName(true) : DotName(false)) + RecLen(joliet ? DotDotName(true) : DotDotName(false));
            foreach (var c in node.Children)
                size += RecLen(joliet ? Ucs2(c.Name) : Asc(ShortAscii(c.Name, 8)));
            foreach (var f in node.Files)
                size += RecLen(joliet ? Ucs2(f.JolietName) : Asc(f.IsoName));
            return RoundUp(size, SECTOR);
        }

        private static long RecLen(byte[] name)
        {
            int idLen = name.Length;
            return 33 + idLen + (idLen % 2 == 0 ? 1 : 0);
        }

        private static byte[] DotName(bool joliet) => joliet ? new byte[] { 0x00, 0x2E } : new byte[] { 0x00 };
        private static byte[] DotDotName(bool joliet) => joliet ? new byte[] { 0x00, 0x2E, 0x00, 0x2E } : new byte[] { 0x00, 0x00 };

        private static byte[] BuildIsoBlock(IsoNode node, Layout layout)
        {
            var buf = new byte[(int)RoundUp(node.IsoSize, SECTOR)];
            int o = PutRecord(buf, 0, DotName(false), FLAG_DIR, node.IsoLba, node.IsoSize, false);
            var parent = node.Parent ?? node;
            o = PutRecord(buf, o, DotDotName(false), FLAG_DIR, parent.IsoLba, parent.IsoSize, false);
            foreach (var c in node.Children)
                o = PutRecord(buf, o, Asc(ShortAscii(c.Name, 8)), FLAG_DIR, c.IsoLba, c.IsoSize, false);
            foreach (var f in node.Files)
                o = PutRecord(buf, o, Asc(f.IsoName), 0, f.Extent, f.Size, false);
            return buf;
        }

        private static byte[] BuildJolBlock(IsoNode node, Layout layout)
        {
            var buf = new byte[(int)RoundUp(node.JolSize, SECTOR)];
            int o = PutRecord(buf, 0, DotName(true), FLAG_DIR, node.JolLba, node.JolSize, true);
            var parent = node.Parent ?? node;
            o = PutRecord(buf, o, DotDotName(true), FLAG_DIR, parent.JolLba, parent.JolSize, true);
            foreach (var c in node.Children)
                o = PutRecord(buf, o, Ucs2(c.Name), FLAG_DIR, c.JolLba, c.JolSize, true);
            foreach (var f in node.Files)
                o = PutRecord(buf, o, Ucs2(f.JolietName), 0, f.Extent, f.Size, true);
            return buf;
        }

        private static int PutRecord(byte[] buf, int o, byte[] name, byte flags, long extent, long dataLength, bool joliet)
        {
            int idLen = name.Length;
            int len = 33 + idLen + (idLen % 2 == 0 ? 1 : 0);
            buf[o] = (byte)len;
            buf[o + 1] = 0;
            WriteBoth32(buf, o + 2, extent);
            WriteBoth32(buf, o + 10, dataLength);
            WriteDirDate(buf, o + 18);
            buf[o + 25] = flags;
            buf[o + 26] = 0;
            buf[o + 27] = 0;
            WriteBoth16(buf, o + 28, 1);
            buf[o + 32] = (byte)idLen;
            Buffer.BlockCopy(name, 0, buf, o + 33, idLen);
            if (idLen % 2 == 0) buf[o + 33 + idLen] = 0;
            return o + len;
        }

        private static void WriteDirDate(byte[] buf, int o)
        {
            byte[] y = Encoding.ASCII.GetBytes((2024 - 1900).ToString("0000"));
            buf[o] = y[0]; buf[o + 1] = y[1]; buf[o + 2] = y[2]; buf[o + 3] = y[3];
            buf[o + 4] = 1; buf[o + 5] = 1; buf[o + 6] = 0;
        }

        // ==================== 路径表 ====================

        private static byte[] PathTableL(Layout layout) => BuildPathTable(layout, false);
        private static byte[] PathTableM(Layout layout) => BuildPathTable(layout, true);

        private static byte[] BuildPathTable(Layout layout, bool bigEndian)
        {
            var buf = new byte[(int)layout.PathTableBytes];
            int o = 0;
            foreach (var e in layout.PathEntries)
            {
                var ident = Encoding.ASCII.GetBytes(e.Name);
                buf[o] = (byte)ident.Length;
                buf[o + 1] = 0;
                if (bigEndian)
                {
                    WriteU32BE(buf, o + 2, (uint)e.Extent);
                    WriteU16BE(buf, o + 6, (ushort)e.ParentNum);
                }
                else
                {
                    WriteU32LE(buf, o + 2, (uint)e.Extent);
                    WriteU16LE(buf, o + 6, (ushort)e.ParentNum);
                }
                Buffer.BlockCopy(ident, 0, buf, o + 8, ident.Length);
                if (ident.Length % 2 == 1) buf[o + 8 + ident.Length] = 0;
                o += 8 + ident.Length + (ident.Length % 2 == 1 ? 1 : 0);
            }
            return buf;
        }

        // ==================== 主卷描述符 (PVD) ====================

        private static byte[] BuildPvd(Layout layout)
        {
            var b = new byte[SECTOR];
            b[0] = VD_PRIMARY;
            CopyAscii(b, 1, "CD001");
            b[6] = 1;
            CopyAsciiPad(b, 8, "L", 0);            // zero unused
            CopyAsciiPad(b, 40, "", 32);           // system identifier
            CopyAPad(b, 72, layout.Label, 32);     // volume identifier
            CopyAsciiPad(b, 104, "", 8);           // volume space size placeholder (unused 8)
            WriteBoth32(b, 81, layout.TotalBytes / SECTOR);  // volume space size
            CopyAsciiPad(b, 121, "", 32);          // unused
            WriteBoth16(b, 129, 1);                // volume set size
            WriteBoth16(b, 133, 1);                // volume sequence number
            WriteBoth16(b, 137, SECTOR);           // logical block size
            WriteBoth32(b, 141, layout.PathTableBytes);  // path table size
            WriteU32LE(b, 145, (uint)layout.LbaTypeL);          // L path table
            WriteU32LE(b, 149, 0);                             // L path table optional
            WriteU32BE(b, 153, (uint)layout.LbaTypeM);         // M path table
            WriteU32BE(b, 157, 0);                             // M path table optional

            // 根目录记录（可省略）— 填充记录头保证解析
            int ro = 0x9D;
            b[ro] = 34;
            b[ro + 1] = 0;
            WriteBoth32(b, ro + 2, layout.Root.IsoLba);
            WriteBoth32(b, ro + 10, layout.Root.IsoSize);
            WriteDirDate(b, ro + 18);
            b[ro + 25] = FLAG_DIR;
            b[ro + 32] = 1;
            b[ro + 33] = 0;

            CopyAsciiPad(b, 0x128, "", 128);       // volume set identifier
            CopyAsciiPad(b, 0x168, "", 128);       // publisher identifier
            CopyAsciiPad(b, 0x1A8, "", 128);       // data preparer
            CopyAsciiPad(b, 0x1E8, "", 128);       // application identifier
            CopyAsciiPad(b, 0x250, "", 37);        // copyright
            CopyAsciiPad(b, 0x275, "", 37);        // abstract
            CopyAsciiPad(b, 0x29A, "", 37);        // bibliographic
            WriteVolumeDate(b, 0x2BF);
            WriteVolumeDate(b, 0x2D0);
            b[0x2E1] = 0;
            CopyAsciiPad(b, 0x2E3, "", 512);       // application use
            return b;
        }

        // ==================== Joliet 补充卷描述符 (SVD) ====================

        private static byte[] BuildJolietPvd(Layout layout)
        {
            var b = new byte[SECTOR];
            b[0] = VD_SUPPLEMENTARY;
            CopyAscii(b, 1, "CD001");
            b[6] = 1;
            b[7] = 0;                              // flags
            CopyAsciiPad(b, 8, "L", 0);
            CopyAsciiPad(b, 40, "", 32);
            // Joliet 卷标识为 UCS-2BE（与卷描述符记录一致）
            CopyUcs2Pad(b, 72, layout.Label, 32);
            WriteBoth32(b, 81, layout.TotalBytes / SECTOR);
            CopyAsciiPad(b, 121, "", 32);
            WriteBoth16(b, 129, 1);
            WriteBoth16(b, 133, 1);
            WriteBoth16(b, 137, SECTOR);
            WriteBoth32(b, 141, layout.PathTableBytes);
            WriteU32LE(b, 145, (uint)layout.LbaTypeL);
            WriteU32LE(b, 149, 0);
            WriteU32BE(b, 153, (uint)layout.LbaTypeM);
            WriteU32BE(b, 157, 0);

            int ro = 0x9D;
            b[ro] = 34;
            b[ro + 1] = 0;
            WriteBoth32(b, ro + 2, layout.Root.JolLba);
            WriteBoth32(b, ro + 10, layout.Root.JolSize);
            WriteDirDate(b, ro + 18);
            b[ro + 25] = FLAG_DIR;
            b[ro + 32] = 1;
            b[ro + 33] = 0;

            // 版本 escape 序列：Esc1/Esc%/Esc@ => UCS-2
            b[0x58] = 0x25; b[0x59] = 0x2F; b[0x5A] = 0x40;

            WriteVolumeDate(b, 0x2BF);
            WriteVolumeDate(b, 0x2D0);
            b[0x2E1] = 1;                          //文件结构版本
            return b;
        }

        // ==================== 引导卷描述符 & 引导目录 ====================

        private static byte[] BuildBootRecord(Layout layout)
        {
            var b = new byte[SECTOR];
            b[0] = VD_BOOT;
            CopyAscii(b, 1, "CD001");
            b[6] = 1;
            CopyAsciiPad(b, 7, "EL TORITO SPECIFICATION", 32);
            CopyAsciiPad(b, 39, "", 32);
            WriteBoth32(b, 71, layout.LbaCat);
            return b;
        }

        private static byte[] BuildTerminator()
        {
            var b = new byte[SECTOR];
            b[0] = VD_TERMINATOR;
            CopyAscii(b, 1, "CD001");
            b[6] = 1;
            return b;
        }

        /// <summary>
        /// 单个 El Torito 引导目录，含 Legacy 默认条目与 UEFI 段（对齐 oscdimg 双引导规范）：
        /// [0 ] Validation Entry (x86) -> [32] Legacy Boot Entry(默认) -> [64] Section Header(EFI,1)
        /// -> [96] EFI Boot Entry -> [128] 终结 Section Header(x86,0)。
        /// 仅 EFI 时初始条目为指向 EFI 段的 Section Header。
        /// </summary>
        private static byte[] BuildBootCatalog(Layout layout)
        {
            var b = new byte[SECTOR];
            bool hasLegacy = !string.IsNullOrEmpty(layout.BootLegacy);
            bool hasEfi = !string.IsNullOrEmpty(layout.BootEfi);

            // ---- Validation Entry (32) ----
            b[0] = 0x01;
            b[1] = PLATFORM_X86;
            CopyAsciiPad(b, 4, "KLC", 0x1B + 1 - 4);
            b[0x1E] = 0x55;                    // key byte 1
            b[0x1F] = 0xAA;                    // key byte 2
            ushort sum = 0;
            for (int i = 0; i <= 0x1E; i += 2)
            {
                if (i == 0x1C) continue;       // checksum word
                ushort w = (ushort)((b[i + 1] << 8) | b[i]);
                sum = (ushort)((sum + w) & 0xFFFF);
            }
            ushort checksum = (ushort)(0x10000 - sum);
            b[0x1C] = (byte)(checksum & 0xFF);
            b[0x1D] = (byte)((checksum >> 8) & 0xFF);

            int o = 32;
            if (hasLegacy)
            {
                // 初始/默认条目：Legacy 引导条目
                PutBootEntry(b, o, legacy: true, layout); o += 32;
                if (hasEfi)
                {
                    // EFI 段头 + EFI 引导条目
                    PutSectionHeader(b, o, PLATFORM_EFI, 1); o += 32;
                    PutBootEntry(b, o, legacy: false, layout); o += 32;
                }
                // 终结段头
                PutSectionHeader(b, o, PLATFORM_X86, 0);
            }
            else if (hasEfi)
            {
                // 仅 UEFI：初始条目为指向 EFI 段的 Section Header
                PutSectionHeader(b, o, PLATFORM_EFI, 1); o += 32;
                PutBootEntry(b, o, legacy: false, layout); o += 32;
                PutSectionHeader(b, o, PLATFORM_X86, 0);
            }
            return b;
        }

        private static void PutSectionHeader(byte[] b, int o, byte platform, int count)
        {
            b[o] = 0x91;                       // header indicator（后续段）
            b[o + 1] = platform;
            b[o + 2] = (byte)count;
            b[o + 3] = 0;
            CopyAsciiPad(b, o + 4, "KLC", 0x20 - 4);
        }

        private static void PutBootEntry(byte[] b, int o, bool legacy, Layout layout)
        {
            b[o] = BOOTABLE;                   // boot indicator
            b[o + 1] = MEDIA_NO_EMU;
            if (legacy)
            {
                WriteU16LE(b, o + 2, 0x07C0);  // load segment
                b[o + 4] = 0;                  // system type
                WriteU16LE(b, o + 6, 4);       // sector count
            }
            else
            {
                WriteU16LE(b, o + 2, 0);       // load segment 0
                b[o + 4] = 0xEF;               // EFI system type
                WriteU16LE(b, o + 6, 4);       // sector count
            }
            var (lba, _) = layout.GetBootImage(legacy);
            WriteU32LE(b, o + 8, (uint)lba);   // load RBA
        }

        // ==================== 名称清洗 / 编码辅助 ====================

        /// <summary>清洗为合法 ISO-9660 文件名：全大写，仅保留 [A-Z0-9_.]，一级点号，最长 30 字节（Level 2）。</summary>
        private static string SanitizeIsoName(string name)
        {
            name = name.ToUpperInvariant();
            var sb = new StringBuilder();
            int dots = 0;
            foreach (var ch in name)
            {
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')
                {
                    if (sb.Length < 30) sb.Append(ch);
                }
                else if (ch == '.' && dots == 0)
                {
                    sb.Append('.');
                    dots++;
                }
            }
            if (sb.Length == 0) return "FILE";
            var s = sb.ToString().TrimStart('.').TrimEnd('.');
            if (s.Length == 0) return "FILE";
            if (s.Length > 30) s = s[..30];
            return s;
        }

        /// <summary>短名（目录/路径表）：大写、保留数字字母下划线点，最多 8 字节。</summary>
        private static string ShortAscii(string name, int max)
        {
            var sb = new StringBuilder();
            foreach (var ch in name.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
                {
                    if (sb.Length >= max) break;
                    sb.Append(ch);
                }
            }
            if (sb.Length == 0) sb.Append('_');
            return sb.ToString();
        }

        private static byte[] Asc(string s) => Encoding.ASCII.GetBytes(s);

        /// <summary>UCS-2 大端编码（Joliet 命名）。</summary>
        private static byte[] Ucs2(string s)
        {
            var bytes = new byte[s.Length * 2];
            for (int i = 0; i < s.Length; i++)
            {
                bytes[i * 2] = (byte)((s[i] >> 8) & 0xFF);
                bytes[i * 2 + 1] = (byte)(s[i] & 0xFF);
            }
            return bytes;
        }

        // ==================== 字段写入辅助 ====================

        private static long RoundUp(long v, long align) => (v + align - 1) / align * align;

        private static void CopyAscii(byte[] b, int offset, string s)
        {
            var e = Encoding.ASCII.GetBytes(s);
            Buffer.BlockCopy(e, 0, b, offset, Math.Min(e.Length, b.Length - offset));
        }

        private static void CopyAsciiPad(byte[] b, int offset, string s, int len)
        {
            for (int i = 0; i < len; i++)
                b[offset + i] = i < s.Length ? (byte)s[i] : (byte)0x20;
        }

        private static void CopyAPad(byte[] b, int offset, string s, int len)
        {
            var upper = s.ToUpperInvariant();
            for (int i = 0; i < len; i++)
            {
                char c = i < upper.Length ? upper[i] : ' ';
                c = (c >= ' ' && c <= '~') ? c : '_';
                b[offset + i] = (byte)c;
            }
        }

        private static void CopyUcs2Pad(byte[] b, int offset, string s, int byteLen)
        {
            int chars = byteLen / 2;
            for (int i = 0; i < chars; i++)
            {
                char c = i < s.Length ? s[i] : ' ';
                b[offset + i * 2] = (byte)((c >> 8) & 0xFF);
                b[offset + i * 2 + 1] = (byte)(c & 0xFF);
            }
        }

        private static void WriteVolumeDate(byte[] b, int offset)
        {
            var d = DateTime.Now;
            string s = d.ToString("yyyyMMddHHmmss") + "00";   // 16 digits
            CopyAscii(b, offset, s);
            b[offset + 16] = 0;                              // GMT offset
        }

        private static void WriteBoth32(byte[] b, int o, long v)
        {
            WriteU32LE(b, o, (uint)v);
            WriteU32BE(b, o + 4, (uint)v);
        }

        private static void WriteBoth16(byte[] b, int o, int v)
        {
            WriteU16LE(b, o, (ushort)v);
            WriteU16BE(b, o + 2, (ushort)v);
        }

        private static void WriteU32LE(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o, 4), v);
        private static void WriteU32BE(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o, 4), v);
        private static void WriteU16LE(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o, 2), v);
        private static void WriteU16BE(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(o, 2), v);

        private static string Label19(string label)
        {
            var sb = new StringBuilder();
            foreach (var ch in label.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                {
                    if (sb.Length >= 32) break;
                    sb.Append(ch);
                }
            }
            if (sb.Length == 0) sb.Append('Z');
            return sb.ToString();
        }
    }
}