-- ============================================================
-- ZS 装机助手 - 数据库安装脚本
-- 版本: 1.0.0
-- 引擎: InnoDB
-- 字符集: utf8mb4
-- ============================================================

CREATE DATABASE IF NOT EXISTS `zs_installer` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `zs_installer`;
SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================
-- 1. zs_users - 管理后台用户
-- ============================================================
DROP TABLE IF EXISTS `zs_users`;
CREATE TABLE `zs_users` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '用户ID',
  `username` varchar(50) NOT NULL COMMENT '用户名',
  `password` varchar(255) NOT NULL COMMENT '密码',
  `nickname` varchar(50) DEFAULT NULL COMMENT '昵称',
  `email` varchar(100) DEFAULT NULL COMMENT '邮箱',
  `avatar` varchar(255) DEFAULT NULL COMMENT '头像',
  `role_id` int unsigned DEFAULT NULL COMMENT '角色ID',
  `is_super` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否超级管理员(1=是,0=否)',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `last_login_time` datetime DEFAULT NULL COMMENT '最后登录时间',
  `last_login_ip` varchar(50) DEFAULT NULL COMMENT '最后登录IP',
  `login_count` int unsigned NOT NULL DEFAULT '0' COMMENT '登录次数',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `deleted_at` datetime DEFAULT NULL COMMENT '删除时间(软删除)',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_username` (`username`),
  KEY `idx_role_id` (`role_id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='管理后台用户';

-- ============================================================
-- 2. zs_roles - 角色权限表
-- ============================================================
DROP TABLE IF EXISTS `zs_roles`;
CREATE TABLE `zs_roles` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '角色ID',
  `name` varchar(50) NOT NULL COMMENT '角色名称',
  `code` varchar(50) NOT NULL COMMENT '角色编码',
  `description` varchar(255) DEFAULT NULL COMMENT '角色描述',
  `permissions` longtext COMMENT '权限列表(JSON数组,存储权限编码)',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色权限表';

