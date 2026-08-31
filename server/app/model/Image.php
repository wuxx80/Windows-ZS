<?php
namespace app\model;

class Image extends BaseModel
{
    protected $table = "zs_images";

    // 客户端/后台均读取整数 file_size；人类可读大小通过附加字段 size_display 提供，避免破坏整数类型
    protected $append = ["size_display"];

    public function scopeEnabled($query)
    {
        return $query->where("status", 1);
    }

    public function scopeFormat($query, $format)
    {
        return $query->where("format", $format);
    }

    public function getTagsAttr($value)
    {
        return $value ? explode(",", $value) : [];
    }

    /** 人类可读文件大小（"1.5 GB" 等），供界面展示；file_size 本身保持整数（字节） */
    public function getSizeDisplayAttr($value)
    {
        return self::formatSize((int) $this->getData("file_size"));
    }

    /** 字节数 → 人类可读大小 */
    private static function formatSize(int $bytes): string
    {
        if ($bytes <= 0) return "0 B";
        $units = ["B", "KB", "MB", "GB", "TB"];
        $i = 0;
        $v = (float) $bytes;
        while ($v >= 1024 && $i < 4) { $v /= 1024; $i++; }
        return round($v, 2) . " " . $units[$i];
    }

    public function versions()
    {
        return $this->hasMany(ImageVersion::class, "image_id", "id");
    }

    public function tags()
    {
        return $this->belongsToMany(ImageTag::class, ImageTagRelation::class, "tag_id", "image_id");
    }

    public function sources()
    {
        return $this->hasMany(ImageSource::class, "image_id", "id");
    }
}

