<?php
namespace app\model;

class Setting extends BaseModel
{
    protected $table = "zs_settings";
    protected $autoWriteTimestamp = false;

    protected $schema = [
        "id"          => "int",
        "group"       => "string",
        "key"         => "string",
        "value"       => "string",
        "type"        => "string",
        "description" => "string",
    ];

    public function getValueAttr($value, $data)
    {
        $type = $data["type"] ?? "string";
        switch ($type) {
            case "int":
                return (int) $value;
            case "float":
                return (float) $value;
            case "bool":
                return in_array($value, ["true", "1", 1, true], true);
            case "json":
                return json_decode($value, true) ?? [];
            default:
                return $value;
        }
    }

    public static function getByGroup($group)
    {
        return self::where("group", $group)->select();
    }

    public static function set($key, $value)
    {
        $setting = self::where("key", $key)->find();
        if ($setting) {
            $setting->save(["value" => $value]);
        }
        return $setting;
    }
}

