<?php
namespace app\controller\admin;

use app\model\Script;

class ScriptController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $type = input('type');

        $query = Script::order('id', 'desc');

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
            'description' => input('description'),
            'type' => input('type', 'powershell'),
            'content' => input('content'),
            'params' => input('params/a', []),
            'timeout' => input('timeout', 300),
            'run_as' => input('run_as', 'system'),
            'status' => input('status', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['content'])) {
            return $this->error('param_error', '名称和脚本内容不能为空');
        }

        $script = Script::create($data);
        return $this->success($script, '创建成功');
    }

    public function edit($id)
    {
        $script = Script::find($id);
        if (!$script) {
            return $this->error('not_found', '脚本不存在');
        }

        $data = [];
        foreach (['name', 'description', 'type', 'content', 'params', 'timeout', 'run_as', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $script->save($data);
        return $this->success($script, '更新成功');
    }

    public function delete($id)
    {
        $script = Script::find($id);
        if (!$script) {
            return $this->error('not_found', '脚本不存在');
        }

        $script->delete();
        return $this->success(null, '删除成功');
    }

    public function execute($id)
    {
        $script = Script::find($id);
        if (!$script) {
            return $this->error('not_found', '脚本不存在');
        }

        $clientId = input('client_id');
        $params = input('params/a', []);

        if (!$clientId) {
            return $this->error('param_error', '请指定执行目标客户端');
        }

        return $this->success([
            'script_id' => $id,
            'client_id' => $clientId,
            'params' => $params,
            'status' => 'pending',
        ], '脚本执行任务已创建');
    }
}
