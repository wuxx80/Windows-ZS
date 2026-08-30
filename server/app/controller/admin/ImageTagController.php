<?php
namespace app\controller\admin;

use app\model\ImageTag;

class ImageTagController extends BaseController
{
    public function index()
    {
        $query = ImageTag::order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'color' => input('color', '#1890ff'),
            'description' => input('description'),
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '标签名称不能为空');
        }

        $exists = ImageTag::where('name', $data['name'])->find();
        if ($exists) {
            return $this->error('param_error', '标签名称已存在');
        }

        $tag = ImageTag::create($data);
        return $this->success($tag, '创建成功');
    }

    public function edit($id)
    {
        $tag = ImageTag::find($id);
        if (!$tag) {
            return $this->error('not_found', '标签不存在');
        }

        $data = [];
        foreach (['name', 'color', 'description'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $tag->save($data);
        return $this->success($tag, '更新成功');
    }

    public function delete($id)
    {
        $tag = ImageTag::find($id);
        if (!$tag) {
            return $this->error('not_found', '标签不存在');
        }

        $tag->delete();
        return $this->success(null, '删除成功');
    }
}
