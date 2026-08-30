<?php
namespace app\controller\admin;

use app\model\PeVersion;

class PeVersionController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $query = PeVersion::order('id', 'desc');
        if ($keyword) {
            $query->where('name|version|description', 'like', '%' . $keyword . '%');
        }
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'version' => input('version'),
            'description' => input('description'),
            'file_url' => input('file_url'),
            'file_size' => input('file_size', 0),
            'md5' => input('md5'),
            'status' => self::parseStatus(input('status', 'enabled')),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['version']) || empty($data['file_url'])) {
            return $this->error('param_error', '名称、版本号和文件URL不能为空');
        }

        $pe = PeVersion::create($data);
        return $this->success($pe, '创建成功');
    }

    public function edit($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        $data = [];
        foreach (['name', 'version', 'description', 'file_url', 'file_size', 'md5'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        $pe->save($data);
        return $this->success($pe, '更新成功');
    }

    public function delete($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        $pe->delete();
        return $this->success(null, '删除成功');
    }
}