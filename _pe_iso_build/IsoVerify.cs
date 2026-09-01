using System;
using System.IO;
using System.Text;

class IsoVerify
{
    const int SECTOR = 2048;
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("usage: PeIsoBuild verify|dump <iso>");
            return 1;
        }
        if (args[0] == "verify")
        {
            if (args.Length < 2) { Console.WriteLine("usage: PeIsoBuild verify <iso>"); return 1; }
            return Verify(args[1]);
        }
        if (args[0] == "dump")
        {
            if (args.Length < 2) { Console.WriteLine("usage: PeIsoBuild dump <iso>"); return 1; }
            return Dump(args[1]);
        }
        return Verify(args[0]);
    }

    static int Dump(string path)
    {
        using var fs = File.OpenRead(path);
        var buf = new byte[SECTOR];
        Console.WriteLine("=== " + path + " (" + fs.Length + " bytes, " + (fs.Length / SECTOR) + " sectors) ===");
        for (int lba = 16; lba < 23 && lba < fs.Length / SECTOR; lba++)
        {
            fs.Position = (long)lba * SECTOR;
            fs.Read(buf, 0, SECTOR);
            Console.WriteLine("--- LBA " + lba + " (first 200 bytes) ---");
            HexDump(buf, 200);
        }
        return 0;
    }

    static void HexDump(byte[] b, int n)
    {
        for (int row = 0; row < n; row += 16)
        {
            var hex = new StringBuilder();
            var asc = new StringBuilder();
            for (int i = 0; i < 16 && row + i < n; i++)
            {
                hex.Append(b[row + i].ToString("X2")).Append(' ');
                char c = (char)b[row + i];
                asc.Append(c >= 0x20 && c < 0x7F ? c : '.');
            }
            Console.WriteLine("  " + row.ToString("X3") + ": " + hex.ToString().PadRight(48) + " |" + asc + "|");
        }
    }

    static int Verify(string path)
    {
        using var fs = File.OpenRead(path);
        Console.WriteLine("file: " + path + " (" + fs.Length + " bytes)");
        long totalSectors = fs.Length / SECTOR;
        Console.WriteLine("totalSectors: " + totalSectors);
        var buf = new byte[SECTOR];
        Console.WriteLine("--- volume descriptors ---");
        int pvdLba = -1, svdLba = -1, brvLba = -1;
        for (int lba = 16; lba < 40; lba++)
        {
            fs.Position = (long)lba * SECTOR;
            fs.Read(buf, 0, SECTOR);
            byte t = buf[0];
            if (t == 1) { pvdLba = lba; Console.WriteLine("  LBA " + lba + ": PVD"); }
            else if (t == 0) { brvLba = lba; Console.WriteLine("  LBA " + lba + ": BootRecord cat=" + BitConverter.ToUInt32(buf, 71)); }
            else if (t == 2) { svdLba = lba; Console.WriteLine("  LBA " + lba + ": SVD(joliet)"); }
            else if (t == 255) { Console.WriteLine("  LBA " + lba + ": TERMINATOR"); break; }
            else { Console.WriteLine("  LBA " + lba + ": type " + t); break; }
        }
        if (pvdLba < 0) { Console.WriteLine("!! no PVD"); return 2; }
        fs.Position = (long)pvdLba * SECTOR;
        fs.Read(buf, 0, SECTOR);
        uint volSpace = BitConverter.ToUInt32(buf, 80);
        uint lpt = BitConverter.ToUInt32(buf, 140);
        uint mpt = (uint)((buf[148] << 24) | (buf[149] << 16) | (buf[150] << 8) | buf[151]);
        uint ptSize = BitConverter.ToUInt32(buf, 132);
        uint rootExtent = BitConverter.ToUInt32(buf, 158);
        uint rootLen = BitConverter.ToUInt32(buf, 166);
        Console.WriteLine("--- PVD ---");
        Console.WriteLine("  volSpace=" + volSpace + " fileSectors=" + totalSectors);
        Console.WriteLine("  ptSize=" + ptSize + " LPT=" + lpt + " MPT(BE)=" + mpt);
        Console.WriteLine("  rootExtent=" + rootExtent + " rootLen=" + rootLen);
        Console.WriteLine("  label='" + ASCII(buf, 40, 32).TrimEnd(' ') + "'");
        if (lpt < totalSectors)
        {
            Console.WriteLine("--- L path table @LBA " + lpt + " ---");
            long ptBytes = Math.Min(ptSize, (totalSectors - lpt) * SECTOR);
            fs.Position = (long)lpt * SECTOR;
            var pt = new byte[ptBytes];
            fs.Read(pt, 0, (int)ptBytes);
            int o = 0, cnt = 0;
            while (o + 8 <= ptBytes && cnt < 50)
            {
                int idLen = pt[o];
                uint ext = BitConverter.ToUInt32(pt, o + 2);
                ushort parent = BitConverter.ToUInt16(pt, o + 6);
                string name = idLen > 0 ? ASCII(pt, o + 8, idLen) : "(root)";
                Console.WriteLine("  " + name.PadRight(10) + " ext=" + ext + " parent=" + parent);
                int recSize = 8 + idLen + (idLen % 2 == 1 ? 1 : 0);
                if (recSize <= 0) break;
                o += recSize; cnt++;
            }
        }
        if (rootExtent < totalSectors)
        {
            Console.WriteLine("--- root dir @LBA " + rootExtent + " ---");
            long dirBytes = Math.Min(rootLen, (totalSectors - rootExtent) * SECTOR);
            fs.Position = (long)rootExtent * SECTOR;
            var dir = new byte[dirBytes];
            fs.Read(dir, 0, (int)dirBytes);
            int doff = 0, dcount = 0;
            while (doff + 34 <= dirBytes && dcount < 60)
            {
                int rl = dir[doff];
                if (rl == 0) { Console.WriteLine("  (end)"); break; }
                if (rl < 34) { Console.WriteLine("  !! bad recLen " + rl + " @ " + doff); break; }
                uint ext = BitConverter.ToUInt32(dir, doff + 2);
                uint len = BitConverter.ToUInt32(dir, doff + 10);
                byte flags = dir[doff + 25];
                int idLen = dir[doff + 32];
                string name = idLen > 0 ? ASCII(dir, doff + 33, idLen) : "(root)";
                string t = (flags & 2) != 0 ? "[DIR]" : "[FILE]";
                Console.WriteLine("  " + t + " " + name.PadRight(14) + " ext=" + ext + " len=" + len);
                doff += rl; dcount++;
            }
        }
        Console.WriteLine("== VERIFY OK ==");
        return 0;
    }
    static string ASCII(byte[] b, int o, int len)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < len; i++) { char c = (char)b[o + i]; sb.Append(c >= 0x20 && c < 0x7F ? c : '.'); }
        return sb.ToString();
    }
}