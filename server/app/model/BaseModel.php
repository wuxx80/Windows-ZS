<?php
namespace app\model;

use think\Model;

class BaseModel extends Model
{
    protected $connection = "mysql";
    protected $autoWriteTimestamp = true;
    protected $createTime = "created_at";
    protected $updateTime = "updated_at";
    protected $deleteTime = false;
    protected $defaultSoftDelete = 0;
}

