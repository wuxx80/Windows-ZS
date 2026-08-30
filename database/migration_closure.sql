-- ZS 装机助手 · 数据库迁移
-- 用途：将现有库升级到「全功能流程闭环」所需结构
-- 对应版本：v0.0.268311（不升版本号，仅结构对齐）

-- 1. zs_tasks.status 增加 waiting（等待 WinPE 执行）枚举
ALTER TABLE `zs_tasks`
  MODIFY COLUMN `status` enum('pending','waiting','running','paused','completed','failed','cancelled')
  NOT NULL DEFAULT 'pending' COMMENT '任务状态(pending=待认领,waiting=等待PE执行,running=执行中,paused=已暂停,completed=已完成,failed=失败,cancelled=已取消)';

-- 2. zs_tasks 增加取消字段
ALTER TABLE `zs_tasks`
  ADD COLUMN `cancelled_at` datetime DEFAULT NULL COMMENT '取消时间' AFTER `progress`,
  ADD COLUMN `cancelled_by` varchar(50) DEFAULT NULL COMMENT '取消人(用户ID或客户端ID)' AFTER `cancelled_at`;

-- 3. zs_clients 增加审批字段
ALTER TABLE `zs_clients`
  ADD COLUMN `approved_at` datetime DEFAULT NULL COMMENT '审批通过时间' AFTER `last_heartbeat`,
  ADD COLUMN `approved_by` int unsigned DEFAULT NULL COMMENT '审批人(管理员用户ID)' AFTER `approved_at`;