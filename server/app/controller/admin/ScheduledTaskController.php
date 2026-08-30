<?php
namespace app\controller\admin;

use app\model\ScheduledTask;
use app\model\ScheduledTaskLog;

class ScheduledTaskController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');

        $query = ScheduledTask::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'type' => input('type', 'install'),
            'cron_expression' => input('cron_expression'),
            'target_ids' => input('target_ids/a', []),
            'params' => input('params/a', []),
            'is_enabled' => input('is_enabled', 0),
            'notify_on_failure' => input('notify_on_failure', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['cron_expression'])) {
            return $this->error('param_error', '名称和Cron表达式不能为空');
        }

        $task = ScheduledTask::create($data);
        return $this->success($task, '创建成功');
    }

    public function edit($id)
    {
        $task = ScheduledTask::find($id);
        if (!$task) {
            return $this->error('not_found', '计划任务不存在');
        }

        $data = [];
        foreach (['name', 'description', 'type', 'cron_expression', 'target_ids', 'params', 'is_enabled', 'notify_on_failure'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $task->save($data);
        return $this->success($task, '更新成功');
    }

    public function delete($id)
    {
        $task = ScheduledTask::find($id);
        if (!$task) {
            return $this->error('not_found', '计划任务不存在');
        }

        $task->delete();
        return $this->success(null, '删除成功');
    }

    public function logs($id)
    {
        $task = ScheduledTask::find($id);
        if (!$task) {
            return $this->error('not_found', '计划任务不存在');
        }

        $query = ScheduledTaskLog::where('task_id', $id)->order('id', 'desc');
        return $this->paginate($query);
    }

    public function trigger($id)
    {
        $task = ScheduledTask::find($id);
        if (!$task) {
            return $this->error('not_found', '计划任务不存在');
        }

        if (!$task->is_enabled) {
            return $this->error('param_error', '任务已禁用，无法触发');
        }

        return $this->success([
            'task_id' => $id,
            'status' => 'triggered',
        ], '任务已触发');
    }
}
