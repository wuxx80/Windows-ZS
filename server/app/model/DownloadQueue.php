<?php
namespace app\model;

class DownloadQueue extends BaseModel
{
    protected $table = "zs_download_queue";

    public function scopePending($query)
    {
        return $query->where("status", 0);
    }

    public function scopeDownloading($query)
    {
        return $query->where("status", 1);
    }
}

