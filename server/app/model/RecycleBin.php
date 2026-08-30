<?php
namespace app\model;

class RecycleBin extends BaseModel
{
    protected $table = "zs_recycle_bin";
    protected $autoWriteTimestamp = false;

    public function restore()
    {
        // placeholder: restore deleted record
    }
}

