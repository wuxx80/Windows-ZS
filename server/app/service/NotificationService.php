<?php
namespace app\service;

use think\facade\Db;

class NotificationService
{
    public static function send(int $recipientId, string $type, string $title, string $content, string $level = 'info', string $relatedType = '', int $relatedId = 0): int
    {
        return Db::name('notifications')->insertGetId([
            'type' => $type,
            'title' => $title,
            'content' => $content,
            'level' => $level,
            'is_read' => 0,
            'recipient_id' => $recipientId,
            'related_type' => $relatedType,
            'related_id' => $relatedId,
            'created_at' => date('Y-m-d H:i:s'),
        ]);
    }

    public static function broadcast(string $type, string $title, string $content, string $level = 'info'): void
    {
        $users = Db::name('users')->where('status', 1)->select();
        foreach ($users as $user) {
            self::send($user['id'], $type, $title, $content, $level);
        }
    }

    public static function sendTaskComplete(int $userId, int $taskId, string $taskNo): void
    {
        self::send($userId, 'task_complete', '装机任务完成', "任务 {$taskNo} 已完成", 'success', 'task', $taskId);
    }

    public static function sendTaskFail(int $userId, int $taskId, string $taskNo, string $error): void
    {
        self::send($userId, 'task_fail', '装机任务失败', "任务 {$taskNo} 失败: {$error}", 'error', 'task', $taskId);
    }
}
