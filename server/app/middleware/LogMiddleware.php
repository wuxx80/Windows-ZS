<?php
namespace app\middleware;

use think\facade\Db;
use think\Request;

class LogMiddleware
{
    public function handle(Request $request, \Closure $next)
    {
        $start = microtime(true);
        $response = $next($request);
        $duration = intval((microtime(true) - $start) * 1000);

        if ($request->user && !in_array($request->controller(), ['Log'])) {
            $params = $request->param();
            if (isset($params['password'])) unset($params['password']);
            if (isset($params['old_password'])) unset($params['old_password']);
            if (isset($params['new_password'])) unset($params['new_password']);

            Db::name('logs')->insert([
                'user_id' => $request->userId ?? 0,
                'username' => $request->user['username'] ?? '',
                'action' => $request->action(),
                'resource_type' => $request->controller(),
                'resource_id' => $request->param('id/d', 0),
                'detail' => $request->method() . ' ' . $request->pathinfo(),
                'request_method' => $request->method(),
                'request_url' => $request->url(true),
                'request_params' => json_encode($params),
                'ip' => $request->ip(),
                'user_agent' => $request->server('HTTP_USER_AGENT', ''),
                'duration' => $duration,
                'created_at' => date('Y-m-d H:i:s'),
            ]);
        }

        return $response;
    }
}
