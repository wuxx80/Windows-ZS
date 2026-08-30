<?php
namespace app\model;

class Client extends BaseModel
{
    protected $table = "zs_clients";

    public function scopeOnline($query)
    {
        return $query->where("status", 1);
    }

    public function scopeByGroup($query, $groupId)
    {
        return $query->where("group_id", $groupId);
    }

    public function group()
    {
        return $this->belongsTo(ClientGroup::class, "group_id", "id");
    }

    /**
     * 最近一次装机任务（客户端详情页展示最近任务）
     */
    public function lastTask()
    {
        return $this->hasOne(Task::class, "client_id", "id")->order("id", "desc");
    }

    public static function generateClientId()
    {
        return strtoupper("ZS-" . bin2hex(random_bytes(8)));
    }
}

