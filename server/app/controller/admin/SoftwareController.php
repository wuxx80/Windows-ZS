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
            $query->where('status', $status);
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
            'file_url' => input('file_url'),
            'file_size' => input('file_size', 0),
            'install_params' => input('install_params'),
            'silent_install' => input('silent_install', 1),
            'os_support' => input('os_support'),
            'arch_support' => input('arch_support', 'x64'),
            'status' => input('status', 1),
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
        foreach (['name', 'category_id', 'description', 'version', 'publisher', 'file_url', 'file_size', 'install_params', 'silent_install', 'os_support', 'arch_support', 'status', 'sort'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
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

        return $this->success([
            'path' => $info->getPathname(),
            'size' => $info->getSize(),
            'filename' => $info->getFilename(),
        ], '上传成功');
    }

    public function category()
    {
        $categories = SoftwareCategory::order('sort', 'asc')->order('id', 'asc')->select();
        return $this->success($categories);
    }
}
