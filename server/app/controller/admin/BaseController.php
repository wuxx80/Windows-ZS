<?php
namespace app\controller\admin;

use app\BaseController as Base;
use think\facade\Cache;
use think\facade\Request;

class BaseController extends Base
{
    protected $user = null;
    protected $userId = 0;
    protected $noAuth = [];

    protected function initialize()
    {
        parent::initialize();
        $this->checkAuth();
    }

    protected function checkAuth()
    {
        $controller = Request::controller();
        $action = Request::action();
        $current = $controller . '/' . $action;

        if (in_array($current, $this->noAuth)) {
            return;
        }

        $token = Request::header('Authorization');
        if (!$token) {
            $this->error('auth_login_required');
        }
        $token = str_replace('Bearer ', '', $token);

        $user = Cache::get('auth_token_' . $token);
        if (!$user) {
            $this->error('auth_token_expired');
        }

        $this->user = $user;
        $this->userId = $user['id'];
    }

    protected function success($data = null, string $msg = '操作成功')
    {
        return json([
            'code' => 0,
            'message' => $msg,
            'data' => $data,
            'request_id' => request()->id(),
            'timestamp' => time(),
        ]);
    }

    protected function error(string $code = 'error', string $msg = '')
    {
        $status = config('status.' . $code, ['code' => 9999, 'message' => '未知错误']);
        if ($msg) {
            $status['message'] = $msg;
        }
        return json($status);
    }

    protected function paginate($query)
    {
        $page = input('page', 1);
        $limit = input('limit', 20);
        $list = $query->paginate($limit, false, ['page' => $page]);
        return $this->success([
            'list' => $list->items(),
            'total' => $list->total(),
            'page' => (int) $page,
            'limit' => (int) $limit,
            'pages' => $list->lastPage(),
        ]);
    }
}
