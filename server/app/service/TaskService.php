<?php
namespace app\service;

use think\facade\Db;

class TaskService
{
    public static function create(array $data): int
    {
        $taskNo = date('YmdHis') . str_pad(mt_rand(0, 999999), 6, '0', STR_PAD_LEFT);
        $data['task_no'] = $taskNo;
        $data['status'] = 'pending';
        $data['progress'] = 0;
        $data['created_at'] = date('Y-m-d H:i:s');
        $data['updated_at'] = date('Y-m-d H:i:s');

        $id = Db::name('tasks')->insertGetId($data);

        Db::name('task_records')->insert([
            'task_id' => $id,
            'step_name' => 'create_task',
            'action' => '创建任务',
            'status' => 'completed',
            'progress' => 100,
            'message' => '任务创建成功，编号: ' . $taskNo,
            'started_at' => date('Y-m-d H:i:s'),
            'completed_at' => date('Y-m-d H:i:s'),
            'created_at' => date('Y-m-d H:i:s'),
        ]);

        return $id;
    }

    public static function updateProgress(int $taskId, int $progress, string $step, string $message = ''): void
    {
        Db::name('tasks')->where('id', $taskId)->update([
            'progress' => $progress,
            'current_step' => $step,
            'updated_at' => date('Y-m-d H:i:s'),
        ]);
    }

    public static function complete(int $taskId): void
    {
        Db::name('tasks')->where('id', $taskId)->update([
            'status' => 'completed',
            'progress' => 100,
            'completed_at' => date('Y-m-d H:i:s'),
            'updated_at' => date('Y-m-d H:i:s'),
        ]);
    }

    public static function fail(int $taskId, string $error): void
    {
        Db::name('tasks')->where('id', $taskId)->update([
            'status' => 'failed',
            'error_message' => $error,
            'completed_at' => date('Y-m-d H:i:s'),
            'updated_at' => date('Y-m-d H:i:s'),
        ]);
    }

    public static function getStats(): array
    {
        $today = date('Y-m-d');
        return [
            'today_installs' => Db::name('tasks')->where('created_at', '>=', $today . ' 00:00:00')->where('status', 'completed')->count(),
            'total_installs' => Db::name('tasks')->where('status', 'completed')->count(),
            'running' => Db::name('tasks')->where('status', 'running')->count(),
            'pending' => Db::name('tasks')->where('status', 'pending')->count(),
            'failed' => Db::name('tasks')->where('status', 'failed')->count(),
        ];
    }
}
