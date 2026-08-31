using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Windows_Client.Models;

namespace Windows_Client.Services
{
    /// <summary>工具大全服务：本地清单加载 / exe 校验 / 运行提权 / 服务器同步 / 下载</summary>
    public class ToolService
    {
        private readonly string _baseDir;
        private readonly string _toolsRoot;
        private readonly string _cacheDir;

        public ToolService(string baseDir)
        {
            _baseDir = baseDir;
            _toolsRoot = Path.Combine(baseDir, "Tools");
            _cacheDir = Path.Combine(baseDir, "ZS_Cache", "tools");
            try { Directory.CreateDirectory(_cacheDir); } catch { }
        }

        /// <summary>读取内置工具清单 tools.json（53 项 10 分类）</summary>
        public List<ToolInfo> LoadLocalTools()
        {
            var list = new List<ToolInfo>();
            try
            {
                var jsonPath = Path.Combine(_baseDir, "Data", "Tools", "tools.json");
                if (!File.Exists(jsonPath))
                    jsonPath = Path.Combine(_baseDir, "tools.json");
                if (!File.Exists(jsonPath)) return list;

                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (!doc.RootElement.TryGetProperty("tools", out var tools)) return list;
                foreach (var t in tools.EnumerateArray())
                {
                    var tool = new ToolInfo
                    {
                        Id = t.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        Name = GetStr(t, "name"),
                        Category = GetStr(t, "category"),
                        Icon = GetStr(t, "icon"),
                        ExePath = GetStr(t, "exe"),
                        Args = GetStr(t, "args"),
                        Description = GetStr(t, "description"),
                        NeedAdmin = t.TryGetProperty("need_admin", out var na) && na.GetBoolean(),
                        Source = "local",
                    };
                    tool.FullPath = ResolveFullPath(tool.ExePath);
                    tool.Status = File.Exists(tool.FullPath) ? "ready" : "missing";
                    list.Add(tool);
                }
            }
            catch { }
            return list;
        }

        /// <summary>读取分类（含「全部」前置项）</summary>
        public List<ToolCategory> LoadCategories()
        {
            var list = new List<ToolCategory>();
            try
            {
                var jsonPath = Path.Combine(_baseDir, "Data", "Tools", "tools.json");
                if (!File.Exists(jsonPath))
                    jsonPath = Path.Combine(_baseDir, "tools.json");
                if (!File.Exists(jsonPath)) return list;

                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (!doc.RootElement.TryGetProperty("categories", out var cats)) return list;
                foreach (var c in cats.EnumerateArray())
                {
                    list.Add(new ToolCategory
                    {
                        Key = GetStr(c, "key"),
                        Name = GetStr(c, "name"),
                        Icon = GetStr(c, "icon"),
                    });
                }
            }
            catch { }
            return list;
        }

        /// <summary>运行工具（NeedAdmin → 提权；cmd/批处理走 cmd.exe）</summary>
        public string Run(ToolInfo tool)
        {
            var path = tool.FullPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "工具文件不存在: " + path;

            try
            {
                var psi = new ProcessStartInfo();
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".cmd" || ext == ".bat")
                {
                    psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    psi.Arguments = "/c \"\"" + path + "\"" + (string.IsNullOrEmpty(tool.Args) ? "" : " " + tool.Args) + "\"";
                    psi.WorkingDirectory = Path.GetDirectoryName(path)!;
                    psi.UseShellExecute = true;
                }
                else
                {
                    psi.FileName = path;
                    psi.Arguments = tool.Args;
                    psi.WorkingDirectory = Path.GetDirectoryName(path)!;
                    psi.UseShellExecute = true;
                }
                if (tool.NeedAdmin)
                    psi.Verb = "runas";

                var p = Process.Start(psi);
                if (p == null) return "启动失败";
                return "";
            }
            catch (Exception ex)
            {
                // 提权被取消 / 权限不足时给出明确提示
                return "启动失败: " + ex.Message;
            }
        }

        /// <summary>打开工具所在目录（不存在则创建，引导用户放置绿色工具）</summary>
        public string OpenDirectory(ToolInfo tool)
        {
            try
            {
                var dir = string.IsNullOrEmpty(tool.FullPath)
                    ? Path.Combine(_toolsRoot, tool.Category)
                    : Path.GetDirectoryName(tool.FullPath);
                if (string.IsNullOrEmpty(dir)) return "无法确定工具目录";
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                return "";
            }
            catch (Exception ex)
            {
                return "打开目录失败: " + ex.Message;
            }
        }

        /// <summary>服务器工具下载到本地缓存（带进度）</summary>
        public async Task<(bool Ok, string Error)> DownloadAsync(
            ToolInfo tool, string url, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                var fileName = (tool.Name.Length > 0 ? tool.Name : "tool") + Path.GetExtension(Path.GetFileName(new Uri(url).AbsolutePath)) ;
                if (string.IsNullOrEmpty(Path.GetExtension(fileName))) fileName += ".exe";
                fileName = SafeFileName(fileName);
                var savePath = Path.Combine(_cacheDir, fileName);

                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                    return (false, "下载失败 HTTP " + (int)response.StatusCode);
                var total = response.Content.Headers.ContentLength ?? 0;
                var tmp = savePath + ".part";
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var stream = await response.Content.ReadAsStreamAsync(ct))
                {
                    var buffer = new byte[1024 * 256];
                    long written = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                        written += read;
                        progress?.Report(total > 0 ? (int)(written * 100 / total) : 0);
                    }
                }
                File.Move(tmp, savePath, true);
                tool.FullPath = savePath;
                tool.Status = "downloaded";
                return (true, "");
            }
            catch (OperationCanceledException)
            {
                return (false, "已取消下载");
            }
            catch (Exception ex)
            {
                return (false, "下载失败: " + ex.Message);
            }
        }

        /// <summary>缓存目录（U盘制作「写入内置工具」联动）</summary>
        public string CacheDir => _cacheDir;

        private string ResolveFullPath(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return "";
            if (Path.IsPathRooted(exe)) return exe;
            return Path.Combine(_baseDir, exe.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetStr(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}