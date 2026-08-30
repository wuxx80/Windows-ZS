<?php
namespace app\controller\admin;

use think\facade\Db;

class SettingController extends BaseController
{
    public function index()
    {
        $group = input('group');

        $query = Db::name('settings')->order('group', 'asc')->order('sort', 'asc');

        if ($group) {
            $query->where('group', $group);
        }

        $settings = $query->select()->toArray();

        $grouped = [];
        foreach ($settings as $setting) {
            $grouped[$setting['group']][] = $setting;
        }

        return $this->success([
            'list' => $grouped,
            'groups' => array_keys($grouped),
        ]);
    }

    public function update()
    {
        $settings = input('settings/a', []);

        if (empty($settings)) {
            return $this->error('param_error', '设置数据不能为空');
        }

        foreach ($settings as $key => $value) {
            $exists = Db::name('settings')->where('key', $key)->find();
            if ($exists) {
                Db::name('settings')->where('key', $key)->update([
                    'value' => is_array($value) ? json_encode($value) : $value,
                    'updated_by' => $this->userId,
                ]);
            } else {
                Db::name('settings')->insert([
                    'key' => $key,
                    'value' => is_array($value) ? json_encode($value) : $value,
                    'created_by' => $this->userId,
                ]);
            }
        }

        return $this->success(null, '设置保存成功');
    }

    public function get($key)
    {
        if (!$key) {
            return $this->error('param_error', '设置键名不能为空');
        }

        $setting = Db::name('settings')->where('key', $key)->find();
        if (!$setting) {
            return $this->error('not_found', '设置不存在');
        }

        return $this->success($setting);
    }
}
