<?php
header("Content-Type: text/html; charset=utf-8");
?>
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ZS 装机助手 - 管理后台</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: "Microsoft YaHei", sans-serif; background: #f5f7fa; color: #333; display: flex; justify-content: center; align-items: center; min-height: 100vh; }
.container { text-align: center; padding: 40px; background: #fff; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,0.08); max-width: 500px; width: 90%; }
.logo { font-size: 48px; font-weight: bold; color: #1a73e8; margin-bottom: 8px; }
.subtitle { font-size: 14px; color: #999; margin-bottom: 30px; }
.status { display: flex; flex-direction: column; gap: 12px; text-align: left; margin-bottom: 30px; }
.status-item { display: flex; justify-content: space-between; padding: 10px 16px; background: #f8f9fa; border-radius: 8px; font-size: 14px; }
.status-item .label { color: #666; }
.status-item .value { color: #333; font-weight: 500; }
.status-item .value.ok { color: #34a853; }
.btn { display: inline-block; padding: 10px 24px; background: #1a73e8; color: #fff; text-decoration: none; border-radius: 6px; font-size: 14px; margin: 4px; }
.btn:hover { background: #1557b0; }
.btn.secondary { background: #fff; color: #1a73e8; border: 1px solid #1a73e8; }
.btn.secondary:hover { background: #f0f5ff; }
.footer { margin-top: 24px; font-size: 12px; color: #999; }
</style>
</head>
<body>
<div class="container">
<div class="logo">ZS</div>
<div class="subtitle">装机助手 · 管理后台</div>
<div class="status">
<div class="status-item"><span class="label">后端状态</span><span class="value ok">运行中</span></div>
<div class="status-item"><span class="label">PHP 版本</span><span class="value"><?php echo PHP_VERSION; ?></span></div>
<div class="status-item"><span class="label">版本号</span><span class="value">v2.1.0</span></div>
</div>
<p style="margin-bottom:20px;font-size:14px;color:#666;">管理后台前端开发中，API 接口可正常访问</p>
<a class="btn" href="/api/v1/dashboard">查看 API 数据</a>
<a class="btn secondary" href="/index.php?s=/api/v1/auth/login">API 登录测试</a>
<div class="footer">© 2026 ZS Studio. All rights reserved.</div>
</div>
</body>
</html>
