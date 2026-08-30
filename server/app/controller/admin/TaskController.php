<?php
namespace app\controller\admin;

use app\model\Task;
use app\model\TaskRecord;
use app\model\Client;
use app\model\Image;
use think\facade\Db;

class TaskController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');
        $clientId = input('client_id');
        $startDate = input('start_date');
        $endDate = input('end_date');

        $query = Task::order('id', 'desc');

        if ($keyword) {
            $query->where('task_no', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
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
        $task = Task::with(['client', 'image', 'records'])->find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }
        return $this->success($task);
    }

    public function create()
    {
        // 字段与 zs_tasks 表结构对齐（以 database/install.sql 设计为准）
        $data = [
            'task_no' => $this->generateTaskNo(),
            'client_id' => input('client_id') ?: null,
            'image_id' => input('image_id'),
            'unattend_template_id' => input('unattend_template_id') ?: input('template_id'),
            'target_disk_index' => (int) input('target_disk_index', 0),
            'target_partition' => input('target_partition', 'C:'),
            'partition_scheme' => in_array(input('partition_scheme'), ['auto', 'custom', 'keep'])
                ? input('partition_scheme') : 'auto',
            'options' => $this->buildOptions(),
            'created_by' => $this->userId,
        ];

        if (empty($data['image_id'])) {
            return $this->error('param_error', '镜像ID不能为空');
        }
        if (!Image::find($data['image_id'])) {
            return $this->error('image_not_found', '镜像不存在');
        }
        if ($data['client_id'] && !Client::find($data['client_id'])) {
            return $this->error('client_not_found', '客户端不存在');
        }

        $task = Task::create($data);
        return $this->success($task, '创建成功');
    }

    /**
     * 生成任务编号：YYYYMMDDHHmmss + 6位随机数
     */
    private function generateTaskNo()
    {
        return date('YmdHis') . str_pad((string) mt_rand(0, 999999), 6, '0', STR_PAD_LEFT);
    }

    /**
     * options 列存储 JSON：优先接收客户端 options，兼容旧 params/type 参数
     */
    private function buildOptions()
    {
        $options = input('options');
        if ($options) {
            return is_array($options) ? json_encode($options, JSON_UNESCAPED_UNICODE) : $options;
        }
        $params = input('params/a', []);
        if ($params) {
            return json_encode($params, JSON_UNESCAPED_UNICODE);
        }
        $type = input('type');
        return $type ? json_encode(['type' => $type], JSON_UNESCAPED_UNICODE)
            : json_encode(['type' => 'install'], JSON_UNESCAPED_UNICODE);
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

        $query = TaskRecord::where('task_id', $id)->order('id', 'asc');
        return $this->paginate($query);
    }

    public function progress($id)
    {
        $task = Task::find($id);
        if (!$task) {
            return $this->error('task_not_found');
        }
        $progress = input('progress/d', 0);
        $message = input('message', '');
        $stepName = input('step_name', '');
        // 客户端可上报状态：running/completed/failed
        $reportStatus = input('status', '');
        if ($progress < 0 || $progress > 100) {
            return $this->error('param_error', '进度值必须在0-100之间');
        }

        $recordStatus = 'running';
        $task->progress = $progress;

        if ($reportStatus === 'failed') {
            $task->status = 'failed';
            $task->error_message = $message ?: '任务执行失败';
            $task->completed_at = date('Y-m-d H:i:s');
            $recordStatus = 'failed';
        } elseif ($reportStatus === 'completed' || $progress >= 100) {
            $task->status = 'completed';
            $task->completed_at = date('Y-m-d H:i:s');
            $task->progress = 100;
            $recordStatus = 'completed';
        } elseif ($task->status === 'pending') {
            $task->status = 'running';
            $task->started_at = date('Y-m-d H:i:s');
        }

        // 计算耗时（秒）
        if ($task->started_at && in_array($task->status, ['completed', 'failed', 'cancelled'])) {
            $task->duration = max(0, strtotime($task->completed_at ?: date('Y-m-d H:i:s')) - strtotime($task->started_at));
        }

        $task->save();

        if ($message || $stepName) {
            TaskRecord::create([
                'task_id' => $id,
                'step_name' => $stepName ?: '进度更新',
                'action' => 'progress',
                'status' => $recordStatus,
                'progress' => $progress,
                'message' => $message,
                'started_at' => date('Y-m-d H:i:s'),
                'completed_at' => in_array($recordStatus, ['completed', 'failed']) ? date('Y-m-d H:i:s') : null,
            ]);
        }
        return $this->success(['progress' => $progress, 'status' => $task->status], '进度已更新');
    }

    public function template()
    {
        return $this->success([
            'types' => ['install', 'usb', 'repair', 'other'],
            'statuses' => ['pending', 'running', 'paused', 'completed', 'failed', 'cancelled'],
            'partition_schemes' => ['auto', 'custom', 'keep'],
            'priorities' => [0 => '低', 1 => '中', 2 => '高', 3 => '紧急'],
        ]);
    }
}