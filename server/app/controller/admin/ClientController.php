<?php
namespace app\controller\admin;

use app\model\Client;
use app\model\ClientGroup;
use think\facade\Db;

class ClientController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');
        $groupId = input('group_id');
        $os = input('os');

        $query = Client::order('last_heartbeat', 'desc');

        if ($keyword) {
            $query->where('hostname|ip_address|mac_address|client_id', 'like', '%' . $keyword . '%');
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }
        if ($groupId) {
            $query->where('group_id', $groupId);
        }
        if ($os) {
            $query->where('os', 'like', '%' . $os . '%');
        }

        $page = input("page", 1);
        $limit = input("limit", input("pageSize", 20));
        $list = $query->paginate($limit, false, ["page" => $page]);

        // 离线判定：last_heartbeat 超过 90 秒（3 次心跳间隔）视为离线（仅展示，不落库）
        $now = time();
        $items = $list->items();
        foreach ($items as $item) {
            $item->display_status = $item->status;
            $item->online = false;
            if ($item->last_heartbeat) {
                $diff = $now - strtotime($item->last_heartbeat);
                $item->online = $diff <= 90;
            }
            // 已批准但心跳超时 → 展示为 offline
            if ($item->status === 'approved' && !$item->online) {
                $item->display_status = 'offline';
            }
        }

        return $this->success([
            "list" => $items,
            "total" => $list->total(),
            "page" => (int) $page,
            "limit" => (int) $limit,
            "pages" => $list->lastPage(),
        ]);
    }

    /**
     * 客户端自注册（WinPE/Windows 客户端连接服务器时调用）
     * 幂等策略（按优先级）：
     *   1. 传入 client_id：按 client_id 匹配，刷新心跳返回已有记录
     *   2. 未传 client_id 但 mac_address 匹配已有客户端：复用（兼容 WinPE 重启丢失本地 client_id）
     *   3. 否则创建新客户端
     */
    public function register()
    {
        $clientCode = input('client_id', '');
        $hostname = input('hostname', '');
        $macAddress = input('mac_address', '');
        $osVersion = input('os_version', '');
        $clientVersion = input('client_version', '0.0.268311');
        $clientType = input('client_type', 'winpe');

        if (!in_array($clientType, ['winpe', 'windows_installer', 'windows'])) {
            $clientType = 'winpe';
        }

        // 幂等匹配
        $existing = null;
        if ($clientCode) {
            $existing = Client::where('client_id', $clientCode)->find();
        }
        if (!$existing && $macAddress && $macAddress !== '00-00-00-00-00-00') {
            $existing = Client::where('mac_address', $macAddress)->order('id', 'asc')->find();
        }

        if ($existing) {
            $existing->last_heartbeat = date('Y-m-d H:i:s');
            $existing->last_ip = request()->ip();
            if ($hostname) { $existing->hostname = $hostname; }
            if ($osVersion) { $existing->os_version = $osVersion; }
            if ($clientVersion) { $existing->client_version = $clientVersion; }
            $existing->save();
            return $this->success([
                'id' => $existing->id,
                'client_id' => $existing->client_id,
                'status' => $existing->status,
            ], '注册成功');
        }

        if (empty($hostname)) {
            return $this->error('param_error', '主机名不能为空');
        }

        $client = Client::create([
            'client_id' => $clientCode ?: Client::generateClientId(),
            'name' => input('name') ?: $hostname,
            'mac_address' => $macAddress ?: '00-00-00-00-00-00',
            'hostname' => $hostname,
            'os_version' => $osVersion ?: 'Unknown',
            'client_version' => $clientVersion,
            'client_type' => $clientType,
            'first_ip' => request()->ip(),
            'last_ip' => request()->ip(),
            'last_heartbeat' => date('Y-m-d H:i:s'),
            // 登录已改为「用户注册/登录」体系，客户端设备自注册即视为已授权，无需后台人工审批
            'status' => 'approved',
        ]);

        return $this->success([
            'id' => $client->id,
            'client_id' => $client->client_id,
            'status' => $client->status,
        ], '注册成功');
    }

    /**
     * 客户端心跳（客户端每 30s 调用一次）
     * 入口: 客户端定时 POST /api/v1/clients/heartbeat
     * 执行: ① 按 client_id → mac 匹配客户端
     *       ② 刷新 last_heartbeat / last_ip / 版本信息
     *       ③ 返回客户端审核状态 + 本机待执行任务数（PE 端据此提示续装）
     * 退出: 客户端未注册（无匹配记录）→ 返回 error，客户端应重新 register
     */
    public function heartbeat()
    {
        $clientCode = input('client_id', '');
        $macAddress = input('mac_address', '');
        $hostname = input('hostname', '');
        $osVersion = input('os_version', '');
        $clientVersion = input('client_version', '');

        $client = null;
        if ($clientCode) {
            $client = Client::where('client_id', $clientCode)->find();
        }
        if (!$client && $macAddress && $macAddress !== '00-00-00-00-00-00') {
            $client = Client::where('mac_address', $macAddress)->order('id', 'asc')->find();
        }

        if (!$client) {
            return $this->error('client_not_registered', '客户端尚未注册，请重新注册');
        }

        $client->last_heartbeat = date('Y-m-d H:i:s');
        $client->last_ip = request()->ip();
        if ($hostname) { $client->hostname = $hostname; }
        if ($osVersion) { $client->os_version = $osVersion; }
        if ($clientVersion) { $client->client_version = $clientVersion; }
        $client->save();

        // 统计本机等待 PE 执行的任务（PE 端据此提示「检测到待执行任务」）
        $waitingCount = \app\model\Task::where('client_id', $client->id)
            ->where('status', 'waiting')
            ->count();

        return $this->success([
            'id' => $client->id,
            'client_id' => $client->client_id,
            'status' => $client->status,
            'waiting_task_count' => (int) $waitingCount,
            'server_time' => date('Y-m-d H:i:s'),
        ], '心跳正常');
    }

    public function detail($id)
    {
        $client = Client::with(['group', 'lastTask'])->find($id);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }
        return $this->success($client);
    }

    public function approve($id)
    {
        $client = Client::find($id);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }

        $client->status = 'approved';
        $client->approved_at = date('Y-m-d H:i:s');
        $client->approved_by = $this->userId;
        $client->save();

        return $this->success($client, '审批通过');
    }

    public function batchApprove()
    {
        $ids = input('ids/a');
        if (empty($ids)) {
            return $this->error('param_error', '请选择客户端');
        }

        Client::whereIn('id', $ids)->update([
            'status' => 'approved',
            'approved_at' => date('Y-m-d H:i:s'),
            'approved_by' => $this->userId,
        ]);

        return $this->success(null, '批量审批通过');
    }

    public function block($id)
    {
        $client = Client::find($id);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }

        $client->status = 'blocked';
        $client->save();

        return $this->success($client, '已禁用');
    }

    public function delete($id)
    {
        $client = Client::find($id);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }

        $client->delete();
        return $this->success(null, '删除成功');
    }

    public function edit($id)
    {
        $client = Client::find($id);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }

        $data = [];
        foreach (['hostname', 'group_id', 'remark', 'tags'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $client->save($data);
        return $this->success($client, '更新成功');
    }

    public function sendCommand()
    {
        $clientId = input('client_id');
        $command = input('command');
        $params = input('params/a', []);

        if (!$clientId || !$command) {
            return $this->error('param_error', '客户端ID和命令不能为空');
        }

        $client = Client::find($clientId);
        if (!$client) {
            return $this->error('not_found', '客户端不存在');
        }

        return $this->success([
            'client_id' => $clientId,
            'command' => $command,
            'params' => $params,
            'status' => 'sent',
        ], '命令已发送');
    }
}
