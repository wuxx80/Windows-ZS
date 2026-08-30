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

        $query = Client::order('last_online_time', 'desc');

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
