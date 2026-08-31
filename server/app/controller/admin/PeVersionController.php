<?php
namespace app\controller\admin;

use app\model\PeVersion;
use app\service\FileService;

class PeVersionController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $query = PeVersion::order('id', 'desc');
        if ($keyword) {
            $query->where('name|version|description', 'like', '%' . $keyword . '%');
        }
        return $this->paginate($query);
    }

    public function create()
    {
        $version = input('version');
        if (empty($version)) {
            return $this->error('param_error', '版本号不能为空');
        }

        $baseOs = input('base_os', '');
        $name = input('name', '');
        if (empty($name)) {
            $name = ($baseOs ? $baseOs . ' ' : '') . 'v' . $version;
        }

        $data = [
            'name' => $name,
            'version' => $version,
            'base_os' => $baseOs,
            'arch' => input('arch', 'x64'),
            'file_name' => input('file_name', ''),
            'file_path' => input('file_path', ''),
            'file_size' => input('file_size', 0),
            'file_hash' => input('file_hash', ''),
            'description' => input('description', ''),
            'is_default' => input('is_default', 0),
            'status' => self::parsePeStatus(input('status', 'published')),
            'created_by' => $this->userId,
        ];

        $pe = PeVersion::create($data);
        return $this->success($pe, '创建成功');
    }

    public function edit($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        $data = [];
        foreach (['name', 'version', 'base_os', 'arch', 'file_name', 'file_path', 'file_size', 'file_hash', 'description', 'is_default'] as $field) {
            $val = input($field);
            if ($val !== null) {
                $data[$field] = $val;
            }
        }
        $statusVal = input('status');
        if ($statusVal !== null) {
            $data['status'] = self::parsePeStatus($statusVal);
        }

        $pe->save($data);
        return $this->success($pe, '更新成功');
    }

    public function delete($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        $pe->delete();
        return $this->success(null, '删除成功');
    }

    /**
     * PE 版本状态解析（后台 UI 用 published/draft/archived，映射为 1/0）
     */
    protected static function parsePeStatus($val): int
    {
        if ($val === null || $val === "") return 1;
        $val = strtolower((string) $val);
        if (in_array($val, ['published', 'enabled', 'active', 'on', '1'])) return 1;
        if (in_array($val, ['draft', 'archived', 'disabled', 'inactive', 'off', '0'])) return 0;
        return intval($val) ? 1 : 0;
    }

    /**
     * 客户端接口：返回启用状态的 PE 版本列表（U盘制作选源）
     */
    public function clientList()
    {
        $list = PeVersion::where('status', 1)
            ->order('is_default', 'desc')
            ->order('id', 'desc')
            ->select()
            ->toArray();

        foreach ($list as &$item) {
            // 本地已托管文件 → 走服务器下载接口（断点续传）；否则返回空
            if (!empty($item['file_path']) && file_exists($item['file_path'])) {
                $item['download_url'] = url('/api/v1/peVersions/' . $item['id'] . '/download');
            } else {
                $item['download_url'] = '';
            }
            $item['size_display'] = self::formatSize((int) ($item['file_size'] ?? 0));
        }
        unset($item);

        return $this->success($list);
    }

    /**
     * 字节数转可读大小（如 1.2 GB），供客户端展示
     */
    protected static function formatSize($bytes): string
    {
        $bytes = (int) $bytes;
        if ($bytes < 1024) return $bytes . ' B';
        $units = ['KB', 'MB', 'GB', 'TB'];
        $i = -1;
        while ($bytes >= 1024 && $i < count($units) - 1) { $bytes /= 1024; $i++; }
        return round($bytes, 1) . ' ' . $units[$i];
    }

    /**
     * 客户端接口：下载 PE 文件（校验启用状态；本地文件流式输出+断点续传）
     */
    public function clientDownload($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }
        if ((int) $pe->status !== 1) {
            return $this->error('disabled', '该PE版本未启用');
        }

        $filePath = $pe->file_path;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error('file_not_found', 'PE文件不存在，请先在后台配置文件路径');
        }

        $fileName = $pe->file_name ?: basename($filePath);
        try {
            FileService::download($filePath, $fileName);
        } catch (\Exception $e) {
            return $this->error('file_not_found', $e->getMessage());
        }
    }
}