<?php
namespace app\controller\admin;

use think\facade\Db;

class SettingController extends BaseController
{
    public function index()
    {
        $group = input('group');

        $query = Db::name('settings')->order('group', 'asc')->order('id', 'asc');

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

        // 兼容两种提交格式：
        //   1) 列表：[{group, key, value}, ...]（管理后台标准格式，group 用于分组归属）
        //   2) 扁平：{key: value}（旧调用方，group 缺省为 basic）
        $items = [];
        $isList = array_keys($settings) === range(0, count($settings) - 1);
        if ($isList) {
            foreach ($settings as $item) {
                $items[] = [
                    'group' => $item['group'] ?? 'basic',
                    'key'   => (string)($item['key'] ?? ''),
                    'value' => $item['value'] ?? '',
                ];
            }
        } else {
            foreach ($settings as $key => $value) {
                $items[] = [
                    'group' => 'basic',
                    'key'   => (string)$key,
                    'value' => $value,
                ];
            }
        }

        foreach ($items as $item) {
            if ($item['key'] === '') {
                continue;
            }
            $value = is_array($item['value']) ? json_encode($item['value']) : $item['value'];
            $exists = Db::name('settings')->where('key', $item['key'])->find();
            if ($exists) {
                Db::name('settings')->where('key', $item['key'])->update([
                    'group' => $item['group'],
                    'value' => $value,
                ]);
            } else {
                Db::name('settings')->insert([
                    'group' => $item['group'],
                    'key'   => $item['key'],
                    'value' => $value,
                    'type'  => 'string',
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