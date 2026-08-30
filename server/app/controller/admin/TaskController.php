<?php
namespace app\controller\admin;

use app\model\Task;
use app\model\TaskLog;
use think\facade\Db;

class TaskController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');
        $type = input('type');
        $clientId = input('client_id');
        $startDate = input('start_date');
        $endDate = input('end_date');

        $query = Task::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }
        if ($type) {
            $query->where('type', $type);
        }
        if ($clientId) {
            $query->where('client_id', $clientId);
        }
        if ($startDate) {
            $query->where('created_at', '>=', $startDate);
        }
        if ($endDate) {
            $query->where('created_at', '<=', $endDate . ' 23:59:59');
        }

        return $this->paginate($query);
    }

    public function detail($id)
    {
        $task = Task::with(['client', 'logs'])->find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }
        return $this->success($task);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'type' => input('type', 'install'),
            'client_id' => input('client_id'),
            'image_id' => input('image_id'),
            'template_id' => input('template_id'),
            'params' => input('params/a', []),
            'scheduled_at' => input('scheduled_at'),
            'priority' => input('priority', 0),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['client_id'])) {
            return $this->error('param_error', '任务名称和客户端不能为空');
        }

        $task = Task::create($data);
        return $this->success($task, '创建成功');
    }

    public function cancel($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }

        if (!in_array($task->status, ['pending', 'waiting'])) {
            return $this->error('task_cancel_not_allowed');
        }

        $task->status = 'cancelled';
        $task->cancelled_at = date('Y-m-d H:i:s');
        $task->cancelled_by = $this->userId;
        $task->save();

        return $this->success($task, '已取消');
    }

    public function retry($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }

        if (!in_array($task->status, ['failed', 'cancelled'])) {
            return $this->error('task_status_error', '当前状态不允许重试');
        }

        $task->status = 'pending';
        $task->retry_count = $task->retry_count + 1;
        $task->save();

        return $this->success($task, '已加入重试队列');
    }

    public function pause($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }

        if ($task->status !== 'running') {
            return $this->error('task_status_error', '只有运行中的任务可以暂停');
        }

        $task->status = 'paused';
        $task->save();

        return $this->success($task, '已暂停');
    }

    public function resume($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }

        if ($task->status !== 'paused') {
            return $this->error('task_status_error', '只有暂停的任务可以恢复');
        }

        $task->status = 'running';
        $task->save();

        return $this->success($task, '已恢复');
    }

    public function logs($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }

        $query = TaskLog::where('task_id', $id)->order('id', 'asc');
        return $this->paginate($query);
    }

    public function template()
    {
        return $this->success([
            'types' => ['install', 'uninstall', 'update', 'custom'],
            'statuses' => ['pending', 'waiting', 'running', 'completed', 'failed', 'cancelled', 'paused'],
            'priorities' => [0 => '低', 1 => '中', 2 => '高', 3 => '紧急'],
        ]);
    }
}
