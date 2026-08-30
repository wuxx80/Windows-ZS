<?php
namespace app\model;

class Image extends BaseModel
{
    protected $table = "zs_images";

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

    public function getFileSizeAttr($value)
    {
        if (!$value) return "0 B";
        $units = ["B", "KB", "MB", "GB", "TB"];
        $i = 0;
        while ($value >= 1024 && $i < 4) {
            $value /= 1024;
            $i++;
        }
        return round($value, 2) . " " . $units[$i];
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

