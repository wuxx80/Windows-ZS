# ZS 装机助手

> 全栈 Windows 装机解决方案 —— Web 管理后台 + WinPE 端 + Windows 客户端
> 当前版本：v0.0.268311（架构切换中，版本号不变）
> **主链路切换声明（2026-09-01 · R7）：原方案「Windows 下单 → U盘/ISO → PE端联网认领任务续装」已废弃；新主链路「Windows 预下载全部资源到任务目录 → BCD 一次性启动项从硬盘加载 PE → PE 完全离线完成装机」。旧代码保留供 Phase 0~2 迁移阶段逐步替换，Phase 3 评估是否移除。**

## 项目简介

ZS 装机助手是一套完整的 Windows 系统安装与维护解决方案，提供 Web 管理后台（PHP + MySQL + Layui 风格）、WinPE 端（.NET 8）和 Windows 客户端（C# WPF）三端协同。主无人值守链路采用 **全离线架构**：用户在正常 Windows 下单时就把镜像 / 驱动 / 软件 / PE WIM / PE Agent 100% 下载完毕并校验，写到 **非系统分区根目录** 的 **ZS_Task** 任务目录，通过 **BCD bootsequence 一次性启动项** 从硬盘加载 PE，PE 端不假设任何网络存在即可完成「分区 → 部署 → 驱动注入 → 引导修复 → SetupComplete.cmd 首次启动自执行」全链路。U 盘 / ISO 生成仅作为 BitLocker / 多引导管理器场景下的兜底逃生通道。

## 功能特性

### 核心功能（R7 新主链路）
- **一键装机（BCD 离线版，P1~P5）**：Windows 端 P1 选盘 → 下载全部资源 → 生成 task.ini / zs_manifest.key → P2 注入 BCD 一次性启动项 → 30 秒倒计时重启 → PE 端 P3 10 秒逃生窗 → P4 八阶段流水线（固件判定 / 分区尾端验证 / SHA256 校验 / 镜像展开 / 驱动注入 / 引导修复 / Unattend 注入 / SetupComplete 注入）→ P5 首次进系统自动装软件 + 系统优化
- **三重逃生安全窗**：Windows 侧 30 秒 shutdown /a 取消；PE 侧 10 秒倒计时，按任意键进入手动装机；任何外部命令 ExitCode 非 0 立即停机
- **PE 永远无网**：真实硬件 50%+ 缺少 PE 网卡驱动，本方案 PE 阶段完全不依赖 HTTP / 网卡
- **固件判定双重方案**：优先注册表快速判定，冲突时以 diskpart Gpt 列事实为准；均未知则停止装机
- **分区脚本尾端验证**：GPT 分支验证 ESP 卷标 + FAT/FAT32；MBR 分支验证 Active 标志
- **U盘制作 / 生成 ISO（兜底）**：写 U 盘 / 生成 ISO 双模式；PE 来源三选一；纯 C# ISO9660 + Joliet + El Torito 双引导生成器
- **工具大全（53 工具 / 10 分类）**：本地运行 + 自动提权；U 盘勾选后 PE 内离线可用
- **绿色软件**：客户端入口卡片（列表 / 详情 / 静默安装）
- **用户注册 / 登录体系**：用户名 + 密码注册登录，本地 SessionStore 保存 Token
- **品牌信息对接后台**：首页品牌渲染 + 边框项（设置 / 登录·退出 / 版权 / 版本 / 联系 / 关于）均读取后台「站点信息」组

### P0 基建缺口（Phase 0 最高优先级）
| # | 缺口 | 影响 |
|---|------|------|
| 1 | RoleController.php 文件缺失 | 后台用户管理角色 CRUD 失效 |
| 2 | 前后端 role 契约不一致（string vs int） | 保存后角色写空或 0 |
| 3 | WinPE_Agent 未入 sln / 未入 Git | 新 clone 缺新主链路 PE 核心执行者 |
| 4 | AuthMiddleware / LogMiddleware 未注册 | zs_logs 永不写入；认证完全手动判断 |
| 5 | 5 个测试脚手架已删未提交 | 原 R2/R4/R6「测试通过」源码不存在 |
| 6 | Git 工作树脏（28改 / 8删 / 6未跟踪） | 新 clone 不能复现本机状态 |

### 后台管理（现行菜单 19 项）
控制台 / 镜像列表 / 镜像源 / 镜像标签 / 无人值守模板 / 软件管理 / 软件分类 / 软件模板 / 驱动 / 脚本 / PE 版本 / PE 定制 / 用户管理 / 系统设置 / 操作日志 / 通知 / 定时任务 / 报表统计 / Webhook 日志 / 回收站。

