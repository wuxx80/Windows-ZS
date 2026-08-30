# ZS 装机助手 · Web 后台详细设计

> 版本：v2.1.0
> 日期：2026-08-30
> 技术栈：PHP + Layui + MySQL + Nginx（宝塔面板）

---

## 目录

1. [后台整体布局](#1-后台整体布局)
2. [控制台仪表盘](#2-控制台仪表盘)
3. [镜像管理模块](#3-镜像管理模块)
4. [客户端管理模块](#4-客户端管理模块)
5. [装机任务模块](#5-装机任务模块)
6. [无人值守模块](#6-无人值守模块)
7. [软件管理模块](#7-软件管理模块)
8. [驱动管理模块](#8-驱动管理模块)
9. [网络部署模块](#9-网络部署模块)
10. [PE 定制模块](#10-pe-定制模块)
11. [客户与工单模块](#11-客户与工单模块)
12. [系统管理模块](#12-系统管理模块)
13. [日志审计模块](#13-日志审计模块)
14. [报告统计模块](#14-报告统计模块)
15. [权限与安全](#15-权限与安全)

---

## 1. 后台整体布局

### 1.1 页面框架

`
+------------------------------------------------------------------+
|  ZS 装机助手 管理后台    [搜索]  🔔 3  admin ▼  [退出]           |
+--------+---------------------------------------------------------+
|        |                                                         |
| 菜单栏  |  面包屑: 首页 > 镜像管理 > 镜像列表                    |
|  (可收  |  +---------------------------------------------------+ |
|  缩)    |  |  页面标题 + 操作按钮组                               | |
|        |  |  [新建] [批量操作 ▼] [刷新] [导出]                   | |
|        |  +---------------------------------------------------+ |
|        |  |  搜索/筛选栏                                          | |
|  📊 控  |  |  关键词 [____] 格式 [▼] 状态 [▼] 日期 [▼] [搜索]  | |
|  📦 镜  |  +---------------------------------------------------+ |
|  💻 客  |  |  数据表格/列表                                       | |
|  📋 任  |  |  ID | 名称 | 格式 | 大小 | 状态 | 装机 | 操作      | |
|  🤖 无  |  |  ...                                               | |
|  💾 数  |  |  ...                                               | |
|  🛡️ 病  |  |  ...                                               | |
|  🌐 网  |  +---------------------------------------------------+ |
|  🖥️ PE  |  |  分页: 共 120 条  第 1/6 页  < 1 2 3 ... 6 >    | |
|  🧩 扩  |  +---------------------------------------------------+ |
|  👥 客  |                                                         |
|  📊 报  |                                                         |
|  📋 日  |                                                         |
|  ⚙️ 系  |                                                         |
|        |                                                         |
+--------+---------------------------------------------------------+
|  © 2026 ZS Install Assistant v2.1.0  |  服务器运行时间: 15天    |
+------------------------------------------------------------------+
`

### 1.2 菜单与权限矩阵

| 菜单 | 路径 | 超级管理员 | 管理员 | 操作员 | 只读 |
|------|------|-----------|--------|--------|------|
| 控制台 | /admin/dashboard | ✅ | ✅ | ✅ | ✅ |
| 镜像管理 | /admin/images | ✅ | ✅ | ✅ | ✅ |
| 镜像上传 | /admin/images/upload | ✅ | ✅ | ✅ | ❌ |
| 镜像编辑 | /admin/images/edit | ✅ | ✅ | ✅ | ❌ |
| 镜像删除 | /admin/images/delete | ✅ | ✅ | ❌ | ❌ |
| 客户端管理 | /admin/clients | ✅ | ✅ | ✅ | ✅ |
| 客户端审核 | /admin/clients/approve | ✅ | ✅ | ✅ | ❌ |
| 装机任务 | /admin/tasks | ✅ | ✅ | ✅ | ✅ |
| 创建任务 | /admin/tasks/create | ✅ | ✅ | ✅ | ❌ |
| 无人值守 | /admin/unattend | ✅ | ✅ | ✅ | ✅ |
| 软件管理 | /admin/software | ✅ | ✅ | ✅ | ✅ |
| 驱动管理 | /admin/drivers | ✅ | ✅ | ✅ | ✅ |
| 网络部署 | /admin/pxe | ✅ | ✅ | ❌ | ❌ |
| PE 定制 | /admin/pe | ✅ | ✅ | ❌ | ❌ |
| 客户工单 | /admin/customers | ✅ | ✅ | ✅ | ✅ |
| 报告统计 | /admin/reports | ✅ | ✅ | ✅ | ✅ |
| 日志审计 | /admin/logs | ✅ | ✅ | ✅ | ✅ |
| 系统管理 | /admin/settings | ✅ | ❌ | ❌ | ❌ |

### 1.3 通用组件规范

**搜索栏组件：**
`
关键词输入框 + 下拉筛选(格式/状态/类型/日期) + [搜索] [重置] 按钮
支持回车搜索，支持 URL 参数持久化（刷新后保持筛选条件）
`

**数据表格组件：**
`
- 列：复选框 | ID | 名称 | 关键字段 | 状态标签 | 时间 | 操作
- 操作：查看 | 编辑 | 删除 | 更多(下拉)
- 状态标签：可用(绿色) / 禁用(灰色) / 待审核(黄色) / 错误(红色)
- 排序：支持点击表头排序
- 分页：显示总条数，每页可调(20/50/100)，页码跳转
`

**表单组件：**
`
- 验证：必填 * 标记，实时校验，提交前整体校验
- 上传：拖拽上传 + 点击上传，进度条，文件类型/大小限制
- 选择器：单选/多选/级联选择/搜索选择
- 时间：日期时间选择器，范围选择
- 富文本：描述/备注使用 textarea
`

---

## 2. 控制台仪表盘

### 2.1 页面布局

`
+------------------------------------------------------------------+
|  控制台 · 仪表盘                                                  |
+------------------------------------------------------------------+
|  [今日] [本周] [本月] [全部]  时间选择器                           |
+------------------------------------------------------------------+
|  +-----------+ +-----------+ +-----------+ +-----------+         |
|  | 📦 镜像   | | 💻 在线   | | 📋 今日   | | ⚠️ 待处理  |        |
|  |   45 个   | |   12 台   | |   8 台    | |   3 个    |        |
|  |  ↑ 较昨日  | |   ↑ 70%  | |   ↑ 较昨日 | |   ↓ 较昨日 |        |
|  +-----------+ +-----------+ +-----------+ +-----------+         |
|                                                                  |
|  +----------------------------+  +----------------------------+  |
|  |  装机趋势 (折线图)          |  |  镜像使用排行 (柱状图)      |  |
|  |  ▄▃▅▇▆▄▅▆▇▆▅▃▄▅▆▇          |  |  Win11 ████████ 35%      |  |
|  |  近7日装机量                |  |  Win10 ██████▌ 28%        |  |
|  |                            |  |  Win7  ████▌ 18%         |  |
|  +----------------------------+  +----------------------------+  |
|                                                                  |
|  +----------------------------+  +----------------------------+  |
|  |  最近任务 (列表)            |  |  在线客户端 (列表)          |  |
|  |  [10:23] C01 装机完成  ✅  |  |  PE-7A3B  Win11PE 在线    |  |
|  |  [10:15] C02 装机中 72%   |  |  PE-4C2D  Win10PE 在线    |  |
|  |  [10:00] C03 等待下载      |  |  WIN-9E8F  Win10  离线    |  |
|  |  [更多 →]                  |  |  [更多 →]                  |  |
|  +----------------------------+  +----------------------------+  |
|                                                                  |
|  +----------------------------+  +----------------------------+  |
|  |  快捷操作                    |  |  系统状态                  |  |
|  |  [📦 上传镜像] [📋 创建任务] |  |  服务器: 正常 12ms        |  |
|  |  [💻 PXE 配置] [🖥️ PE定制] |  |  磁盘: 使用 45% / 500GB  |  |
|  |  [📊 查看报表] [🔧 系统设置] |  |  MySQL: 正常 0.02s       |  |
|  +----------------------------+  +----------------------------+  |
+------------------------------------------------------------------+
`

### 2.2 统计卡片 API

`http
GET /api/v1/stats/dashboard
Authorization: Bearer <token>
`

`json
{
  "code": 0,
  "data": {
    "images": { "total": 45, "change": "+3 较昨日" },
    "clients_online": { "total": 12, "percent": 70 },
    "today_installs": { "total": 8, "change": "+2 较昨日" },
    "pending_tasks": { "total": 3, "change": "-1 较昨日" },
    "install_trend": [
      {"date": "08-24", "count": 5},
      {"date": "08-25", "count": 7},
      {"date": "08-26", "count": 4},
      {"date": "08-27", "count": 9},
      {"date": "08-28", "count": 6},
      {"date": "08-29", "count": 11},
      {"date": "08-30", "count": 8}
    ],
    "image_ranking": [
      {"name": "Windows 11 Pro x64", "count": 35, "percent": 35},
      {"name": "Windows 10 LTSC", "count": 28, "percent": 28},
      {"name": "Windows 7 SP1", "count": 18, "percent": 18},
      {"name": "Windows 10 Pro", "count": 12, "percent": 12},
      {"name": "其他", "count": 7, "percent": 7}
    ],
    "recent_tasks": [...],
    "online_clients": [...],
    "system_status": {
      "server": "正常",
      "latency": "12ms",
      "disk_usage": "45%",
      "disk_total": "500GB",
      "mysql": "正常 0.02s"
    }
  }
}
`

### 2.3 后端控制器逻辑

`php
// app/controller/admin/Dashboard.php
class Dashboard extends BaseController {
    public function index() {
         = [
            'images' => ImageModel::where('status', 1)->count(),
            'clients_online' => ClientModel::where('status', 'online')->count(),
            'today_installs' => TaskModel::whereDate('created_at', date('Y-m-d'))
                ->where('status', 'completed')->count(),
            'pending_tasks' => TaskModel::where('status', 'pending')->count(),
        ];
        ->assign('stats', );
        return ->fetch();
    }

    public function getStats() {
         = [
            'install_trend' => ->getInstallTrend(7),
            'image_ranking' => ->getImageRanking(),
            'recent_tasks' => TaskModel::orderBy('created_at', 'desc')->limit(5)->select(),
            'online_clients' => ClientModel::where('status', 'online')->limit(5)->select(),
            'system_status' => ->getSystemStatus(),
        ];
        return json(['code' => 0, 'data' => ]);
    }

    private function getInstallTrend() {
         = [];
        for ( =  - 1;  >= 0; --) {
             = date('Y-m-d', strtotime("- days"));
             = TaskModel::whereDate('created_at', )
                ->where('status', 'completed')->count();
            [] = ['date' => date('m-d', strtotime()), 'count' => ];
        }
        return ;
    }
}
`

---

## 3. 镜像管理模块

### 3.1 镜像列表页

**页面功能：**
- 表格展示所有镜像（ID/名称/格式/大小/系统类型/架构/装机次数/状态/时间）
- 搜索：关键词、格式(WIM/ISO/ESD/GHO)、系统类型(Windows 11/10/7)、状态
- 排序：按上传时间、装机次数、文件大小
- 批量操作：批量删除、批量启用/禁用、批量导出
- 单行操作：查看详情、编辑、删除、下载、校验

**操作按钮：**
`
[上传镜像] [镜像源管理] [格式转换] [批量操作 ▼] [刷新]
`

**API：**
`http
GET /api/v1/images?page=1&per_page=20&keyword=win11&format=wim&status=1&sort=install_count&order=desc
`

**后端控制器：**
`php
// app/controller/admin/ImageController.php
class ImageController extends BaseController {
    public function index() {
         = input('page', 1);
         = input('per_page', 20);
         = input('keyword', '');
         = input('format', '');
         = input('status', '');

         = ImageModel::orderBy('created_at', 'desc');

        if () ->where('name', 'like', "%%");
        if () ->where('format', );
        if ( !== '') ->where('status', );

         = ->paginate(, );
        return json(['code' => 0, 'data' => ]);
    }

    public function detail() {
         = ImageModel::find();
        if (!) return json(['code' => 1001, 'message' => '镜像不存在']);
        return json(['code' => 0, 'data' => ]);
    }

    public function delete() {
         = ImageModel::find();
        if (!) return json(['code' => 1001, 'message' => '镜像不存在']);

        // 删除物理文件
         = ->file_path;
        if (file_exists()) unlink();

        // 删除数据库记录
        ->delete();

        // 记录日志
        LogModel::create([
            'log_type' => 'operation',
            'user_id' => session('user_id'),
            'action' => '删除镜像',
            'target_type' => 'image',
            'target_id' => ,
            'detail' => "删除镜像: {->name}",
        ]);

        return json(['code' => 0, 'message' => '删除成功']);
    }

    public function batchDelete() {
         = input('ids/a', []);
        if (empty()) return json(['code' => 1002, 'message' => '请选择镜像']);

        foreach ( as ) {
             = ImageModel::find();
            if ( && file_exists(->file_path)) {
                unlink(->file_path);
            }
            ImageModel::destroy();
        }
        return json(['code' => 0, 'message' => '批量删除成功']);
    }
}
`

### 3.2 上传镜像页

**页面布局：**
`
+------------------------------------------------------------------+
|  上传镜像                                                          |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐     |
|  │  拖拽文件到此处或点击上传                                  │     |
|  │  支持格式: WIM / ISO / ESD / SWM / GHO                   │     |
|  │  单文件最大: 20GB      │  已选: 0 个文件                   │     |
|  └──────────────────────────────────────────────────────────┘     |
|  ┌──────────────────────────────────────────────────────────┐     |
|  │  上传队列                                                   │    |
|  │  ├ Win11_Pro_x64_22H2.wim  ████████████ 100%  ✅ 已完成  │    |
|  │  ├ Win10_LTSC_2021.esd     ████████░░░░  72%  4.2MB/s   │    |
|  │  └ Win7_SP1_x64.gho        ░░░░░░░░░░░░   0%  等待中    │    |
|  └──────────────────────────────────────────────────────────┘     |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐     |
|  |  镜像信息编辑                                               |    |
|  |  名称: [________________]  *必填                           |    |
|  |  系统类型: [Windows 11 ▼]  架构: [x64 ▼]  语言: [zh-CN ▼] |    |
|  |  版本: [________________]  例如: 22621.3825                |    |
|  |  标签: [推荐, 新硬件, 纯净]  逗号分隔                      |    |
|  |  描述: [____________________________________________]    |    |
|  |  [保存信息]                                                |    |
|  └──────────────────────────────────────────────────────────┘     |
+------------------------------------------------------------------+
`

**上传逻辑（PHP 后端）：**
`php
// app/controller/admin/ImageUploadController.php
class ImageUploadController extends BaseController {
    public function upload() {
         = request()->file('image');
        if (!) return json(['code' => 1002, 'message' => '请选择文件']);

        // 验证文件格式
         = ['wim', 'iso', 'esd', 'swm', 'gho'];
         = strtolower(->extension());
        if (!in_array(, )) {
            return json(['code' => 1003, 'message' => '不支持的镜像格式']);
        }

        // 验证文件大小（最大 20GB）
         = 20 * 1024 * 1024 * 1024;
        if (->getSize() > ) {
            return json(['code' => 1004, 'message' => '文件大小超过限制']);
        }

        // 生成存储路径
         = date('Y/m/d');
         = "/storage/images/{}/";
         = uniqid() . '.' . ;
         = public_path() .  . ;

        // 创建目录
         = dirname();
        if (!is_dir()) mkdir(, 0755, true);

        // 移动文件
        ->move(, );

        // 计算文件哈希
         = hash_file('sha256', );

        // 创建数据库记录
         = ImageModel::create([
            'name' => ->getOriginalName(),
            'file_name' => ,
            'file_path' => ,
            'file_size' => ->getSize(),
            'file_hash' => ,
            'format' => ,
            'source_type' => 'upload',
            'status' => 1,
        ]);

        // 尝试自动解析镜像信息
        ->parseImageInfo();

        return json(['code' => 0, 'message' => '上传成功', 'data' => ['id' => ->id]]);
    }

    private function parseImageInfo() {
        // 对于 WIM/ESD 格式，尝试读取镜像信息
        if (in_array(->format, ['wim', 'esd'])) {
             = "wimlib-imagex info \"{->file_path}\" 2>&1";
             = shell_exec();
            // 解析输出获取系统版本、架构等信息
            // 更新数据库记录
        }
    }
}
`

**前端上传组件（Layui + 分片上传）：**
`javascript
// 大文件分片上传
layui.use('upload', function() {
    var upload = layui.upload;
    upload.render({
        elem: '#uploadBtn',
        url: '/api/v1/images/upload',
        accept: 'file',
        size: 20480, // 20GB
        multiple: true,
        // 大文件分片
        chunked: true,
        chunkSize: 5 * 1024 * 1024, // 5MB 分片
        done: function(res) {
            if (res.code === 0) {
                layer.msg('上传成功');
                table.reload('imageTable');
            }
        },
        progress: function(n, elem, res, index) {
            // 更新进度条
            element.progress('progress-' + index, n + '%');
        }
    });
});
`

### 3.3 镜像编辑页

**页面功能：**
- 编辑镜像基本信息（名称/系统类型/版本/架构/标签/描述）
- 镜像源 URL 编辑（网络拉取源）
- 状态切换（启用/禁用）
- 重新计算哈希
- 删除镜像（含确认弹窗 + 删除物理文件）

**表单字段：**
`php
// 编辑表单验证规则
 = [
    'name' => 'require|max:200',
    'format' => 'require|in:wim,iso,esd,swm,gho',
    'os_type' => 'in:Windows 11,Windows 10,Windows 7,Windows 8,Windows Server,Other',
    'os_edition' => 'max:100',
    'os_arch' => 'in:x64,x86,arm64',
    'os_version' => 'max:50',
    'language' => 'max:20',
    'tags' => 'max:500',
    'description' => 'max:2000',
];
`

### 3.4 镜像源管理

**页面功能：**
- 列表展示所有镜像源（本地/远程/同步）
- 添加镜像源（名称/URL/类型/认证信息）
- 编辑镜像源
- 同步镜像（从远程源拉取镜像列表）
- 测试连接

**镜像源表：**
`sql
CREATE TABLE zs_image_sources (
  id int unsigned NOT NULL AUTO_INCREMENT,
  
ame varchar(100) NOT NULL COMMENT '源名称',
  source_type enum('local','remote','sync') NOT NULL COMMENT '类型',
  url varchar(500) DEFAULT NULL COMMENT 'URL',
  uth_type enum('none','basic','token') DEFAULT 'none' COMMENT '认证类型',
  uth_username varchar(100) DEFAULT NULL COMMENT '用户名',
  uth_password varchar(255) DEFAULT NULL COMMENT '密码',
  uth_token varchar(500) DEFAULT NULL COMMENT 'Token',
  sync_interval int DEFAULT '0' COMMENT '同步间隔(分钟)，0=手动',
  last_sync_at datetime DEFAULT NULL COMMENT '最后同步时间',
  sync_status enum('idle','syncing','success','failed') DEFAULT 'idle' COMMENT '同步状态',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='镜像源管理';
`

**同步逻辑：**
`php
// app/service/ImageSyncService.php
class ImageSyncService {
    public function syncFromSource() {
         = ImageSourceModel::find();
        ->sync_status = 'syncing';
        ->save();

        try {
             = new \GuzzleHttp\Client();
             = ->get(->url, [
                'headers' => ->getAuthHeaders(),
                'timeout' => 30,
            ]);

             = json_decode(->getBody(), true);
            foreach ( as ) {
                // 检查是否已存在
                 = ImageModel::where('file_hash', ['hash'])->find();
                if (!) {
                    // 添加到下载队列
                    DownloadQueueModel::create([
                        'image_name' => ['name'],
                        'remote_url' => ['url'],
                        'file_hash' => ['hash'],
                        'file_size' => ['size'],
                        'status' => 'pending',
                    ]);
                }
            }

            ->last_sync_at = date('Y-m-d H:i:s');
            ->sync_status = 'success';
            ->save();
        } catch (\Exception ) {
            ->sync_status = 'failed';
            ->save();
            throw ;
        }
    }
}
`

### 3.5 格式转换

**页面功能：**
- 选择源镜像（下拉选择）
- 选择目标格式（WIM / ESD / SWM）
- 可选：压缩类型（max/fast/none）、分卷大小（SWM）
- 转换进度显示
- 转换完成后自动添加到镜像列表

**后端转换逻辑：**
`php
// app/controller/admin/ImageConvertController.php
class ImageConvertController extends BaseController {
    public function convert() {
         = input('image_id');
         = input('target_format');
         = input('compress', 'max');

         = ImageModel::find();
        if (!) return json(['code' => 1001, 'message' => '镜像不存在']);

        // 生成输出路径
         = dirname(->file_path) . '/' . pathinfo(->file_name, PATHINFO_FILENAME) . '.' . ;

        // 构建转换命令
        // WIMLIB 转换: wimlib-imagex export <source> <dest> --compress=<type>
        // DISM 转换: dism /Export-Image /SourceImageFile:<source> /SourceIndex:1 /DestinationImageFile:<dest> /Compress:max

         = "wimlib-imagex export \"{->file_path}\" all \"{}\" --compress={} 2>&1";

        // 异步执行（使用后台进程）
         = uniqid('convert_');
         = runtime_path() . "/convert/{}.log";
         = "{} > {} 2>&1 &";

        exec();

        // 记录转换任务
        ConvertTaskModel::create([
            'process_id' => ,
            'source_image_id' => ,
            'target_format' => ,
            'target_path' => ,
            'status' => 'running',
            'log_file' => ,
        ]);

        return json(['code' => 0, 'message' => '转换已开始', 'data' => ['process_id' => ]]);
    }

    public function convertProgress() {
         = ConvertTaskModel::where('process_id', )->find();
        if (!) return json(['code' => 1001, 'message' => '任务不存在']);

        // 读取日志文件获取进度
         = file_get_contents(->log_file);
        // 解析进度信息...

        return json(['code' => 0, 'data' => ]);
    }
}
`

### 3.6 镜像校验

**页面功能：**
- 对已上传镜像重新计算 SHA256
- 与数据库记录的哈希比对
- 显示校验结果（匹配/不匹配/文件缺失）
- 批量校验

**校验逻辑：**
`php
public function verify() {
     = ImageModel::find();
    if (!) return json(['code' => 1001, 'message' => '镜像不存在']);

    if (!file_exists(->file_path)) {
        ->status = 0;
        ->save();
        return json(['code' => 2001, 'message' => '文件缺失', 'data' => ['status' => 'missing']]);
    }

     = hash_file('sha256', ->file_path);
     = filesize(->file_path);

     =  === ->file_hash;
    ->file_hash = ;
    ->file_size = ;
    ->save();

    return json([
        'code' => 0,
        'data' => [
            'status' =>  ? 'matched' : 'mismatch',
            'stored_hash' => ->file_hash,
            'current_hash' => ,
            'file_size' => ,
        ]
    ]);
}
`

### 3.7 镜像详情弹窗

点击镜像行"查看"按钮弹出详情面板：

`
+------------------------------------------------------------------+
|  镜像详情 — Windows 11 Pro x64 22H2                               |
+------------------------------------------------------------------+
|  ┌───────────┐  ┌───────────────────────────────────────────┐    |
|  │  🪟       │  │  名称: Windows 11 Pro x64 22H2           │    |
|  │  镜像预览  │  │  文件: Win11_Pro_22H2.wim               │    |
|  │  (图标)    │  │  路径: /storage/images/.../xxx.wim       │    |
|  │           │  │  格式: WIM         大小: 4.8 GB          │    |
|  └───────────┘  │  哈希: sha256:a3f8...                    │    |
|                 │  系统: Windows 11   架构: x64             │    |
|                 │  版本: 22621.3825   语言: zh-CN           │    |
|                 │  标签: 推荐, 新硬件, 纯净                  │    |
|                 │  来源: 上传         状态: ✔ 可用          │    |
|                 │  上传: 2026-08-15 10:30                   │    |
|                 │  装机: 356 次       下载: 1280 次         │    |
|                 ├───────────────────────────────────────────┤    |
|                 │  描述: 纯净专业版，集成最新补丁至2026年8月   │    |
|                 └───────────────────────────────────────────┘    |
|  [校验] [下载] [编辑] [删除] [关闭]                               |
+------------------------------------------------------------------+
`

---

## 4. 客户端管理模块

### 4.1 客户端列表

**页面功能：**
- 表格展示：ID/名称/类型/版本/IP/MAC/状态/审核状态/最后在线/操作
- 搜索：关键词、类型(WinPE/Windows)、状态(在线/离线/待审核/禁用)、审核状态
- 状态标签：在线(绿色)、离线(灰色)、待审核(黄色)、禁用(红色)
- 操作：查看详情、编辑、审核通过/拒绝、禁用/启用、删除、远程命令

**批量操作：**
`
[批量审核] [批量禁用] [批量启用] [批量删除] [发送命令]
`

### 4.2 客户端详情弹窗

`
+------------------------------------------------------------------+
|  客户端详情 — PE-7A3B2C1D4E5F                                     |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  基本信息                                                 │    |
|  │  ID: PE-7A3B2C1D4E5F     名称: 维修店-PE-01              │    |
|  │  类型: WinPE              版本: 2.1.0.1234                │    |
|  │  MAC: 00:1A:2B:3C:4D:5E  IP: 192.168.1.102              │    |
|  │  主机名: MININT-7A3B2C1   分组: 维修店                    │    |
|  │  状态: ✔ 在线              审核: ✔ 已通过                  │    |
|  │  最后在线: 2026-08-30 10:23:45                            │    |
|  ├──────────────────────────────────────────────────────────┤    |
|  │  硬件信息                                                 │    |
|  │  CPU: Intel(R) Core(TM) i7-12700                        │    |
|  │  内存: 16 GB                                             │    |
|  │  磁盘: KINGSTON SSD 240GB + WDC HDD 1TB                 │    |
|  ├──────────────────────────────────────────────────────────┤    |
|  │  最近任务                                                 │    |
|  │  [10:23] 装机 Windows 11 Pro  ✅ 完成                    │    |
|  │  [09:15] 备份数据              ✅ 完成                    │    |
|  │  [08:00] 引导修复              ✅ 完成                    │    |
|  ├──────────────────────────────────────────────────────────┤    |
|  │  备注: 维修店专用客户端，已预授权                          │    |
|  └──────────────────────────────────────────────────────────┘    |
|  [编辑] [审核] [远程命令] [WOL唤醒] [禁用] [删除] [关闭]        |
+------------------------------------------------------------------+
`

### 4.3 客户端审核

**列表页面：**
`
+------------------------------------------------------------------+
|  客户端登录审核 — 待审核 (5)                                      |
+------------------------------------------------------------------+
|  □ | ID | 名称 | MAC | IP | 注册时间 | 操作                    |
|  ☑ | PE-7A3B | 维修店-PE | 00:1A:2B | 192.168.1.102 | 10:20 | [通过] [拒绝] |
|  ☐ | PE-4C2D | 新客户端 | 00:3C:4D | 192.168.1.103 | 10:22 | [通过] [拒绝] |
|  ☐ | WIN-9E8F | 办公室 | 00:5E:6F | 192.168.1.104 | 10:25 | [通过] [拒绝] |
|  [批量通过] [批量拒绝]                                             |
+------------------------------------------------------------------+
`

**审核逻辑：**
`php
public function approve() {
     = ClientModel::find();
    ->auth_status = 'approved';
    ->status = 'online';
    ->save();

    // 记录日志
    LogModel::create([
        'log_type' => 'operation',
        'user_id' => session('user_id'),
        'action' => '审核通过客户端',
        'target_type' => 'client',
        'target_id' => ,
        'detail' => "客户端 {->name} 审核通过",
    ]);

    return json(['code' => 0, 'message' => '审核通过']);
}
`

### 4.4 客户端分组

**页面功能：**
- 树形分组列表（拖拽排序）
- 创建/编辑/删除分组
- 分配客户端到分组
- 分组统计（每组在线/离线/总数）

**分组表：**
`sql
CREATE TABLE zs_client_groups (
  id int unsigned NOT NULL AUTO_INCREMENT,
  
ame varchar(100) NOT NULL COMMENT '分组名称',
  parent_id int unsigned DEFAULT NULL COMMENT '上级分组',
  sort_order int DEFAULT '0' COMMENT '排序',
  description text COMMENT '描述',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='客户端分组';
`

### 4.5 版本管理

**页面功能：**
- 列表展示所有客户端版本
- 上传新版本（exe/msi/zip 安装包）
- 版本号管理（主版本.次版本.修订.构建号）
- 强制更新策略（可选/推荐/强制）
- 灰度发布（按百分比/按分组/按客户端）
- 版本发布记录

**版本表：**
`sql
CREATE TABLE zs_client_versions (
  id int unsigned NOT NULL AUTO_INCREMENT,
  ersion varchar(20) NOT NULL COMMENT '版本号',
  ersion_code int unsigned NOT NULL COMMENT '版本代码',
  ile_name varchar(255) NOT NULL COMMENT '文件名',
  ile_path varchar(500) NOT NULL COMMENT '存储路径',
  ile_size bigint unsigned DEFAULT '0' COMMENT '文件大小',
  ile_hash varchar(128) DEFAULT NULL COMMENT '文件Hash',
  client_type enum('winpe','windows','all') DEFAULT 'all' COMMENT '客户端类型',
  update_type enum('optional','recommended','force') DEFAULT 'optional' COMMENT '更新类型',
  
elease_scope enum('all','percent','group') DEFAULT 'all' COMMENT '发布范围',
  
elease_percent tinyint unsigned DEFAULT '100' COMMENT '发布百分比',
  
elease_groups text COMMENT '发布分组(JSON)',
  changelog text COMMENT '更新日志',
  is_current tinyint(1) DEFAULT '0' COMMENT '是否当前版本',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_client_type (client_type),
  KEY idx_is_current (is_current)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='客户端版本';
`

### 4.6 远程管理

**页面功能：**
- 远程执行命令（重启/关机/执行脚本/更新配置）
- 远程文件传输（上传文件到客户端/从客户端下载文件）
- 实时状态查看（CPU/内存/磁盘/网络）
- 远程桌面/远程协助（可选）

**命令执行 API：**
`http
POST /api/v1/clients/{id}/command
{
  "command": "execute_script",
  "params": {
    "script": "shutdown /r /t 0",
    "timeout": 30
  }
}
`

**命令队列机制：**
`php
// 命令存储在心跳响应的 commands 字段中
// 客户端下次心跳时获取并执行
// 执行结果在下次心跳时回传
`

---

## 5. 装机任务模块

### 5.1 任务列表

**页面功能：**
- 表格展示：ID/任务名称/类型/客户端/镜像/状态/进度/创建时间/操作
- 搜索：关键词、状态(进行中/已完成/失败/等待)、类型、客户端
- 状态标签：进行中(蓝色)、已完成(绿色)、失败(红色)、等待(灰色)、已取消(黄色)
- 进度条：显示百分比 + 进度条
- 操作：查看详情、取消、重试、删除

**任务详情弹窗：**
`
+------------------------------------------------------------------+
|  任务详情 — #123 Windows 11 Pro 装机                              |
+------------------------------------------------------------------+
|  状态: 进行中 ─── ████████████████████░░░░░░ 72%                |
|  当前步骤: 应用镜像 (72%)                                        |
|  耗时: 1分35秒  预计剩余: 3分钟                                   |
|                                                                  |
|  ┌────────── 步骤清单 ───────────────────────────────────────┐  |
|  │  ✅ 创建任务                    00:01                     │  |
|  │  ✅ 下载镜像                    ⚡ 580MB/s  00:15         │  |
|  │  ✅ 校验镜像                    SHA256 匹配                │  |
|  │  ✅ 备份数据                    45GB → D:\Backup          │  |
|  │  ✅ 分区/格式化                 GPT · 120GB+103GB        │  |
|  │  ⏳ 应用镜像                    WIM 还原 72%              │  |
|  │  📋 注入驱动                    等待中                     │  |
|  │  📋 无人值守                     等待中                     │  |
|  │  📋 引导修复                     等待中                     │  |
|  │  📋 重启系统                     等待中                     │  |
|  └──────────────────────────────────────────────────────────┘  |
|                                                                  |
|  实时日志:                                                       |
|  [10:23:45] 正在应用 Windows 11 Pro 镜像...                    |
|  [10:23:50] WIM 还原进度: 72%                                  |
|  [10:24:00] 预计剩余时间: 约3分钟                              |
|                                                                  |
|  [重试] [取消] [查看客户端] [关闭]                                |
+------------------------------------------------------------------+
`

### 5.2 创建任务

**页面布局：**
`
+------------------------------------------------------------------+
|  创建装机任务                                                      |
+------------------------------------------------------------------+
|  步骤 1: 基本信息                                                 |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  任务名称: [___________]  *必填                           │    |
|  │  任务类型: [装机 ▼]  [备份 ▼]  [还原 ▼]  [修复 ▼]       │    |
|  │  客户端: [选择客户端 ▼] 或 [选择分组 ▼] 或 [所有客户端]   │    |
|  │  优先级: ○ 高  ● 中  ○ 低                               │    |
|  │  调度时间: ○ 立即执行  ● 定时执行: [2026-08-30 14:00]   │    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  步骤 2: 选择镜像                                                 |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  ○ Windows 11 Pro x64 22H2  WIM  4.8GB  装机 #1        │    |
|  │  ● Windows 10 LTSC 2021     ESD  3.2GB  装机 #2        │    |
|  │  ○ Windows 10 Pro x64       ISO  4.2GB                  │    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  步骤 3: 安装选项                                                 |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  ☑ 引导修复  ☑ 无人值守  ☑ 注入驱动  ☐ 自动分区         │    |
|  │  ☑ 备份数据  ☐ 装后装软件  ☑ 系统优化  ☐ 激活           │    |
|  │  ☐ 保留数据                                               │    |
|  │                                                           │    |
|  │  无人值守模板: [维修店标准 ▼]  [编辑]                     │    |
|  │  软件模板: [办公标配 ▼]  [编辑]                           │    |
|  │  驱动包: [自动检测 ▼]                                    │    |
|  │  优化方案: [性能优化 ▼]                                   │    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [创建任务]  [保存为模板]  [取消]                                 |
+------------------------------------------------------------------+
`

### 5.3 任务模板

**页面功能：**
- 基于已有任务保存为模板
- 模板列表展示
- 使用模板快速创建任务

**模板表：**
`sql
CREATE TABLE zs_task_templates (
  id int unsigned NOT NULL AUTO_INCREMENT,
  
ame varchar(100) NOT NULL COMMENT '模板名称',
  description text COMMENT '描述',
  	ask_type enum('install','backup','restore','repair') DEFAULT 'install',
  image_id int unsigned DEFAULT NULL COMMENT '默认镜像',
  options text COMMENT '选项(JSON)',
  unattend_template_id int unsigned DEFAULT NULL COMMENT '默认无人值守模板',
  software_template_id int unsigned DEFAULT NULL COMMENT '默认软件模板',
  created_by int unsigned DEFAULT NULL,
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='任务模板';
`

### 5.4 定时任务

**页面功能：**
- 列表展示所有定时任务
- 创建定时任务（选择客户端+镜像+选项+时间）
- 周期任务（每天/每周/每月）
- 任务日历视图（按日期查看所有定时任务）
- 暂停/恢复/删除定时任务

### 5.5 装机记录

**页面功能：**
- 按月/按年查看装机历史
- 统计图表（每日装机量/镜像分布/客户端排行）
- 详细记录列表（可导出 CSV/Excel）
- 单条记录查看详情

---

## 6. 无人值守模块

### 6.1 模板管理

**页面功能：**
- 列表展示所有无人值守模板
- 创建模板（填写配置项）
- 编辑模板（可视化编辑 JSON）
- 设为默认模板
- 复制模板
- 删除模板
- 验证模板（检查配置项完整性）

**编辑界面：**
`
+------------------------------------------------------------------+
|  编辑无人值守模板 — 维修店标准                                     |
+------------------------------------------------------------------+
|  ┌───── Tab 页 ─────────────────────────────────────────────┐    |
|  |  [基础设置] [用户账户] [网络配置] [磁盘分区] [组件配置]    |    |
|  |  [个性化] [安全配置] [首次登录] [JSON 编辑]               |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [基础设置] 当前 Tab:                                            |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  时区: [China Standard Time ▼]                           |    |
|  |  语言: [zh-CN ▼]                                        |    |
|  |  键盘布局: [0804:00000804 ▼]                            |    |
|  |  产品密钥: [______________]  留空使用内置密钥            |    |
|  |  计算机名: [ZS-PC]  [随机生成]  [使用MAC地址]            |    |
|  |  ☑ 跳过 OOBE 设置                                        |    |
|  |  ☑ 跳过网络配置                                          |    |
|  |  ☐ 自动激活                                              |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [保存] [验证] [另存为] [设为默认] [取消]                          |
+------------------------------------------------------------------+
`

### 6.2 JSON 编辑模式

提供给高级用户直接编辑 JSON 配置：

`json
{
  "general": { "timezone": "China Standard Time", "language": "zh-CN" },
  "user_account": { "username": "Admin", "auto_login": true },
  "network": { "dhcp": true },
  "disk": { "auto_partition": true, "partition_scheme": "gpt" },
  "components": { "disable_defender": true, "disable_uac": true },
  "customization": { "taskbar_align": "left" },
  "first_logon": { "install_software": true, "software_template_id": 1 },
  "security": { "uac_level": "never_notify" }
}
`

### 6.3 验证逻辑

`php
public function validate() {
     = UnattendTemplateModel::find();
    if (!) return json(['code' => 1001, 'message' => '模板不存在']);

    System.Collections.Hashtable = json_decode(->config_data, true);
     = [];

    // 验证必填字段
    if (empty(System.Collections.Hashtable['general']['timezone'])) {
        [] = '时区不能为空';
    }
    if (empty(System.Collections.Hashtable['general']['language'])) {
        [] = '语言不能为空';
    }
    if (System.Collections.Hashtable['user_account']['create_local'] && empty(System.Collections.Hashtable['user_account']['username'])) {
        [] = '用户名不能为空';
    }

    // 验证分区方案
    if (System.Collections.Hashtable['disk']['auto_partition']) {
         = 0;
        foreach (System.Collections.Hashtable['disk']['partitions'] as ) {
            if (['size'] === '0' || strpos(['size'], '剩余') !== false) {
                // 最后一块分区可以是剩余空间
            } else {
                 += (int)filter_var(['size'], FILTER_SANITIZE_NUMBER_INT);
            }
        }
    }

    return json([
        'code' => 0,
        'data' => [
            'valid' => empty(),
            'errors' => ,
            'warnings' => [],
        ]
    ]);
}
`

---

## 7. 软件管理模块

### 7.1 软件列表

**页面功能：**
- 表格展示：ID/名称/分类/版本/大小/安装类型/下载次数/状态/操作
- 搜索：关键词、分类、安装类型(静默/普通/自定义)
- 分类筛选：办公/系统/安全/媒体/网络/开发/驱动/其他
- 操作：编辑、删除、下载、添加到模板

### 7.2 上传软件

**页面布局：**
`
+------------------------------------------------------------------+
|  上传软件                                                          |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  拖拽文件到此处或点击上传                                  │    |
|  │  支持格式: exe / msi / zip / 7z                          │    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  软件信息                                                  |    |
|  |  名称: [________________]  *必填                          |    |
|  |  分类: [办公软件 ▼]  [新建分类]                          |    |
|  |  版本: [________________]  发布者: [________________]     |    |
|  |  安装类型: ○ 静默安装  ● 普通安装  ○ 自定义参数          |    |
|  |  静默参数: [______________]  例如: /S /V""/qn""          |    |
|  |  ☑ 绿色版（免安装）                                      |    |
|  |  ☑ 免费                                                  |    |
|  |  描述: [____________________________________________]    |    |
|  |  [保存]  [取消]                                           |    |
|  └──────────────────────────────────────────────────────────┘    |
+------------------------------------------------------------------+
`

### 7.3 软件分类管理

**页面功能：**
- 分类列表（名称/排序/软件数量/操作）
- 创建/编辑/删除分类
- 拖拽排序

### 7.4 装机软件模板

**页面功能：**
- 列表展示所有软件模板
- 创建模板：选择软件 + 设置安装顺序 + 参数覆盖
- 编辑模板
- 复制模板
- 删除模板

**模板编辑界面：**
`
+------------------------------------------------------------------+
|  编辑软件模板 — 办公标配                                          |
+------------------------------------------------------------------+
|  模板名称: [办公标配______________]                               |
|  描述: [适用于日常办公场景________________]                        |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  可选软件列表                           已选软件 (8)      |    |
|  |  ┌──────────┐  ┌──────────┐              ┌──────────┐   |    |
|  |  | QQ       |  | 百度网盘 |              | 7-Zip 1  |   |    |
|  |  | 钉钉      |  | 迅雷     |  ======>     | 微信 2   |   |    |
|  |  | 网易云    |  | ...      |              | WPS 3    |   |    |
|  |  └──────────┘  └──────────┘              | Chrome 4 |   |    |
|  |                                           | 搜狗 5   |   |    |
|  |                                           └──────────┘   |    |
|  |                                           拖拽调整顺序     |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [保存] [验证] [取消]                                             |
+------------------------------------------------------------------+
`

---

## 8. 驱动管理模块

### 8.1 驱动列表

**页面功能：**
- 表格展示：ID/名称/类型/适用系统/版本/大小/状态/操作
- 分类：NVMe/磁盘控制器/网卡/芯片组/USB3.x/显卡/其他
- 搜索：关键词、类型、适用系统
- 操作：编辑、删除、注入到镜像、下载

### 8.2 上传驱动包

**页面布局：**
`
+------------------------------------------------------------------+
|  上传驱动包                                                        |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  拖拽文件夹或 ZIP 包到此处                                |    |
|  |  支持格式: zip / 7z (自动解压)                            |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  驱动信息                                                  |    |
|  |  名称: [________________]  *必填                          |    |
|  |  类型: [NVMe ▼]  [磁盘控制器 ▼]  [网卡 ▼]  ...          |    |
|  |  适用系统: ☑ Win10  ☑ Win11  ☐ Win7  ☐ Win8            |    |
|  |  版本: [________________]  发布者: [________________]     |    |
|  |  描述: [____________________________________________]    |    |
|  |  [保存]  [取消]                                           |    |
|  └──────────────────────────────────────────────────────────┘    |
+------------------------------------------------------------------+
`

### 8.3 驱动注入到镜像

**页面功能：**
- 选择镜像（下拉选择）
- 选择驱动包（多选）
- 注入配置（强制/不强制签名）
- 注入进度显示
- 注入日志

**注入逻辑：**
`php
public function injectDrivers() {
     = input('image_id');
     = input('driver_ids/a');

     = ImageModel::find();
     = temp_path() . '/mount_' . uniqid();

    // 1. 挂载镜像
     = "dism /Mount-Image /ImageFile:\"{->file_path}\" /Index:1 /MountDir:\"{}\"";
    exec(, , );

    // 2. 注入驱动
    foreach ( as ) {
         = DriverModel::find();
         = ->extracted_path;
         = "dism /Image:\"{}\" /Add-Driver /Driver:\"{}\" /Recurse";
        exec(, , );
    }

    // 3. 卸载镜像
     = "dism /Unmount-Image /MountDir:\"{}\" /Commit";
    exec(, , );

    return json(['code' => 0, 'message' => '驱动注入完成']);
}
`

---

## 9. 网络部署模块

### 9.1 PXE 配置

**页面功能：**
- 编辑 PXE 配置（DHCP 范围/子网/网关/DNS/TFTP 根目录）
- 启动菜单编辑（iPXE 脚本）
- 引导镜像管理
- 服务状态控制（启动/停止/重启）
- 部署日志查看

**配置表单：**
`
+------------------------------------------------------------------+
|  PXE 配置                                                         |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  DHCP 配置                                                |    |
|  |  ☑ 启用 DHCP 服务                                        |    |
|  |  IP 范围: 192.168.1.100 — 192.168.1.200                  |    |
|  |  子网掩码: 255.255.255.0                                  |    |
|  |  网关: 192.168.1.1                                        |    |
|  |  DNS: 114.114.114.114  备选: 8.8.8.8                     |    |
|  |  租约时间: [24] 小时                                      |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  TFTP 配置                                                |    |
|  |  TFTP 根目录: /pxe/boot/                                  |    |
|  |  引导文件: ipxe.efi (UEFI) / ipxe.pxe (Legacy)            |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  启动菜单                                                  |    |
|  |  [编辑 iPXE 脚本]  [预览]                                 |    |
|  |  默认启动项: [网络安装 Windows 11 ▼]                      |    |
|  |  超时时间: [10] 秒                                        |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [保存配置] [重启服务] [停止服务] [测试连接]                       |
+------------------------------------------------------------------+
`

### 9.2 网络克隆

**页面功能：**
- 创建网络克隆任务（选择镜/目标客户端列表/选项）
- 多播配置（组播地址/端口/速率限制）
- 克隆进度实时监控（多个客户端列表 + 各自进度）
- 克隆完成报告

### 9.3 部署报告

**页面功能：**
- 按任务/按时间查看部署报告
- 成功/失败统计
- 失败详情（失败原因/时间/客户端）
- 导出报告

---

## 10. PE 定制模块

### 10.1 PE 版本管理

**页面功能：**
- 列表展示所有 PE 版本
- 上传 PE 基础 ISO/WIM
- 版本信息编辑（名称/版本号/架构/大小）
- 设为当前版本

### 10.2 PE 定制配置

**页面功能：**
- 壁纸更换（上传图片，自动生成预览）
- 启动画面定制（上传图片/动画）
- 启动菜单编辑（菜单项/背景色/字体）
- 桌面图标配置（默认显示哪些图标）
- 主题配置（颜色/字体/透明度）

**配置界面：**
`
+------------------------------------------------------------------+
|  PE 定制 — Win11PE x64 v2.1                                      |
+------------------------------------------------------------------+
|  ┌───── Tab 页 ─────────────────────────────────────────────┐    |
|  |  [壁纸] [启动画面] [启动菜单] [桌面图标] [内置工具] [网络] |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [壁纸] 当前 Tab:                                                |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  ┌──────────────────┐  ┌──────────────────┐             |    |
|  |  │                  │  │                  │             |    |
|  |  │  当前壁纸        │  │  点击更换壁纸    │             |    |
|  |  │  (预览)          │  │  [上传新壁纸]    │             |    |
|  |  └──────────────────┘  └──────────────────┘             |    |
|  |                                                          |    |
|  |  壁纸填充方式: [拉伸 ▼]  [平铺 ▼]  [居中 ▼]  [适应 ▼]  |    |
|  |  壁纸颜色: ■ 纯色  [选择颜色]                            |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [保存] [构建 PE] [导出 ISO] [取消]                               |
+------------------------------------------------------------------+
`

### 10.3 内置工具管理

**页面功能：**
- 列表展示 PE 内置的所有工具
- 添加工具（上传 exe 或绿色版工具）
- 移除工具
- 更新工具版本
- 工具分类管理

### 10.4 PE 构建导出

**页面功能：**
- 选择基础 PE 版本
- 应用定制配置
- 注入驱动
- 添加内置工具
- 构建新模式（生成 WIM）
- 导出为 ISO

**构建逻辑：**
`php
public function build() {
     = input('pe_version_id');
     = PeVersionModel::find();

    // 1. 挂载 PE 基础镜像
     = temp_path() . '/pe_mount_' . uniqid();
    exec("dism /Mount-Image /ImageFile:\"{->file_path}\" /Index:1 /MountDir:\"{}\"");

    // 2. 应用定制配置
     = PeCustomizeModel::where('pe_version_id', )->find();
    // 替换壁纸
    if (->wallpaper_path) {
        copy(->wallpaper_path, "{}/Windows/System32/winpe.jpg");
    }
    // 修改注册表
    exec("reg load HKLM\PE_SOFT {}/Windows/System32/config/SOFTWARE");
    // ... 应用注册表修改
    exec("reg unload HKLM\PE_SOFT");

    // 3. 注入驱动
     = PeDriverModel::where('pe_version_id', )->select();
    foreach ( as ) {
        exec("dism /Image:\"{}\" /Add-Driver /Driver:\"{->path}\" /Recurse");
    }

    // 4. 添加内置工具
     = PeToolModel::where('pe_version_id', )->select();
    foreach ( as ) {
         = "{}/Program Files/ZS_Tools/{->category}";
        if (!is_dir()) mkdir(, 0755, true);
        copy(->file_path, "{}/{->file_name}");
    }

    // 5. 卸载并保存
     = storage_path() . "/pe/output/ZS_PE_{->version}.wim";
    exec("dism /Unmount-Image /MountDir:\"{}\" /Commit");
    copy(->file_path, );

    // 6. 生成 ISO
    // 使用 oscdimg 或 mkisofs 生成 ISO

    return json(['code' => 0, 'message' => 'PE 构建完成', 'data' => ['path' => ]]);
}
`

---

## 11. 客户与工单模块

### 11.1 客户信息管理

**页面功能：**
- 列表展示：姓名/电话/地址/设备数/工单数/最后服务/操作
- 搜索：姓名、电话、关键词
- 创建/编辑/删除客户
- 客户详情弹窗（基本信息 + 历史工单 + 设备记录）

### 11.2 维修工单

**页面功能：**
- 列表展示：工单号/客户/设备/故障/状态/费用/处理人/时间/操作
- 状态：待处理(黄色)、处理中(蓝色)、已完成(绿色)、已取消(灰色)
- 创建工单（选择客户/设备信息/故障描述/服务类型/处理人）
- 编辑工单
- 工单流转（处理中 → 完成 → 收费）
- 工单详情弹窗

**创建工单表单：**
`
+------------------------------------------------------------------+
|  创建维修工单                                                      |
+------------------------------------------------------------------+
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  客户信息                                                  |    |
|  |  选择客户: [搜索或选择 ▼] 或 [新建客户]                   |    |
|  |  姓名: [________]  电话: [________]  地址: [________]     |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  设备信息                                                  |    |
|  |  品牌: [________]  型号: [________]  序列号: [________]  |    |
|  |  故障描述: [________________________________________]    |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  服务信息                                                  |    |
|  |  服务类型: [系统重装 ▼]  [硬件维修 ▼]  [数据恢复 ▼] ...  |    |
|  |  处理人: [admin ▼]                                        |    |
|  |  费用: ¥ [________]  支付状态: [未支付 ▼]               |    |
|  |  保修到期: [________]                                     |    |
|  |  备注: [____________________________________________]    |    |
|  └──────────────────────────────────────────────────────────┘    |
|  [创建] [取消]                                                    |
+------------------------------------------------------------------+
`

### 11.3 工单统计

**页面功能：**
- 今日工单数/本周工单数/本月工单数
- 工单状态分布（饼图）
- 服务类型分布（柱状图）
- 收入统计（折线图）
- 处理人工作量排行

---

## 12. 系统管理模块

### 12.1 系统配置

**页面布局：**
`
+------------------------------------------------------------------+
|  系统配置                                                          |
+------------------------------------------------------------------+
|  ┌───── Tab 页 ─────────────────────────────────────────────┐    |
|  |  [基本设置] [存储设置] [通知设置] [安全设置] [API设置]    |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  [基本设置] 当前 Tab:                                            |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  站点名称: [ZS 装机助手]                                  |    |
|  |  站点 URL: [http://192.168.1.100:8080]                   |    |
|  |  系统语言: [zh-CN ▼]                                     |    |
|  |  时区: [Asia/Shanghai ▼]                                  |    |
|  |  日期格式: [Y-m-d H:i:s ▼]                               |    |
|  |  每页条数: [20]                                           |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  [存储设置]                                               |    |
|  |  镜像存储路径: [/storage/images/]   [可用: 255GB]        |    |
|  |  软件存储路径: [/storage/software/]  [可用: 255GB]       |    |
|  |  驱动存储路径: [/storage/drivers/]   [可用: 255GB]       |    |
|  |  PE 存储路径: [/storage/pe/]        [可用: 255GB]        |    |
|  |  备份存储路径: [/storage/backup/]   [可用: 255GB]        |    |
|  |  存储空间报警阈值: [90] %                                |    |
|  ├──────────────────────────────────────────────────────────┤    |
|  |  [通知设置]                                               |    |
|  |  ☑ 客户端注册通知                                        |    |
|  |  ☑ 任务完成通知                                          |    |
|  |  ☑ 任务失败通知                                          |    |
|  |  ☑ 存储空间不足通知                                      |    |
|  |  通知方式: ☑ 站内通知  ☑ 邮件  ☐ 短信                   |    |
|  |  通知邮箱: [admin@zs-install.com]                        |    |
|  └──────────────────────────────────────────────────────────┘    |
|  [保存]                                                          |
+------------------------------------------------------------------+
`

### 12.2 用户权限管理

**页面功能：**
- 用户列表：用户名/昵称/角色/邮箱/状态/最后登录/操作
- 创建/编辑/删除用户
- 重置密码
- 角色管理：创建/编辑/删除角色，分配权限
- 权限树：按菜单分配权限

**角色表：**
`sql
CREATE TABLE zs_roles (
  id int unsigned NOT NULL AUTO_INCREMENT,
  
ame varchar(50) NOT NULL COMMENT '角色名称',
  description text COMMENT '描述',
  permissions text COMMENT '权限列表(JSON)',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色权限';
`

### 12.3 存储管理

**页面功能：**
- 各存储目录使用情况（饼图）
- 存储空间趋势（折线图）
- 大文件列表（按大小排序，显示所有存储文件）
- 清理缓存（临时文件/过期备份/日志）
- 存储迁移（将文件迁移到其他目录/远程存储）

### 12.4 通知公告

**页面功能：**
- 列表展示所有通知公告
- 创建通知（标题/内容/发布范围/置顶/过期时间）
- 编辑/删除通知
- 通知推送（通知在线客户端）

### 12.5 插件扩展

**页面功能：**
- 插件列表（名称/版本/作者/描述/状态/操作）
- 安装插件（上传插件包）
- 启用/禁用插件
- 卸载插件
- 插件配置

### 12.6 Webhook 配置

**页面功能：**
- 列表展示所有 Webhook
- 创建 Webhook（名称/URL/事件/Secret/状态）
- 编辑/删除 Webhook
- 测试 Webhook
- 发送记录查看

**Webhook 事件列表：**
| 事件 | 说明 | 触发时机 |
|------|------|----------|
| task.created | 任务创建 | 创建装机任务时 |
| task.completed | 任务完成 | 装机任务完成时 |
| task.failed | 任务失败 | 装机任务失败时 |
| client.registered | 客户端注册 | 新客户端注册时 |
| client.approved | 客户端审核通过 | 客户端审核通过时 |
| client.offline | 客户端离线 | 客户端心跳超时 |
| image.uploaded | 镜像上传完成 | 镜像上传完成时 |
| storage.warning | 存储空间不足 | 存储空间低于阈值 |

### 12.7 API 管理

**页面功能：**
- API 密钥列表
- 创建 API 密钥（名称/权限范围/过期时间）
- 启用/禁用密钥
- 删除密钥
- API 调用统计（调用次数/频率/错误率）
- API 文档查看（Swagger/OpenAPI）

---

## 13. 日志审计模块

### 13.1 日志列表

**页面功能：**
- 表格展示：时间/类型/用户/操作/目标/详情/IP
- 搜索：时间范围、类型(操作/登录/客户端/任务/系统/错误)、关键词
- 类型筛选：彩色标签
- 导出日志（CSV/Excel）
- 日志详情弹窗

### 13.2 日志类型分布

**统计图表：**
- 各类型日志占比（饼图）
- 日志趋势（折线图，按天统计）
- 操作频率排行（柱状图，按用户）

### 13.3 日志清理

**清理策略：**
- 自动清理：保留最近 N 天日志（默认 90 天）
- 手动清理：按时间范围/按类型清理
- 归档策略：清理前导出归档

---

## 14. 报告统计模块

### 14.1 装机报表

**页面功能：**
- 时间选择器（日/周/月/年/自定义范围）
- 统计卡片：装机总数/成功/失败/成功率
- 装机趋势图（折线图）
- 每日装机对比（柱状图）
- 镜像使用排行（柱状图）
- 客户端装机排行（柱状图）
- 导出报表（PDF/Excel/CSV）

### 14.2 客户端统计

**页面功能：**
- 在线/离线/待审核/禁用 比例（饼图）
- 客户端版本分布（柱状图）
- 客户端类型分布（WinPE vs Windows）
- 各分组客户端数量（柱状图）

### 14.3 镜像使用排行

**页面功能：**
- 装机次数排行（柱状图）
- 下载次数排行（柱状图）
- 镜像大小分布（饼图）
- 镜像格式分布（饼图）

### 14.4 工单统计

**页面功能：**
- 工单状态分布（饼图）
- 服务类型分布（柱状图）
- 收入统计（折线图）
- 处理人工作量排行（柱状图）
- 客户回访统计

---

## 15. 权限与安全

### 15.1 权限模型

`
用户 → 角色 → 权限

权限粒度: 菜单 + 操作

操作权限: list / create / edit / delete / export / approve
`

### 15.2 权限中间件

`php
// app/middleware/AuthCheck.php
class AuthCheck {
    public function handle(, \Closure ) {
        // 检查登录状态
        if (!session('user_id')) {
            if (->isAjax()) {
                return json(['code' => 401, 'message' => '未登录']);
            }
            return redirect('/admin/login');
        }

        // 检查权限
         = ->pathinfo();
         = session('user_id');
         = UserModel::find();
         = RoleModel::find(->role_id);
         = json_decode(->permissions, true);

        if (!in_array(, )) {
            if (->isAjax()) {
                return json(['code' => 403, 'message' => '权限不足']);
            }
            return view('error/403');
        }

        return ();
    }
}
`

### 15.3 安全措施

| 措施 | 实现方式 |
|------|----------|
| 密码加密 | bcrypt 或 password_hash |
| CSRF 保护 | Token 验证 |
| XSS 防护 | HTML 转义输出 |
| SQL 注入防护 | 参数绑定查询 |
| 文件上传验证 | 格式/大小/MIME 校验 |
| API 限流 | 按 IP/用户 限制请求频率 |
| 登录失败锁定 | 5 次失败锁定 15 分钟 |
| 操作日志记录 | 关键操作自动记录日志 |

### 3.8 镜像下载管理

**页面功能：**
- 镜像下载链接生成（临时/永久链接）
- 下载限速配置（全局/单镜像）
- 下载统计（下载次数/IP/时间）
- 断点续传支持（HTTP Range）
- 下载认证（Token 验证/IP 白名单）

**下载链接生成：**
```php
// app/controller/admin/ImageDownloadController.php
class ImageDownloadController extends BaseController {
    public function generateLink() {
        $image_id = input('image_id');
        $expire_hours = input('expire_hours', 24);
        $speed_limit = input('speed_limit', 0);

        $image = ImageModel::find($image_id);
        if (!$image) return json(['code' => 1001, 'message' => '镜像不存在']);

        $token = bin2hex(random_bytes(32));
        $data = [
            'image_id' => $image_id,
            'token' => $token,
            'expire_at' => date('Y-m-d H:i:s', time() + $expire_hours * 3600),
            'speed_limit' => $speed_limit,
            'ip_restrict' => request()->ip(),
            'created_at' => date('Y-m-d H:i:s'),
        ];
        DownloadLinkModel::create($data);

        $url = url("/api/v1/images/download/{$token}.bin");
        return json(['code' => 0, 'data' => [
            'url' => $url,
            'expire_at' => $data['expire_at'],
            'token' => $token,
        ]]);
    }

    public function download() {
        $token = input('token');
        $link = DownloadLinkModel::where('token', $token)
            ->where('expire_at', '>', date('Y-m-d H:i:s'))->find();
        if (!$link) return json(['code' => 2003, 'message' => '链接无效或已过期']);

        $image = ImageModel::find($link['image_id']);
        if (!$image) return json(['code' => 1001, 'message' => '镜像不存在']);

        header("Accept-Ranges: bytes");
        header("Content-Length: " . filesize($image->file_path));
        header("Content-Type: application/octet-stream");
        header("Content-Disposition: attachment; filename=\"" . $image->file_name . "\"");

        DownloadLogModel::create([
            'image_id' => $image->id,
            'token' => $token,
            'ip' => request()->ip(),
            'user_agent' => request()->header('User-Agent'),
            'downloaded_at' => date('Y-m-d H:i:s'),
        ]);
        $image->increment('download_count');
        readfile($image->file_path);
        exit;
    }
}
```

**下载链接表：**
```sql
CREATE TABLE zs_download_links (
  id int unsigned NOT NULL AUTO_INCREMENT,
  image_id int unsigned NOT NULL COMMENT '镜像ID',
  token varchar(128) NOT NULL COMMENT '下载Token',
  expire_at datetime NOT NULL COMMENT '过期时间',
  speed_limit int unsigned DEFAULT '0' COMMENT '限速(bytes/s)',
  ip_restrict varchar(45) DEFAULT NULL COMMENT 'IP限制',
  download_count int unsigned DEFAULT '0' COMMENT '已下载次数',
  max_downloads int unsigned DEFAULT '0' COMMENT '最大下载次数(0=不限)',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_token (token),
  KEY idx_image_id (image_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='下载链接管理';
```

**下载日志表：**
```sql
CREATE TABLE zs_download_logs (
  id bigint unsigned NOT NULL AUTO_INCREMENT,
  image_id int unsigned NOT NULL COMMENT '镜像ID',
  link_id int unsigned DEFAULT NULL COMMENT '下载链接ID',
  ip varchar(45) NOT NULL COMMENT '下载IP',
  user_agent varchar(500) DEFAULT NULL COMMENT '用户代理',
  referer varchar(500) DEFAULT NULL COMMENT '来源',
  bytes_sent bigint unsigned DEFAULT '0' COMMENT '发送字节数',
  duration int unsigned DEFAULT '0' COMMENT '下载耗时(秒)',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  downloaded_at datetime NOT NULL COMMENT '下载时间',
  PRIMARY KEY (id),
  KEY idx_image_id (image_id),
  KEY idx_ip (ip),
  KEY idx_downloaded_at (downloaded_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='下载日志';
```

### 3.9 镜像版本历史

**页面功能：**
- 查看镜像的所有版本历史
- 版本对比（显示差异字段）
- 版本回滚（恢复到指定版本）
- 版本备注

**版本表：**
```sql
CREATE TABLE zs_image_versions (
  id int unsigned NOT NULL AUTO_INCREMENT,
  image_id int unsigned NOT NULL COMMENT '镜像ID',
  version int unsigned NOT NULL COMMENT '版本号',
  name varchar(200) NOT NULL COMMENT '镜像名称',
  file_name varchar(255) NOT NULL COMMENT '文件名',
  file_path varchar(500) NOT NULL COMMENT '存储路径',
  file_size bigint unsigned DEFAULT '0' COMMENT '文件大小',
  file_hash varchar(128) DEFAULT NULL COMMENT '文件Hash',
  format enum('wim','iso','esd','swm','gho') NOT NULL COMMENT '格式',
  os_type varchar(50) DEFAULT NULL COMMENT '系统类型',
  os_edition varchar(100) DEFAULT NULL COMMENT '版本',
  os_arch enum('x64','x86','arm64') DEFAULT 'x64' COMMENT '架构',
  os_version varchar(50) DEFAULT NULL COMMENT '系统版本号',
  description text COMMENT '描述',
  tags varchar(500) DEFAULT NULL COMMENT '标签',
  status tinyint(1) DEFAULT '1' COMMENT '状态',
  change_log text COMMENT '变更说明',
  created_by int unsigned DEFAULT NULL COMMENT '操作人',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_image_id (image_id, version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='镜像版本历史';
```

### 3.10 镜像标签系统

**页面功能：**
- 标签列表管理（CRUD）
- 自动标签规则（根据名称/格式/版本自动打标签）
- 批量打标签
- 按标签筛选镜像
- 标签统计（每个标签的镜像数量）

**标签表：**
```sql
CREATE TABLE zs_image_tags (
  id int unsigned NOT NULL AUTO_INCREMENT,
  name varchar(50) NOT NULL COMMENT '标签名称',
  color varchar(7) DEFAULT '#1890ff' COMMENT '标签颜色',
  is_auto tinyint(1) DEFAULT '0' COMMENT '是否自动标签',
  auto_rule text COMMENT '自动规则(JSON)',
  sort_order int DEFAULT '0' COMMENT '排序',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uk_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='镜像标签';
```

**镜像-标签关联表：**
```sql
CREATE TABLE zs_image_tag_relations (
  id int unsigned NOT NULL AUTO_INCREMENT,
  image_id int unsigned NOT NULL COMMENT '镜像ID',
  tag_id int unsigned NOT NULL COMMENT '标签ID',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uk_image_tag (image_id, tag_id),
  KEY idx_tag_id (tag_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='镜像标签关联';
```

### 3.11 镜像过期清理策略

**页面功能：**
- 设置过期策略（全局/按镜像）
- 过期镜像列表展示
- 自动清理开关
- 手动清理
- 清理日志

**过期策略配置：**
```php
return [
    'enabled' => true,
    'default_retention_days' => 365,
    'check_interval' => '0 3 * * *',
    'auto_delete' => true,
    'before_delete_backup' => true,
    'recycle_bin_days' => 30,
    'exclude_tags' => ['推荐', '最新系统'],
    'min_install_count' => 5,
    'notify_before_days' => 7,
];
```

### 3.12 增强镜像信息解析

**解析能力：**
- WIM 头部信息读取（映像名称/描述/显示名称/标志）
- 映像索引遍历（多卷 WIM 文件）
- ISO 文件系统信息读取
- 系统版本号精确提取
- 架构信息检测（x86/x64/arm64）
- 安装映像 vs 启动映像识别

**WIM 解析逻辑：**
```php
class ImageInfoParser {
    public function parse($image) {
        switch ($image->format) {
            case 'wim': case 'esd': case 'swm':
                return $this->parseWim($image);
            case 'iso':
                return $this->parseIso($image);
            case 'gho':
                return $this->parseGho($image);
        }
    }

    private function parseWim($image) {
        $info = [];
        $output = shell_exec("wimlib-imagex info \"{$image->file_path}\" --xml 2>&1");

        if ($output) {
            preg_match('/<DISPLAYNAME>(.*?)<\/DISPLAYNAME>/', $output, $m);
            if (!empty($m)) $info['display_name'] = $m[1];

            preg_match('/<DESCRIPTION>(.*?)<\/DESCRIPTION>/', $output, $m);
            if (!empty($m)) $info['description'] = $m[1];

            preg_match('/<VERSION>(.*?)<\/VERSION>/', $output, $m);
            if (!empty($m)) $info['os_version'] = $m[1];

            preg_match('/<ARCH>(\d+)<\/ARCH>/', $output, $m);
            if (!empty($m)) {
                $arch = $m[1];
                $info['os_arch'] = $arch == 0 ? 'x86' : ($arch == 9 ? 'x64' : 'arm64');
            }
        }
        return $info;
    }
}
```

### 3.13 远程镜像添加（外部链接）

**页面功能：**
- 通过外部 URL 直接添加镜像（无需本地上传）
- 支持 HTTP/HTTPS/FTP 协议
- 自动添加到后台下载队列
- 支持断点续传
- 支持批量添加（多个 URL）
- 支持从镜像源同步列表中选择

**添加远程镜像表单：**
```
+------------------------------------------------------------------+
|  添加远程镜像                                                      |
+------------------------------------------------------------------+
|  [Tab: 单条添加]  [Tab: 批量添加]  [Tab: 从源同步]                |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │  单条添加                                                  |    |
|  │  镜像名称: [________________]  *必填                       |    |
|  │  远程 URL: [________________________________]  *必填      |    |
|  │  支持 HTTP/HTTPS/FTP 协议                                  |    |
|  │  ☑ 需要认证  用户名: [________]  密码: [________]         |    |
|  │                                                           |    |
|  │  ☑ 添加后自动下载到本地存储                                |    |
|  │  ☐ 仅添加记录（使用时再下载）                              |    |
|  │                                                           |    |
|  │  镜像格式: [自动检测 ▼]  [WIM] [ISO] [ESD] [GHO]          |    |
|  │  系统类型: [Windows 11 ▼]  架构: [x64 ▼]                 |    |
|  │  标签: [________________]  逗号分隔                        |    |
|  │  描述: [______________________________________]           |    |
|  │  [测试连接]  [添加镜像]  [取消]                            |    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  ┌──────────────────────────────────────────────────────────┐    |
|  |  下载队列                                                    |    |
|  |  ┌──────────────────────────────────────────────────┐    |    |
|  |  │ Win11_Pro_x64_22H2.iso  ████████████ 100% ✅    │    |    |
|  |  │ Win10_LTSC_2021.esd     ██████░░░░░░  60% 3MB/s │    |    |
|  |  │ Win7_SP1_x64.gho        ░░░░░░░░░░░░   0% 等待中 │    |    |
|  |  └──────────────────────────────────────────────────┘    |    |
|  └──────────────────────────────────────────────────────────┘    |
+------------------------------------------------------------------+
```

**API 接口：**
```http
POST /api/v1/images/remote-add
{
  "name": "Windows 11 Pro x64 22H2",
  "remote_url": "https://mirror.example.com/images/win11_pro_22h2.iso",
  "format": "iso",
  "auth_required": true,
  "auth_username": "user",
  "auth_password": "pass",
  "auto_download": true,
  "os_type": "Windows 11",
  "os_arch": "x64",
  "tags": "推荐,新硬件",
  "description": "远程镜像"
}
```

**后端控制器逻辑：**
```php
class ImageRemoteController extends BaseController {
    public function remoteAdd() {
        $name = input('name');
        $remote_url = input('remote_url');
        $format = input('format', '');
        $auto_download = input('auto_download', true);

        if (!filter_var($remote_url, FILTER_VALIDATE_URL)) {
            return json(['code' => 1002, 'message' => 'URL 格式无效']);
        }

        $allowed_protocols = ['http', 'https', 'ftp'];
        $scheme = parse_url($remote_url, PHP_URL_SCHEME);
        if (!in_array($scheme, $allowed_protocols)) {
            return json(['code' => 1003, 'message' => '不支持的协议']);
        }

        if (empty($format)) {
            $ext = strtolower(pathinfo(parse_url($remote_url, PHP_URL_PATH), PATHINFO_EXTENSION));
            $allowed_ext = ['wim', 'iso', 'esd', 'swm', 'gho'];
            if (in_array($ext, $allowed_ext)) $format = $ext;
        }

        $image = ImageModel::create([
            'name' => $name,
            'file_name' => basename(parse_url($remote_url, PHP_URL_PATH)),
            'file_path' => $remote_url,
            'file_size' => 0,
            'format' => $format,
            'os_type' => input('os_type'),
            'os_arch' => input('os_arch'),
            'tags' => input('tags'),
            'description' => input('description'),
            'source_type' => 'remote',
            'status' => $auto_download ? 0 : 1,
        ]);

        if ($auto_download) {
            DownloadQueueModel::create([
                'image_id' => $image->id,
                'remote_url' => $remote_url,
                'auth_required' => input('auth_required', false),
                'auth_username' => input('auth_username'),
                'auth_password' => input('auth_password', '', true),
                'status' => 'pending',
                'priority' => 0,
            ]);
        }

        return json(['code' => 0, 'message' => '远程镜像添加成功', 'data' => ['id' => $image->id]]);
    }

    public function testConnection() {
        $url = input('url');
        if (!filter_var($url, FILTER_VALIDATE_URL)) {
            return json(['code' => 1002, 'message' => 'URL 格式无效']);
        }

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_HEADER => true, CURLOPT_NOBODY => true,
            CURLOPT_TIMEOUT => 10, CURLOPT_FOLLOWLOCATION => true,
        ]);
        curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode >= 200 && $httpCode < 400) {
            return json(['code' => 0, 'data' => ['reachable' => true, 'http_code' => $httpCode]]);
        }
        return json(['code' => 2001, 'message' => "HTTP {$httpCode}"]);
    }
}
```

**远程下载队列管理页面：**
```
+------------------------------------------------------------------+
|  远程下载队列                                                      |
+------------------------------------------------------------------+
|  [全部] [下载中] [已完成] [失败] [等待中]                          |
|                                                                  |
|  □ | ID | 镜像名称 | 来源URL | 进度 | 速度 | 大小 | 状态 | 操作 |
|  ☐ | 1 | Win11 Pro | https://... | 100% | - | 4.8GB | 成功 | [查看] |
|  ☐ | 2 | Win10 LTSC | https://... | 60% | 3.2MB/s | 3.2GB | 下载中 | [暂停] |
|  ☐ | 3 | Win7 SP1 | https://... | 0% | - | 2.1GB | 暂停 | [继续] |
|  ☐ | 4 | Win Server | https://... | - | - | - | 失败 | [重试] |
|                                                                  |
|  [批量重试] [批量删除] [暂停全部] [继续全部]                       |
+------------------------------------------------------------------+
```

**下载队列表：**
```sql
CREATE TABLE zs_download_queue (
  id int unsigned NOT NULL AUTO_INCREMENT,
  image_id int unsigned NOT NULL COMMENT '镜像ID',
  remote_url varchar(1000) NOT NULL COMMENT '远程URL',
  auth_required tinyint(1) DEFAULT '0' COMMENT '需要认证',
  auth_username varchar(100) DEFAULT NULL COMMENT '用户名',
  auth_password varchar(255) DEFAULT NULL COMMENT '密码(加密)',
  priority int DEFAULT '0' COMMENT '优先级',
  status enum('pending','downloading','paused','completed','failed') DEFAULT 'pending' COMMENT '状态',
  progress tinyint unsigned DEFAULT '0' COMMENT '下载进度',
  downloaded_bytes bigint unsigned DEFAULT '0' COMMENT '已下载字节',
  total_bytes bigint unsigned DEFAULT '0' COMMENT '总字节',
  speed int unsigned DEFAULT '0' COMMENT '下载速度(bytes/s)',
  support_range tinyint(1) DEFAULT '0' COMMENT '支持断点续传',
  error_message text COMMENT '错误信息',
  retry_count tinyint unsigned DEFAULT '0' COMMENT '重试次数',
  max_retries tinyint unsigned DEFAULT '3' COMMENT '最大重试次数',
  started_at datetime DEFAULT NULL,
  completed_at datetime DEFAULT NULL,
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_image_id (image_id),
  KEY idx_status (status),
  KEY idx_priority (priority, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='远程下载队列';
```

---

## 16. 统一错误码与响应规范

### 16.1 统一响应格式

所有 API 响应统一格式：
```json
{
  "code": 0,
  "message": "操作成功",
  "data": {},
  "request_id": "req_abc123",
  "timestamp": 1693372800
}
```

**分页格式：**
```json
{
  "code": 0,
  "message": "查询成功",
  "data": {
    "list": [],
    "total": 120,
    "page": 1,
    "per_page": 20,
    "last_page": 6,
    "has_more": true
  }
}
```

### 16.2 全局错误码表

| 错误码 | 分类 | 说明 |
|--------|------|------|
| 0 | 成功 | 操作成功 |
| 1001 | 资源不存在 | 请求的资源/记录不存在 |
| 1002 | 参数错误 | 请求参数校验失败 |
| 1003 | 格式不支持 | 不支持的格式/类型 |
| 1004 | 文件过大 | 文件大小超过限制 |
| 1005 | 资源冲突 | 资源已存在/状态冲突 |
| 1006 | 操作失败 | 操作执行失败 |
| 1007 | 状态不允许 | 当前状态不允许该操作 |
| 2001 | 文件系统错误 | 文件不存在/权限不足/磁盘满 |
| 2002 | 存储空间不足 | 磁盘空间不足 |
| 2003 | 下载链接无效 | 下载链接过期或无效 |
| 2004 | 上传失败 | 文件上传失败 |
| 2005 | 校验失败 | 文件校验和不匹配 |
| 3001 | 认证失败 | 登录凭证无效 |
| 3002 | 权限不足 | 没有操作权限 |
| 3003 | Token 过期 | 访问令牌已过期 |
| 3004 | 客户端未审核 | 客户端尚未通过审核 |
| 4001 | 客户端离线 | 目标客户端不在线 |
| 4002 | 命令执行失败 | 远程命令执行失败 |
| 4003 | 客户端版本不兼容 | 客户端版本过低 |
| 5001 | 任务冲突 | 任务冲突/重复 |
| 5002 | 任务已取消 | 任务已被取消 |
| 5003 | 任务执行失败 | 任务执行过程中出错 |
| 6001 | 镜像处理错误 | WIM/DISM 操作失败 |
| 6002 | 镜像注入失败 | 驱动注入失败 |
| 6003 | 格式转换失败 | 镜像格式转换失败 |
| 7001 | 配置错误 | 系统配置错误 |
| 7002 | 服务不可用 | 依赖服务不可用 |
| 7003 | 数据库错误 | 数据库操作异常 |
| 9001 | 系统内部错误 | 未预期的系统错误 |
| 9002 | 接口限流 | 请求频率超过限制 |
| 9003 | 功能未实现 | 该功能尚未实现 |

---

## 17. 数据库模型关系

### 17.1 核心表关系

```
┌───────────┐     ┌──────────────┐     ┌──────────────────┐
│ zs_users  │────→│ zs_roles     │     │ zs_images        │
└───────────┘     └──────────────┘     └────────┬─────────┘
     │                                          │
     │   ┌──────────────────────┐               ├── zs_image_versions
     ├──→│ zs_operation_logs    │               ├── zs_image_tags
     │   └──────────────────────┘               ├── zs_image_tag_relations
     │                                          ├── zs_image_sources
     │   ┌──────────────────────┐               ├── zs_download_links
     ├──→│ zs_notifications     │               └── zs_download_logs
     │   └──────────────────────┘
     │                          ┌──────────────────┐
     │   ┌──────────────────────┐│ zs_clients       │
     └──→│ zs_webhook_logs      │└────────┬─────────┘
         └──────────────────────┘         │
                                          ├── zs_client_groups
     ┌──────────────────┐                 ├── zs_client_versions
     │ zs_tasks          │────→│ zs_images│ └── zs_client_logs
     └────────┬─────────┘     └──────────┘
              │
              ├── zs_task_templates
              ├── zs_task_steps
              └── zs_task_logs

     ┌──────────────────┐     ┌──────────────────┐
     │ zs_unattend_templates│  │ zs_software      │
     └──────────────────┘     └────────┬─────────┘
                                       │
     ┌──────────────────┐             ├── zs_software_categories
     │ zs_drivers        │             ├── zs_software_templates
     └──────────────────┘             └── zs_template_software

     ┌──────────────────┐     ┌──────────────────┐
     │ zs_pe_versions    │     │ zs_work_orders   │
     └──────────────────┘     └──────────────────┘

     ┌──────────────────┐     ┌──────────────────┐
     │ zs_customers      │────→│ zs_work_orders   │
     └──────────────────┘     └──────────────────┘

     ┌──────────────────┐     ┌──────────────────┐
     │ zs_system_config  │     │ zs_audit_logs    │
     └──────────────────┘     └──────────────────┘
```

### 17.2 主要外键关系

| 表 | 外键 | 关联表 | 说明 |
|----|------|--------|------|
| zs_images | source_id | zs_image_sources.id | 镜像来源 |
| zs_images | parent_id | zs_images.id | 父镜像（版本衍生） |
| zs_image_versions | image_id | zs_images.id | 镜像版本历史 |
| zs_image_tag_relations | image_id | zs_images.id | 镜像标签关联 |
| zs_image_tag_relations | tag_id | zs_image_tags.id | 标签关联 |
| zs_download_links | image_id | zs_images.id | 下载链接 |
| zs_download_logs | image_id | zs_images.id | 下载日志 |
| zs_clients | group_id | zs_client_groups.id | 客户端分组 |
| zs_tasks | image_id | zs_images.id | 装机任务使用的镜像 |
| zs_tasks | client_id | zs_clients.id | 装机任务关联的客户端 |
| zs_tasks | unattend_id | zs_unattend_templates.id | 无人值守模板 |
| zs_work_orders | customer_id | zs_customers.id | 工单客户 |
| zs_work_orders | assignee_id | zs_users.id | 工单处理人 |
| zs_operation_logs | user_id | zs_users.id | 操作人 |
| zs_users | role_id | zs_roles.id | 用户角色 |

---

## 18. 实时通信（WebSocket）

### 18.1 架构设计

```
┌──────────┐     WebSocket      ┌──────────────┐     PHP    ┌──────────┐
│ 浏览器   │◄──────────────────►│ Node.js/WSS  │◄──────────►│ MySQL    │
│ (Layui)  │                    │ (Gateway)    │            └──────────┘
└──────────┘                    └──────┬───────┘
                                       │
                              ┌───────┴────────┐
                              │                │
                        ┌─────▼─────┐   ┌──────▼──────┐
                        │ 客户端PE  │   │ 客户端Win   │
                        └───────────┘   └─────────────┘
```

### 18.2 事件列表

| 事件 | 方向 | 说明 |
|------|------|------|
| task.progress | 服务器→客户端 | 任务进度更新 |
| task.status | 服务器→客户端 | 任务状态变更 |
| task.log | 服务器→客户端 | 任务实时日志 |
| client.status | 服务器→客户端 | 客户端在线状态 |
| client.command | 服务器→客户端 | 远程命令 |
| client.command_result | 客户端→服务器 | 命令执行结果 |
| notification | 服务器→客户端 | 系统通知 |
| install.progress | 服务器→客户端 | 装机进度推送 |
| install.complete | 服务器→客户端 | 装机完成通知 |

### 18.3 前端 WebSocket 连接

```javascript
var ws = {
    socket: null,
    reconnectTimer: null,
    heartbeatTimer: null,

    connect: function() {
        var token = layui.data('zs_admin').token;
        var url = 'ws://' + window.location.host + '/ws?token=' + token;
        this.socket = new WebSocket(url);

        this.socket.onopen = function() {
            ws.startHeartbeat();
        };

        this.socket.onmessage = function(e) {
            var data = JSON.parse(e.data);
            ws.handleEvent(data);
        };

        this.socket.onclose = function() {
            ws.stopHeartbeat();
            ws.reconnect();
        };
    },

    handleEvent: function(data) {
        switch (data.event) {
            case 'task.progress':
                if (typeof window.updateTaskProgress === 'function')
                    window.updateTaskProgress(data.data);
                break;
            case 'task.log':
                if (typeof window.appendTaskLog === 'function')
                    window.appendTaskLog(data.data);
                break;
            case 'client.status':
                if (typeof window.updateClientStatus === 'function')
                    window.updateClientStatus(data.data);
                break;
            case 'notification':
                layui.layer.msg(data.data.message, {icon: 1});
                break;
        }
    },

    reconnect: function() {
        this.reconnectTimer = setTimeout(function() { ws.connect(); }, 5000);
    },

    startHeartbeat: function() {
        this.heartbeatTimer = setInterval(function() {
            if (ws.socket && ws.socket.readyState === WebSocket.OPEN)
                ws.socket.send(JSON.stringify({type: 'ping'}));
        }, 30000);
    },

    stopHeartbeat: function() {
        if (this.heartbeatTimer) {
            clearInterval(this.heartbeatTimer);
            this.heartbeatTimer = null;
        }
    }
};

layui.ready(function() { ws.connect(); });
```

---

## 19. 消息通知系统

### 19.1 通知类型

| 通知类型 | 触发条件 | 通知方式 | 重要程度 |
|----------|----------|----------|----------|
| 客户端注册 | 新客户端注册 | 站内 + 邮件 | 中 |
| 客户端离线 | 心跳超时（>5分钟） | 站内 | 低 |
| 任务完成 | 装机任务成功完成 | 站内 + 邮件 | 中 |
| 任务失败 | 装机任务执行失败 | 站内 + 邮件 | 高 |
| 存储不足 | 磁盘使用率超过阈值 | 站内 + 邮件 | 高 |
| 镜像校验失败 | 镜像校验和不匹配 | 站内 | 中 |
| 版本更新 | 客户端有新版本 | 站内 | 低 |
| 工单分配 | 维修工单被分配 | 站内 | 中 |
| 系统警告 | 系统异常/错误 | 站内 + 邮件 | 高 |
| 登录提醒 | 异地/异常登录 | 邮件 | 高 |

### 19.2 通知表

```sql
CREATE TABLE zs_notifications (
  id bigint unsigned NOT NULL AUTO_INCREMENT,
  type varchar(50) NOT NULL COMMENT '通知类型',
  title varchar(200) NOT NULL COMMENT '标题',
  message text COMMENT '内容',
  level enum('low','medium','high','urgent') DEFAULT 'medium' COMMENT '重要程度',
  sender_id int unsigned DEFAULT NULL COMMENT '发送人ID',
  receiver_id int unsigned DEFAULT NULL COMMENT '接收人ID',
  receiver_type enum('user','role','all') DEFAULT 'user' COMMENT '接收类型',
  related_type varchar(50) DEFAULT NULL COMMENT '关联类型',
  related_id int unsigned DEFAULT NULL COMMENT '关联ID',
  is_read tinyint(1) DEFAULT '0' COMMENT '已读',
  read_at datetime DEFAULT NULL COMMENT '已读时间',
  is_sent tinyint(1) DEFAULT '0' COMMENT '已发送',
  sent_at datetime DEFAULT NULL COMMENT '发送时间',
  extra_data text COMMENT '扩展数据(JSON)',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_receiver (receiver_id, is_read, created_at),
  KEY idx_type (type, created_at),
  KEY idx_related (related_type, related_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='消息通知';
```

---

## 20. 数据备份与恢复

### 20.1 备份策略

| 备份类型 | 频率 | 保留时间 | 存储位置 |
|----------|------|----------|----------|
| 全量备份 | 每周日 03:00 | 30 天 | /storage/backup/full/ |
| 增量备份 | 每天 03:00 | 7 天 | /storage/backup/incremental/ |
| 数据库备份 | 每天 04:00 | 90 天 | /storage/backup/database/ |
| 配置文件备份 | 配置变更时 | 10 个版本 | /storage/backup/config/ |
| 镜像文件备份 | 删除前自动 | 30 天(回收站) | /storage/backup/recycle/ |

### 20.2 备份配置

```php
return [
    'database' => [
        'enabled' => true,
        'type' => 'mysqldump',
        'compress' => 'gzip',
        'exclude_tables' => ['zs_operation_logs', 'zs_download_logs'],
        'save_path' => '/storage/backup/database/',
    ],
    'files' => [
        'enabled' => true,
        'include_paths' => ['/storage/images/', '/storage/software/', '/storage/drivers/', '/storage/pe/'],
        'exclude_patterns' => ['*.tmp', '*.log', 'cache/'],
        'split_size' => 1073741824,
    ],
    'schedule' => [
        'full_backup' => '0 3 * * 0',
        'db_backup' => '0 4 * * *',
        'cleanup' => '0 5 * * *',
    ],
];
```

### 20.3 备份管理页面

**页面功能：**
- 备份列表（类型/时间/大小/状态/操作）
- 手动创建备份
- 备份下载
- 备份还原（选择备份 -> 确认 -> 还原）
- 备份配置
- 备份日志

### 20.4 回收站管理

**页面功能：**
- 回收站列表（来源/类型/文件/删除时间/过期时间/操作）
- 还原（恢复到原位置）
- 彻底删除
- 清空回收站

**回收站表：**
```sql
CREATE TABLE zs_recycle_bin (
  id bigint unsigned NOT NULL AUTO_INCREMENT,
  target_type varchar(50) NOT NULL COMMENT '目标类型',
  target_id int unsigned NOT NULL COMMENT '目标ID',
  origin_path varchar(500) DEFAULT NULL COMMENT '原路径',
  file_path varchar(500) DEFAULT NULL COMMENT '文件路径',
  file_size bigint unsigned DEFAULT '0' COMMENT '文件大小',
  data longtext COMMENT '数据(JSON)',
  deleted_by int unsigned DEFAULT NULL COMMENT '删除人',
  expire_at datetime DEFAULT NULL COMMENT '过期时间',
  created_at datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_target (target_type, target_id),
  KEY idx_expire (expire_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='回收站';
```

---

## 21. 任务调度系统

### 21.1 调度架构

```
┌───────────────────────────────────────────────────────────────┐
│                     ZS 任务调度系统                            │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌───────────────────────────────────────────────────────┐   │
│  │                 调度中心 (PHP)                         │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────────────┐    │   │
│  │  │ 定时任务  │  │ 队列任务  │  │ 周期任务          │    │   │
│  │  │ (Cron)   │  │ (Queue)  │  │ 采集/清理/备份    │    │   │
│  │  └────┬─────┘  └────┬─────┘  └────────┬─────────┘    │   │
│  └───────┼──────────────┼─────────────────┼──────────────┘   │
│          │              │                  │                  │
│          ▼              ▼                  ▼                  │
│  ┌──────────────┐  ┌──────────┐  ┌──────────────────┐       │
│  │  MySQL 事件   │  │ 进程管理  │  │ 日志/告警        │       │
│  └──────────────┘  └──────────┘  └──────────────────┘       │
└───────────────────────────────────────────────────────────────┘
```

### 21.2 定时任务一览

| 任务名称 | 调度表达式 | 说明 |
|----------|-----------|------|
| 客户端心跳检测 | */5 * * * * | 每5分钟检测客户端在线状态 |
| 过期镜像清理 | 0 3 * * * | 每天凌晨3点清理过期镜像 |
| 自动标签更新 | 0 2 * * * | 每天凌晨2点更新自动标签 |
| 数据库备份 | 0 4 * * * | 每天凌晨4点备份数据库 |
| 文件备份 | 0 3 * * 0 | 每周日凌晨3点全量备份 |
| 备份清理 | 0 5 * * * | 每天凌晨5点清理过期备份 |
| 日志清理 | 0 6 * * * | 每天凌晨6点清理过期日志 |
| 回收站清理 | 0 7 * * * | 每天凌晨7点清理过期回收站 |
| 统计汇总 | 0 1 * * * | 每天凌晨1点生成统计报表 |
| 镜像源同步 | 0 */6 * * * | 每6小时同步远程镜像源 |
| 存储空间检查 | 0 */2 * * * | 每2小时检查存储空间 |
| 通知清理 | 0 8 * * * | 每天8点清理已读通知 |

### 21.3 调度任务管理页面

**页面功能：**
- 任务列表（名称/Key/表达式/状态/最后执行/操作）
- 启用/禁用任务
- 手动执行任务
- 查看执行历史
- 执行日志详情
- 编辑任务配置（Cron/参数/重试策略）
- 执行耗时统计（图表）

---

> 文档版本：v2.0
> 最后更新：2026-08-30
> 编写：ZS Studio


