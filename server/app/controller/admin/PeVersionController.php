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

        // 删除关联的 R7 资产文件
        foreach (['boot_wim_path', 'boot_sdi_path', 'agent_path'] as $field) {
            if (!empty($pe->$field) && file_exists($pe->$field)) {
                @unlink($pe->$field);
            }
        }

        $pe->delete();
        return $this->success(null, '删除成功');
    }

    // ============ R7 PE 资产端点 ============

    /**
     * 上传 PE 资产文件（boot.wim / boot.sdi / agent.exe）。
     * POST /api/v1/peVersions/{id}/uploadAsset?type=bootWim|bootSdi|agent
     */
    public function uploadAsset($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        $type = input('type');
        if (!in_array($type, ['bootWim', 'bootSdi', 'agent'])) {
            return $this->error('param_error', '无效的资产类型，允许：bootWim/bootSdi/agent');
        }

        $file = request()->file('file');
        if (!$file) {
            return $this->error('file_upload_failed', '未检测到上传文件');
        }

        $assetDir = config('upload.path', runtime_path() . 'uploads') . '/pe_assets/' . $id;
        if (!is_dir($assetDir)) {
            mkdir($assetDir, 0755, true);
        }

        $ext = strtolower($file->extension());
        $allowedExt = ['wim', 'sdi', 'exe', 'img'];
        if (!in_array($ext, $allowedExt)) {
            return $this->error('param_error', '文件类型不允许: .' . $ext . '，允许: .wim / .sdi / .exe');
        }

        $fileName = $type . '.' . $ext;
        $filePath = $assetDir . '/' . $fileName;

        $file->move($assetDir, $fileName);
        if (!file_exists($filePath)) {
            return $this->error('file_upload_failed', '文件保存失败');
        }

        $hash = hash_file("sha256", $filePath);
        $size = filesize($filePath);

        // 映射字段名
        $fieldMap = [
            'bootWim' => ['path' => 'boot_wim_path', 'size' => 'boot_wim_size', 'hash' => 'boot_wim_hash'],
            'bootSdi' => ['path' => 'boot_sdi_path', 'size' => 'boot_sdi_size', 'hash' => 'boot_sdi_hash'],
            'agent'   => ['path' => 'agent_path',    'size' => 'agent_size',    'hash' => 'agent_hash'],
        ];
        $fields = $fieldMap[$type];

        $pe->save([
            $fields['path'] => str_replace("\\", "/", $filePath),
            $fields['size'] => $size,
            $fields['hash'] => $hash,
        ]);

        return $this->success([
            'type'      => $type,
            'file_path' => str_replace("\\", "/", $filePath),
            'file_size' => $size,
            'file_hash' => $hash,
        ], $type . ' 上传成功');
    }

    /**
     * 下载 PE 资产文件。
     * GET /api/v1/peVersions/{id}/bootWim
     * GET /api/v1/peVersions/{id}/bootSdi
     * GET /api/v1/peVersions/{id}/agent
     */
    public function bootWim($id)   { return $this->serveAsset($id, 'boot_wim', 'boot.wim'); }
    public function bootSdi($id)   { return $this->serveAsset($id, 'boot_sdi', 'boot.sdi'); }
    public function agent($id)     { return $this->serveAsset($id, 'agent', 'ZS_PE_Agent.exe'); }

    /**
     * 获取 PE 资产元信息（SHA-256 + 大小）。
     * GET /api/v1/peVersions/{id}/assetsMeta
     */
    public function assetsMeta($id)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }

        return $this->success([
            'boot_wim' => [
                'exists' => !empty($pe->boot_wim_path) && file_exists($pe->boot_wim_path),
                'size'   => (int) $pe->boot_wim_size,
                'sha256' => $pe->boot_wim_hash,
            ],
            'boot_sdi' => [
                'exists' => !empty($pe->boot_sdi_path) && file_exists($pe->boot_sdi_path),
                'size'   => (int) $pe->boot_sdi_size,
                'sha256' => $pe->boot_sdi_hash,
            ],
            'agent' => [
                'exists' => !empty($pe->agent_path) && file_exists($pe->agent_path),
                'size'   => (int) $pe->agent_size,
                'sha256' => $pe->agent_hash,
            ],
        ]);
    }

    /**
     * 通用资产服务方法。
     */
    protected function serveAsset($id, string $prefix, string $displayName)
    {
        $pe = PeVersion::find($id);
        if (!$pe) {
            return $this->error('not_found', 'PE版本不存在');
        }
        if ((int) $pe->status !== 1) {
            return $this->error('disabled', '该PE版本未启用');
        }

        $pathField = $prefix . '_path';
        $filePath = $pe->$pathField;
        if (!$filePath || !file_exists($filePath)) {
            return $this->error('file_not_found', $displayName . ' 不存在，请先在后台上传');
        }

        try {
            FileService::download($filePath, $displayName);
        } catch (\Exception $e) {
            return $this->error('file_not_found', $e->getMessage());
        }
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