<?php
namespace app\model;

class UnattendTemplate extends BaseModel
{
    protected $table = "zs_unattend_templates";

    // config 列存 JSON 字符串，读出时自动解码为数组
    public function getConfigAttr($value)
    {
        return json_decode($value, true) ?? [];
    }

    // 写入 config 时自动编码为 JSON
    public function setConfigAttr($value)
    {
        return is_array($value) ? json_encode($value, JSON_UNESCAPED_UNICODE) : $value;
    }
}