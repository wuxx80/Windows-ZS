<?php
namespace app\controller\admin;

use app\model\Software;
use app\model\SoftwareCategory;

class SoftwareController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $categoryId = input('category_id');
        $status = input('status');

        $query = Software::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description|publisher', 'like', '%' . $keyword . '%');
        }
        if ($categoryId) {
            $query->where('category_id', $categoryId);
        }
        if ($status !== null && $status !== '') {
            $query->where('status', self::parseStatus($status));
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'category_id' => input('category_id', 0),
            'description' => input('description'),
            'version' => input('version'),
            'publisher' => input('publisher'),
            'file_name' => input('file_name', ''),
            'file_path' => input('file_path'),
            'file_size' => input('file_size', 0),
            'install_params' => input('install_params'),
            'silent_install' => input('silent_install', 1),
            'os_support' => input('os_support'),
            'arch_support' => input('arch_support', 'x64'),
            'status' => self::parseStatus(input('status', 'enabled')),
            'sort' => input('sort', 0),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '软件名称不能为空');
        }

        $software = Software::create($data);
        return $this->success($software, '创建成功');
    }

    public function edit($id)
    {
        $software = Software::find($id);
        if (!$software) {
            return $this->error('not_found', '软件不存在');
        }

        $data = [];
        foreach (['name', 'category_id', 'description', 'version', 'publisher', 'file_name', 'file_path', 'file_size', 'install_params', 'silent_install', 'os_support', 'arch_support', 'sort'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        $software->save($data);
        return $this->success($software, '更新成功');
    }

    public function delete($id)
    {
        $software = Software::find($id);
        if (!$software) {
            return $this->error('not_found', '软件不存在');
        }

        $software->delete();
        return $this->success(null, '删除成功');
    }

    /**
     * 客户端接口：直接流式下载软件安装包（校验启用状态；支持断点续传）。
     * GET /api/v1/software/{id}/clientDownload
     */
    public function clientDownload($id)
    {
        $software = Software::find($id);
        if (!$software) {
            return $this->error('not_found', '软件不存在');
        }
        if ((int) $software->status !== 1) {
            return $this->error('disabled', '该软件未启用');
        }

        $filePath = $software->file_path;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error('file_not_found', '软件文件不存在，请先在后台上传');
        }

        $fileName = $software->file_name ?: basename($filePath);
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

        $info = $file->move(config('upload.path') . '/software');
        if (!$info) {
            return $this->error('file_upload_failed', $file->getError());
        }

        $filePath = str_replace("\\", "/", $info->getPathname());
        $fileName = $info->getFilename();
        $fileSize = $info->getSize();
        $fileHash = hash_file('sha256', $info->getPathname());

        // 创建软件记录（前端上传表单已包含所有字段）
        $data = [
            'name'          => input('name', $fileName),
            'version'       => input('version', '1.0.0'),
            'description'   => input('description', ''),
            'publisher'     => input('publisher', ''),
            'category_id'   => input('category_id', 0),
            'silent_install' => input('silent_install', 1),
            'os_support'    => input('os_support', ''),
            'file_path'     => $filePath,
            'file_name'     => $fileName,
            'file_size'     => $fileSize,
            'file_hash'     => $fileHash,
            'status'        => self::parseStatus(input('status', 'enabled')),
            'created_by'    => $this->userId,
        ];
        $software = Software::create($data);

        return $this->success($software->toArray(), '上传成功');
    }

    public function category()
    {
        $categories = SoftwareCategory::order('sort', 'asc')->order('id', 'asc')->select();
        return $this->success($categories);
    }
}