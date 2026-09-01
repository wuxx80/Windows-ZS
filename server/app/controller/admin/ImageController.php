<?php
namespace app\controller\admin;

use app\model\Image;
use app\model\ImageVersion;
use app\service\ImageService;
use app\service\FileService;
use think\facade\Db;
use think\facade\Cache;

class ImageController extends BaseController
{
    public function index()
    {
        $keyword = input("keyword");
        $format = input("format");
        $status = input("status");
        $osType = input("os_type");
        $tagId = input("tag_id");
        $dateFrom = input("date_from");
        $dateTo = input("date_to");
        $sortField = input("sort_field", "id");
        $sortOrder = input("sort_order", "desc");

        $allowedSort = ["id", "name", "file_size", "created_at", "os_type"];
        if (!in_array($sortField, $allowedSort)) $sortField = "id";
        if (!in_array($sortOrder, ["asc", "desc"])) $sortOrder = "desc";

        $query = Image::order($sortField, $sortOrder);

        if ($keyword) {
            $query->where("name|description|file_name|os_type|os_version", "like", "%" . $keyword . "%");
        }
        if ($format) {
            $query->where("format", $format);
        }
        if ($status !== null && $status !== "") {
            $statusVal = $status === 'enabled' ? 1 : ($status === 'disabled' ? 0 : intval($status));
            $query->where("status", $statusVal);
        }
        if ($osType) {
            $query->where("os_type", "like", "%" . $osType . "%");
        }
        if ($dateFrom) {
            $query->where("created_at", ">=", $dateFrom . " 00:00:00");
        }
        if ($dateTo) {
            $query->where("created_at", "<=", $dateTo . " 23:59:59");
        }
        if ($tagId) {
            $imageIds = Db::name("image_tag_relations")
                ->where("tag_id", $tagId)
                ->column("image_id");
            $query->whereIn("id", $imageIds ?: [0]);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $name = input("name");
        $filename = input("file_name");

        if (empty($name)) {
            return $this->error("param_error", "镜像名称不能为空");
        }
        if (empty($filename)) {
            return $this->error("param_error", "文件名不能为空");
        }

        $format = input("format", "");
        if (empty($format)) {
            $format = strtolower(pathinfo($filename, PATHINFO_EXTENSION));
        }

        $filePath = input("file_path", "");
        $fileSize = 0;
        $osInfo = ["os_type" => "", "os_edition" => "", "os_arch" => "x64", "os_version" => "", "os_language" => "zh-CN"];

        if ($filePath && file_exists($filePath)) {
            $osInfo = ImageService::detectOsInfo($filePath);
            $fileSize = filesize($filePath);
        }

        $data = [
            "name"        => $name,
            "description" => input("description", ""),
            "file_name"   => $filename,
            "file_path"   => $filePath,
            "format"      => $format ?: "wim",
            "file_size"   => input("file_size", $fileSize, "intval"),
            "file_hash"   => input("sha256", ""),
            "os_type"     => input("os_type", $osInfo["os_type"]),
            "os_edition"  => input("os_edition", $osInfo["os_edition"]),
            "os_version"  => input("os_version", $osInfo["os_version"]),
            "os_arch"     => input("os_arch", $osInfo["os_arch"]),
            "language"    => input("os_language", "zh-CN"),
            "source_id"   => input("source_id", 0, "intval"),
            "source_type" => input("source_type", "upload"),
            "status"      => self::parseStatus(input("status", "enabled")),
            "created_by"  => $this->userId,
            "created_at"  => date("Y-m-d H:i:s"),
        ];

        $image = Image::create($data);

        $tagIds = input("tag_ids/a", []);
        if (!empty($tagIds)) {
            ImageService::setTags($image->id, $tagIds);
        }

        ImageService::createSnapshot($image->id, "初始版本");
        $this->log("image", "创建镜像: " . $name, $image->id);

        return $this->success($image, "创建成功");
    }

    public function edit($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found");
        }

        $data = [];
        $fields = ["name", "description", "file_name", "file_path", "format", "file_size",
                    "file_hash", "os_type", "os_edition", "os_version", "os_arch",
                    "os_language", "source_id", "source_url", "is_public"];
        foreach ($fields as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        // 单独处理 status 字段，需 parseStatus 将字符串转为整数
        $statusVal = input("status");
        if ($statusVal !== null) {
            $data["status"] = self::parseStatus($statusVal);
        }

        if (!empty($data)) {
            $data["updated_at"] = date("Y-m-d H:i:s");
            $image->save($data);
            ImageService::createSnapshot($id, "手动更新");
        }

        $tagIds = input("tag_ids/a");
        if ($tagIds !== null) {
            ImageService::setTags($id, $tagIds);
        }

        $this->log("image", "编辑镜像: " . $image->name, $id);
        return $this->success(ImageService::getInfo($id), "更新成功");
    }

    public function delete($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found");
        }

        $activeTasks = Db::name("tasks")
            ->where("image_id", $id)
            ->whereIn("status", ["pending", "running", "paused"])
            ->count();
        if ($activeTasks > 0) {
            return $this->error("image_has_active_tasks", "该镜像有关联的活跃任务，无法删除");
        }

        $imageData = $image->toArray();

        // 先删除关联标签
        Db::name("image_tag_relations")->where("image_id", $id)->delete();

        // 删除关联版本记录
        Db::name("image_versions")->where("image_id", $id)->delete();

        // 记录到回收站后真删除
        Db::name("recycle_bin")->insert([
            "original_table" => "zs_images",
            "original_id"   => $id,
            "data"          => json_encode($imageData),
            "deleted_by"    => $this->userId,
            "deleted_at"    => date("Y-m-d H:i:s"),
            "expire_at"     => date("Y-m-d H:i:s", strtotime("+30 days")),
        ]);

        Image::destroy($id);

        $this->log("image", "删除镜像: " . $image->name, $id);
        return $this->success(null, "删除成功");
    }

