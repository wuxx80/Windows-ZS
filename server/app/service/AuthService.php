<?php
namespace app\service;

use think\facade\Cache;
use think\facade\Db;

class AuthService
{
    public static function login(string $username, string $password): array
    {
        $user = Db::name('users')->where('username', $username)->find();
        if (!$user) {
            throw new \Exception('用户名或密码错误');
        }
        if ($user['status'] != 1) {
            throw new \Exception('账号已被禁用');
        }
        if (!password_verify($password, $user['password'])) {
            throw new \Exception('用户名或密码错误');
        }

        $token = bin2hex(random_bytes(32));
        $expire = config('jwt.expire', 86400);

        $userData = [
            'id' => $user['id'],
            'username' => $user['username'],
            'nickname' => $user['nickname'],
            'email' => $user['email'],
            'avatar' => $user['avatar'],
            'role_id' => $user['role_id'],
            'is_super' => $user['is_super'],
        ];
        Cache::set('auth_token_' . $token, $userData, $expire);

        Db::name('users')->where('id', $user['id'])->update([
            'last_login_time' => date('Y-m-d H:i:s'),
            'last_login_ip' => request()->ip(),
            'login_count' => $user['login_count'] + 1,
        ]);

        return ['token' => $token, 'user' => $userData, 'expire' => $expire];
    }

    public static function logout(string $token): void
    {
        Cache::delete('auth_token_' . $token);
    }

    public static function checkPermission(int $userId, string $permission): bool
    {
        $user = Db::name('users')->find($userId);
        if (!$user || $user['is_super']) return true;
        $role = Db::name('roles')->find($user['role_id']);
        if (!$role || $role['status'] != 1) return false;
        $perms = json_decode($role['permissions'] ?? '[]', true);
        return in_array($permission, $perms);
    }
}
