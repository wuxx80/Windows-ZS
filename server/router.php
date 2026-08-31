<?php
// ThinkPHP 6 内置服务器路由脚本（开发用）
// 启动方式: php -S 127.0.0.1:8001 router.php （在 server/ 目录下）
//
// 说明：Windows 下 `-t public` 的 DOCUMENT_ROOT 有时不生效，
// 因此这里不依赖 DOCUMENT_ROOT，直接以 public 目录为根手动处理静态文件。

$publicDir = realpath(__DIR__ . '/public');
$uriPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
$uriPath = urldecode($uriPath);

// 静态文件（带目录穿越防护：解析后必须仍在 public 目录内）
$target = realpath($publicDir . str_replace('/', DIRECTORY_SEPARATOR, $uriPath));
if ($target && strpos($target, $publicDir . DIRECTORY_SEPARATOR) === 0 && is_file($target)) {
    $mime = [
        'html' => 'text/html; charset=utf-8',
        'css'  => 'text/css; charset=utf-8',
        'js'   => 'application/javascript; charset=utf-8',
        'mjs'  => 'application/javascript; charset=utf-8',
        'json' => 'application/json; charset=utf-8',
        'txt'  => 'text/plain; charset=utf-8',
        'map'  => 'application/json; charset=utf-8',
        'png'  => 'image/png',
        'jpg'  => 'image/jpeg',
        'jpeg' => 'image/jpeg',
        'gif'  => 'image/gif',
        'svg'  => 'image/svg+xml',
        'ico'  => 'image/x-icon',
        'webp' => 'image/webp',
        'woff' => 'font/woff',
        'woff2'=> 'font/woff2',
        'ttf'  => 'font/ttf',
        'eot'  => 'application/vnd.ms-fontobject',
    ];
    $ext = strtolower(pathinfo($target, PATHINFO_EXTENSION));
    header('Content-Type: ' . ($mime[$ext] ?? 'application/octet-stream'));
    header('Content-Length: ' . filesize($target));
    readfile($target);
    return true;
}

// 动态请求：路由到 ThinkPHP 入口
$_SERVER['SCRIPT_FILENAME'] = $publicDir . DIRECTORY_SEPARATOR . 'index.php';
$_SERVER['SCRIPT_NAME'] = '/index.php';
require $publicDir . DIRECTORY_SEPARATOR . 'index.php';
