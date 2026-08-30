<?php
namespace app\controller\admin;

use app\model\User;

class UserController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');

        $query = User::order('id', 'desc');

        if ($keyword) {
            $query->where('username|nickname|email', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'username' => input('username'),
            'password' => input('password'),
            'nickname' => input('nickname'),
            'email' => input('email'),
            'avatar' => input('avatar'),
            'role_id' => input('role_id'),
            'is_super' => input('is_super', 0),
            'status' => self::parseStatus(input('status', 'enabled')),
        ];

        if (empty($data['username'])) {
            return $this->error('param_error', 'username required');
        }
        if (empty($data['password'])) {
            return $this->error('param_error', 'password required');
        }

        if (User::where('username', $data['username'])->find()) {
            return $this->error('param_error', 'username exists');
        }

        $user = User::create($data);
        return $this->success($user, 'created');
    }

    public function edit($id)
    {
        $user = User::find($id);
        if (!$user) {
            return $this->error('not_found', 'user not found');
        }

        $data = [];
        foreach (['username', 'nickname', 'email', 'avatar', 'role_id', 'is_super'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $password = input('password');
        if ($password !== null && $password !== '') {
            $data['password'] = $password;
        }

        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        if (isset($data['username'])) {
            $exists = User::where('username', $data['username'])->where('id', '<>', $id)->find();
            if ($exists) {
                return $this->error('param_error', 'username exists');
            }
        }

        $user->save($data);
        $user->refresh();
        return $this->success($user, 'updated');
    }

    public function delete($id)
    {
        $user = User::find($id);
        if (!$user) {
            return $this->error('not_found', 'user not found');
        }

        if ((int) $id === (int) $this->userId) {
            return $this->error('forbidden', 'cannot delete self');
        }

        $user->delete();
        return $this->success(null, 'deleted');
    }

    public function detail($id)
    {
        $user = User::find($id);
        if (!$user) {
            return $this->error('not_found', 'user not found');
        }
        return $this->success($user);
    }
}