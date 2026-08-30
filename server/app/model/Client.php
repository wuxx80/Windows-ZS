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

    public static function generateClientId()
    {
        return strtoupper("ZS-" . bin2hex(random_bytes(8)));
    }
}

