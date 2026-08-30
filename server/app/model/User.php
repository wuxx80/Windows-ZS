<?php
namespace app\model;

class User extends BaseModel
{
    protected $table = "zs_users";

    protected $schema = [
        "id"              => "int",
        "username"        => "string",
        "password"        => "string",
        "nickname"        => "string",
        "email"           => "string",
        "avatar"          => "string",
        "role_id"         => "int",
        "is_super"        => "int",
        "status"          => "int",
        "last_login_time" => "datetime",
        "last_login_ip"   => "string",
        "login_count"     => "int",
    ];

    public function findByUsername($username)
    {
        return self::where("username", $username)->find();
    }

    public function getRoleId()
    {
        return $this->getAttr("role_id");
    }

    public function setPasswordAttr($value)
    {
        return password_hash($value, PASSWORD_BCRYPT);
    }

    public function getLastLoginTimeAttr($value)
    {
        if ($value) {
            return date("Y-m-d H:i:s", strtotime($value));
        }
        return null;
    }
}