<?php
namespace app\model;

class Role extends BaseModel
{
    protected $table = "zs_roles";

    protected $schema = [
        "id"          => "int",
        "name"        => "string",
        "code"        => "string",
        "description" => "string",
        "permissions" => "string",
        "status"      => "int",
    ];

    public function getPermissionsAttr($value)
    {
        return json_decode($value, true) ?? [];
    }
}