> 已从菜单移除（底层保留作过渡期复用，Phase 3 评估是否永久删除）：PXE 配置、网络部署、客户端管理（3 项）、任务管理（2 项）、客户 / 工单。

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 后端语言 | PHP | 8.0+ |
| 后端框架 | ThinkPHP 6 | 6.1.4 |
| 数据库 | MySQL | 5.7+ |
| Web 服务器 | Nginx | 1.20+ |
| 后台前端 | Layui 2.9+ 风格 | 原生 HTML/CSS/JS |
| 旧 GUI 客户端 | C# WPF (.NET 8) | WinPE_Client + Windows_Client |
| 新 PE 端核心执行者 | .NET 8 AOT 控制台 | WinPE_Agent |
| API 协议 | RESTful JSON | — |

## 项目真实状态（磁盘实查 · 2026-09-02）

| 项 | 状态 | 说明 |
|---|---|---|
| BCD 离线架构设计 v1 | ✅ 完成 | docs/superpowers/specs/…-design.md |
| 客户端模块调整清单 | ✅ R7-C 新增 | docs/superpowers/specs/2026-09-01-客户端模块调整清单.md |
| 后端骨架（35 表 / 31 控制器 / 36 模型） | ✅ 可用 | 缺 RoleController；中间件未注册 |
| 管理后台前端（22 页 + 登录 + 控制台） | ✅ 基本可用 | 角色分配失效 / 日志页空（P0-1/4） |
| WinPE_Client（WPF 六步 + U盘 + 工具） | ✅ 编译 0 错 0 警 | Phase 3 评估是否降级；R7-C 已新增 TaskIni/TaskIniParser/ManifestValidator 3 个文件供 WinPE_Agent 复用 |
| WinPE_Agent（§6 八阶段流水线） | ✅ R7-C 契约对齐完成 | 已入 sln + Git；新增 --auto/--task/--manifest/--log 参数 + RunAutoMode 入口；编译 0 错 0 警 |
| Windows_Client（登录 + 六步 + U盘 + 工具 + 绿软） | ✅ 编译 0 错 0 警 | Phase 2 改一键装机为 P1+P2（11 个新 Service 待新增） |
| 5 个测试脚手架 | ❌ 已删、Git delete 未提交 | 可选恢复 |
| 旧主链路心跳 / 认领 / 进度 API | ⚠️ 代码保留 | Phase 3 评估删留 |
| Git 工作树 | ⚠️ 本次 R7-C 变更待提交 | 见下方「本次变更」 |

## 文档列表（六件套）

| 文档 | 说明 |
|------|------|
| README.md | 项目说明（本文件） |
| 项目理解报告.md | 架构 + 数据流 + P0 缺口 + 真实完成度 |
| 项目结构.txt | 实际目录结构（对齐 Git + 磁盘） |
| 操作指南.md | 安装 / 配置 / 启动 / 使用 / 部署 |
| 版本更新记录.md | R1~R7 开发轮索引 + R7-C 子轮 + 债务记录 |
| 开发计划表.md | Phase 0~6 任务分解 + 审计官 + 风险 |

> 架构演进参考：
> - docs/superpowers/specs/2026-09-01-zs-perfect-unattended-deployment-v1-design.md（R7 主设计 v1）
> - docs/superpowers/specs/2026-09-01-客户端模块调整清单.md（R7-C 客户端模块调整清单）

## 快速开始

1. 环境：PHP 8.0+、MySQL 5.7+、Nginx（宝塔面板）、Composer、.NET 8 SDK
2. 伪静态：⚠️ **需要你操作**，宝塔面板 → 网站 → 设置 → 伪静态，粘贴 server/thinkphp.nginx.rewrite.conf 内容。严禁修改宝塔 PHP 配置文件。
3. 数据库：source database/install.sql
4. .env：复制 server/.env.example → server/.env，填数据库 + JWT
5. 依赖：cd server ; composer install
6. 后台：http://IP/admin/login.html （admin / admin123）
7. ⚠️ **登录后先做 Phase 0 基建修复**：① 补齐 RoleController + 路由 ② users.html role → role_id ③ 注册 Auth + Log 中间件 ④ WinPE_Agent 入 sln + Git
8. 编译客户端：dotnet restore ZS_Installer.slnx ; dotnet build ZS_Installer.slnx -c Release

## 默认账号
- 后台：admin / admin123
- 客户端测试：wuxx80 / a111111（仅本地联调）

Copyright © 2026 ZS Studio. All rights reserved.