    public function detail($id)
    {
        $image = Image::find($id);
        if (!$image || !$image->id) {
            return $this->error("image_not_found");
        }
        return $this->success(ImageService::getInfo($id));
    }

    public function upload()
    {
        $file = request()->file("file");
        if (!$file) {
            return $this->error("file_upload_failed", "未检测到上传文件");
        }

        $chunk = input("chunk", 0, "intval");
        $chunks = input("chunks", 1, "intval");
        $md5 = input("md5", "");
        $filename = input("filename", "");

        try {
            if ($chunks > 1) {
                $chunkDir = runtime_path() . "chunks/" . $md5;
                if (!is_dir($chunkDir)) {
                    mkdir($chunkDir, 0755, true);
                }
                $file->move($chunkDir, $chunk . ".part");

                $completedChunks = count(glob($chunkDir . "/*.part"));

                if ($completedChunks >= $chunks) {
                    $uploadDir = FileService::getUploadPath("images");
                    $finalName = $filename ?: $md5;
                    $finalPath = $uploadDir . "/" . $finalName;

                    $fp = fopen($finalPath, "wb");
                    for ($i = 0; $i < $chunks; $i++) {
                        $partFile = $chunkDir . "/" . $i . ".part";
                        if (file_exists($partFile)) {
                            fwrite($fp, file_get_contents($partFile));
                            unlink($partFile);
                        }
                    }
                    fclose($fp);
                    @rmdir($chunkDir);

                    $fileSize = filesize($finalPath);
                    $sha256 = hash_file("sha256", $finalPath);

                    return $this->success([
                        "path"     => str_replace("\\", "/", $finalPath),
                        "size"     => $fileSize,
                        "size_human" => FileService::formatBytes($fileSize),
                        "filename" => $finalName,
                        "sha256"   => $sha256,
                    ], "上传完成");
                }

                return $this->success([
                    "chunk"   => $chunk,
                    "chunks"  => $chunks,
                    "md5"     => $md5,
                    "progress" => round($completedChunks / $chunks * 100, 1),
                ], "分片上传中");
            }

            $result = FileService::upload($file, "images");
            return $this->success($result, "上传成功");

        } catch (\Exception $e) {
            return $this->error("file_upload_failed", $e->getMessage());
        }
    }

    public function uploadComplete()
    {
        $md5 = input("md5");
        $filename = input("filename");
        if (!$md5 || !$filename) {
            return $this->error("param_error", "参数不完整");
        }

        $uploadDir = FileService::getUploadPath("images");
        $finalPath = $uploadDir . "/" . $filename;

        if (!file_exists($finalPath)) {
            return $this->error("file_not_found", "文件未找到");
        }

        return $this->success([
            "path"     => str_replace("\\", "/", $finalPath),
            "size"     => filesize($finalPath),
            "size_human" => FileService::formatBytes(filesize($finalPath)),
            "sha256"   => hash_file("sha256", $finalPath),
            "filename" => $filename,
        ]);
    }

