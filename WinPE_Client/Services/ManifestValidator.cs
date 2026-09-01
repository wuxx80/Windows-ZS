using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace WinPE_Client.Services
{
    /// <summary>
    /// zs_manifest.key 校验器（对应设计文档 §2.2 + §6.6）。
    /// manifest 格式：每行 `sha256=哈希=相对路径`，例如：
    ///   sha256=906A28A3...=boot.wim
    ///   sha256=966D29A3...=boot.sdi
    /// PE 端装机第一条命令就是遍历这个表，逐个 SHA-256 校验；
    /// 任何一行对不上 → 终止部署，报"文件损坏请重新下单"。
    /// </summary>
    public static class ManifestValidator
    {
        /// <summary>解析结果：单条 manifest 记录</summary>
        public class ManifestEntry
        {
            public string ExpectedHash { get; set; } = "";
            public string RelativePath { get; set; } = "";
        }

        /// <summary>校验结果：单条文件的校验状态</summary>
        public class VerifyResult
        {
            public string RelativePath { get; set; } = "";
            public string ExpectedHash { get; set; } = "";
            public string? ActualHash { get; set; }
            public bool FileExists { get; set; }
            public bool HashMatch { get; set; }
            public string? Error { get; set; }
            public bool Pass => FileExists && HashMatch && Error == null;
        }

        /// <summary>整体校验报告</summary>
        public class VerifyReport
        {
            public int Total { get; set; }
            public int Passed { get; set; }
            public int Failed { get; set; }
            public List<VerifyResult> Results { get; set; } = new();
            public bool AllPass => Failed == 0 && Total > 0;
        }

        /// <summary>解析 zs_manifest.key 为条目列表；解析失败的行会被跳过</summary>
        public static List<ManifestEntry> Parse(string manifestPath)
        {
            var list = new List<ManifestEntry>();
            if (!File.Exists(manifestPath)) return list;

            // manifest 设计要求 UTF-8；兼容 BOM
            string[] lines;
            try { lines = File.ReadAllLines(manifestPath, new System.Text.UTF8Encoding(false)); }
            catch { return list; }

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[")) continue; // [zs_manifest_v1] section header

                // 期望格式: sha256=HASH=relative_path
                // 但 HASH 本身不含 = 号，所以分割成三段
                var parts = line.Split('=');
                if (parts.Length < 3) continue;
                if (parts[0].Trim().ToLowerInvariant() != "sha256") continue;
                var hash = parts[1].Trim().ToUpperInvariant();
                // relative path 可能本身含 =（理论上极少），把第 3 段起的合并
                var rel = string.Join("=", parts, 2, parts.Length - 2).Trim();
                if (hash.Length < 32 || rel.Length == 0) continue;

                list.Add(new ManifestEntry { ExpectedHash = hash, RelativePath = rel });
            }

            return list;
        }

        /// <summary>
        /// 逐行校验 manifest：对每个条目计算文件实际 SHA-256，对比期望值。
        /// basePath = ZS_Task/ 根目录（manifest 文件所在目录）。
        /// </summary>
        public static VerifyReport Verify(string manifestPath, string? basePath = null)
        {
            var report = new VerifyReport();
            var entries = Parse(manifestPath);
            report.Total = entries.Count;

            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrEmpty(basePath)) basePath = Directory.GetCurrentDirectory();
            }
            basePath = basePath.TrimEnd('\\', '/') + "\\";

            foreach (var e in entries)
            {
                var r = new VerifyResult
                {
                    RelativePath = e.RelativePath,
                    ExpectedHash = e.ExpectedHash
                };

                try
                {
                    var abs = Path.Combine(basePath, e.RelativePath);
                    r.FileExists = File.Exists(abs);
                    if (!r.FileExists)
                    {
                        r.Error = "file not found";
                        report.Results.Add(r);
                        report.Failed++;
                        continue;
                    }

                    r.ActualHash = ComputeSha256(abs);
                    r.HashMatch = string.Equals(r.ActualHash, e.ExpectedHash, StringComparison.OrdinalIgnoreCase);
                    if (!r.HashMatch)
                    {
                        r.Error = "hash mismatch";
                        report.Failed++;
                    }
                    else report.Passed++;
                }
                catch (Exception ex)
                {
                    r.Error = ex.Message;
                    report.Failed++;
                }

                report.Results.Add(r);
            }

            return report;
        }

        /// <summary>计算文件 SHA-256（大写 hex）</summary>
        public static string ComputeSha256(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fs)).ToUpperInvariant();
        }
    }
}
