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
        $version = input('version');
        $fileUrl = input('file_url');

        if (empty($version) || empty($fileUrl)) {
            return $this->error('param_error', '版本号和文件URL不能为空');
        }

        $exists = ClientVersion::where('version', $version)->find();
        if ($exists) {
            return $this->error('param_error', '版本号已存在');
        }

        $data = [
            'version'       => $version,
            'client_type'   => input('client_type', 'windows'),
            'file_name'     => input('file_name', basename(parse_url($fileUrl, PHP_URL_PATH))),
            'file_path'     => $fileUrl,
            'file_size'     => input('file_size', 0, 'intval'),
            'file_hash'     => input('md5', ''),
            'changelog'     => input('description', ''),
            'is_force_update' => input('force_update', 0, 'intval'),
            'status'        => self::parseStatus(input('status', 'enabled')),
            'publish_time'  => date('Y-m-d H:i:s'),
        ];

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
        foreach (['version', 'changelog', 'file_path', 'file_size', 'file_hash', 'is_force_update', 'file_name', 'client_type', 'min_compatible_version'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
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