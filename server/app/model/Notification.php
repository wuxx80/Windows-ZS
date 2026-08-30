<?php
namespace app\model;

class Notification extends BaseModel
{
    protected $table = "zs_notifications";

    public function scopeUnread($query)
    {
        return $query->where("is_read", 0);
    }

    public function scopeByRecipient($query, $userId)
    {
        return $query->where("recipient_id", $userId);
    }
}

