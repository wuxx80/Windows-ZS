<?php
namespace app\controller\admin;

use app\model\ImageSource;

class ImageSourceController extends BaseController
{
    public function index()
    {
        $query = ImageSource::order('sort', 'asc')->order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'type' => input('type', 'local'),
            'url' => input('url'),
            'username' => input('username'),
            'password' => input('password'),
            'path' => input('path'),
            'status' => self::parseStatus(input('status', 'enabled')),
            'sort' => input('sort', 0),
            'description' => input('description'),
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
        foreach (['name', 'type', 'url', 'username', 'password', 'path', 'sort', 'description'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
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