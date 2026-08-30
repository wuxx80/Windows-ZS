<?php
namespace app\service;

use think\facade\Db;
use think\facade\Cache;

class ImageService
{
    /**
     * 获取镜像详细信息（含标签、版本、源）
     */
    public static function getInfo(int $id): array
    {
        $image = Db::name("images")->find($id);
        if (!$image) return [];

        $image["tags"] = Db::name("image_tag_relations")
            ->alias("r")
            ->join("zs_image_tags t", "r.tag_id = t.id")
            ->where("r.image_id", $id)
            ->field("t.id, t.name, t.color, t.type")
            ->select()
            ->toArray();

        $image["versions"] = Db::name("image_versions")
            ->where("image_id", $id)
            ->order("version", "desc")
            ->limit(20)
            ->select()
            ->toArray();

        $image["sources"] = Db::name("image_sources")
            ->where("image_id", $id)
            ->select()
            ->toArray();

        $image["file_size_human"] = self::getFormattedSize($image["file_size"] ?? 0);
        $image["download_count"] = Db::name("download_logs")->where("image_id", $id)->count();
        $image["task_count"] = Db::name("tasks")->where("image_id", $id)->count();

        return $image;
    }

    /**
     * 验证镜像文件哈希完整性
     */
    public static function verifyHash(int $id): array
    {
        $image = Db::name("images")->find($id);
        if (!$image) {
            return ["match" => false, "error" => "镜像不存在"];
        }
        if (empty($image["file_path"]) || !file_exists($image["file_path"])) {
            return ["match" => false, "error" => "文件不存在: " . ($image["file_path"] ?? "空路径")];
        }

        $actualHash = hash_file("sha256", $image["file_path"]);
        $expectedHash = $image["sha256"] ?? "";
        $match = !empty($expectedHash) && strtolower($actualHash) === strtolower($expectedHash);

        // 更新哈希值
        if (empty($image["sha256"])) {
            Db::name("images")->where("id", $id)->update(["sha256" => $actualHash]);
        }

        $fileSize = filesize($image["file_path"]);

        return [
            "match"         => $match,
            "sha256"        => $actualHash,
            "expected"      => $expectedHash ?: $actualHash,
            "file_size"     => $fileSize,
            "file_size_human" => self::getFormattedSize($fileSize),
            "file_exists"   => true,
        ];
    }

    /**
     * 从文件名自动检测操作系统信息
     */
    public static function detectOsInfo(string $filePath): array
    {
        $ext = strtolower(pathinfo($filePath, PATHINFO_EXTENSION));
        $name = basename($filePath);
        $info = [
            "os_type"    => "",
            "os_edition" => "",
            "os_arch"    => "x64",
            "os_version" => "",
            "os_language" => "zh-CN",
        ];

        // 检测操作系统类型
        if (preg_match("/Windows\s*(11|10|8\.1|8|7|Vista|XP|Server)/i", $name, $m)) {
            $ver = $m[1];
            if (strtolower($ver) === "server") {
                $info["os_type"] = "Windows Server";
                if (preg_match("/(2019|2022|2016|2012|2008)/i", $name, $sm)) {
                    $info["os_type"] = "Windows Server " . $sm[1];
                }
            } else {
                $info["os_type"] = "Windows " . $ver;
            }
        } elseif (preg_match("/(Win11|Win10|Win7|Win8)/i", $name, $m)) {
            $info["os_type"] = str_replace("Win", "Windows ", $m[1]);
        } elseif (stripos($name, "winpe") !== false || stripos($name, "pe") !== false) {
            $info["os_type"] = "Windows PE";
        } elseif (stripos($name, "ubuntu") !== false) {
            $info["os_type"] = "Ubuntu";
            if (preg_match("/(\d+\.\d+)/", $name, $m)) $info["os_version"] = $m[1];
        } elseif (stripos($name, "centos") !== false) {
            $info["os_type"] = "CentOS";
            if (preg_match("/(\d+)/", $name, $m)) $info["os_version"] = $m[1];
        } elseif (stripos($name, "deepin") !== false) {
            $info["os_type"] = "Deepin";
        }

        // 检测版本号
        if (empty($info["os_version"])) {
            if (preg_match("/(\d+\.\d+(?:\.\d+)?)/", $name, $m)) {
                $info["os_version"] = $m[1];
            }
        }

        // 检测版本（Pro/Enterprise/Education/LTSC）
        if (preg_match("/(Pro|Enterprise|Education|Home|LTSC|LTSB|Workstation|Professional)/i", $name, $m)) {
            $ed = $m[1];
            $map = [
                "Pro" => "专业版",
                "Professional" => "专业版",
                "Enterprise" => "企业版",
                "Education" => "教育版",
                "Home" => "家庭版",
                "LTSC" => "LTSC",
                "LTSB" => "LTSB",
                "Workstation" => "工作站版",
            ];
            $info["os_edition"] = $map[$ed] ?? $ed;
        }

        // 检测架构
        if (stripos($name, "x86") !== false || stripos($name, "32bit") !== false) {
            $info["os_arch"] = "x86";
        } elseif (stripos($name, "arm64") !== false) {
            $info["os_arch"] = "arm64";
        } elseif (stripos($name, "arm") !== false) {
            $info["os_arch"] = "arm";
        }

        // 检测语言
        if (stripos($name, "en") !== false || stripos($name, "english") !== false) {
            $info["os_language"] = "en-US";
        } elseif (stripos($name, "ja") !== false || stripos($name, "japanese") !== false) {
            $info["os_language"] = "ja-JP";
        }

        return $info;
    }

