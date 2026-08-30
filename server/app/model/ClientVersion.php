<?php
namespace app\model;

class ClientVersion extends BaseModel
{
    protected $table = "zs_client_versions";

    public function scopeLatest($query)
    {
        return $query->order("id", "desc");
    }
}

