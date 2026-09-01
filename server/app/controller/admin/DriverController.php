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
            'file_name' => input('file_name', ''),
            'file_path' => input('file_path'),
            'file_size' => input('file_size', 0),
            'os_support' => input('os_support'),
            'arch_support' => input('arch_support', 'x64'),
            'status' => self::parseStatus(input('status', 'enabled')),
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
        foreach (['name', 'description', 'device_type', 'publisher', 'version', 'file_name', 'file_path', 'file_size', 'os_support', 'arch_support'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
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

    /**
     * 客户端接口：直接流式下载驱动文件（校验启用状态；支持断点续传）。
     * GET /api/v1/drivers/{id}/clientDownload
     */
    public function clientDownload($id)
    {
        $driver = Driver::find($id);
        if (!$driver) {
            return $this->error('not_found', '驱动不存在');
        }
        if ((int) $driver->status !== 1) {
            return $this->error('disabled', '该驱动未启用');
        }

        $filePath = $driver->file_path;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error('file_not_found', '驱动文件不存在，请先在后台上传');
        }

        $fileName = $driver->file_name ?: basename($filePath);
        try {
            \app\service\FileService::download($filePath, $fileName);
        } catch (\Exception $e) {
            return $this->error('file_not_found', $e->getMessage());
        }
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

        $filePath = str_replace("\\", "/", $info->getPathname());
        $fileName = $info->getFilename();
        $fileSize = $info->getSize();
        $fileHash = hash_file('sha256', $info->getPathname());

        // 创建驱动记录（前端上传表单已包含所有字段）
        $data = [
            'name'        => input('name', $fileName),
            'version'     => input('version', '1.0.0'),
            'description' => input('description', ''),
            'device_type' => input('type', 'other'),
            'publisher'   => input('publisher', ''),
            'os_support'  => input('os_support', ''),
            'file_path'   => $filePath,
            'file_name'   => $fileName,
            'file_size'   => input('file_size', $fileSize),
            'file_hash'   => $fileHash,
            'status'      => self::parseStatus(input('status', 'enabled')),
            'created_by'  => $this->userId,
        ];
        $driver = Driver::create($data);

        return $this->success($driver->toArray(), '上传成功');
    }
}