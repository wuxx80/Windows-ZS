<?php
namespace app\controller\admin;

use think\facade\Db;

class LogController extends BaseController
{
    public function index()
    {
        $type = input('type');
        $keyword = input('keyword');
        $startDate = input('start_date');
        $endDate = input('end_date');
        $userId = input('user_id');

        $query = Db::name('logs')->order('id', 'desc');

        if ($type) {
            $query->where('type', $type);
        }
        if ($keyword) {
            $query->where('content|ip_address|user_agent', 'like', '%' . $keyword . '%');
        }
        if ($startDate) {
            $query->where('created_at', '>=', $startDate);
        }
        if ($endDate) {
            $query->where('created_at', '<=', $endDate . ' 23:59:59');
        }
        if ($userId) {
            $query->where('user_id', $userId);
        }

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

    public function detail($id)
    {
        $log = Db::name('logs')->find($id);
        if (!$log) {
            return $this->error('not_found', '日志不存在');
        }
        return $this->success($log);
    }

    public function types()
    {
        $types = Db::name('logs')
            ->field('type, COUNT(*) as count')
            ->group('type')
            ->order('count', 'desc')
            ->select()
            ->toArray();

        return $this->success($types);
    }

    public function clear()
    {
        $beforeDays = input('before_days', 30);
        $date = date('Y-m-d H:i:s', strtotime('-' . $beforeDays . ' days'));

        $count = Db::name('logs')->where('created_at', '<', $date)->delete();

        return $this->success(['deleted' => $count], '已清理' . $count . '条日志');
    }
}
