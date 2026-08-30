<?php
namespace app\controller\admin;

use app\model\UnattendTemplate;

class UnattendController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $osType = input('os_type');

        $query = UnattendTemplate::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        if ($osType) {
            $query->where('os_type', $osType);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'os_type' => input('os_type', 'windows'),
            'os_version' => input('os_version'),
            'content' => input('content'),
            'params' => input('params/a', []),
            'status' => self::parseStatus(input('status', 'enabled')),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['content'])) {
            return $this->error('param_error', '名称和内容不能为空');
        }

        $template = UnattendTemplate::create($data);
        return $this->success($template, '创建成功');
    }

    public function edit($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $data = [];
        foreach (['name', 'description', 'os_type', 'os_version', 'content', 'params'] as $field) {
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
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $template->delete();
        return $this->success(null, '删除成功');
    }

    public function preview($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        return $this->success([
            'id' => $template->id,
            'name' => $template->name,
            'content' => $template->content,
            'rendered' => $template->content,
        ]);
    }

    public function generateXml($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $xml = '<?xml version="1.0" encoding="utf-8"?>' . "\n";
        $xml .= '<unattend xmlns="urn:schemas-microsoft-com:unattend">' . "\n";
        $xml .= '    <settings pass="windowsPE">' . "\n";
        $xml .= '        <component name="Microsoft-Windows-Setup" processorArchitecture="amd64">' . "\n";
        $xml .= '            <UserData>' . "\n";
        $xml .= '                <AcceptEula>true</AcceptEula>' . "\n";
        $xml .= '            </UserData>' . "\n";
        $xml .= '        </component>' . "\n";
        $xml .= '    </settings>' . "\n";
        $xml .= '</unattend>';

        return $this->success([
            'id' => $template->id,
            'name' => $template->name,
            'xml' => $xml,
        ]);
    }

    public function validate($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('not_found', '模板不存在');
        }

        $content = $template->content;
        $errors = [];

        if (empty($content)) {
            $errors[] = '模板内容为空';
        }

        if (strpos($content, '<?xml') === false && strpos($content, '<unattend') === false) {
            $errors[] = '内容缺少XML声明或unattend根元素';
        }

        return $this->success([
            'valid' => empty($errors),
            'errors' => $errors,
        ], empty($errors) ? '验证通过' : '验证失败');
    }
}