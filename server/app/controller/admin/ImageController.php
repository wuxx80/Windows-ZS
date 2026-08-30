<?php
namespace app\controller\admin;

use app\model\Image;
use app\model\ImageVersion;
use app\model\ImageTag;
use think\facade\Db;
use think\facade\Cache;

class ImageController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $format = input('format');
        $status = input('status');

        $query = Image::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description|filename', 'like', '%' . $keyword . '%');
        }
        if ($format) {
            $query->where('format', $format);
        }
        if ($status !== null && $status !== '') {
            $query->where('status', $status);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'filename' => input('filename'),
            'format' => input('format', 'wim'),
            'file_size' => input('file_size', 0),
            'sha256' => input('sha256'),
            'os_type' => input('os_type'),
            'os_version' => input('os_version'),
            'os_arch' => input('os_arch', 'x64'),
            'source_id' => input('source_id', 0),
            'status' => input('status', 1),
            'created_by' => $this->userId,
        ];

        if (empty($data['name']) || empty($data['filename'])) {
            return $this->error('param_error', '名称和文件名不能为空');
        }

        $image = Image::create($data);
        return $this->success($image, '创建成功');
    }

    public function edit($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }

        $data = [];
        foreach (['name', 'description', 'filename', 'format', 'file_size', 'sha256', 'os_type', 'os_version', 'os_arch', 'source_id', 'status'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }

        $image->save($data);
        return $this->success($image, '更新成功');
    }

    public function delete($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }

        $image->delete_time = time();
        $image->save();
        return $this->success(null, '已移入回收站');
    }

    public function detail($id)
    {
        $image = Image::with(['versions', 'tags'])->find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }
        return $this->success($image);
    }

    public function upload()
    {
        $file = request()->file('file');
        if (!$file) {
            return $this->error('file_upload_failed', '未检测到上传文件');
        }

        $chunk = input('chunk', 0);
        $chunks = input('chunks', 1);
        $md5 = input('md5');
        $filename = input('filename');

        if ($chunks > 1) {
            $chunkDir = runtime_path() . 'chunks/' . $md5;
            if (!is_dir($chunkDir)) {
                mkdir($chunkDir, 0755, true);
            }
            $file->move($chunkDir, $chunk . '.part');

            if ($chunk == $chunks - 1) {
                $finalPath = config('upload.path') . '/' . $filename;
                $fp = fopen($finalPath, 'wb');
                for ($i = 0; $i < $chunks; $i++) {
                    $partFile = $chunkDir . '/' . $i . '.part';
                    if (file_exists($partFile)) {
                        fwrite($fp, file_get_contents($partFile));
                        unlink($partFile);
                    }
                }
                fclose($fp);
                rmdir($chunkDir);

                $fileSize = filesize($finalPath);
                return $this->success([
                    'path' => $finalPath,
                    'size' => $fileSize,
                    'filename' => $filename,
                ], '上传完成');
            }

            return $this->success([
                'chunk' => $chunk,
                'chunks' => $chunks,
                'md5' => $md5,
            ], '分片上传中');
        }

        $info = $file->move(config('upload.path'), $filename ?: '');
        if (!$info) {
            return $this->error('file_upload_failed', $file->getError());
        }

        return $this->success([
            'path' => $info->getPathname(),
            'size' => $info->getSize(),
            'filename' => $info->getFilename(),
        ], '上传成功');
    }

    public function uploadComplete()
    {
        $md5 = input('md5');
        $filename = input('filename');

        if (!$md5 || !$filename) {
            return $this->error('param_error');
        }

        $finalPath = config('upload.path') . '/' . $filename;
        if (!file_exists($finalPath)) {
            return $this->error('file_not_found');
        }

        $sha256 = hash_file('sha256', $finalPath);
        $fileSize = filesize($finalPath);

        return $this->success([
            'path' => $finalPath,
            'size' => $fileSize,
            'sha256' => $sha256,
            'filename' => $filename,
        ]);
    }

    public function addRemoteUrl()
    {
        $url = input('url');
        $name = input('name');
        $format = input('format');

        if (!$url || !$name) {
            return $this->error('param_error', 'URL和名称不能为空');
        }

        $data = [
            'name' => $name,
            'description' => input('description'),
            'filename' => basename($url),
            'format' => $format ?: pathinfo($url, PATHINFO_EXTENSION),
            'source_url' => $url,
            'status' => 0,
            'created_by' => $this->userId,
        ];

        $image = Image::create($data);
        return $this->success($image, '远程镜像已添加');
    }

    public function verify($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }

        $filePath = config('upload.path') . '/' . $image->filename;
        if (!file_exists($filePath)) {
            return $this->error('file_not_found');
        }

        $sha256 = hash_file('sha256', $filePath);
        $match = $sha256 === $image->sha256;

        $image->sha256 = $sha256;
        $image->save();

        return $this->success([
            'sha256' => $sha256,
            'expected' => $image->sha256,
            'match' => $match,
        ], $match ? '校验通过' : '校验失败');
    }

    public function convert($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }

        $targetFormat = input('target_format');
        if (!$targetFormat) {
            return $this->error('param_error', '请指定目标格式');
        }

        return $this->success([
            'image_id' => $id,
            'target_format' => $targetFormat,
            'status' => 'pending',
        ], '转换任务已创建');
    }

    public function batchDelete()
    {
        $ids = input('ids/a');
        if (empty($ids)) {
            return $this->error('param_error', '请选择要删除的镜像');
        }

        Image::whereIn('id', $ids)->update(['delete_time' => time()]);
        return $this->success(null, '批量删除成功');
    }

    public function batchEnable()
    {
        $ids = input('ids/a');
        if (empty($ids)) {
            return $this->error('param_error', '请选择要启用的镜像');
        }

        Image::whereIn('id', $ids)->update(['status' => 1]);
        return $this->success(null, '批量启用成功');
    }

    public function batchDisable()
    {
        $ids = input('ids/a');
        if (empty($ids)) {
            return $this->error('param_error', '请选择要禁用的镜像');
        }

        Image::whereIn('id', $ids)->update(['status' => 0]);
        return $this->success(null, '批量禁用成功');
    }

    public function download($id)
    {
        $image = Image::find($id);
        if (!$image) {
            return $this->error('image_not_found');
        }

        $filePath = config('upload.path') . '/' . $image->filename;
        if (!file_exists($filePath)) {
            return $this->error('file_not_found');
        }

        $downloadToken = md5(uniqid(mt_rand(), true));
        Cache::set('download_token_' . $downloadToken, [
            'image_id' => $id,
            'path' => $filePath,
            'filename' => $image->filename,
        ], 3600);

        return $this->success([
            'download_url' => url('/api/download/' . $downloadToken),
            'token' => $downloadToken,
            'expires_in' => 3600,
        ]);
    }

    public function restore($id)
    {
        $versionId = input('version_id');
        if (!$versionId) {
            return $this->error('param_error', '请指定版本ID');
        }

        $version = ImageVersion::find($versionId);
        if (!$version || $version->image_id != $id) {
            return $this->error('not_found', '版本记录不存在');
        }

        $image = Image::find($id);
        $image->save($version->snapshot);

        return $this->success($image, '版本恢复成功');
    }
}
