<?php
namespace app\controller\admin;

use app\model\ClientVersion;

class ClientVersionController extends BaseController
{
    public function index()
    {
        $query = ClientVersion::order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'version' => input('version'),
            'description' => input('description'),
            'file_url' => input('file_url'),
            'file_size' => input('file_size', 0),
            'md5' => input('md5'),
            'force_update' => input('force_update', 0),
            'status' => input('status', 0),
            'created_by' => $this->userId,
        ];

        if (empty($data['version']) || empty($data['file_url'])) {
            return $this->error('param_error', '版本号和文件URL不能为空');
        }

        $exists = ClientVersion::where('version', $data['version'])->find();
        if ($exists) {
            return $this->error('param_error', '版本号已存在');
        }

        $version = ClientVersion::create($data);
        return $this->success($version, '创建成功');
    }

    public function edit($id)
    {
        $version = ClientVersion::find($id);
        if (!$version) {
            return $this->error('not_found', '版本不存在');
        }

        $data = [];
        foreach (['version', 'description', 'file_url', 'file_size', 'md5', 'force_update', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $version->save($data);
        return $this->success($version, '更新成功');
    }

    public function delete($id)
    {
        $version = ClientVersion::find($id);
        if (!$version) {
            return $this->error('not_found', '版本不存在');
        }

        $version->delete();
        return $this->success(null, '删除成功');
    }

    public function publish($id)
    {
        $version = ClientVersion::find($id);
        if (!$version) {
            return $this->error('not_found', '版本不存在');
        }

        ClientVersion::where('status', 1)->update(['status' => 0]);
        $version->status = 1;
        $version->published_at = date('Y-m-d H:i:s');
        $version->save();

        return $this->success($version, '发布成功');
    }
}
