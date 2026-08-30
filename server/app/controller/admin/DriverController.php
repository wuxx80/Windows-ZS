<?php
namespace app\controller\admin;

use app\model\Driver;

class DriverController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $deviceType = input('device_type');
        $osSupport = input('os_support');

        $query = Driver::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description|publisher', 'like', '%' . $keyword . '%');
        }
        if ($deviceType) {
            $query->where('device_type', $deviceType);
        }
        if ($osSupport) {
            $query->where('os_support', 'like', '%' . $osSupport . '%');
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'device_type' => input('device_type'),
            'publisher' => input('publisher'),
            'version' => input('version'),
            'file_url' => input('file_url'),
            'file_size' => input('file_size', 0),
            'os_support' => input('os_support'),
            'arch_support' => input('arch_support', 'x64'),
            'status' => input('status', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '驱动名称不能为空');
        }

        $driver = Driver::create($data);
        return $this->success($driver, '创建成功');
    }

    public function edit($id)
    {
        $driver = Driver::find($id);
        if (!$driver) {
            return $this->error('not_found', '驱动不存在');
        }

        $data = [];
        foreach (['name', 'description', 'device_type', 'publisher', 'version', 'file_url', 'file_size', 'os_support', 'arch_support', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $driver->save($data);
        return $this->success($driver, '更新成功');
    }

    public function delete($id)
    {
        $driver = Driver::find($id);
        if (!$driver) {
            return $this->error('not_found', '驱动不存在');
        }

        $driver->delete();
        return $this->success(null, '删除成功');
    }

    public function upload()
    {
        $file = request()->file('file');
        if (!$file) {
            return $this->error('file_upload_failed', '未检测到上传文件');
        }

        $info = $file->move(config('upload.path') . '/drivers');
        if (!$info) {
            return $this->error('file_upload_failed', $file->getError());
        }

        return $this->success([
            'path' => $info->getPathname(),
            'size' => $info->getSize(),
            'filename' => $info->getFilename(),
        ], '上传成功');
    }
}
