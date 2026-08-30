<?php
namespace app\model;

class TaskRecord extends BaseModel
{
    protected $table = "zs_task_records";

    public function task()
    {
        return $this->belongsTo(Task::class, "task_id", "id");
    }
}

