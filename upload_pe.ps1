# ============================================================
# ZS deployment system - upload PE assets to server (Phase 1)
# ------------------------------------------------------------
# Takes the boot.wim and boot.sdi produced by make_boot_wim.ps1
# and registers them on the server.
#
# Two modes:
#   Mode A (Local copy): Copy files from _pe_build\boot_assets\
#                        to a local server storage path
#                        (use this when the dev machine IS the server)
#   Mode B (API register): Login to admin API, upsert a PeVersion record
#                          with file_path/file_size/file_hash
#                          (use this after manually uploading files
#                           to the server, e.g. via scp/rsync)
#
# ADK SETUP (for Mode A of make_boot_wim.ps1):
#   - Download Windows ADK: https://learn.microsoft.com/windows-hardware/get-started/adk-install
#   - Download Windows PE add-on: https://learn.microsoft.com/windows-hardware/get-started/winpe-intro
#   - Install both on a Windows 10/11 dev machine (default paths:
#       C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Windows Preinstallation Environment\
#       C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Windows Preinstallation Environment\)
#   - After install, copype.cmd is available. Run:
#       .\make_boot_wim.ps1 -Mode A
#     This will use ADK's copype.cmd to create a fresh WinPE that natively
#     has choice.exe and all required builtin tools.
#
# Usage (PowerShell, Administrator recommended for file copy):
#   .\upload_pe.ps1 -Mode A -StoragePath "D:\data\images\pe"
#   .\upload_pe.ps1 -Mode B -ApiBase "http://127.0.0.1:8001" -ApiUser admin -ApiPass secret -Version "v1.0.0"
#   .\upload_pe.ps1 -Help
# ============================================================

param(
    [ValidateSet('A','B')]
    [string]$Mode = 'A',

    # Mode A: where to copy files
    [string]$StoragePath = 'D:\data\images\pe',

    # Mode B: API connection
    [string]$ApiBase = 'http://127.0.0.1:8001',
    [string]$ApiUser = '',
    [string]$ApiPass = '',
    [string]$Version = 'v1.0.0',
    [string]$Name = '',
    [string]$BaseOs = 'WinPE',
    [string]$Arch = 'x64',

    # Common
    [string]$AssetsDir = '',
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

# ---------- Path initialization ----------
$ROOT       = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $AssetsDir) { $AssetsDir = Join-Path $ROOT '_pe_build\boot_assets' }

if ($Help) {
    Write-Host "ZS PE assets uploader (Phase 1)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Mode A (local file copy):"
    Write-Host "  .\upload_pe.ps1 -Mode A -StoragePath 'D:\data\images\pe'"
    Write-Host ""
    Write-Host "Mode B (API register):"
    Write-Host "  .\upload_pe.ps1 -Mode B -ApiBase 'http://127.0.0.1:8001' -ApiUser admin -ApiPass secret -Version 'v1.0.0'"
    Write-Host ""
    Write-Host "  -Mode          A = local copy, B = API register"
    Write-Host "  -StoragePath   Local server storage directory (Mode A)"
    Write-Host "  -ApiBase       Server base URL (Mode B, no trailing slash)"
    Write-Host "  -ApiUser       Admin username (Mode B)"
    Write-Host "  -ApiPass       Admin password (Mode B)"
    Write-Host "  -Version       PE version string, e.g. 'v1.0.0' (Mode B)"
    Write-Host "  -Name          Display name (optional, Mode B)"
    Write-Host "  -BaseOs        Base OS tag (optional, Mode B, default WinPE)"
    Write-Host "  -Arch          Architecture (optional, Mode B, default x64)"
    Write-Host "  -AssetsDir     Build output dir (default _pe_build\boot_assets)"
    exit 0
}

# ---------- Helper functions ----------
function Write-Step  { param([string]$Msg) Write-Host "[*] $Msg" -ForegroundColor Cyan }
function Write-OK    { param([string]$Msg) Write-Host "[OK] $Msg" -ForegroundColor Green }
function Write-Warn2 { param([string]$Msg) Write-Host "[!] $Msg" -ForegroundColor Yellow }
function Write-Err   { param([string]$Msg) Write-Host "[ERROR] $Msg" -ForegroundColor Red }

function Get-FileSHA256 {
    param([string]$Path)
    if (-not $Path) { return $null }
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpper()
}

