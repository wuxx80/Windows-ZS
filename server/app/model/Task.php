<?php
namespace app\model;

class Task extends BaseModel
{
    protected $table = "zs_tasks";

    public function scopeRunning($query)
    {
        return $query->where("status", 1);
    }

    public function scopeByStatus($query, $status)
    {
        return $query->where("status", $status);
    }

    public function records()
    {
        return $this->hasMany(TaskRecord::class, "task_id", "id");
    }

    public function client()
    {
        return $this->belongsTo(Client::class, "client_id", "id");
    }

    public function image()
    {
        return $this->belongsTo(Image::class, "image_id", "id");
    }
}

