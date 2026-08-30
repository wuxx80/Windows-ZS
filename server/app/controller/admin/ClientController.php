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

        return $this->paginate($query);
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
            'status' => 'pending',
        ]);

        return $this->success([
            'id' => $client->id,
            'client_id' => $client->client_id,
            'status' => $client->status,
        ], '注册成功，等待审核');
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