function Read-Manifest {
    param([string]$ManifestPath)
    if (-not (Test-Path -LiteralPath $ManifestPath)) { return $null }
    $lines = Get-Content -LiteralPath $ManifestPath -Encoding UTF8
    $result = @{}
    foreach ($line in $lines) {
        if ($line -match '^(sha256|size_bytes|size_mb|sdi_size_bytes|sdi_size_mb)=(.+)$') {
            $result[$matches[1]] = $matches[2]
        } elseif ($line -match '^sha256=(.+?)=(boot\.wim|boot\.sdi|Startnet\.cmd)$') {
            $result["hash_$($matches[2])"] = $matches[1]
        }
    }
    return $result
}

# ---------- Main ----------
try {
    $bootWim  = Join-Path $AssetsDir 'boot.wim'
    $bootSdi  = Join-Path $AssetsDir 'boot.sdi'
    $manifest = Join-Path $AssetsDir 'boot_manifest.txt'

    if (-not (Test-Path -LiteralPath $bootWim)) {
        throw "boot.wim not found in $AssetsDir. Run make_boot_wim.ps1 first."
    }
    if (-not (Test-Path -LiteralPath $bootSdi)) {
        Write-Warn2 "boot.sdi not found - only boot.wim will be uploaded"
    }

    $wimHash = Get-FileSHA256 $bootWim
    $wimSize = (Get-Item -LiteralPath $bootWim).Length
    $wimSizeMB = [math]::Round($wimSize / 1048576, 1)

    $sdiHash = $null
    $sdiSize = 0
    $sdiSizeMB = 0
    if (Test-Path -LiteralPath $bootSdi) {
        $sdiHash = Get-FileSHA256 $bootSdi
        $sdiSize = (Get-Item -LiteralPath $bootSdi).Length
        $sdiSizeMB = [math]::Round($sdiSize / 1048576, 2)
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  ZS PE Assets Uploader (Phase 1)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Source: $AssetsDir" -ForegroundColor Gray
    Write-Host "  boot.wim: $wimSizeMB MB  sha256=$wimHash" -ForegroundColor Gray
    if ($sdiHash) {
        Write-Host "  boot.sdi: $sdiSizeMB MB  sha256=$sdiHash" -ForegroundColor Gray
    }
    Write-Host ""

    if ($Mode -eq 'A') {
        # ---------- Mode A: Local file copy ----------
        Write-Step "Mode A: copying PE assets to local server storage..."
        Write-Host "  Target: $StoragePath" -ForegroundColor Gray

        if (-not (Test-Path $StoragePath)) {
            New-Item -ItemType Directory -Path $StoragePath -Force | Out-Null
            Write-OK "Created storage directory: $StoragePath"
        }

        # Subdirectory per version to keep history
        $versionDir = Join-Path $StoragePath $Version
        if (-not (Test-Path $versionDir)) {
            New-Item -ItemType Directory -Path $versionDir -Force | Out-Null
        }

        $dstWim = Join-Path $versionDir 'boot.wim'
        $dstSdi = Join-Path $versionDir 'boot.sdi'

        Write-Step "Copying boot.wim..."
        Copy-Item -LiteralPath $bootWim -Destination $dstWim -Force
        Write-OK "boot.wim -> $dstWim"

        if (Test-Path -LiteralPath $bootSdi) {
            Write-Step "Copying boot.sdi..."
            Copy-Item -LiteralPath $bootSdi -Destination $dstSdi -Force
            Write-OK "boot.sdi -> $dstSdi"
        }

        # Copy manifest as well, for audit
        if (Test-Path -LiteralPath $manifest) {
            $dstManifest = Join-Path $versionDir 'boot_manifest.txt'
            Copy-Item -LiteralPath $manifest -Destination $dstManifest -Force
            Write-OK "manifest -> $dstManifest"
        }

        # Generate a small instruction file for the admin
        $instructions = @(
            "# PE Assets placed at: $versionDir",
            "# Generated: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))",
            "# Version: $Version",
            "boot.wim.sha256 = $wimHash",
            "boot.wim.size   = $wimSize bytes ($wimSizeMB MB)",
            ""
        )
        if ($sdiHash) {
            $instructions += "boot.sdi.sha256 = $sdiHash"
            $instructions += "boot.sdi.size   = $sdiSize bytes ($sdiSizeMB MB)"
        }
        $instructions += @(
            ""
            "# Next: register this PE version in admin UI or via Mode B of this script."
            "# Admin UI path: PE版本管理 -> 新建 -> 填写 file_path=$dstWim"
        )
        $instPath = Join-Path $versionDir 'README.txt'
        Set-Content -LiteralPath $instPath -Value $instructions -Encoding UTF8
        Write-OK "instructions -> $instPath"

        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  Mode A upload SUCCESS" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  Files copied to: $versionDir" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  Next step (manual):" -ForegroundColor Yellow
        Write-Host "    1. Open admin UI -> PE versions" -ForegroundColor Yellow
        Write-Host "    2. Create new PE version with file_path=$dstWim" -ForegroundColor Yellow
        Write-Host "    3. Set file_hash=$wimHash, file_size=$wimSize" -ForegroundColor Yellow
        Write-Host "    4. Set status=published" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  OR run Mode B to register via API:" -ForegroundColor Yellow
        Write-Host "    .\upload_pe.ps1 -Mode B -ApiBase <url> -ApiUser <user> -ApiPass <pass> -Version $Version" -ForegroundColor Yellow
    } else {
        # ---------- Mode B: API register ----------
        if (-not $ApiUser -or -not $ApiPass) {
            throw "Mode B requires -ApiUser and -ApiPass"
        }

        # Use PS 7+ HttpClient if available, fallback to Invoke-RestMethod
        Write-Step "Mode B: logging in to admin API..."
        $loginBody = @{ username = $ApiUser; password = $ApiPass } | ConvertTo-Json
        try {
            $loginResp = Invoke-RestMethod -Uri "$ApiBase/admin/auth/login" -Method Post -Body $loginBody -ContentType 'application/json' -ErrorAction Stop
        } catch {
            # Try alternate field names
            $loginBody = @{ account = $ApiUser; password = $ApiPass } | ConvertTo-Json
            $loginResp = Invoke-RestMethod -Uri "$ApiBase/admin/auth/login" -Method Post -Body $loginBody -ContentType 'application/json'
        }

        $token = $null
        if ($loginResp.PSObject.Properties.Name -contains 'token') {
            $token = $loginResp.token
        } elseif ($loginResp.data.token) {
            $token = $loginResp.data.token
        }
        if (-not $token) {
            throw "Login did not return token. Response: $($loginResp | ConvertTo-Json -Depth 3)"
        }
        Write-OK "Login OK, token acquired"

        $headers = @{ 'Authorization' = "Bearer $token"; 'Content-Type' = 'application/json' }

        # Check if a version with same version string already exists
        Write-Step "Checking for existing PE version: $Version"
        $listResp = Invoke-RestMethod -Uri "$ApiBase/admin/peVersions?keyword=$Version" -Method Get -Headers $headers
        $existing = $null
        $listData = $null
        if ($listResp.PSObject.Properties.Name -contains 'data') {
            $listData = $listResp.data
        } else {
            $listData = $listResp
        }
        if ($listData -is [array]) {
            $existing = $listData | Where-Object { $_.version -eq $Version } | Select-Object -First 1
        } elseif ($listData.PSObject.Properties.Name -contains 'data') {
            $existing = $listData.data | Where-Object { $_.version -eq $Version } | Select-Object -First 1
        }

        # The file path on the server (admin must have already copied the files
        # to a path the server can read - we just register the path here)
        $filePathHint = "/data/images/pe/$Version/boot.wim"
        Write-Warn2 "Assuming files are at server path: $filePathHint"
        Write-Warn2 "If different, manually edit the PeVersion record after creation."

        $name = if ($Name) { $Name } else { "$BaseOs $Version" }
        $body = @{
            name        = $name
            version     = $Version
            base_os     = $BaseOs
            arch        = $Arch
            file_name   = 'boot.wim'
            file_path   = $filePathHint
            file_size   = $wimSize
            file_hash   = $wimHash
            description = "Auto-registered by upload_pe.ps1 at $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))"
            is_default  = 0
            status      = 'published'
        } | ConvertTo-Json -Depth 3

        if ($existing) {
            $existingId = $existing.id
            Write-Step "Updating existing PE version id=$existingId..."
            $updateResp = Invoke-RestMethod -Uri "$ApiBase/admin/peVersions/$existingId" -Method Put -Headers $headers -Body $body
            Write-OK "Updated PE version id=$existingId"
        } else {
            Write-Step "Creating new PE version..."
            $createResp = Invoke-RestMethod -Uri "$ApiBase/admin/peVersions" -Method Post -Headers $headers -Body $body
            Write-OK "Created new PE version"
        }

        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  Mode B register SUCCESS" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "  Version: $Version" -ForegroundColor Cyan
        Write-Host "  file_path: $filePathHint" -ForegroundColor Cyan
        Write-Host "  file_hash: $wimHash" -ForegroundColor Cyan
    }

    exit 0
}
catch {
    Write-Err $_.Exception.Message
    Write-Err $_.ScriptStackTrace
    exit 1
}
