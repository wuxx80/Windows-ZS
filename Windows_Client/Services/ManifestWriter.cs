using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Windows_Client.Services
{
    /// <summary>
    /// zs_manifest.key 校验清单生成器（对应设计文档 §2.2 / §3.1 步骤 5）。
    /// 格式与 PE 端 ManifestValidator 解析契约对齐：
    ///   [zs_manifest_v1]
    ///   sha256=大写HEX=相对路径
    /// PE 端装机第一条命令就是遍历此清单逐个 SHA-256 校验，
    /// 任何一行对不上 → Exit(1) 报"文件损坏请重新下单"。
    /// </summary>
    public static class ManifestWriter
    {
        /// <summary>
        /// 扫描 baseDir 下所有文件（含子目录），计算 SHA-256，写入 manifestPath。
        /// 自动排除 manifest 自身、pe_log.txt（PE 阶段产物）、.part 临时文件。
        /// </summary>
        public static bool Write(string baseDir, string manifestPath)
        {
            try
            {
                var entries = ScanAndHash(baseDir, manifestPath);
                return WriteEntries(manifestPath, entries);
            }
            catch { return false; }
        }

        /// <summary>对指定相对路径文件列表计算 SHA-256 并写入 manifest</summary>
        public static bool WriteFiles(string baseDir, string manifestPath, IEnumerable<string> relativeFiles)
        {
            try
            {
                var entries = new List<(string Hash, string Rel)>();
                foreach (var rel in relativeFiles)
                {
                    var abs = Path.Combine(baseDir, rel);
                    if (!File.Exists(abs)) continue;
                    entries.Add((ComputeSha256(abs), rel.Replace('\\', '/')));
                }
                return WriteEntries(manifestPath, entries);
            }
            catch { return false; }
        }

        /// <summary>扫描 baseDir 下所有文件并计算哈希</summary>
        private static List<(string Hash, string Rel)> ScanAndHash(string baseDir, string manifestPath)
        {
            var result = new List<(string Hash, string Rel)>();
            var manifestName = Path.GetFileName(manifestPath);
            baseDir = baseDir.TrimEnd('\\', '/') + "\\";

            foreach (var file in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                // 排除 manifest 自身、PE 阶段日志、.part 下载临时文件
                if (name.Equals(manifestName, StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("pe_log.txt", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) continue;

                var rel = Path.GetRelativePath(baseDir, file).Replace('\\', '/');
                result.Add((ComputeSha256(file), rel));
            }

            // 按相对路径排序，便于审计
            result.Sort((a, b) => string.Compare(a.Rel, b.Rel, StringComparison.Ordinal));
            return result;
        }

        /// <summary>把哈希条目列表写入 manifest 文件（UTF-8 无 BOM）</summary>
        private static bool WriteEntries(string manifestPath, List<(string Hash, string Rel)> entries)
        {
            var dir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder(1024 + entries.Count * 80);
            sb.AppendLine("[zs_manifest_v1]");
            foreach (var e in entries)
                sb.AppendLine("sha256=" + e.Hash + "=" + e.Rel);

            File.WriteAllText(manifestPath, sb.ToString(), new UTF8Encoding(false));
            return true;
        }

        /// <summary>计算文件 SHA-256（大写 hex，与 ManifestValidator 对齐）</summary>
        public static string ComputeSha256(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fs)).ToUpperInvariant();
        }
    }
}
