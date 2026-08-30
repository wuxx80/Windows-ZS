<?php
namespace app\model;

class PeCustomize extends BaseModel
{
    protected $table = "zs_pe_customize";

    public function peVersion()
    {
        return $this->belongsTo(PeVersion::class, "pe_version_id", "id");
    }
}

