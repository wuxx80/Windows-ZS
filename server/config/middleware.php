<?php
// 全局中间件：CORS + 操作日志
// AuthMiddleware 已在 route/admin.php 路由组级别注册（line 241）
// LogMiddleware 在响应阶段记录用户操作到 zs_logs 表
return [
    \app\middleware\CorsMiddleware::class,
    \app\middleware\LogMiddleware::class,
];
