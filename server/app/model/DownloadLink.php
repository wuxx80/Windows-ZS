<?php
namespace app\model;

class DownloadLink extends BaseModel
{
    protected $table = "zs_download_links";

    public function scopeValid($query)
    {
        return $query->where("expire_time", ">", date("Y-m-d H:i:s"))
            ->where("status", 1)
            ->where("download_count", "<", \think\facade\Db::raw("max_downloads"));
    }

    public static function generateToken()
    {
        return md5(uniqid(mt_rand(), true)) . bin2hex(random_bytes(16));
    }
}