-- ============================================================
-- 3. zs_images - 镜像管理
-- ============================================================
DROP TABLE IF EXISTS `zs_images`;
CREATE TABLE `zs_images` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '镜像ID',
  `name` varchar(200) NOT NULL COMMENT '镜像名称',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_path` varchar(500) NOT NULL COMMENT '文件路径',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `file_hash` varchar(128) DEFAULT NULL COMMENT '文件哈希(SHA256)',
  `format` enum('wim','iso','esd','swm','gho') NOT NULL COMMENT '镜像格式',
  `os_type` varchar(50) DEFAULT NULL COMMENT '操作系统类型(如Windows 10, Windows 11)',
  `os_edition` varchar(100) DEFAULT NULL COMMENT '操作系统版本(如专业版,企业版)',
  `os_arch` enum('x64','x86','arm64') NOT NULL DEFAULT 'x64' COMMENT '系统架构',
  `os_version` varchar(50) DEFAULT NULL COMMENT '系统版本号',
  `language` varchar(20) NOT NULL DEFAULT 'zh-CN' COMMENT '语言',
  `description` text COMMENT '描述信息',
  `tags` varchar(500) DEFAULT NULL COMMENT '标签(逗号分隔)',
  `source_id` int unsigned DEFAULT NULL COMMENT '来源源ID',
  `source_type` enum('upload','download','import','sync') NOT NULL DEFAULT 'upload' COMMENT '来源类型',
  `download_count` int unsigned NOT NULL DEFAULT '0' COMMENT '下载次数',
  `approved_at` datetime DEFAULT NULL COMMENT '审批通过时间',
  `approved_by` int unsigned DEFAULT NULL COMMENT '审批人(管理员用户ID)',
  `install_count` int unsigned NOT NULL DEFAULT '0' COMMENT '安装次数',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `version` int unsigned NOT NULL DEFAULT '1' COMMENT '当前版本号',
  `parent_id` int unsigned DEFAULT NULL COMMENT '父镜像ID(版本继承)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_format` (`format`),
  KEY `idx_os_type` (`os_type`),
  KEY `idx_status` (`status`),
  KEY `idx_parent_id` (`parent_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='镜像管理';
-- ============================================================
-- 4. zs_image_sources - 镜像源管理
-- ============================================================
DROP TABLE IF EXISTS `zs_image_sources`;
CREATE TABLE `zs_image_sources` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '源ID',
  `name` varchar(100) NOT NULL COMMENT '源名称',
  `source_type` enum('local','remote','network') NOT NULL COMMENT '源类型',
  `url` varchar(500) DEFAULT NULL COMMENT '源地址',
  `protocol` enum('http','https','ftp','smb') NOT NULL DEFAULT 'http' COMMENT '传输协议',
  `auth_type` enum('none','basic','token') NOT NULL DEFAULT 'none' COMMENT '认证方式',
  `auth_username` varchar(100) DEFAULT NULL COMMENT '认证用户名',
  `auth_password` varchar(255) DEFAULT NULL COMMENT '认证密码',
  `sync_interval` int unsigned NOT NULL DEFAULT '0' COMMENT '同步间隔(分钟,0=手动)',
  `last_sync_time` datetime DEFAULT NULL COMMENT '最后同步时间',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_source_type` (`source_type`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='镜像源管理';

-- ============================================================
-- 5. zs_image_versions - 镜像版本历史
-- ============================================================
DROP TABLE IF EXISTS `zs_image_versions`;
CREATE TABLE `zs_image_versions` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '记录ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `version` int unsigned NOT NULL COMMENT '版本号',
  `file_hash` varchar(128) NOT NULL COMMENT '文件哈希',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `change_log` text COMMENT '变更日志',
  `operator_id` int unsigned DEFAULT NULL COMMENT '操作人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_image_id` (`image_id`),
  CONSTRAINT `fk_iv_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='镜像版本历史';

-- ============================================================
-- 6. zs_image_tags - 镜像标签
-- ============================================================
DROP TABLE IF EXISTS `zs_image_tags`;
CREATE TABLE `zs_image_tags` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '标签ID',
  `name` varchar(50) NOT NULL COMMENT '标签名称',
  `color` varchar(7) NOT NULL DEFAULT '#1890FF' COMMENT '标签颜色(十六进制)',
  `is_auto` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否自动标签(1=是,0=否)',
  `auto_rule` varchar(500) DEFAULT NULL COMMENT '自动打标规则',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '排序(越小越靠前)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='镜像标签';

-- ============================================================
-- 7. zs_image_tag_relations - 镜像标签关联
-- ============================================================
DROP TABLE IF EXISTS `zs_image_tag_relations`;
CREATE TABLE `zs_image_tag_relations` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '关联ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `tag_id` int unsigned NOT NULL COMMENT '标签ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_image_tag` (`image_id`,`tag_id`),
  KEY `idx_tag_id` (`tag_id`),
  CONSTRAINT `fk_itr_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_itr_tag` FOREIGN KEY (`tag_id`) REFERENCES `zs_image_tags` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='镜像标签关联';

-- ============================================================
-- 8. zs_download_links - 下载链接管理
-- ============================================================
DROP TABLE IF EXISTS `zs_download_links`;
CREATE TABLE `zs_download_links` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '链接ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `token` varchar(64) NOT NULL COMMENT '下载令牌(唯一)',
  `type` enum('temp','permanent') NOT NULL DEFAULT 'temp' COMMENT '链接类型(temp=临时,permanent=永久)',
  `expire_time` datetime DEFAULT NULL COMMENT '过期时间',
  `max_downloads` int unsigned NOT NULL DEFAULT '0' COMMENT '最大下载次数(0=不限)',
  `download_count` int unsigned NOT NULL DEFAULT '0' COMMENT '已下载次数',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `expired_at` datetime DEFAULT NULL COMMENT '实际过期时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_token` (`token`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_created_by` (`created_by`),
  CONSTRAINT `fk_dl_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_dl_user` FOREIGN KEY (`created_by`) REFERENCES `zs_users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='下载链接管理';

-- ============================================================
-- 9. zs_download_logs - 下载日志
-- ============================================================
DROP TABLE IF EXISTS `zs_download_logs`;
CREATE TABLE `zs_download_logs` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '日志ID',
  `link_id` int unsigned DEFAULT NULL COMMENT '下载链接ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `client_ip` varchar(50) NOT NULL COMMENT '客户端IP',
  `user_agent` varchar(500) DEFAULT NULL COMMENT '用户代理',
  `downloaded_bytes` bigint unsigned NOT NULL DEFAULT '0' COMMENT '已下载字节数',
  `status` enum('started','completed','failed','cancelled') NOT NULL COMMENT '下载状态',
  `error_message` text COMMENT '错误信息',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(秒)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_link_id` (`link_id`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_status` (`status`),
  KEY `idx_created_at` (`created_at`),
  CONSTRAINT `fk_dlog_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_dlog_link` FOREIGN KEY (`link_id`) REFERENCES `zs_download_links` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='下载日志';

-- ============================================================
-- 10. zs_download_queue - 远程下载队列
-- ============================================================
DROP TABLE IF EXISTS `zs_download_queue`;
CREATE TABLE `zs_download_queue` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '队列ID',
  `image_id` int unsigned DEFAULT NULL COMMENT '关联镜像ID',
  `url` varchar(500) NOT NULL COMMENT '下载地址',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件总大小(字节)',
  `downloaded_bytes` bigint unsigned NOT NULL DEFAULT '0' COMMENT '已下载字节数',
  `status` enum('waiting','downloading','paused','completed','failed','cancelled') NOT NULL DEFAULT 'waiting' COMMENT '下载状态',
  `support_range` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否支持断点续传',
  `priority` tinyint(1) NOT NULL DEFAULT '0' COMMENT '优先级(0=普通,1=高)',
  `error_message` text COMMENT '错误信息',
  `retry_count` int unsigned NOT NULL DEFAULT '0' COMMENT '已重试次数',
  `max_retries` int unsigned NOT NULL DEFAULT '3' COMMENT '最大重试次数',
  `started_at` datetime DEFAULT NULL COMMENT '开始时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_dq_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='远程下载队列';
-- ============================================================
-- 11. zs_clients - 客户端管理
-- ============================================================
DROP TABLE IF EXISTS `zs_clients`;
CREATE TABLE `zs_clients` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '客户端ID',
  `client_id` varchar(64) NOT NULL COMMENT '客户端唯一标识',
  `name` varchar(100) DEFAULT NULL COMMENT '客户端名称',
  `mac_address` varchar(50) NOT NULL COMMENT 'MAC地址',
  `cpu_serial` varchar(100) DEFAULT NULL COMMENT 'CPU序列号',
  `motherboard_serial` varchar(100) DEFAULT NULL COMMENT '主板序列号',
  `disk_serial` varchar(100) DEFAULT NULL COMMENT '磁盘序列号',
  `hostname` varchar(100) NOT NULL COMMENT '主机名',
  `os_version` varchar(100) NOT NULL COMMENT '操作系统版本',
  `client_version` varchar(20) NOT NULL COMMENT '客户端版本',
  `client_type` enum('winpe','windows_installer','windows') NOT NULL DEFAULT 'winpe' COMMENT '客户端类型',
  `group_id` int unsigned DEFAULT NULL COMMENT '分组ID',
  `status` enum('pending','approved','blocked','offline') NOT NULL DEFAULT 'pending' COMMENT '状态(pending=待审核,approved=已批准,blocked=已封禁,offline=离线)',
  `first_ip` varchar(50) NOT NULL COMMENT '首次连接IP',
  `last_ip` varchar(50) NOT NULL COMMENT '最后连接IP',
  `last_heartbeat` datetime DEFAULT NULL COMMENT '最后心跳时间',
  `approved_at` datetime DEFAULT NULL COMMENT '审批通过时间',
  `approved_by` int unsigned DEFAULT NULL COMMENT '审批人(管理员用户ID)',
  `install_count` int unsigned NOT NULL DEFAULT '0' COMMENT '安装次数',
  `total_online_time` int unsigned NOT NULL DEFAULT '0' COMMENT '总在线时长(秒)',
  `remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_client_id` (`client_id`),
  KEY `idx_mac_address` (`mac_address`),
  KEY `idx_status` (`status`),
  KEY `idx_group_id` (`group_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户端管理';

-- ============================================================
-- 12. zs_client_groups - 客户端分组
-- ============================================================
DROP TABLE IF EXISTS `zs_client_groups`;
CREATE TABLE `zs_client_groups` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '分组ID',
  `name` varchar(100) NOT NULL COMMENT '分组名称',
  `description` varchar(255) DEFAULT NULL COMMENT '分组描述',
  `parent_id` int unsigned DEFAULT NULL COMMENT '父分组ID',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '排序(越小越靠前)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_parent_id` (`parent_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户端分组';

-- ============================================================
-- 13. zs_client_versions - 客户端版本管理
-- ============================================================
DROP TABLE IF EXISTS `zs_client_versions`;
CREATE TABLE `zs_client_versions` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '版本ID',
  `version` varchar(20) NOT NULL COMMENT '版本号',
  `client_type` enum('winpe','windows_installer','windows') NOT NULL COMMENT '客户端类型',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_path` varchar(500) NOT NULL COMMENT '文件路径',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `file_hash` varchar(128) NOT NULL COMMENT '文件哈希',
  `changelog` text COMMENT '更新日志',
  `is_force_update` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否强制更新',
  `min_compatible_version` varchar(20) DEFAULT NULL COMMENT '最低兼容版本',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `publish_time` datetime NOT NULL COMMENT '发布时间',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_version` (`version`),
  KEY `idx_client_type` (`client_type`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户端版本管理';

-- ============================================================
-- 14. zs_tasks - 装机任务
-- ============================================================
DROP TABLE IF EXISTS `zs_tasks`;
CREATE TABLE `zs_tasks` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '任务ID',
  `task_no` varchar(32) NOT NULL COMMENT '任务编号(格式:YYYYMMDDHHmmss+6位随机数)',
  `client_id` int unsigned DEFAULT NULL COMMENT '客户端ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `unattend_template_id` int unsigned DEFAULT NULL COMMENT '无人值守模板ID',
  `target_disk_index` int NOT NULL COMMENT '目标磁盘索引',
  `target_partition` varchar(10) NOT NULL COMMENT '目标分区',
  `partition_scheme` enum('auto','custom','keep') NOT NULL DEFAULT 'auto' COMMENT '分区方案(auto=自动,custom=自定义,keep=保留)',
  `options` longtext COMMENT '其他选项(JSON)',
  `status` enum('pending','waiting','running','paused','completed','failed','cancelled') NOT NULL DEFAULT 'pending' COMMENT '任务状态(pending=待认领,waiting=等待PE执行,running=执行中,paused=已暂停,completed=已完成,failed=失败,cancelled=已取消',
  `progress` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '进度(0-100)',
  `current_step` varchar(50) DEFAULT NULL COMMENT '当前步骤',
  `started_at` datetime DEFAULT NULL COMMENT '开始时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(秒)',
  `error_message` text COMMENT '错误信息',
  `retry_count` int unsigned NOT NULL DEFAULT '0' COMMENT '重试次数',
  `cancelled_at` datetime DEFAULT NULL COMMENT '取消时间',
  `cancelled_by` varchar(50) DEFAULT NULL COMMENT '取消人(用户ID或客户端ID)',
  `created_by` varchar(50) DEFAULT NULL COMMENT '创建人',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_task_no` (`task_no`),
  KEY `idx_client_id` (`client_id`),
  KEY `idx_status` (`status`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_created_at` (`created_at`),
  CONSTRAINT `fk_task_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_task_client` FOREIGN KEY (`client_id`) REFERENCES `zs_clients` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='装机任务';

-- ============================================================
-- 15. zs_task_records - 任务执行记录/日志
-- ============================================================
DROP TABLE IF EXISTS `zs_task_records`;
CREATE TABLE `zs_task_records` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '记录ID',
  `task_id` int unsigned NOT NULL COMMENT '任务ID',
  `step_name` varchar(50) NOT NULL COMMENT '步骤名称',
  `action` varchar(100) NOT NULL COMMENT '执行操作',
  `status` enum('pending','running','completed','failed','skipped') NOT NULL DEFAULT 'pending' COMMENT '执行状态',
  `progress` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '进度(0-100)',
  `message` text COMMENT '执行消息',
  `started_at` datetime DEFAULT NULL COMMENT '开始时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(毫秒)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_task_id` (`task_id`),
  CONSTRAINT `fk_tr_task` FOREIGN KEY (`task_id`) REFERENCES `zs_tasks` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='任务执行记录/日志';

-- ============================================================
-- 16. zs_task_templates - 任务模板
-- ============================================================
DROP TABLE IF EXISTS `zs_task_templates`;
CREATE TABLE `zs_task_templates` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '模板ID',
  `name` varchar(100) NOT NULL COMMENT '模板名称',
  `description` text COMMENT '模板描述',
  `image_id` int unsigned DEFAULT NULL COMMENT '关联镜像ID',
  `unattend_template_id` int unsigned DEFAULT NULL COMMENT '关联无人值守模板ID',
  `partition_scheme` enum('auto','custom','keep') NOT NULL DEFAULT 'auto' COMMENT '分区方案',
  `options` longtext COMMENT '其他选项(JSON)',
  `is_default` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否默认模板',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_created_by` (`created_by`),
  KEY `idx_is_default` (`is_default`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='任务模板';

-- ============================================================
-- 17. zs_unattend_templates - 无人值守模板
-- ============================================================
DROP TABLE IF EXISTS `zs_unattend_templates`;
CREATE TABLE `zs_unattend_templates` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '模板ID',
  `name` varchar(100) NOT NULL COMMENT '模板名称',
  `description` text COMMENT '模板描述',
  `template_type` enum('standard','domain','kiosk','custom') NOT NULL DEFAULT 'standard' COMMENT '模板类型',
  `config` longtext COMMENT '配置项(JSON)',
  `xml_content` longtext COMMENT '无人值守XML内容',
  `is_default` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否默认模板',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_template_type` (`template_type`),
  KEY `idx_is_default` (`is_default`),
  KEY `idx_created_by` (`created_by`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='无人值守模板';

-- ============================================================
-- 18. zs_software - 软件管理
-- ============================================================
DROP TABLE IF EXISTS `zs_software`;
CREATE TABLE `zs_software` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '软件ID',
  `name` varchar(100) NOT NULL COMMENT '软件名称',
  `category_id` int unsigned DEFAULT NULL COMMENT '分类ID',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_path` varchar(500) NOT NULL COMMENT '文件路径',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `file_hash` varchar(128) DEFAULT NULL COMMENT '文件哈希',
  `version` varchar(50) NOT NULL COMMENT '软件版本',
  `publisher` varchar(100) DEFAULT NULL COMMENT '发布者',
  `description` text COMMENT '软件描述',
  `install_params` varchar(500) DEFAULT NULL COMMENT '静默安装参数',
  `uninstall_params` varchar(500) DEFAULT NULL COMMENT '卸载参数',
  `is_portable` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否便携版(免安装)',
  `os_arch` varchar(20) NOT NULL DEFAULT 'x64' COMMENT '支持架构',
  `download_count` int unsigned NOT NULL DEFAULT '0' COMMENT '下载次数',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_category_id` (`category_id`),
  KEY `idx_status` (`status`),
  KEY `idx_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='软件管理';

-- ============================================================
-- 19. zs_software_categories - 软件分类
-- ============================================================
DROP TABLE IF EXISTS `zs_software_categories`;
CREATE TABLE `zs_software_categories` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '分类ID',
  `name` varchar(50) NOT NULL COMMENT '分类名称',
  `icon` varchar(100) DEFAULT NULL COMMENT '图标',
  `parent_id` int unsigned DEFAULT NULL COMMENT '父分类ID',
  `sort_order` int NOT NULL DEFAULT '0' COMMENT '排序(越小越靠前)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_parent_id` (`parent_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='软件分类';

-- ============================================================
-- 20. zs_software_templates - 装机软件模板
-- ============================================================
DROP TABLE IF EXISTS `zs_software_templates`;
CREATE TABLE `zs_software_templates` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '模板ID',
  `name` varchar(100) NOT NULL COMMENT '模板名称',
  `description` text COMMENT '模板描述',
  `software_ids` text COMMENT '软件ID列表(逗号分隔)',
  `is_default` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否默认模板',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_created_by` (`created_by`),
  KEY `idx_is_default` (`is_default`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='装机软件模板';
-- ============================================================
-- 21. zs_drivers - 驱动管理
-- ============================================================
DROP TABLE IF EXISTS `zs_drivers`;
CREATE TABLE `zs_drivers` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '驱动ID',
  `name` varchar(100) NOT NULL COMMENT '驱动名称',
  `category` enum('chipset','vga','audio','net','storage','usb','other') NOT NULL DEFAULT 'other' COMMENT '驱动分类',
  `manufacturer` varchar(100) DEFAULT NULL COMMENT '制造商',
  `model` varchar(200) DEFAULT NULL COMMENT '型号',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_path` varchar(500) NOT NULL COMMENT '文件路径',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `file_hash` varchar(128) DEFAULT NULL COMMENT '文件哈希',
  `target_os` varchar(100) DEFAULT NULL COMMENT '目标操作系统',
  `os_arch` varchar(20) NOT NULL DEFAULT 'x64' COMMENT '支持架构',
  `description` text COMMENT '驱动描述',
  `download_count` int unsigned NOT NULL DEFAULT '0' COMMENT '下载次数',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_category` (`category`),
  KEY `idx_status` (`status`),
  KEY `idx_manufacturer` (`manufacturer`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='驱动管理';

-- ============================================================
-- 22. zs_scripts - 脚本管理
-- ============================================================
DROP TABLE IF EXISTS `zs_scripts`;
CREATE TABLE `zs_scripts` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '脚本ID',
  `name` varchar(100) NOT NULL COMMENT '脚本名称',
  `type` enum('powershell','cmd','vbs','bat') NOT NULL DEFAULT 'powershell' COMMENT '脚本类型',
  `content` longtext NOT NULL COMMENT '脚本内容',
  `description` text COMMENT '脚本描述',
  `parameters` text COMMENT '参数定义(JSON)',
  `timeout` int unsigned NOT NULL DEFAULT '60' COMMENT '超时时间(秒)',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_type` (`type`),
  KEY `idx_status` (`status`),
  KEY `idx_created_by` (`created_by`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='脚本管理';

-- ============================================================
-- 23. zs_pe_versions - PE 版本管理
-- ============================================================
DROP TABLE IF EXISTS `zs_pe_versions`;
CREATE TABLE `zs_pe_versions` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT 'PE版本ID',
  `name` varchar(100) NOT NULL COMMENT 'PE名称',
  `version` varchar(20) NOT NULL COMMENT '版本号',
  `arch` enum('x64','x86') NOT NULL DEFAULT 'x64' COMMENT '架构',
  `base_os` varchar(50) NOT NULL COMMENT '基础操作系统',
  `file_name` varchar(255) NOT NULL COMMENT '文件名',
  `file_path` varchar(500) NOT NULL COMMENT '文件路径',
  `file_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT '文件大小(字节)',
  `file_hash` varchar(128) DEFAULT NULL COMMENT '文件哈希',
  `description` text COMMENT '描述信息',
  `is_default` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否默认版本',
  `status` tinyint(1) NOT NULL DEFAULT '1' COMMENT '状态(1=启用,0=禁用)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_arch` (`arch`),
  KEY `idx_is_default` (`is_default`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='PE 版本管理';

-- ============================================================
-- 24. zs_pe_customize - PE 定制配置
-- ============================================================
DROP TABLE IF EXISTS `zs_pe_customize`;
CREATE TABLE `zs_pe_customize` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '定制ID',
  `pe_version_id` int unsigned NOT NULL COMMENT 'PE版本ID',
  `name` varchar(100) NOT NULL COMMENT '定制名称',
  `wallpaper` varchar(500) DEFAULT NULL COMMENT '壁纸路径',
  `boot_screen` varchar(500) DEFAULT NULL COMMENT '启动画面路径',
  `boot_animation` varchar(500) DEFAULT NULL COMMENT '启动动画路径',
  `builtin_tools` text COMMENT '内置工具列表(JSON数组)',
  `custom_drivers` text COMMENT '自定义驱动(JSON数组)',
  `custom_scripts` text COMMENT '自定义脚本(JSON数组)',
  `additional_files` text COMMENT '附加文件(JSON)',
  `config` longtext COMMENT '其他配置(JSON)',
  `build_status` enum('none','building','completed','failed') NOT NULL DEFAULT 'none' COMMENT '构建状态',
  `build_output` varchar(500) DEFAULT NULL COMMENT '构建输出路径',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_pe_version_id` (`pe_version_id`),
  KEY `idx_build_status` (`build_status`),
  KEY `idx_created_by` (`created_by`),
  CONSTRAINT `fk_pec_pev` FOREIGN KEY (`pe_version_id`) REFERENCES `zs_pe_versions` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='PE 定制配置';

-- ============================================================
-- 25. zs_pxe_configs - PXE 配置
-- ============================================================
DROP TABLE IF EXISTS `zs_pxe_configs`;
CREATE TABLE `zs_pxe_configs` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT 'PXE配置ID',
  `name` varchar(100) NOT NULL COMMENT '配置名称',
  `boot_image` varchar(500) NOT NULL COMMENT '启动镜像路径',
  `boot_file` varchar(255) NOT NULL DEFAULT 'pxeboot.n12' COMMENT '启动文件名',
  `dhcp_range_start` varchar(50) DEFAULT NULL COMMENT 'DHCP起始IP',
  `dhcp_range_end` varchar(50) DEFAULT NULL COMMENT 'DHCP结束IP',
  `subnet_mask` varchar(50) NOT NULL DEFAULT '255.255.255.0' COMMENT '子网掩码',
  `gateway` varchar(50) DEFAULT NULL COMMENT '网关',
  `dns_servers` varchar(200) DEFAULT NULL COMMENT 'DNS服务器(逗号分隔)',
  `tftp_root` varchar(500) NOT NULL COMMENT 'TFTP根目录',
  `menu_config` text COMMENT '启动菜单配置(JSON)',
  `is_active` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否启用',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_is_active` (`is_active`),
  KEY `idx_created_by` (`created_by`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='PXE 配置';

-- ============================================================
-- 26. zs_network_deploy - 网络部署任务
-- ============================================================
DROP TABLE IF EXISTS `zs_network_deploy`;
CREATE TABLE `zs_network_deploy` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '部署ID',
  `name` varchar(100) NOT NULL COMMENT '部署名称',
  `pxe_config_id` int unsigned NOT NULL COMMENT 'PXE配置ID',
  `image_id` int unsigned NOT NULL COMMENT '镜像ID',
  `client_ids` text COMMENT '目标客户端ID列表(JSON数组)',
  `deploy_type` enum('full','clone') NOT NULL DEFAULT 'full' COMMENT '部署类型(full=全新安装,clone=克隆)',
  `schedule_time` datetime DEFAULT NULL COMMENT '计划执行时间',
  `status` enum('pending','running','paused','completed','failed','cancelled') NOT NULL DEFAULT 'pending' COMMENT '部署状态',
  `progress` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '总体进度(0-100)',
  `completed_count` int unsigned NOT NULL DEFAULT '0' COMMENT '已完成数量',
  `total_count` int unsigned NOT NULL DEFAULT '0' COMMENT '总数量',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_pxe_config_id` (`pxe_config_id`),
  KEY `idx_image_id` (`image_id`),
  KEY `idx_status` (`status`),
  KEY `idx_created_by` (`created_by`),
  CONSTRAINT `fk_nd_pxe` FOREIGN KEY (`pxe_config_id`) REFERENCES `zs_pxe_configs` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_nd_image` FOREIGN KEY (`image_id`) REFERENCES `zs_images` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='网络部署任务';

-- ============================================================
-- 27. zs_customers - 客户信息
-- ============================================================
DROP TABLE IF EXISTS `zs_customers`;
CREATE TABLE `zs_customers` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '客户ID',
  `name` varchar(100) NOT NULL COMMENT '客户姓名',
  `phone` varchar(20) NOT NULL COMMENT '联系电话',
  `email` varchar(100) DEFAULT NULL COMMENT '电子邮箱',
  `wechat` varchar(100) DEFAULT NULL COMMENT '微信号',
  `company` varchar(200) DEFAULT NULL COMMENT '公司名称',
  `address` varchar(500) DEFAULT NULL COMMENT '地址',
  `remark` text COMMENT '备注',
  `total_orders` int unsigned NOT NULL DEFAULT '0' COMMENT '总工单数',
  `total_amount` decimal(10,2) NOT NULL DEFAULT '0.00' COMMENT '总消费金额',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_phone` (`phone`),
  KEY `idx_name` (`name`),
  KEY `idx_created_by` (`created_by`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户信息';

-- ============================================================
-- 28. zs_work_orders - 维修工单
-- ============================================================
DROP TABLE IF EXISTS `zs_work_orders`;
CREATE TABLE `zs_work_orders` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '工单ID',
  `order_no` varchar(32) NOT NULL COMMENT '工单编号(唯一)',
  `customer_id` int unsigned NOT NULL COMMENT '客户ID',
  `device_type` varchar(100) NOT NULL COMMENT '设备类型',
  `device_model` varchar(200) DEFAULT NULL COMMENT '设备型号',
  `device_sn` varchar(100) DEFAULT NULL COMMENT '设备序列号',
  `fault_description` text NOT NULL COMMENT '故障描述',
  `solution` text COMMENT '解决方案',
  `task_id` int unsigned DEFAULT NULL COMMENT '关联装机任务ID',
  `status` enum('pending','processing','completed','cancelled') NOT NULL DEFAULT 'pending' COMMENT '工单状态',
  `priority` enum('low','normal','high','urgent') NOT NULL DEFAULT 'normal' COMMENT '优先级',
  `charge_amount` decimal(10,2) NOT NULL DEFAULT '0.00' COMMENT '收费金额',
  `remark` text COMMENT '备注',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_order_no` (`order_no`),
  KEY `idx_customer_id` (`customer_id`),
  KEY `idx_status` (`status`),
  KEY `idx_task_id` (`task_id`),
  KEY `idx_created_by` (`created_by`),
  CONSTRAINT `fk_wo_customer` FOREIGN KEY (`customer_id`) REFERENCES `zs_customers` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_wo_task` FOREIGN KEY (`task_id`) REFERENCES `zs_tasks` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='维修工单';
-- ============================================================
-- 29. zs_settings - 系统配置
-- ============================================================
DROP TABLE IF EXISTS `zs_settings`;
CREATE TABLE `zs_settings` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '配置ID',
  `group` varchar(50) NOT NULL DEFAULT 'basic' COMMENT '配置分组',
  `key` varchar(100) NOT NULL COMMENT '配置键名',
  `value` longtext COMMENT '配置值',
  `type` enum('string','int','bool','json','textarea') NOT NULL DEFAULT 'string' COMMENT '值类型',
  `description` varchar(255) DEFAULT NULL COMMENT '配置说明',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_key` (`key`),
  KEY `idx_group` (`group`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置';

-- ============================================================
-- 30. zs_logs - 操作日志
-- ============================================================
DROP TABLE IF EXISTS `zs_logs`;
CREATE TABLE `zs_logs` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT COMMENT '日志ID',
  `user_id` int unsigned DEFAULT NULL COMMENT '操作用户ID',
  `username` varchar(50) NOT NULL COMMENT '操作用户名',
  `action` varchar(100) NOT NULL COMMENT '操作动作',
  `resource_type` varchar(50) NOT NULL COMMENT '资源类型',
  `resource_id` int unsigned DEFAULT NULL COMMENT '资源ID',
  `detail` text COMMENT '操作详情',
  `request_method` varchar(10) NOT NULL COMMENT '请求方法',
  `request_url` varchar(500) NOT NULL COMMENT '请求URL',
  `request_params` longtext COMMENT '请求参数',
  `ip` varchar(50) NOT NULL COMMENT '操作IP',
  `user_agent` varchar(500) DEFAULT NULL COMMENT '用户代理',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(毫秒)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_user_id` (`user_id`),
  KEY `idx_action` (`action`),
  KEY `idx_resource_type` (`resource_type`),
  KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='操作日志';

-- ============================================================
-- 31. zs_scheduled_tasks - 调度任务
-- ============================================================
DROP TABLE IF EXISTS `zs_scheduled_tasks`;
CREATE TABLE `zs_scheduled_tasks` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '任务ID',
  `name` varchar(100) NOT NULL COMMENT '任务名称',
  `task_type` enum('heartbeat_check','image_clean','backup','log_clean','cache_clean','sync_source','report_generate','notification','custom') NOT NULL COMMENT '任务类型',
  `cron_expression` varchar(50) NOT NULL COMMENT 'Cron表达式',
  `handler` varchar(200) NOT NULL COMMENT '处理器',
  `params` text COMMENT '任务参数(JSON)',
  `status` enum('active','paused','error') NOT NULL DEFAULT 'active' COMMENT '任务状态',
  `last_run_time` datetime DEFAULT NULL COMMENT '最后运行时间',
  `last_run_result` varchar(500) DEFAULT NULL COMMENT '最后运行结果',
  `fail_count` int unsigned NOT NULL DEFAULT '0' COMMENT '连续失败次数',
  `max_fail_count` int unsigned NOT NULL DEFAULT '5' COMMENT '最大允许失败次数',
  `notify_on_fail` tinyint(1) NOT NULL DEFAULT '1' COMMENT '失败是否通知',
  `created_by` int unsigned NOT NULL COMMENT '创建人ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_task_type` (`task_type`),
  KEY `idx_status` (`status`),
  KEY `idx_created_by` (`created_by`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='调度任务';

-- ============================================================
-- 32. zs_scheduled_task_logs - 调度任务执行日志
-- ============================================================
DROP TABLE IF EXISTS `zs_scheduled_task_logs`;
CREATE TABLE `zs_scheduled_task_logs` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT COMMENT '日志ID',
  `task_id` int unsigned NOT NULL COMMENT '调度任务ID',
  `status` enum('running','completed','failed') NOT NULL COMMENT '执行状态',
  `started_at` datetime NOT NULL COMMENT '开始时间',
  `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(毫秒)',
  `output` longtext COMMENT '执行输出',
  `error_message` text COMMENT '错误信息',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_task_id` (`task_id`),
  KEY `idx_status` (`status`),
  KEY `idx_created_at` (`created_at`),
  CONSTRAINT `fk_stl_task` FOREIGN KEY (`task_id`) REFERENCES `zs_scheduled_tasks` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='调度任务执行日志';

-- ============================================================
-- 33. zs_notifications - 消息通知
-- ============================================================
DROP TABLE IF EXISTS `zs_notifications`;
CREATE TABLE `zs_notifications` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '通知ID',
  `type` enum('task_complete','task_fail','client_offline','image_ready','system_error','version_update','order_update','backup_result','scheduled_task_result','custom') NOT NULL DEFAULT 'custom' COMMENT '通知类型',
  `title` varchar(200) NOT NULL COMMENT '通知标题',
  `content` text NOT NULL COMMENT '通知内容',
  `level` enum('info','warning','error','success') NOT NULL DEFAULT 'info' COMMENT '通知级别',
  `is_read` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否已读',
  `recipient_id` int unsigned NOT NULL COMMENT '接收人ID',
  `related_type` varchar(50) DEFAULT NULL COMMENT '关联类型',
  `related_id` int unsigned DEFAULT NULL COMMENT '关联ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_recipient_id` (`recipient_id`),
  KEY `idx_is_read` (`is_read`),
  KEY `idx_type` (`type`),
  KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='消息通知';

-- ============================================================
-- 34. zs_recycle_bin - 回收站
-- ============================================================
DROP TABLE IF EXISTS `zs_recycle_bin`;
CREATE TABLE `zs_recycle_bin` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '记录ID',
  `original_table` varchar(50) NOT NULL COMMENT '原表名',
  `original_id` int unsigned NOT NULL COMMENT '原记录ID',
  `data` longtext NOT NULL COMMENT '原始数据(JSON)',
  `deleted_by` int unsigned NOT NULL COMMENT '删除人ID',
  `deleted_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '删除时间',
  `expire_at` datetime NOT NULL COMMENT '过期时间(自动清理)',
  PRIMARY KEY (`id`),
  KEY `idx_original` (`original_table`,`original_id`),
  KEY `idx_expire_at` (`expire_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='回收站';

-- ============================================================
-- 35. zs_webhook_logs - Webhook 日志
-- ============================================================
DROP TABLE IF EXISTS `zs_webhook_logs`;
CREATE TABLE `zs_webhook_logs` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT COMMENT '日志ID',
  `event` varchar(100) NOT NULL COMMENT '事件类型',
  `url` varchar(500) NOT NULL COMMENT 'Webhook URL',
  `request_body` longtext COMMENT '请求体',
  `response_body` longtext COMMENT '响应体',
  `response_code` int NOT NULL DEFAULT '0' COMMENT 'HTTP响应状态码',
  `status` enum('success','failed','timeout') NOT NULL DEFAULT 'success' COMMENT '调用状态',
  `duration` int unsigned NOT NULL DEFAULT '0' COMMENT '耗时(毫秒)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_event` (`event`),
  KEY `idx_status` (`status`),
  KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Webhook 日志';
-- ============================================================
-- 种子数据
-- ============================================================

-- zs_roles - 角色种子数据
INSERT IGNORE INTO `zs_roles` (`id`, `name`, `code`, `description`, `permissions`, `status`) VALUES
(1, '超级管理员', 'admin', '系统超级管理员，拥有所有权限', '["*"]', 1),
(2, '普通操作员', 'operator', '普通操作员，可执行日常运维操作', '["image:view","image:upload","task:view","task:create","task:execute","client:view","software:view","software:install","report:view"]', 1),
(3, '只读查看', 'viewer', '只读查看人员，只能查看数据无法操作', '["image:view","task:view","client:view","software:view","report:view"]', 1),
(4, '普通用户', 'user', '客户端注册的普通用户，仅限客户端登录使用', '["client:use","task:use","software:use"]', 1);

-- zs_users - 客户端测试账号种子（wuxx80 / a111111，普通用户角色）
INSERT IGNORE INTO `zs_users` (`username`, `password`, `nickname`, `role_id`, `is_super`, `status`) VALUES
('wuxx80', '$2y$10$erqQoFeg8RmE5ijbM2EqS.7WiprKZdjXSE0uu0nTmns7sMFNG0DNG', '测试用户', 4, 0, 1);

-- zs_image_tags - 镜像标签种子数据
INSERT IGNORE INTO `zs_image_tags` (`id`, `name`, `color`, `is_auto`, `auto_rule`, `sort_order`) VALUES
(1, '推荐', '#52C41A', 0, NULL, 1),
(2, '稳定', '#1890FF', 0, NULL, 2),
(3, '新硬件', '#722ED1', 0, NULL, 3),
(4, '旧硬件', '#FA8C16', 0, NULL, 4),
(5, '纯净', '#13C2C2', 0, NULL, 5),
(6, '精简', '#EB2F96', 0, NULL, 6),
(7, '游戏', '#F5222D', 0, NULL, 7),
(8, '办公', '#1890FF', 0, NULL, 8),
(9, '企业', '#FAAD14', 0, NULL, 9);

-- zs_software_categories - 软件分类种子数据
INSERT IGNORE INTO `zs_software_categories` (`id`, `name`, `icon`, `parent_id`, `sort_order`) VALUES
(1, '办公软件', 'office', NULL, 1),
(2, '开发工具', 'code', NULL, 2),
(3, '媒体工具', 'media', NULL, 3),
(4, '网络工具', 'network', NULL, 4),
(5, '安全工具', 'security', NULL, 5),
(6, '系统工具', 'system', NULL, 6),
(7, '图形设计', 'design', NULL, 7),
(8, '其他', 'other', NULL, 8);

-- zs_settings - 系统配置种子数据
INSERT IGNORE INTO `zs_settings` (`group`, `key`, `value`, `type`, `description`) VALUES
('basic', 'site_name', 'ZS 装机助手', 'string', '站点名称'),
('basic', 'site_logo', '', 'string', '站点Logo'),
('basic', 'storage_path', '/data/images', 'string', '镜像存储路径'),
('basic', 'max_upload_size', '21474836480', 'int', '最大上传大小(字节)'),
('basic', 'download_limit_speed', '0', 'int', '下载限速(字节/秒,0=不限)'),
('basic', 'heartbeat_interval', '30', 'int', '客户端心跳间隔(秒)'),
('basic', 'client_offline_timeout', '180', 'int', '客户端离线超时(秒)'),
('basic', 'auto_clean_days', '30', 'int', '自动清理天数'),
('basic', 'backup_enabled', '1', 'bool', '是否启用自动备份'),
('basic', 'backup_keep_days', '7', 'int', '备份保留天数'),
('basic', 'notification_enabled', '1', 'bool', '是否启用通知'),
('basic', 'websocket_port', '2346', 'int', 'WebSocket端口');

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 安装完成
-- ============================================================
