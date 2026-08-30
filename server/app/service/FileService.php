<?php
namespace app\service;

use think\facade\Cache;

class FileService
{
    /**
     * 单文件上传
     */
    public static function upload($file, string $dir = "images"): array
    {
        if (!$file || !$file->isValid()) {
            throw new \Exception("无效的上传文件");
        }

        $ext = strtolower($file->extension());
        $allowed = config("upload.extensions", []);
        if (!in_array($ext, $allowed)) {
            throw new \Exception("文件类型不允许: " . $ext . "，允许的类型: " . implode(", ", $allowed));
        }

        $maxSize = config("upload.max_size", 21474836480);
        if ($file->getSize() > $maxSize) {
            $maxHuman = self::formatBytes($maxSize);
            throw new \Exception("文件大小超出限制（最大 " . $maxHuman . "）");
        }

        $savePath = config("upload.path", runtime_path() . "uploads") . "/" . $dir;
        if (!is_dir($savePath)) {
            if (!mkdir($savePath, 0755, true)) {
                throw new \Exception("无法创建上传目录: " . $savePath);
            }
        }

        $fileName = date("Ymd_His_") . uniqid() . "." . $ext;
        $filePath = $savePath . "/" . $fileName;

        $file->move($savePath, $fileName);
        if (!file_exists($filePath)) {
            throw new \Exception("文件保存失败");
        }

        $hash = hash_file("sha256", $filePath);
        $mime = mime_content_type($filePath);

        return [
            "file_name"  => $file->getOriginalName(),
            "file_path"  => str_replace("\\", "/", $filePath),
            "file_size"  => $file->getSize(),
            "file_hash"  => $hash,
            "extension"  => $ext,
            "mime_type"  => $mime,
        ];
    }

    /**
     * 分片上传
     */
    public static function uploadChunk($file, string $identifier, int $chunkNumber, int $totalChunks, string $dir = "images"): array
    {
        $tmpDir = config("upload.path", runtime_path() . "uploads") . "/" . $dir . "/tmp/" . $identifier;
        if (!is_dir($tmpDir)) {
            if (!mkdir($tmpDir, 0755, true)) {
                throw new \Exception("无法创建分片临时目录");
            }
        }

        $chunkFile = $tmpDir . "/" . $chunkNumber;
        $content = file_get_contents($file->getPathname());
        if ($content === false) {
            throw new \Exception("读取分片内容失败");
        }
        if (file_put_contents($chunkFile, $content) === false) {
            throw new \Exception("写入分片文件失败");
        }

        $received = count(glob($tmpDir . "/*"));
        $done = $received >= $totalChunks;

        return [
            "received" => $received,
            "total"    => $totalChunks,
            "done"     => $done,
        ];
    }

    /**
     * 合并分片
     */
    public static function mergeChunks(string $identifier, string $originalName, string $dir = "images"): array
    {
        $tmpDir = config("upload.path", runtime_path() . "uploads") . "/" . $dir . "/tmp/" . $identifier;
        if (!is_dir($tmpDir)) {
            throw new \Exception("分片临时目录不存在");
        }

        $ext = strtolower(pathinfo($originalName, PATHINFO_EXTENSION));
        $fileName = date("Ymd_His_") . uniqid() . "." . $ext;
        $saveDir = config("upload.path", runtime_path() . "uploads") . "/" . $dir;
        if (!is_dir($saveDir)) {
            mkdir($saveDir, 0755, true);
        }
        $savePath = $saveDir . "/" . $fileName;

        $chunks = glob($tmpDir . "/*");
        if (empty($chunks)) {
            throw new \Exception("没有找到分片文件");
        }

        sort($chunks, SORT_NATURAL);
        $totalSize = 0;
        $out = fopen($savePath, "wb");
        if (!$out) {
            throw new \Exception("无法创建合并文件");
        }

        try {
            foreach ($chunks as $chunk) {
                $data = file_get_contents($chunk);
                if ($data === false) {
                    throw new \Exception("读取分片失败: " . $chunk);
                }
                fwrite($out, $data);
                $totalSize += strlen($data);
                @unlink($chunk);
            }
        } finally {
            fclose($out);
        }

        @rmdir($tmpDir);

        if (!file_exists($savePath)) {
            throw new \Exception("合并文件失败");
        }

        $hash = hash_file("sha256", $savePath);
        $mime = mime_content_type($savePath);

        return [
            "file_name"  => $originalName,
            "file_path"  => str_replace("\\", "/", $savePath),
            "file_size"  => $totalSize,
            "file_hash"  => $hash,
            "extension"  => $ext,
            "mime_type"  => $mime,
        ];
    }

    /**
     * 删除文件
     */
    public static function delete(string $filePath): bool
    {
        if (empty($filePath)) return true;
        // 支持相对路径和绝对路径
        $fullPath = $filePath;
        if (!file_exists($fullPath)) {
            $fullPath = config("upload.path", runtime_path() . "uploads") . "/" . ltrim($filePath, "/");
        }
        if (file_exists($fullPath) && is_file($fullPath)) {
            return @unlink($fullPath);
        }
        return true;
    }

