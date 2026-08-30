<?php
// ThinkPHP 6 内置服务器路由脚本（开发用）
// 启动方式: php -S 127.0.0.1:8001 router.php （在 server/ 目录下）
if (is_file($_SERVER["DOCUMENT_ROOT"] . $_SERVER["SCRIPT_NAME"])) {
    return false;
} else {
    $_SERVER["SCRIPT_FILENAME"] = __DIR__ . '/public/index.php';
    require __DIR__ . '/public/index.php';
}
