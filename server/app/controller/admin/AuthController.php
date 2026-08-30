<?php
namespace app\controller\admin;

use app\model\User;
use think\facade\Cache;

class AuthController extends BaseController
{
    protected $noAuth = ['Auth/login', 'Auth/logout'];

    public function login()
    {
        $username = input('username');
        $password = input('password');

        if (!$username || !$password) {
            return $this->error('param_error', '请输入用户名和密码');
        }

        $user = User::where('username', $username)->find();
        if (!$user) {
            return $this->error('auth_failed', '用户名或密码错误');
        }

        if ($user->status != 1) {
            return $this->error('auth_account_blocked');
        }

        if (!password_verify($password, $user->password)) {
            return $this->error('auth_failed', '用户名或密码错误');
        }

        $token = md5(uniqid(mt_rand(), true));
        $userInfo = $user->toArray();
        unset($userInfo['password']);

        Cache::set('auth_token_' . $token, $userInfo, config('jwt.expire', 86400));

        $user->last_login_time = date('Y-m-d H:i:s');
        $user->last_login_ip = request()->ip();
        $user->login_count = $user->login_count + 1;
        $user->save();

        return $this->success([
            'token' => $token,
            'user' => $userInfo,
        ], '登录成功');
    }

    public function logout()
    {
        $token = request()->header('Authorization');
        if ($token) {
            $token = str_replace('Bearer ', '', $token);
            Cache::delete('auth_token_' . $token);
        }
        return $this->success(null, '已退出登录');
    }

    public function profile()
    {
        return $this->success($this->user);
    }

    public function updatePassword()
    {
        $oldPassword = input('old_password');
        $newPassword = input('new_password');

        if (!$oldPassword || !$newPassword) {
            return $this->error('param_error', '请提供旧密码和新密码');
        }

        if (strlen($newPassword) < 6) {
            return $this->error('param_error', '新密码长度不能少于6位');
        }

        $user = User::find($this->userId);
        if (!$user) {
            return $this->error('not_found', '用户不存在');
        }

        if (!password_verify($oldPassword, $user->password)) {
            return $this->error('auth_failed', '旧密码错误');
        }

        $user->password = password_hash($newPassword, PASSWORD_BCRYPT);
        $user->save();

        Cache::delete('auth_token_' . request()->header('Authorization'));

        return $this->success(null, '密码修改成功');
    }
}