<?php
namespace app\controller\admin;

use app\model\ImageSource;

class ImageSourceController extends BaseController
{
    public function index()
    {
        $query = ImageSource::order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'source_type' => input('type', 'local'),
            'url' => input('url'),
            'auth_username' => input('username'),
            'auth_password' => input('password'),
            'protocol' => input('protocol', 'http'),
            'auth_type' => input('auth_type', 'none'),
            'sync_interval' => input('sync_interval', 0, 'intval'),
            'status' => self::parseStatus(input('status', 'enabled')),
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '名称不能为空');
        }

        $source = ImageSource::create($data);
        return $this->success($source, '创建成功');
    }

    public function edit($id)
    {
        $source = ImageSource::find($id);
        if (!$source) {
            return $this->error('not_found', '来源不存在');
        }

        $data = [];
        foreach (['name', 'source_type', 'url', 'auth_username', 'auth_password', 'protocol', 'auth_type', 'sync_interval'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        // 兼容旧字段名
        $typeVal = input('type');
        if ($typeVal !== null && !isset($data['source_type'])) {
            $data['source_type'] = $typeVal;
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        $source->save($data);
        return $this->success($source, '更新成功');
    }

    public function delete($id)
    {
        $source = ImageSource::find($id);
        if (!$source) {
            return $this->error('not_found', '来源不存在');
        }

        $source->delete();
        return $this->success(null, '删除成功');
    }

    public function sync($id)
    {
        $source = ImageSource::find($id);
        if (!$source) {
            return $this->error('not_found', '来源不存在');
        }

        return $this->success([
            'source_id' => $id,
            'status' => 'syncing',
        ], '同步任务已启动');
    }
}