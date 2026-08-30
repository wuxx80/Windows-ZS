<?php
namespace app\middleware;

use think\facade\Cache;
use think\Request;

class AuthMiddleware
{
    public function handle(Request $request, \Closure $next)
    {
        $token = $request->header('Authorization');
        if (!$token) {
            return json(['code' => 3004, 'message' => '请先登录']);
        }
        $token = str_replace('Bearer ', '', $token);
        $user = Cache::get('auth_token_' . $token);
        if (!$user) {
            return json(['code' => 3001, 'message' => 'Token 已过期']);
        }
        $request->user = $user;
        $request->userId = $user['id'];
        return $next($request);
    }
}
