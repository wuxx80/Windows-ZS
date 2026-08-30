<?php
namespace app\model;

class ImageTagRelation extends BaseModel
{
    protected $table = "zs_image_tag_relations";
    protected $autoWriteTimestamp = false;

    protected $schema = [
        "id"         => "int",
        "image_id"   => "int",
        "tag_id"     => "int",
        "created_at" => "datetime",
    ];
}