    /**
     * 创建镜像版本快照
     */
    public static function createSnapshot(int $imageId, string $remark = ""): int
    {
        $image = Db::name("images")->find($imageId);
        if (!$image) throw new \Exception("镜像不存在");

        $lastVersion = Db::name("image_versions")
            ->where("image_id", $imageId)
            ->max("version");

        $newVersion = ($lastVersion ?: 0) + 1;

        $snapshot = [
            "image_id"    => $imageId,
            "version"     => $newVersion,
            "name"        => $image["name"],
            "description" => $image["description"],
            "filename"    => $image["filename"],
            "file_path"   => $image["file_path"],
            "file_size"   => $image["file_size"],
            "sha256"      => $image["sha256"],
            "format"      => $image["format"],
            "os_type"     => $image["os_type"],
            "os_version"  => $image["os_version"],
            "os_arch"     => $image["os_arch"],
            "snapshot"    => json_encode($image),
            "remark"      => $remark,
            "created_by"  => $image["created_by"] ?? 0,
            "created_at"  => date("Y-m-d H:i:s"),
        ];

        return Db::name("image_versions")->insertGetId($snapshot);
    }

    /**
     * 回滚到指定版本
     */
    public static function restoreToVersion(int $imageId, int $versionId): array
    {
        $version = Db::name("image_versions")->where("id", $versionId)->where("image_id", $imageId)->find();
        if (!$version) throw new \Exception("版本记录不存在");

        $snapshot = json_decode($version["snapshot"] ?? "{}", true);
        if (empty($snapshot)) throw new \Exception("版本快照数据异常");

        // 先创建当前版本的快照
        self::createSnapshot($imageId, "回滚前自动备份 v" . $version["version"]);

        // 恢复数据
        unset($snapshot["id"], $snapshot["create_time"], $snapshot["update_time"]);
        Db::name("images")->where("id", $imageId)->update($snapshot);

        return Db::name("images")->find($imageId);
    }

    /**
     * 获取镜像格式统计
     */
    public static function getFormatStats(): array
    {
        return Db::name("images")
            ->field("format, COUNT(*) as count, SUM(file_size) as total_size")
            ->group("format")
            ->select()
            ->toArray();
    }

    /**
     * 获取过期镜像
     */
    public static function getExpiredImages(int $days = 30): array
    {
        $deadline = date("Y-m-d H:i:s", strtotime("-" . $days . " days"));
        return Db::name("images")
            ->where("status", 0)
            ->where("delete_time", "<>", 0)
            ->where("delete_time", "<", strtotime($deadline))
            ->select()
            ->toArray();
    }

    /**
     * 自动清理过期镜像
     */
    public static function cleanExpired(int $days = 30): int
    {
        $images = self::getExpiredImages($days);
        $count = 0;
        foreach ($images as $image) {
            if (!empty($image["file_path"]) && file_exists($image["file_path"])) {
                @unlink($image["file_path"]);
            }
            // 删除相关记录
            Db::name("image_versions")->where("image_id", $image["id"])->delete();
            Db::name("image_tag_relations")->where("image_id", $image["id"])->delete();
            Db::name("download_links")->where("image_id", $image["id"])->delete();
            Db::name("images")->where("id", $image["id"])->delete();
            $count++;
        }
        return $count;
    }

    /**
     * 获取所有标签
     */
    public static function getAllTags(): array
    {
        $tags = Db::name("image_tags")->order("id", "asc")->select()->toArray();
        foreach ($tags as &$tag) {
            $tag["image_count"] = Db::name("image_tag_relations")
                ->where("tag_id", $tag["id"])
                ->count();
        }
        return $tags;
    }

    /**
     * 为镜像设置标签
     */
    public static function setTags(int $imageId, array $tagIds): void
    {
        Db::name("image_tag_relations")->where("image_id", $imageId)->delete();
        $data = [];
        foreach ($tagIds as $tagId) {
            if (intval($tagId) > 0) {
                $data[] = ["image_id" => $imageId, "tag_id" => intval($tagId)];
            }
        }
        if (!empty($data)) {
            Db::name("image_tag_relations")->insertAll($data);
        }
    }

    /**
     * 格式化文件大小
     */
    public static function getFormattedSize(int $bytes): string
    {
        if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . " GB";
        if ($bytes >= 1048576) return round($bytes / 1048576, 2) . " MB";
        if ($bytes >= 1024) return round($bytes / 1024, 2) . " KB";
        return $bytes . " B";
    }

    /**
     * 获取磁盘使用统计
     */
    public static function getStorageStats(): array
    {
        $totalSize = Db::name("images")->sum("file_size");
        $totalCount = Db::name("images")->count();
        $activeCount = Db::name("images")->where("status", 1)->count();

        $diskUsage = FileService::getDiskUsage();

        return [
            "total_size" => $totalSize,
            "total_size_human" => self::getFormattedSize($totalSize),
            "total_count" => $totalCount,
            "active_count" => $activeCount,
            "format_stats" => self::getFormatStats(),
            "disk_usage" => $diskUsage,
        ];
    }
}