    /**
     * 下载文件（支持断点续传和速度限制）
     */
    public static function download(string $filePath, string $fileName = "", int $speedLimit = 0): void
    {
        if (!file_exists($filePath)) {
            throw new \Exception("文件不存在");
        }

        $fileSize = filesize($filePath);
        if (!$fileName) {
            $fileName = basename($filePath);
        }

        $fp = fopen($filePath, "rb");
        if (!$fp) {
            throw new \Exception("无法读取文件");
        }

        try {
            $etag = md5_file($filePath);
            $lastModified = gmdate("D, d M Y H:i:s", filemtime($filePath)) . " GMT";

            header("Content-Type: application/octet-stream");
            header("Content-Disposition: attachment; filename=\"" . $fileName . "\"");
            header("Content-Length: " . $fileSize);
            header("ETag: \"" . $etag . "\"");
            header("Last-Modified: " . $lastModified);
            header("Accept-Ranges: bytes");
            header("Cache-Control: no-cache");
            header("Pragma: no-cache");

            // 断点续传支持
            $httpRange = $_SERVER["HTTP_RANGE"] ?? "";
            if ($httpRange && preg_match("/bytes=(\d+)-(\d*)/", $httpRange, $matches)) {
                $start = intval($matches[1]);
                $end = $matches[2] !== "" ? intval($matches[2]) : $fileSize - 1;
                if ($start > $end || $start >= $fileSize) {
                    header("HTTP/1.1 416 Range Not Satisfiable");
                    header("Content-Range: bytes */" . $fileSize);
                    return;
                }
                fseek($fp, $start);
                header("HTTP/1.1 206 Partial Content");
                header("Content-Range: bytes " . $start . "-" . $end . "/" . $fileSize);
                header("Content-Length: " . ($end - $start + 1));
                $fileSize = $end - $start + 1;
            }

            // 速度限制（字节/秒）
            $chunkSize = $speedLimit > 0 ? $speedLimit : 1048576; // 默认1MB

            $sent = 0;
            while ($sent < $fileSize && !connection_aborted()) {
                $readSize = min($chunkSize, $fileSize - $sent);
                $data = fread($fp, $readSize);
                if ($data === false) break;
                echo $data;
                flush();
                $sent += strlen($data);
                if ($speedLimit > 0) {
                    usleep(1000000); // 1秒
                }
            }
        } finally {
            fclose($fp);
        }
    }

    /**
     * 获取磁盘使用情况
     */
    public static function getDiskUsage(): array
    {
        $path = config("upload.path", runtime_path() . "uploads");
        if (!is_dir($path)) {
            mkdir($path, 0755, true);
        }
        $total = @disk_total_space($path);
        $free = @disk_free_space($path);
        $total = $total ?: 0;
        $free = $free ?: 0;
        $used = $total - $free;

        return [
            "total"   => self::formatBytes($total),
            "used"    => self::formatBytes($used),
            "free"    => self::formatBytes($free),
            "percent" => $total > 0 ? round(($used / $total) * 100, 1) : 0,
            "bytes_total" => $total,
            "bytes_free"  => $free,
            "bytes_used"  => $used,
        ];
    }

    /**
     * 清理临时文件（超过指定时间的分片）
     */
    public static function cleanTempFiles(int $expireSeconds = 86400): int
    {
        $uploadPath = config("upload.path", runtime_path() . "uploads");
        $count = 0;
        $now = time();

        foreach (["images", "software", "drivers", "others"] as $dir) {
            $tmpDir = $uploadPath . "/" . $dir . "/tmp";
            if (!is_dir($tmpDir)) continue;
            $items = new \RecursiveIteratorIterator(
                new \RecursiveDirectoryIterator($tmpDir, \RecursiveDirectoryIterator::SKIP_DOTS),
                \RecursiveIteratorIterator::CHILD_FIRST
            );
            foreach ($items as $item) {
                if ($item->isFile() && ($now - $item->getMTime()) > $expireSeconds) {
                    @unlink($item->getPathname());
                    $count++;
                }
            }
            // 清理空目录
            $dirs = new \RecursiveIteratorIterator(
                new \RecursiveDirectoryIterator($tmpDir, \RecursiveDirectoryIterator::SKIP_DOTS),
                \RecursiveIteratorIterator::CHILD_FIRST
            );
            foreach ($dirs as $d) {
                if ($d->isDir()) {
                    @rmdir($d->getPathname());
                }
            }
        }
        return $count;
    }

    /**
     * 验证文件哈希
     */
    public static function verifyHash(string $filePath, string $expectedHash, string $algo = "sha256"): bool
    {
        if (!file_exists($filePath)) return false;
        $actualHash = hash_file($algo, $filePath);
        return strtolower($actualHash) === strtolower($expectedHash);
    }

    /**
     * 获取文件信息
     */
    public static function getFileInfo(string $filePath): array
    {
        if (!file_exists($filePath)) {
            throw new \Exception("文件不存在");
        }
        return [
            "name"      => basename($filePath),
            "path"      => str_replace("\\", "/", $filePath),
            "size"      => filesize($filePath),
            "size_human" => self::formatBytes(filesize($filePath)),
            "ext"       => strtolower(pathinfo($filePath, PATHINFO_EXTENSION)),
            "mime"      => mime_content_type($filePath) ?: "application/octet-stream",
            "sha256"    => hash_file("sha256", $filePath),
            "md5"       => hash_file("md5", $filePath),
            "modified"  => filemtime($filePath),
            "is_readable" => is_readable($filePath),
        ];
    }

    /**
     * 格式化字节大小
     */
    public static function formatBytes(int $bytes): string
    {
        if ($bytes >= 1099511627776) return round($bytes / 1099511627776, 2) . " TB";
        if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . " GB";
        if ($bytes >= 1048576) return round($bytes / 1048576, 2) . " MB";
        if ($bytes >= 1024) return round($bytes / 1024, 2) . " KB";
        return $bytes . " B";
    }

    /**
     * 获取上传路径
     */
    public static function getUploadPath(string $subDir = ""): string
    {
        $base = config("upload.path", runtime_path() . "uploads");
        if ($subDir) {
            $base .= "/" . ltrim($subDir, "/");
        }
        if (!is_dir($base)) {
            mkdir($base, 0755, true);
        }
        return str_replace("\\", "/", $base);
    }
}