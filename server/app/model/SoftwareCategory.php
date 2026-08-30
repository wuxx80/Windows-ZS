<?php
namespace app\model;

class SoftwareCategory extends BaseModel
{
    protected $table = "zs_software_categories";

    protected $schema = [
        "id"         => "int",
        "name"       => "string",
        "icon"       => "string",
        "parent_id"  => "int",
        "sort_order" => "int",
    ];

    public function children()
    {
        return $this->hasMany(__CLASS__, "parent_id", "id");
    }
}

