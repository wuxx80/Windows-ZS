<?php
namespace app\controller\admin;

use app\model\PeCustomize;
use think\facade\Cache;

class PeCustomizeController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $query = PeCustomize::order('id', 'desc');
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
            'pe_version_id' => input('pe_version_id'),
            'config' => input('config/a', []),
            'include_drivers' => input('include_drivers/a', []),
            'include_software' => input('include_software/a', []),
            'include_scripts' => input('include_scripts/a', []),
            'wallpaper' => input('wallpaper'),
            'boot_logo' => input('boot_logo'),
            'status' => input('status', 0),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '定制名称不能为空');
        }

        $customize = PeCustomize::create($data);
        return $this->success($customize, '创建成功');
    }

    public function edit($id)
    {
        $customize = PeCustomize::find($id);
        if (!$customize) {
            return $this->error('not_found', 'PE定制不存在');
        }

        $data = [];
        foreach (['name', 'description', 'pe_version_id', 'config', 'include_drivers', 'include_software', 'include_scripts', 'wallpaper', 'boot_logo', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $customize->save($data);
        return $this->success($customize, '更新成功');
    }

    public function delete($id)
    {
        $customize = PeCustomize::find($id);
        if (!$customize) {
            return $this->error('not_found', 'PE定制不存在');
        }

        $customize->delete();
        return $this->success(null, '删除成功');
    }

    public function build($id)
    {
        $customize = PeCustomize::find($id);
        if (!$customize) {
            return $this->error('not_found', 'PE定制不存在');
        }

        $customize->status = 'building';
        $customize->build_started_at = date('Y-m-d H:i:s');
        $customize->save();

        return $this->success([
            'id' => $id,
            'status' => 'building',
        ], '构建任务已启动');
    }

    public function download($id)
    {
        $customize = PeCustomize::find($id);
        if (!$customize) {
            return $this->error('not_found', 'PE定制不存在');
        }

        if ($customize->status !== 'completed') {
            return $this->error('param_error', '构建尚未完成');
        }

        $downloadToken = md5(uniqid(mt_rand(), true));
        Cache::set('download_token_pe_' . $downloadToken, $id, 3600);

        return $this->success([
            'download_url' => url('/api/pe/download/' . $downloadToken),
            'token' => $downloadToken,
            'expires_in' => 3600,
        ]);
    }
}
