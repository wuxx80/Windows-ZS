<?php
namespace app\model;

class ClientGroup extends BaseModel
{
    protected $table = "zs_client_groups";

    protected $schema = [
        "id"          => "int",
        "name"        => "string",
        "icon"        => "string",
        "parent_id"   => "int",
        "sort_order"  => "int",
    ];

    public function children()
    {
        return $this->hasMany(__CLASS__, "parent_id", "id");
    }
}

