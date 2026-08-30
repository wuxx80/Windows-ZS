<?php
namespace app\model;

class UnattendTemplate extends BaseModel
{
    protected $table = "zs_unattend_templates";

    public function getConfigAttr($value)
    {
        return json_decode($value, true) ?? [];
    }

    public function generateXml()
    {
        // placeholder: generate unattend XML
    }
}

