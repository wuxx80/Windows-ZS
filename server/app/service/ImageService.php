<?php
namespace app\service;

use think\facade\Db;

class ImageService
{
    public static function getInfo(int $id): array
    {
        $image = Db::name('images')->find($id);
        if (!$image) return [];
        $image['tags'] = Db::name('image_tag_relations')
            ->alias('r')
            ->join('zs_image_tags t', 'r.tag_id = t.id')
            ->where('r.image_id', $id)
            ->select()
            ->toArray();
        $image['versions'] = Db::name('image_versions')
            ->where('image_id', $id)
            ->order('version', 'desc')
            ->select()
            ->toArray();
        return $image;
    }

    public static function verifyHash(int $id): bool
    {
        $image = Db::name('images')->find($id);
        if (!$image || !$image['file_path']) return false;
        $file = $image['file_path'];
        if (!file_exists($file)) return false;
        $hash = hash_file('sha256', $file);
        return $hash === $image['file_hash'];
    }

    public static function getFormattedSize(int $bytes): string
    {
        if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . ' GB';
        if ($bytes >= 1048576) return round($bytes / 1048576, 2) . ' MB';
        if ($bytes >= 1024) return round($bytes / 1024, 2) . ' KB';
        return $bytes . ' B';
    }

    public static function detectOsInfo(string $filePath): array
    {
        $ext = strtolower(pathinfo($filePath, PATHINFO_EXTENSION));
        $info = ['os_type' => '', 'os_edition' => '', 'os_arch' => 'x64', 'os_version' => ''];

        if ($ext === 'wim' || $ext === 'esd') {
            $name = basename($filePath);
            if (preg_match('/Windows\s*(10|11|7|8|8\.1)/i', $name, $m)) {
                $info['os_type'] = 'Windows ' . $m[1];
            }
            if (preg_match('/(Pro|Enterprise|Education|Home|LTSC|LTSB)/i', $name, $m)) {
                $info['os_edition'] = $m[1];
            }
            if (stripos($name, 'x86') !== false) $info['os_arch'] = 'x86';
            if (stripos($name, 'arm64') !== false) $info['os_arch'] = 'arm64';
            if (preg_match('/(\d+\.\d+)/', $name, $m)) {
                $info['os_version'] = $m[1];
            }
        }

        return $info;
    }
}
