<?php
namespace app\exception;

use think\exception\Handle;
use think\exception\HttpException;
use think\exception\ValidateException;
use think\Response;

class HttpExceptionHandler extends Handle
{
    public function render($request, \Throwable $e): Response
    {
        if ($e instanceof ValidateException) {
            return json([
                'code' => 1001,
                'message' => $e->getError(),
                'request_id' => $request->id(),
                'timestamp' => time(),
            ]);
        }

        if ($e instanceof HttpException) {
            return json([
                'code' => 1002,
                'message' => '资源不存在',
                'request_id' => $request->id(),
                'timestamp' => time(),
            ], $e->getStatusCode());
        }

        if (config('app.debug')) {
            return parent::render($request, $e);
        }

        return json([
            'code' => 1000,
            'message' => '系统错误',
            'request_id' => $request->id(),
            'timestamp' => time(),
        ]);
    }
}
