<?php
namespace app\middleware;

use think\Request;

class CorsMiddleware
{
    public function handle(Request $request, \Closure $next)
    {
        header('Access-Control-Allow-Origin: *');
        header('Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS');
        header('Access-Control-Allow-Headers: Authorization, Content-Type, X-Requested-With');
        header('Access-Control-Max-Age: 86400');

        if ($request->method() == 'OPTIONS') {
            return response()->code(204);
        }

        return $next($request);
    }
}
