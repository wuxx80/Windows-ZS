# ============================================================
# ZS 装机助手 - 服务器端部署包制作脚本
# ============================================================
# 用法：
#   .\deploy_server.ps1                          # 打包 server 目录为 zip
#   .\deploy_server.ps1 -Output D:\out\zs_server.zip   # 指定输出路径
# ============================================================

param(
    [string]$Output = ""
)

$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
$SERVER = Join-Path $ROOT "server"

if (-not (Test-Path $SERVER)) {
    Write-Host "[X] server 目录不存在: $SERVER" -ForegroundColor Red
    exit 1
}

# 默认输出：项目根目录下
if (-not $Output) {
    $Output = Join-Path $ROOT "zs_server_deploy.zip"
}
$Output = [System.IO.Path]::GetFullPath($Output)
$outDir = Split-Path -Parent $Output
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# 临时暂存目录
$stage = Join-Path $env:TEMP ("zs_deploy_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stage -Force | Out-Null

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  制作服务器端部署包" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# 需要复制的内容（排除运行时可再生成 / 敏感文件）
$include = @(
    "app",
    "config",
    "route",
    "public"
)
$includeFiles = @(
    "composer.json",
    "composer.lock",
    "think",
    ".env.example",
    "thinkphp.nginx.rewrite.conf"
)

$dest = Join-Path $stage "server"
New-Item -ItemType Directory -Path $dest -Force | Out-Null

foreach ($item in $include) {
    $src = Join-Path $SERVER $item
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination (Join-Path $dest $item) -Recurse -Force
    } else {
        Write-Host "[WARN] 缺失目录: $item" -ForegroundColor Yellow
    }
}

foreach ($f in $includeFiles) {
    $src = Join-Path $SERVER $f
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination (Join-Path $dest $f) -Force
    }
}

# 清理构建/运行残留
$cleanDirs = @(
    "runtime",
    "public\uploads",
    "public\images"
)
foreach ($d in $cleanDirs) {
    $p = Join-Path $dest $d
    if (Test-Path $p) { Remove-Item -Path $p -Recurse -Force }
}
Get-ChildItem -Path (Join-Path $dest "public") -Filter "uploads" -Directory -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 生成部署说明
$readme = @"
# ZS 装机助手 - 服务器端部署包

版本：v0.0.268311
说明：ThinkPHP 6 后端 + 管理后台前端

## 部署步骤（宝塔面板）

1. 将本目录上传到网站根目录（如 /www/wwwroot/zs_installer）
2. 设置网站运行目录为 public
3. 创建 MySQL 数据库并导入 database/install.sql（部署包不含 database，需从源码获取）
4. 复制 .env.example 为 .env 并填写数据库连接
5. 安装依赖：composer install
6. 配置伪静态：server/thinkphp.nginx.rewrite.conf 内容（独立文件，不改宝塔配置）
7. 设置 runtime 目录可写：chmod -R 755 runtime
8. 生产环境：APP_DEBUG=false，更换 JWT 密钥

详细步骤见源码根目录 操作指南.md
"@
Set-Content -Path (Join-Path $dest "部署说明.txt") -Value $readme -Encoding UTF8

# 打包 zip
Write-Host "[*] 正在压缩..." -ForegroundColor Cyan
if (Test-Path $Output) { Remove-Item -Path $Output -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $Output -CompressionLevel Optimal

# 清理暂存
Remove-Item -Path $stage -Recurse -Force

$size = [math]::Round((Get-Item $Output).Length / 1MB, 2)
Write-Host "[OK] 部署包已生成: $Output ($size MB)" -ForegroundColor Green
Write-Host ""
Write-Host "下一步（需要你操作）：" -ForegroundColor Cyan
Write-Host "  1. 将部署包上传到宝塔面板服务器" -ForegroundColor Gray
Write-Host "  2. 按部署说明.txt 完成配置" -ForegroundColor Gray
