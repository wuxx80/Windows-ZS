-- ZS 装机助手 · R7 PE 资产字段迁移
-- 用途：扩展 zs_pe_versions 表，增加 boot.wim / boot.sdi / ZS_PE_Agent.exe 三个独立资产字段
-- 对应版本：R7-D（不升版本号，仅结构对齐）
-- 执行方式：在 MySQL 管理工具（如 phpMyAdmin / Adminer）中执行

-- 1. boot.wim 字段（PE 启动镜像，~290MB）
ALTER TABLE `zs_pe_versions`
  ADD COLUMN `boot_wim_path` varchar(500) DEFAULT NULL COMMENT 'boot.wim 文件路径' AFTER `file_hash`,
  ADD COLUMN `boot_wim_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT 'boot.wim 大小(字节)' AFTER `boot_wim_path`,
  ADD COLUMN `boot_wim_hash` varchar(128) DEFAULT NULL COMMENT 'boot.wim SHA-256' AFTER `boot_wim_size`;

-- 2. boot.sdi 字段（RAM 磁盘模板，~3MB）
ALTER TABLE `zs_pe_versions`
  ADD COLUMN `boot_sdi_path` varchar(500) DEFAULT NULL COMMENT 'boot.sdi 文件路径' AFTER `boot_wim_hash`,
  ADD COLUMN `boot_sdi_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT 'boot.sdi 大小(字节)' AFTER `boot_sdi_path`,
  ADD COLUMN `boot_sdi_hash` varchar(128) DEFAULT NULL COMMENT 'boot.sdi SHA-256' AFTER `boot_sdi_size`;

-- 3. ZS_PE_Agent.exe 字段（PE 端装机程序，~34MB）
ALTER TABLE `zs_pe_versions`
  ADD COLUMN `agent_path` varchar(500) DEFAULT NULL COMMENT 'ZS_PE_Agent.exe 文件路径' AFTER `boot_sdi_hash`,
  ADD COLUMN `agent_size` bigint unsigned NOT NULL DEFAULT '0' COMMENT 'agent.exe 大小(字节)' AFTER `agent_path`,
  ADD COLUMN `agent_hash` varchar(128) DEFAULT NULL COMMENT 'agent.exe SHA-256' AFTER `agent_size`;

-- 4. pe_assets 目录存在性验证（空操作，仅用于记录）
-- 文件上传至 runtime/uploads/pe_assets/{version_id}/ 目录