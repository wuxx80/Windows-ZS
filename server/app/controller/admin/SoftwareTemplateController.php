<?php
namespace app\controller\admin;

use app\model\Software;
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

    /**
     * 模板详情：返回模板信息 + 解析后的软件清单（WinPE 生成首次登录脚本用）
     */
    public function detail($id)
    {
        $template = SoftwareTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        // software_ids 兼容两种存储：JSON 数组 或 逗号分隔
        $raw = $template->software_ids;
        if (is_string($raw)) {
            $decoded = json_decode($raw, true);
            $softwareIds = is_array($decoded) ? $decoded : array_filter(array_map('trim', explode(',', (string) $raw)));
        } else {
            $softwareIds = is_array($raw) ? $raw : [];
        }

        $softwareList = [];
        if ($softwareIds) {
            $softwareList = Software::whereIn('id', array_map('intval', $softwareIds))
                ->where('status', 1)
                ->order('sort', 'asc')
                ->select()
                ->toArray();
        }

        $data = $template->toArray();
        $data['software_list'] = $softwareList;
        return $this->success($data);
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