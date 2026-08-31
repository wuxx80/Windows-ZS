# ============================================================
# ZS 装机助手 - 客户端发布脚本（单文件 exe）
# ============================================================
# 用法：
#   .\publish.ps1              # 发布所有客户端
#   .\publish.ps1 -WinPE       # 仅发布 WinPE 客户端（自包含单文件）
#   .\publish.ps1 -Windows     # 仅发布 Windows 客户端（框架依赖单文件）
# ============================================================

param(
    [switch]$WinPE,
    [switch]$Windows,
    [switch]$Help
)

$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
$OUTPUT = Join-Path $ROOT "publish"

if ($Help) {
    Write-Host "用法: .\publish.ps1 [-WinPE] [-Windows] [-Help]" -ForegroundColor Cyan
    Write-Host "  -WinPE    仅发布 WinPE 客户端（自包含单文件）" -ForegroundColor Gray
    Write-Host "  -Windows  仅发布 Windows 客户端（框架依赖单文件）" -ForegroundColor Gray
    Write-Host "  不带参数   发布所有客户端" -ForegroundColor Gray
    exit 0
}

# 检查 .NET SDK
try {
    $dotnetVer = dotnet --version 2>$null
    if (-not $dotnetVer) { throw "dotnet not found" }
    Write-Host "[OK] .NET SDK: $dotnetVer" -ForegroundColor Green
} catch {
    Write-Host "[X] 未找到 .NET SDK，请安装 .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# 检查 .NET 版本 >= 8.0（使用 [version] 类型比较，避免字符串比较误判）
try {
    $ver = [version]($dotnetVer -replace "-.*$", "")
    if ($ver -lt [version]"8.0") {
        Write-Host "[X] 需要 .NET 8.0+，当前版本: $dotnetVer" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[X] 无法解析 .NET 版本: $dotnetVer" -ForegroundColor Red
    exit 1
}

$publishBoth = -not $WinPE -and -not $Windows

# ============================================================
# WinPE 客户端
# ============================================================
if ($publishBoth -or $WinPE) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  发布 WinPE 客户端（自包含单文件）" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow

    $peProject = Join-Path $ROOT "WinPE_Client\WinPE_Client.csproj"
    $peOutput = Join-Path $OUTPUT "WinPE_Client"

    if (-not (Test-Path $peProject)) {
        Write-Host "[X] 项目文件不存在: $peProject" -ForegroundColor Red
    } else {
        Write-Host "[*] 还原依赖..." -ForegroundColor Cyan
        dotnet restore $peProject

        Write-Host "[*] 构建 Release..." -ForegroundColor Cyan
        dotnet build $peProject -c Release --no-restore

        Write-Host "[*] 发布自包含单文件..." -ForegroundColor Cyan
        dotnet publish $peProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $peOutput

        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] WinPE 客户端发布完成: $peOutput" -ForegroundColor Green
            $size = (Get-ChildItem -Recurse $peOutput | Measure-Object -Property Length -Sum).Sum
            Write-Host "     发布大小: $([math]::Round($size / 1MB, 1)) MB" -ForegroundColor Gray
        } else {
            Write-Host "[X] WinPE 客户端发布失败" -ForegroundColor Red
        }
    }
}

# ============================================================
# Windows 客户端
# ============================================================
if ($publishBoth -or $Windows) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  发布 Windows 客户端（框架依赖单文件）" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow

    $winProject = Join-Path $ROOT "Windows_Client\Windows_Client.csproj"
    $winOutput = Join-Path $OUTPUT "Windows_Client"

    if (-not (Test-Path $winProject)) {
        Write-Host "[X] 项目文件不存在: $winProject" -ForegroundColor Red
    } else {
        Write-Host "[*] 还原依赖..." -ForegroundColor Cyan
        dotnet restore $winProject

        Write-Host "[*] 构建 Release..." -ForegroundColor Cyan
        dotnet build $winProject -c Release --no-restore

        Write-Host "[*] 发布框架依赖单文件..." -ForegroundColor Cyan
        dotnet publish $winProject -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $winOutput

        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Windows 客户端发布完成: $winOutput" -ForegroundColor Green
            $size = (Get-ChildItem -Recurse $winOutput | Measure-Object -Property Length -Sum).Sum
            Write-Host "     发布大小: $([math]::Round($size / 1MB, 1)) MB" -ForegroundColor Gray
        } else {
            Write-Host "[X] Windows 客户端发布失败" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  发布完成" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

if (Test-Path $OUTPUT) {
    Write-Host "输出目录: $OUTPUT" -ForegroundColor Cyan
    Get-ChildItem $OUTPUT -Directory | ForEach-Object {
        $size = (Get-ChildItem -Recurse $_.FullName | Measure-Object -Property Length -Sum).Sum
        Write-Host "  [$($_.Name)] $([math]::Round($size / 1MB, 1)) MB" -ForegroundColor Gray
    }
}