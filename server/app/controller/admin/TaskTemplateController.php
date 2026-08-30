<?php
namespace app\controller\admin;

use app\model\TaskTemplate;

class TaskTemplateController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $type = input('type');

        $query = TaskTemplate::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        if ($type) {
            $query->where('type', $type);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'type' => input('type', 'install'),
            'description' => input('description'),
            'content' => input('content/a', []),
            'params' => input('params/a', []),
            'is_default' => input('is_default', 0),
            'status' => input('status', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '模板名称不能为空');
        }

        $template = TaskTemplate::create($data);
        return $this->success($template, '创建成功');
    }

    public function edit($id)
    {
        $template = TaskTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $data = [];
        foreach (['name', 'type', 'description', 'content', 'params', 'is_default', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $template->save($data);
        return $this->success($template, '更新成功');
    }

    public function delete($id)
    {
        $template = TaskTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $template->delete();
        return $this->success(null, '删除成功');
    }

    public function setDefault($id)
    {
        $template = TaskTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        TaskTemplate::where('type', $template->type)->update(['is_default' => 0]);
        $template->is_default = 1;
        $template->save();

        return $this->success($template, '已设为默认模板');
    }
}
