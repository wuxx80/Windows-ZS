<?php
namespace app\service;

class FileService
{
    public static function upload($file, string $dir = 'images'): array
    {
        $ext = strtolower($file->extension());
        $allowed = config('upload.extensions', []);
        if (!in_array($ext, $allowed)) {
            throw new \Exception('文件类型不允许: ' . $ext);
        }

        $maxSize = config('upload.max_size', 21474836480);
        if ($file->getSize() > $maxSize) {
            throw new \Exception('文件大小超出限制');
        }

        $savePath = config('upload.path', '/data') . '/' . $dir;
        if (!is_dir($savePath)) {
            mkdir($savePath, 0755, true);
        }

        $fileName = date('Ymd_His_') . uniqid() . '.' . $ext;
        $filePath = $savePath . '/' . $fileName;

        $file->move($savePath, $fileName);

        $hash = hash_file('sha256', $filePath);

        return [
            'file_name' => $file->getOriginalName(),
            'file_path' => $filePath,
            'file_size' => $file->getSize(),
            'file_hash' => $hash,
            'extension' => $ext,
        ];
    }

    public static function uploadChunk($file, string $identifier, int $chunkNumber, int $totalChunks, string $dir = 'images'): array
    {
        $tmpDir = config('upload.path', '/data') . '/' . $dir . '/tmp/' . $identifier;
        if (!is_dir($tmpDir)) {
            mkdir($tmpDir, 0755, true);
        }

        $chunkFile = $tmpDir . '/' . $chunkNumber;
        file_put_contents($chunkFile, file_get_contents($file->getPathname()));

        $received = count(glob($tmpDir . '/*'));
        $done = $received >= $totalChunks;

        return ['received' => $received, 'total' => $totalChunks, 'done' => $done];
    }

    public static function mergeChunks(string $identifier, string $originalName, string $dir = 'images'): array
    {
        $tmpDir = config('upload.path', '/data') . '/' . $dir . '/tmp/' . $identifier;
        $ext = strtolower(pathinfo($originalName, PATHINFO_EXTENSION));
        $fileName = date('Ymd_His_') . uniqid() . '.' . $ext;
        $savePath = config('upload.path', '/data') . '/' . $dir . '/' . $fileName;

        if (!is_dir(dirname($savePath))) {
            mkdir(dirname($savePath), 0755, true);
        }

        $chunks = glob($tmpDir . '/*');
        sort($chunks, SORT_NATURAL);

        $totalSize = 0;
        $out = fopen($savePath, 'wb');
        foreach ($chunks as $chunk) {
            $size = filesize($chunk);
            fwrite($out, file_get_contents($chunk));
            $totalSize += $size;
            unlink($chunk);
        }
        fclose($out);

        rmdir($tmpDir);
        $hash = hash_file('sha256', $savePath);

        return [
            'file_name' => $originalName,
            'file_path' => $savePath,
            'file_size' => $totalSize,
            'file_hash' => $hash,
            'extension' => $ext,
        ];
    }

    public static function delete(string $filePath): bool
    {
        if (file_exists($filePath)) {
            return unlink($filePath);
        }
        return true;
    }

    public static function getDiskUsage(): array
    {
        $path = config('upload.path', '/data');
        $total = disk_total_space($path);
        $free = disk_free_space($path);
        $used = $total - $free;
        return [
            'total' => self::formatBytes($total),
            'used' => self::formatBytes($used),
            'free' => self::formatBytes($free),
            'percent' => $total > 0 ? round(($used / $total) * 100, 1) : 0,
        ];
    }

    private static function formatBytes(int $bytes): string
    {
        if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . ' GB';
        if ($bytes >= 1048576) return round($bytes / 1048576, 2) . ' MB';
        if ($bytes >= 1024) return round($bytes / 1024, 2) . ' KB';
        return $bytes . ' B';
    }
}