    public function addRemoteUrl()
    {
        $url = input("url");
        $name = input("name");

        if (!$url || !$name) {
            return $this->error("param_error", "URL和名称不能为空");
        }

        $format = input("format", "");
        if (empty($format)) {
            $format = strtolower(pathinfo(parse_url($url, PHP_URL_PATH), PATHINFO_EXTENSION));
        }

        $filename = basename(parse_url($url, PHP_URL_PATH)) ?: $name . "." . $format;

        $data = [
            "name"        => $name,
            "description" => input("description", ""),
            "file_name"   => $filename,
            "format"      => $format ?: "wim",
            "file_path"   => "",
            "source_id"   => input("source_id", 0, "intval"),
            "source_type" => "download",
            "status"      => 0,
            "created_by"  => $this->userId,
            "created_at"  => date("Y-m-d H:i:s"),
        ];

        $image = Image::create($data);

        Db::name("download_queue")->insert([
            "image_id"   => $image->id,
            "source_url" => $url,
            "status"     => "pending",
            "created_at" => date("Y-m-d H:i:s"),
        ]);

        $this->log("image", "添加远程镜像: " . $name, $image->id);
        return $this->success($image, "远程镜像已添加，后台正在准备下载");
    }

    public function verify($id)
    {
        $result = ImageService::verifyHash($id);
        if (isset($result["error"])) {
            return $this->error("file_not_found", $result["error"]);
        }

        Db::name("images")->where("id", $id)->update([
            "verify_status" => $result["match"] ? 1 : 2,
            "last_verify_time" => date("Y-m-d H:i:s"),
        ]);

        $this->log("image", "校验镜像: id=" . $id . " " . ($result["match"] ? "通过" : "失败"), $id);
        return $this->success($result, $result["match"] ? "校验通过" : "校验失败，文件可能已损坏");
    }

    public function convert($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found");
        }

        $targetFormat = input("target_format");
        if (!$targetFormat) {
            return $this->error("param_error", "请指定目标格式");
        }

        $supportedFormats = ["wim", "iso", "esd", "gho"];
        if (!in_array($targetFormat, $supportedFormats)) {
            return $this->error("param_error", "不支持的目标格式，支持: " . implode(", ", $supportedFormats));
        }

        if ($targetFormat === $image->format) {
            return $this->error("param_error", "目标格式与当前格式相同");
        }

        $taskId = Db::name("scheduled_tasks")->insertGetId([
            "type"       => "image_convert",
            "target_id"  => $id,
            "params"     => json_encode(["target_format" => $targetFormat]),
            "status"     => "pending",
            "created_at" => date("Y-m-d H:i:s"),
        ]);

