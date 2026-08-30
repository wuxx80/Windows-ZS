<?php
namespace app\controller\admin;

use app\model\ClientGroup;

class ClientGroupController extends BaseController
{
    public function index()
    {
        $query = ClientGroup::order('sort', 'asc')->order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'sort' => input('sort', 0),
            'status' => input('status', 1),
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '分组名称不能为空');
        }

        $group = ClientGroup::create($data);
        return $this->success($group, '创建成功');
    }

    public function edit($id)
    {
        $group = ClientGroup::find($id);
        if (!$group) {
            return $this->error('not_found', '分组不存在');
        }

        $data = [];
        foreach (['name', 'description', 'sort', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $group->save($data);
        return $this->success($group, '更新成功');
    }

    public function delete($id)
    {
        $group = ClientGroup::find($id);
        if (!$group) {
            return $this->error('not_found', '分组不存在');
        }

        $clientCount = \app\model\Client::where('group_id', $id)->count();
        if ($clientCount > 0) {
            return $this->error('param_error', '该分组下存在客户端，无法删除');
        }

        $group->delete();
        return $this->success(null, '删除成功');
    }
}
