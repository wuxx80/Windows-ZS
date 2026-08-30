<?php
namespace app\model;

class ScheduledTask extends BaseModel
{
    protected $table = "zs_scheduled_tasks";

    public function logs()
    {
        return $this->hasMany(ScheduledTaskLog::class, "task_id", "id");
    }
}