        $this->log("image", "创建格式转换任务: " . $image->name . " -> " . $targetFormat, $id);
        return $this->success([
            "image_id"      => $id,
            "target_format" => $targetFormat,
            "task_id"       => $taskId,
            "status"        => "pending",
        ], "转换任务已创建");
    }

    public function download($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found");
        }

        $filePath = $image->file_path;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error("file_not_found", "镜像文件不存在");
        }

        $downloadToken = md5(uniqid(mt_rand(), true));
        $expire = 3600;

        Cache::set("download_token_" . $downloadToken, [
            "image_id" => $id,
            "path"     => $filePath,
            "filename" => $image->file_name ?: basename($filePath),
        ], $expire);

        Db::name("download_logs")->insert([
            "image_id"   => $id,
            "user_id"    => $this->userId,
            "file_size"  => $image->file_size,
            "ip"         => request()->ip(),
            "created_at" => date("Y-m-d H:i:s"),
        ]);

        $this->log("image", "下载镜像: " . $image->name, $id);
        return $this->success([
            "download_url" => url("/api/v1/images/downloadFile/" . $downloadToken),
            "token"        => $downloadToken,
            "expires_in"   => $expire,
            "filename"     => $image->file_name ?: basename($filePath),
            "file_size"    => $image->file_size,
            "file_size_human" => FileService::formatBytes($image->file_size),
        ]);
    }

    public function downloadFile($token)
    {
        $info = Cache::get("download_token_" . $token);
        if (!$info) {
            return $this->error("auth_token_expired", "下载链接已过期或无效");
        }

        try {
            FileService::download($info["path"], $info["filename"]);
        } catch (\Exception $e) {
            return $this->error("file_not_found", $e->getMessage());
        }
    }

    /**
     * 客户端接口：直接流式下载镜像文件（校验启用状态；支持断点续传）。
     * 供 PE/Windows 客户端在装机场内拉取镜像，鉴权复用客户端 Token（AuthMiddleware）。
     */
    public function clientDownload($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found", "镜像不存在");
        }
        if ((int) $image->status !== 1) {
            return $this->error("disabled", "该镜像未启用");
        }

        $filePath = $image->file_path;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error("file_not_found", "镜像文件不存在，请先在后台配置文件路径");
        }

        $fileName = $image->file_name ?: basename($filePath);
        try {
            FileService::download($filePath, $fileName);
        } catch (\Exception $e) {
            return $this->error("file_not_found", $e->getMessage());
        }
    }

    public function restore($id)
    {
        $versionId = input("version_id");
        if (!$versionId) {
            return $this->error("param_error", "请指定版本ID");
        }

        try {
            $image = ImageService::restoreToVersion($id, $versionId);
            $this->log("image", "回滚镜像版本: id=" . $id . " version_id=" . $versionId, $id);
            return $this->success($image, "版本恢复成功");
        } catch (\Exception $e) {
            return $this->error("not_found", $e->getMessage());
        }
    }

    public function versions($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error("image_not_found");
        }
        $versions = ImageVersion::where("image_id", $id)
            ->order("version", "desc")
            ->select()
            ->toArray();
        return $this->success($versions);
    }

    public function batchDelete()
    {
        $ids = input("ids/a");
        if (empty($ids)) {
            return $this->error("param_error", "请选择要删除的镜像");
        }

        $activeTasks = Db::name("tasks")
            ->whereIn("image_id", $ids)
            ->whereIn("status", ["pending", "running", "paused"])
            ->count();
        if ($activeTasks > 0) {
            return $this->error("image_has_active_tasks", "部分镜像关联了活跃任务，无法删除");
        }

        $images = Image::whereIn("id", $ids)->select();
        foreach ($images as $image) {
            $imageData = $image->toArray();

            // 删除关联标签和版本
            Db::name("image_tag_relations")->where("image_id", $image->id)->delete();
            Db::name("image_versions")->where("image_id", $image->id)->delete();

            Db::name("recycle_bin")->insert([
                "original_table" => "zs_images",
                "original_id"   => $image->id,
                "data"          => json_encode($imageData),
                "deleted_by"    => $this->userId,
                "deleted_at"    => date("Y-m-d H:i:s"),
            ]);

            Image::destroy($image->id);
        }

        $this->log("image", "批量删除镜像: " . implode(",", $ids));
        return $this->success(null, "批量删除成功");
    }

    public function batchEnable()
    {
        $ids = input("ids/a");
        if (empty($ids)) {
            return $this->error("param_error", "请选择要启用的镜像");
        }
        Image::whereIn("id", $ids)->update(["status" => 1, "updated_at" => date("Y-m-d H:i:s")]);
        $this->log("image", "批量启用镜像: " . implode(",", $ids));
        return $this->success(null, "批量启用成功");
    }

    public function batchDisable()
    {
        $ids = input("ids/a");
        if (empty($ids)) {
            return $this->error("param_error", "请选择要禁用的镜像");
        }
        $activeTasks = Db::name("tasks")
            ->whereIn("image_id", $ids)
            ->whereIn("status", ["running"])
            ->count();
        if ($activeTasks > 0) {
            return $this->error("image_has_active_tasks", "部分镜像有正在执行的任务，无法禁用");
        }
        Image::whereIn("id", $ids)->update(["status" => 0, "updated_at" => date("Y-m-d H:i:s")]);
        $this->log("image", "批量禁用镜像: " . implode(",", $ids));
        return $this->success(null, "批量禁用成功");
    }

    public function formats()
    {
        $formats = Db::name("images")
            ->field("format, COUNT(*) as count")
            ->group("format")
            ->select()
            ->toArray();
        return $this->success($formats);
    }

    public function storageStats()
    {
        return $this->success(ImageService::getStorageStats());
    }

    private function log(string $type, string $action, $targetId = null)
    {
        try {
            Db::name("logs")->insert([
                "user_id"    => $this->userId,
                "type"       => $type,
                "action"     => $action,
                "target_id"  => $targetId,
                "ip"         => request()->ip(),
                "user_agent" => request()->header("User-Agent", ""),
                "created_at" => date("Y-m-d H:i:s"),
            ]);
        } catch (\Exception $e) {}
    }
}