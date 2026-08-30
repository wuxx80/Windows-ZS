<?php
namespace app\controller\admin;

use app\model\PxeConfig;

class PxeConfigController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $query = PxeConfig::order('id', 'desc');
        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'boot_file' => input('boot_file'),
            'config_file' => input('config_file'),
            'next_server' => input('next_server'),
            'dhcp_range' => input('dhcp_range'),
            'subnet_mask' => input('subnet_mask'),
            'gateway' => input('gateway'),
            'dns_servers' => input('dns_servers'),
            'image_id' => input('image_id', 0),
            'unattend_id' => input('unattend_id', 0),
            'is_active' => input('is_active', 0),
            'status' => self::parseStatus(input('status', 'enabled')),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '配置名称不能为空');
        }

        $config = PxeConfig::create($data);
        return $this->success($config, '创建成功');
    }

    public function edit($id)
    {
        $config = PxeConfig::find($id);
        if (!$config) {
            return $this->error('not_found', 'PXE配置不存在');
        }

        $data = [];
        foreach (['name', 'description', 'boot_file', 'config_file', 'next_server', 'dhcp_range', 'subnet_mask', 'gateway', 'dns_servers', 'image_id', 'unattend_id', 'is_active'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        $config->save($data);
        return $this->success($config, '更新成功');
    }

    public function delete($id)
    {
        $config = PxeConfig::find($id);
        if (!$config) {
            return $this->error('not_found', 'PXE配置不存在');
        }

        $config->delete();
        return $this->success(null, '删除成功');
    }

    public function activate($id)
    {
        $config = PxeConfig::find($id);
        if (!$config) {
            return $this->error('not_found', 'PXE配置不存在');
        }

        PxeConfig::where('1=1')->update(['is_active' => 0]);
        $config->is_active = 1;
        $config->save();

        return $this->success($config, '已激活');
    }
}