<?php
namespace app\controller\admin;

use app\model\Image;
use think\facade\Db;

class RecycleBinController extends BaseController
{
    public function index()
    {
        $type = input('type', 'image');
        $keyword = input('keyword');

        $query = Image::where('delete_time', '>', 0)
            ->where('delete_time', '<>', '')
            ->order('delete_time', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }

        return $this->paginate($query);
    }

    public function restore($id)
    {
        $image = Image::where('id', $id)
            ->where('delete_time', '>', 0)
            ->find();

        if (!$image) {
            return $this->error('not_found', '资源不存在或未被删除');
        }

        $image->delete_time = 0;
        $image->save();

        return $this->success($image, '已恢复');
    }

    public function delete($id)
    {
        $image = Image::where('id', $id)
            ->where('delete_time', '>', 0)
            ->find();

        if (!$image) {
            return $this->error('not_found', '资源不存在或未被删除');
        }

        $image->force()->delete();
        return $this->success(null, '已永久删除');
    }

    public function clear()
    {
        $beforeDays = input('before_days', 30);
        $date = date('Y-m-d H:i:s', strtotime('-' . $beforeDays . ' days'));

        $count = Image::where('delete_time', '>', 0)
            ->where('delete_time', '<', strtotime($date))
            ->force()
            ->delete();

        return $this->success(['deleted' => $count], '已清理' . $count . '条记录');
    }
}
