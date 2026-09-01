-- ============================================================
-- 修复 zs_software 和 zs_drivers 表缺失的字段
-- 执行：在 phpMyAdmin / Adminer 中运行此文件
-- ============================================================

-- 1. zs_software 补充 silent_install 和 os_support 字段
ALTER TABLE `zs_software`
  ADD COLUMN `silent_install` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否静默安装(1=是,0=否)' AFTER `install_params`,
  ADD COLUMN `os_support` varchar(100) DEFAULT NULL COMMENT '支持操作系统' AFTER `silent_install`;

-- 2. zs_drivers 补充 publisher、version、device_type、os_support、arch_support 字段
ALTER TABLE `zs_drivers`
  ADD COLUMN `publisher` varchar(100) DEFAULT NULL COMMENT '发布者' AFTER `model`,
  ADD COLUMN `version` varchar(50) NOT NULL DEFAULT '1.0.0' COMMENT '驱动版本' AFTER `publisher`,
  ADD COLUMN `device_type` varchar(50) DEFAULT 'other' COMMENT '设备类型' AFTER `category`,
  ADD COLUMN `os_support` varchar(100) DEFAULT NULL COMMENT '支持操作系统' AFTER `target_os`,
  ADD COLUMN `arch_support` varchar(20) DEFAULT 'x64' COMMENT '支持架构' AFTER `os_arch`;