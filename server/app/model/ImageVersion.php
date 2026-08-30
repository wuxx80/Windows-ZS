<?php
namespace app\model;

class ImageVersion extends BaseModel
{
    protected $table = "zs_image_versions";

    public function image()
    {
        return $this->belongsTo(Image::class, "image_id", "id");
    }
}

