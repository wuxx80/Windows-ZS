<?php
namespace app\controller\admin;

use think\facade\Db;

class WebhookController extends BaseController
{
    public function index()
    {
        $status = input('status');
        $event = input('event');

        $query = Db::name('webhook_logs')->order('id', 'desc');

        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }
        if ($event) {
            $query->where('event', $event);
        }

        return $this->paginate($query);
    }

    public function retry($id)
    {
        $log = Db::name('webhook_logs')->find($id);
        if (!$log) {
            return $this->error('not_found', 'Webhook日志不存在');
        }

        Db::name('webhook_logs')->where('id', $id)->update([
            'status' => 'pending',
            'retry_count' => $log['retry_count'] + 1,
            'updated_at' => date('Y-m-d H:i:s'),
        ]);

        return $this->success(null, '已加入重试队列');
    }
}
