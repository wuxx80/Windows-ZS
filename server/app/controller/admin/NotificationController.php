<?php
namespace app\controller\admin;

use app\model\Notification;

class NotificationController extends BaseController
{
    public function index()
    {
        $type = input('type');
        $isRead = input('is_read');

        $query = Notification::where('recipient_id', $this->userId)
            ->order('id', 'desc');

        if ($type) {
            $query->where('type', $type);
        }
        if ($isRead !== null && $isRead !== '') {
            $query->where('is_read', $isRead);
        }

        return $this->paginate($query);
    }

    public function read($id)
    {
        $notification = Notification::where('id', $id)
            ->where('recipient_id', $this->userId)
            ->find();

        if (!$notification) {
            return $this->error('not_found', '通知不存在');
        }

        $notification->is_read = 1;
        $notification->read_at = date('Y-m-d H:i:s');
        $notification->save();

        return $this->success($notification, '已标记为已读');
    }

    public function batchRead()
    {
        $ids = input('ids/a');
        if (empty($ids)) {
            return $this->error('param_error', '请选择通知');
        }

        Notification::whereIn('id', $ids)
            ->where('recipient_id', $this->userId)
            ->update([
                'is_read' => 1,
                'read_at' => date('Y-m-d H:i:s'),
            ]);

        return $this->success(null, '已标记为已读');
    }

    public function unreadCount()
    {
        $count = Notification::where('recipient_id', $this->userId)
            ->where('is_read', 0)
            ->count();

        return $this->success([
            'unread_count' => $count,
        ]);
    }
}