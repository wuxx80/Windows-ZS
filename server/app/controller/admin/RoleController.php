<?php
namespace app\controller\admin;

use app\model\Role;

/**
 * 角色管理控制器
 * 提供 CRUD + 分页，供后台 users.html 下拉选择
 */
class RoleController extends BaseController
{
    /**
     * 角色列表（分页）
     */
    public function index()
    {
        $keyword = input('keyword');

        $query = Role::order('id', 'asc');

        if ($keyword) {
            $query->where('name|code', 'like', '%' . $keyword . '%');
        }

        return $this->paginate($query);
    }

    /**
     * 创建角色
     */
    public function create()
    {
        $data = [
            'name'        => input('name'),
            'code'        => input('code'),
            'description' => input('description', ''),
            'permissions' => input('permissions', '[]'),
            'status'      => self::parseStatus(input('status', 'enabled')),
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', 'name required');
        }
        if (empty($data['code'])) {
            return $this->error('param_error', 'code required');
        }

        if (Role::where('code', $data['code'])->find()) {
            return $this->error('param_error', 'code exists');
        }

        $role = Role::create($data);
        return $this->success($role, 'created');
    }

    /**
     * 编辑角色
     */
    public function edit($id)
    {
        $role = Role::find($id);
        if (!$role) {
            return $this->error('not_found', 'role not found');
        }

        $data = [];
        foreach (['name', 'code', 'description', 'permissions'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        if (isset($data['code'])) {
            $exists = Role::where('code', $data['code'])->where('id', '<>', $id)->find();
            if ($exists) {
                return $this->error('param_error', 'code exists');
            }
        }

        $role->save($data);
        $role->refresh();
        return $this->success($role, 'updated');
    }

    /**
     * 删除角色
     */
    public function delete($id)
    {
        $role = Role::find($id);
        if (!$role) {
            return $this->error('not_found', 'role not found');
        }

        // 内置角色不可删除
        if (in_array($role->code, ['super_admin', 'admin', 'user'])) {
            return $this->error('forbidden', 'built-in role cannot be deleted');
        }

        $role->delete();
        return $this->success(null, 'deleted');
    }

    /**
     * 角色详情
     */
    public function detail($id)
    {
        $role = Role::find($id);
        if (!$role) {
            return $this->error('not_found', 'role not found');
        }
        return $this->success($role);
    }
}
