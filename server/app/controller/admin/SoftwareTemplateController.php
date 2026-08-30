<?php
namespace app\controller\admin;

use app\model\SoftwareTemplate;

class SoftwareTemplateController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $query = SoftwareTemplate::order('id', 'desc');
        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'software_ids' => input('software_ids/a', []),
            'is_default' => input('is_default', 0),
            'status' => self::parseStatus(input('status', 'enabled')),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '模板名称不能为空');
        }

        $template = SoftwareTemplate::create($data);
        return $this->success($template, '创建成功');
    }

    public function edit($id)
    {
        $template = SoftwareTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $data = [];
        foreach (['name', 'description', 'software_ids', 'is_default'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parseStatus($statusVal);
        }

        $template->save($data);
        return $this->success($template, '更新成功');
    }

    public function delete($id)
    {
        $template = SoftwareTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $template->delete();
        return $this->success(null, '删除成功');
    }

    public function setDefault($id)
    {
        $template = SoftwareTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        SoftwareTemplate::where('1=1')->update(['is_default' => 0]);
        $template->is_default = 1;
        $template->save();

        return $this->success($template, '已设为默认模板');
    }
}