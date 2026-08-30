<?php
namespace app\controller\admin;

use app\model\SoftwareCategory;

class SoftwareCategoryController extends BaseController
{
    public function index()
    {
        $query = SoftwareCategory::order('sort', 'asc')->order('id', 'desc');
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'parent_id' => input('parent_id', 0),
            'description' => input('description'),
            'icon' => input('icon'),
            'sort' => input('sort', 0),
            'status' => input('status', 1),
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '分类名称不能为空');
        }

        $category = SoftwareCategory::create($data);
        return $this->success($category, '创建成功');
    }

    public function edit($id)
    {
        $category = SoftwareCategory::find($id);
        if (!$category) {
            return $this->error('not_found', '分类不存在');
        }

        $data = [];
        foreach (['name', 'parent_id', 'description', 'icon', 'sort', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $category->save($data);
        return $this->success($category, '更新成功');
    }

    public function delete($id)
    {
        $category = SoftwareCategory::find($id);
        if (!$category) {
            return $this->error('not_found', '分类不存在');
        }

        $childCount = SoftwareCategory::where('parent_id', $id)->count();
        if ($childCount > 0) {
            return $this->error('param_error', '该分类下存在子分类，无法删除');
        }

        $softwareCount = \app\model\Software::where('category_id', $id)->count();
        if ($softwareCount > 0) {
            return $this->error('param_error', '该分类下存在软件，无法删除');
        }

        $category->delete();
        return $this->success(null, '删除成功');
    }
}
