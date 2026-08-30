<?php
namespace app\model;

class ScheduledTaskLog extends BaseModel
{
    protected $table = "zs_scheduled_task_logs";

    public function task()
    {
        return $this->belongsTo(ScheduledTask::class, "task_id", "id");
    }
}

