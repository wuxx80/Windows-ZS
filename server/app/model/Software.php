<?php
namespace app\model;

class Software extends BaseModel
{
    protected $table = "zs_software";

    public function category()
    {
        return $this->belongsTo(SoftwareCategory::class, "category_id", "id");
    }
}

