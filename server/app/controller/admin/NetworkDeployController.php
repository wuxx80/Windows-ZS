<?php
namespace app\controller\admin;

use app\model\NetworkDeploy;

class NetworkDeployController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $status = input('status');

        $query = NetworkDeploy::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description|target_ip', 'like', '%' . $keyword . '%');
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
            'target_ip' => input('target_ip'),
            'target_mac' => input('target_mac'),
            'image_id' => input('image_id'),
            'pxe_config_id' => input('pxe_config_id'),
            'unattend_id' => input('unattend_id'),
            'software_template_id' => input('software_template_id'),
            'scheduled_at' => input('scheduled_at'),
            'params' => input('params/a', []),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['target_ip'])) {
            return $this->error('param_error', '名称和目标IP不能为空');
        }

        $deploy = NetworkDeploy::create($data);
        return $this->success($deploy, '创建成功');
    }

    public function edit($id)
    {
        $deploy = NetworkDeploy::find($id);
        if (!$deploy) {
            return $this->error('not_found', '网络部署不存在');
        }

        $data = [];
        foreach (['name', 'description', 'target_ip', 'target_mac', 'image_id', 'pxe_config_id', 'unattend_id', 'software_template_id', 'scheduled_at', 'params'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $deploy->save($data);
        return $this->success($deploy, '更新成功');
    }

    public function delete($id)
    {
        $deploy = NetworkDeploy::find($id);
        if (!$deploy) {
            return $this->error('not_found', '网络部署不存在');
        }

        $deploy->delete();
        return $this->success(null, '删除成功');
    }

    public function start($id)
    {
        $deploy = NetworkDeploy::find($id);
        if (!$deploy) {
            return $this->error('not_found', '网络部署不存在');
        }

        $deploy->status = 'deploying';
        $deploy->started_at = date('Y-m-d H:i:s');
        $deploy->save();

        return $this->success($deploy, '部署已启动');
    }

    public function report($id)
    {
        $deploy = NetworkDeploy::find($id);
        if (!$deploy) {
            return $this->error('not_found', '网络部署不存在');
        }

        return $this->success([
            'id' => $deploy->id,
            'name' => $deploy->name,
            'status' => $deploy->status,
            'progress' => $deploy->progress ?? 0,
            'started_at' => $deploy->started_at,
            'completed_at' => $deploy->completed_at,
            'log' => $deploy->log,
        ]);
    }
}
