<?php
namespace app\model;

class Log extends BaseModel
{
    protected $table = "zs_logs";

    public function scopeByAction($query, $action)
    {
        return $query->where("action", $action);
    }

    public function scopeByTime($query, $start, $end)
    {
        return $query->whereBetween("created_at", [$start, $end]);
    }
}

