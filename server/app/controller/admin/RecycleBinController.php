<?php
namespace app\controller\admin;

use think\facade\Db;

class RecycleBinController extends BaseController
{
    public function index()
    {
        $type = input('type');
        $keyword = input('keyword');

        $query = Db::name('recycle_bin')->order('deleted_at', 'desc');

        if ($type) {
            $query->where('original_table', $type);
        }
        if ($keyword) {
            $query->where('data', 'like', '%' . $keyword . '%');
        }

        $total = $query->count();
        $page = input('page', 1);
        $limit = input('limit', 20);
        $list = $query->page($page, $limit)->select()->toArray();

        return $this->success([
            'list' => $list,
            'total' => $total,
            'page' => (int)$page,
            'limit' => (int)$limit,
        ]);
    }

    public function restore($id)
    {
        $record = Db::name('recycle_bin')->where('id', $id)->find();
        if (!$record) {
            return $this->error('not_found', '记录不存在');
        }

        $originalData = json_decode($record['data'], true);
        if (!$originalData) {
            return $this->error('param_error', '恢复数据损坏');
        }

        Db::name($record['original_table'])->insert($originalData);
        Db::name('recycle_bin')->where('id', $id)->delete();

        return $this->success(null, '已恢复');
    }

    public function delete($id)
    {
        $record = Db::name('recycle_bin')->where('id', $id)->find();
        if (!$record) {
            return $this->error('not_found', '记录不存在');
        }

        Db::name('recycle_bin')->where('id', $id)->delete();
        return $this->success(null, '已永久删除');
    }

    public function clear()
    {
        $beforeDays = input('before_days', 30);
        $date = date('Y-m-d H:i:s', strtotime('-' . $beforeDays . ' days'));

        $count = Db::name('recycle_bin')
            ->where('deleted_at', '<', $date)
            ->delete();

        return $this->success(['deleted' => $count], '已清理' . $count . '条记录');
    }
}