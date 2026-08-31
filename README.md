# ZS 装机助手

> 全栈 Windows 装机解决方案 —— Web 管理后台 + WinPE/Windows 客户端

## 项目简介

ZS 装机助手是一套完整的 Windows 系统安装与维护解决方案，提供 Web 管理后台、WinPE 客户端和 Windows 客户端三端协同工作。支持一键装机、U 盘制作、系统修复、驱动注入、无人值守、网络部署等专业功能，适用于电脑维修店、企业 IT 部门、系统集成商等场景。

## 功能特性

### 核心功能
- 一键装机：镜像选择、磁盘分区、驱动注入、无人值守、引导修复全自动完成
- U 盘制作：PE 写入、格式化还原、多 PE 启动盘
- 工具大全：磁盘管理、系统修复、密码重置、数据恢复
- 绿色软件：软件分类、在线安装、静默安装

### 后台管理
- 控制台仪表盘：统计卡片、装机趋势、镜像排行、快捷操作
- 镜像管理：上传/下载/格式转换/校验/版本历史/标签系统
- 客户端管理：注册审核/分组管理/版本管理/远程命令
- 装机任务：创建/调度/监控/日志/模板
- 无人值守模板：可视化编辑/JSON 编辑/验证
- 软件/驱动管理：分类/上传/注入/模板
- 网络部署：PXE 配置/网络克隆/部署报告
- PE 定制：壁纸/启动画面/内置工具/构建导出
- 客户工单：客户信息/维修工单/统计
- 系统管理：用户权限/存储管理/通知/插件/Webhook/API
- 日志审计：操作日志/类型分布/清理
- 报告统计：装机报表/客户端统计/镜像排行/工单统计

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 后端语言 | PHP | 8.0+ |
| 后端框架 | ThinkPHP 6 | 6.1.4 |
| 数据库 | MySQL | 5.7+ |
| 缓存 | 文件缓存 | 默认 |
| Web 服务器 | Nginx | 1.20+ |
| 后台前端 | Layui 2.9+ 风格 | 原生 HTML/CSS/JS |
| 客户端 | C# WPF (.NET 8) | WinPE + Windows 双端 |
| API 协议 | RESTful JSON | - |

## 项目状态

当前版本：v0.0.268311（联调部署完成，客户端可打包发布）

| 阶段 | 状态 |
|------|------|
| 设计文档 | ✅ 已完成 |
| 后端骨架（ThinkPHP 6 + 35张表 + 30个控制器） | ✅ 已完成 |
| 管理后台前端（29个管理页面，Layui风格） | ✅ 已完成 |
| 核心业务逻辑（所有控制器+服务层，浏览器全量测试验证） | ✅ 已完成 |
| WinPE 客户端（.NET 8 WPF，六步向导 + U盘制作 + 工具大全） | ✅ 已完成 |
| Windows 客户端（.NET 8 WPF，六步向导 + U盘制作 + 工具大全） | ✅ 已完成 |
| 客户端集成（注册/建任务/进度上报） | ✅ 已完成 |
| API 全量联调测试（44/44 接口通过） | ✅ 已完成 |
| 客户端端到端回归测试（36 项 PASS） | ✅ 已完成 |
| 全功能流程闭环整理（首页 + 一键装机 + 无人值守全链路，见 设计文档/全功能流程闭环清单.md） | ✅ 已完成 |
| U 盘制作与工具大全（B5 联动：写入内置工具，PE 内离线可用） | ✅ 已完成 |
| 一键安装闭环修复（9 项：真实下载/校验/备份、认领并发保护、身份校验、任务重试复用等） | ✅ 已完成 |
| 性能优化（N+1 查询消除） | ✅ 已完成 |
| 安全审查（认证/CORS/上传/日志） | ✅ 已完成 |
| 部署文档与打包发布（publish.ps1 / deploy_server.ps1） | ✅ 已完成 |

## 文档列表（六件套）

| 文档 | 说明 |
|------|------|
| [README.md](README.md) | 项目说明（本文档） |
| [项目理解报告.md](项目理解报告.md) | 项目架构与技术方案 |
| [项目结构.txt](项目结构.txt) | 目录结构说明 |
| [操作指南.md](操作指南.md) | 安装/配置/启动/使用/部署 |
| [版本更新记录.md](版本更新记录.md) | 版本变更历史 |
| [开发计划表.md](开发计划表.md) | 详细开发任务清单与里程碑 |

## 目录结构

