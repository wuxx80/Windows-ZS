<?php
namespace app\model;

class TaskTemplate extends BaseModel
{
    protected $table = "zs_task_templates";

    public function getOptionsAttr($value)
    {
        return json_decode($value, true) ?? [];
    }
}

