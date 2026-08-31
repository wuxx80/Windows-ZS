<?php
namespace app\controller\admin;

use app\model\Role;
use app\model\User;
use think\facade\Cache;

class AuthController extends BaseController
{
    protected $noAuth = ['Auth/login', 'Auth/register', 'Auth/logout'];

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

        $token = $this->issueToken($user);
        $user->last_login_time = date('Y-m-d H:i:s');
        $user->last_login_ip = request()->ip();
        $user->login_count = $user->login_count + 1;
        $user->save();

        return $this->success([
            'token' => $token,
            'user' => $this->userInfo($user),
        ], '登录成功');
    }

    /**
     * 用户注册（客户端公开接口）：注册普通用户并自动登录
     * 角色固定为「普通用户」(code=user)，不可注册为管理员
     */
    public function register()
    {
        $username = input('username');
        $password = input('password');
        $nickname = input('nickname');

        if (!$username || !$password) {
            return $this->error('param_error', '请输入用户名和密码');
        }
        if (mb_strlen($username) < 3 || mb_strlen($username) > 20) {
            return $this->error('param_error', '用户名长度需为3-20个字符');
        }
        if (preg_match('/^[A-Za-z0-9_\x{4e00}-\x{9fa5}]+$/u', $username) !== 1) {
            return $this->error('param_error', '用户名仅支持字母/数字/下划线/中文');
        }
        if (strlen($password) < 6) {
            return $this->error('param_error', '密码长度不能少于6位');
        }

        if (User::where('username', $username)->find()) {
            return $this->error('param_error', '用户名已存在');
        }

        $role = Role::where('code', 'user')->find();
        $user = User::create([
            'username' => $username,
            'password' => $password,
            'nickname' => $nickname ?: $username,
            'role_id'  => $role ? $role->id : null,
            'is_super' => 0,
            'status'   => 1,
        ]);

        // create() 会写入 int 时间戳，重载模型以 datetime 字符串保存登录信息（避免 updated_at 类型错误）
        $user = User::find($user->id);
        $token = $this->issueToken($user);
        $user->last_login_time = date('Y-m-d H:i:s');
        $user->last_login_ip = request()->ip();
        $user->login_count = 1;
        $user->save();

        return $this->success([
            'token' => $token,
            'user' => $this->userInfo($user),
        ], '注册成功');
    }

    /** 签发登录令牌并缓存用户信息（有效期由 config/jwt.php 控制） */
    private function issueToken(User $user): string
    {
        $token = md5(uniqid(mt_rand(), true));
        $userInfo = $this->userInfo($user);
        Cache::set('auth_token_' . $token, $userInfo, config('jwt.expire', 86400));
        return $token;
    }

    /** 用户公开信息（剔除密码，附加角色编码供客户端区分身份） */
    private function userInfo(User $user): array
    {
        $info = $user->toArray();
        unset($info['password']);
        $info['role_code'] = '';
        if (!empty($info['role_id'])) {
            $role = Role::find($info['role_id']);
            $info['role_code'] = $role ? $role->code : '';
        }
        return $info;
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
        $user = $this->user;
        if (!$user) {
            $user = request()->user ?? null;
        }
        return $this->success($user);
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