```
Windows-ZS/
├── .gitignore
├── README.md
├── 项目理解报告.md
├── 项目结构.txt
├── 操作指南.md
├── 版本更新记录.md
├── 开发计划表.md                    ← 第六件套
├── ZS_Installer.sln                 ← .NET 解决方案文件
├── publish.ps1                      ← 客户端发布脚本
├── deploy_server.ps1                ← 服务器端部署包制作脚本
├── 设计文档/
│   ├── 详细设计文档.md
│   ├── Web后台详细设计.md
│   ├── 一键装机交互设计.md
│   ├── 全功能流程闭环清单.md          ← 全流程闭环梳理（入口→执行→退出→联动→状态）
│   └── U盘制作与工具大全详细设计.md     ← U盘四步向导 + 工具大全（53工具/10分类）+ B5 工具联动
├── _b5_logic_test/                  ← B5 逻辑测试脚手架（IncludeTools 合并拷贝等 4 项）
├── _b5_ui_harness/                  ← B5 UI 自动化测试脚手架（STA WPF 测试 4 项）
├── database/
│   ├── install.sql                  ← 35张表建表SQL
│   └── migration_closure.sql        ← 闭环轮增量迁移（waiting 状态等）
├── server/                          ← PHP 后端项目
│   ├── app/
│   │   ├── controller/admin/        ← 32个后台控制器
│   │   ├── model/                   ← 36个数据模型
│   │   ├── service/                 ← 8个服务层
│   │   ├── middleware/              ← 3个中间件
│   │   └── exception/              ← 异常处理
│   ├── config/                      ← 11个配置文件
│   ├── route/
│   ├── public/
│   │   ├── index.php                ← ThinkPHP 入口
│   │   ├── index.html               ← 首页（介绍下载网站）
│   │   ├── admin/                   ← 管理后台前端页面
│   │   │   ├── login.html           ← 登录页
│   │   │   ├── index.html           ← 主布局（侧边栏+顶部栏+iframe）
│   │   │   └── pages/               ← 29个功能页面
│   │   └── assets/                  ← 前端静态资源
│   │       ├── css/
│   │       │   ├── admin.css        ← 后台样式
│   │       │   └── style.css        ← 首页样式
│   │       └── js/
│   │           └── admin-common.js  ← 后台通用工具库
│   ├── .env
│   ├── composer.json
│   └── thinkphp.nginx.rewrite.conf  ← 独立伪静态规则
├── WinPE_Client/                    ← WinPE 客户端 (.NET 8 WPF)
│   ├── WinPE_Client.csproj
│   ├── App.xaml
│   ├── MainWindow.xaml
│   ├── InstallWizardWindow.xaml       ← 一键装机六步向导窗口
│   ├── UDiskWindow.xaml               ← U 盘制作四步向导窗口
│   ├── ToolsWindow.xaml               ← 工具大全窗口
│   ├── Models/                       （含 WizardModels.cs 向导模型）
│   ├── Services/                     （含 UDiskService/ToolService）
│   ├── ViewModels/                   （含 InstallWizardViewModel/UDiskViewModel/ToolsViewModel）
│   ├── Data/Tools/tools.json          ← 工具清单（53工具/10分类）
│   └── Helpers/
├── Windows_Client/                  ← Windows 客户端 (.NET 8 WPF)
│   ├── Windows_Client.csproj
│   ├── App.xaml
│   ├── MainWindow.xaml
│   ├── InstallWizardWindow.xaml       ← 一键装机六步向导窗口（安全版）
│   ├── UDiskWindow.xaml               ← U 盘制作四步向导窗口
│   ├── ToolsWindow.xaml               ← 工具大全窗口
│   ├── Models/
│   ├── Services/                     （含 Device/DiskPart/ImageDeploy/UDiskService/ToolService）
│   ├── ViewModels/                   （含 InstallWizardViewModel/UDiskViewModel/ToolsViewModel）
│   ├── Data/Tools/tools.json          ← 工具清单（53工具/10分类）
│   └── Helpers/
└── publish/                         ← 客户端发布产物（git 忽略）
    ├── WinPE_Client/                ← 自包含发布（~146MB）
    └── Windows_Client/              ← 框架依赖发布（~0.5MB）
```

## 快速开始

### 1. 环境要求
- PHP 8.0+
- MySQL 5.7+
- Nginx
- Composer
- 宝塔面板（推荐）

### 2. 配置伪静态（宝塔面板）
进入 宝塔面板 → 网站 → 设置 → 伪静态 → 选择"自定义规则"，粘贴以下内容：

```
location / {
    if (!-e $request_filename) {
        rewrite ^(.*)$ /index.php?s=$1 last;
        break;
    }
}
```

或者直接使用项目中的 server/thinkphp.nginx.rewrite.conf 文件内容。

### 3. 导入数据库
```sql
source /path/to/database/install.sql;
```

### 4. 配置环境
复制 .env.example 为 .env，修改数据库连接信息。

### 5. 安装依赖
```bash
cd server
composer install
```

### 6. 访问后台
浏览器访问 http://你的域名/admin/login.html

### 7. 构建客户端
```powershell
# 发布所有客户端（WinPE 自包含 + Windows 框架依赖）
.\publish.ps1

# 或单独发布
.\publish.ps1 -WinPE
.\publish.ps1 -Windows
```

## 默认管理员
- 用户名：admin
- 密码：admin123

## 许可证
Copyright © 2026 ZS Studio. All rights reserved.