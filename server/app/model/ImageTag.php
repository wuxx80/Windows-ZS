<?php
namespace app\model;

class ImageTag extends BaseModel
{
    protected $table = "zs_image_tags";

    protected $schema = [
        "id"         => "int",
        "name"       => "string",
        "color"      => "string",
        "is_auto"    => "int",
        "auto_rule"  => "string",
        "sort_order" => "int",
    ];

    public function images()
    {
        return $this->belongsToMany(Image::class, ImageTagRelation::class, "image_id", "tag_id");
    }
}

