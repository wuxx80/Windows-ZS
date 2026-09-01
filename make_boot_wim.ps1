# ============================================================
# ZS deployment system - boot.wim packaging script (Phase 1)
# ------------------------------------------------------------
# Purpose: Inject pe_assets\Startnet.cmd into WinPE boot.wim
#          and supplement missing native tools (choice.exe)
#
# Two modes:
#   Mode A (ADK): Requires Windows ADK + WinPE Addon installed locally
#                 Uses copype.cmd to create a fresh WinPE working directory
#   Mode B (Mount): Uses only the built-in DISM
#                   Mounts an existing boot.wim, overwrites Startnet.cmd, commits
#
# Usage (requires Administrator privileges):
#   .\make_boot_wim.ps1 -Mode B                                  # default source
#   .\make_boot_wim.ps1 -Mode B -SourceWim "D:\path\boot.wim"
#   .\make_boot_wim.ps1 -Mode A                                  # requires ADK
#   .\make_boot_wim.ps1 -Help
# ============================================================

param(
    [ValidateSet('A','B')]
    [string]$Mode = 'B',

    [string]$SourceWim = 'C:\ZS_Cache\pe\boot.wim',

    [string]$SourceSdi = '',

    [string]$StartnetSrc = '',

    [string]$OutDir = '',

    [switch]$SkipChoiceInject,

    [switch]$Help
)

$ErrorActionPreference = 'Stop'

# ---------- Path initialization ----------
$ROOT          = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $StartnetSrc) { $StartnetSrc = Join-Path $ROOT 'pe_assets\Startnet.cmd' }
if (-not $OutDir)      { $OutDir      = Join-Path $ROOT '_pe_build\boot_assets' }
$MountDir      = Join-Path $ROOT '_pe_build\mount'
$Manifest      = Join-Path $OutDir 'boot_manifest.txt'

if ($Help) {
    Write-Host "ZS boot.wim builder (Phase 1)" -ForegroundColor Cyan
    Write-Host "Usage: .\make_boot_wim.ps1 -Mode B [-SourceWim <path>] [-SourceSdi <path>]"
    Write-Host "       .\make_boot_wim.ps1 -Mode A"
    Write-Host ""
    Write-Host "  -Mode A             Use Windows ADK copype to create fresh WinPE (requires ADK installed)"
    Write-Host "  -Mode B             Mount existing boot.wim and inject Startnet.cmd (DISM only)"
    Write-Host "  -SourceWim          Source boot.wim path for Mode B (default: C:\ZS_Cache\pe\boot.wim)"
    Write-Host "  -SourceSdi           Source boot.sdi path to copy as-is"
    Write-Host "  -StartnetSrc         Path to Startnet.cmd (default: pe_assets\Startnet.cmd)"
    Write-Host "  -OutDir              Output directory (default: _pe_build\boot_assets)"
    Write-Host "  -SkipChoiceInject    Do not inject choice.exe even if missing in source WIM"
    Write-Host "  -Help                Show this help"
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

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-MountSucceeded {
    param([string]$MountDir)
    # After a successful mount, Windows\System32\cmd.exe must exist
    $probe = Join-Path $MountDir 'Windows\System32\cmd.exe'
    return (Test-Path -LiteralPath $probe)
}

# ---------- Main flow ----------
$mountSucceeded = $false
try {
    if (-not (Test-Path -LiteralPath $StartnetSrc)) {
        throw "Startnet.cmd not found: $StartnetSrc"
    }

    if (-not (Test-Admin)) {
        Write-Warn2 "DISM mount requires Administrator privileges."
        Write-Warn2 "Please re-run this script from an elevated PowerShell."
        exit 1
    }

    # Clean stale mount points
    Write-Step "Cleaning stale DISM mount points..."
    & dism /Cleanup-Mountpoints 2>&1 | Out-Null
    Write-OK "Cleanup done"

    # Clean mount working directory leftovers (prevent orphan files from previous failed runs)
    if (Test-Path $MountDir) {
        Get-ChildItem $MountDir -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -LiteralPath $MountDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Prepare directories
    if (-not (Test-Path $OutDir))   { New-Item -ItemType Directory -Path $OutDir   -Force | Out-Null }
    New-Item -ItemType Directory -Path $MountDir -Force | Out-Null

    $outWim = Join-Path $OutDir 'boot.wim'
    $outSdi = Join-Path $OutDir 'boot.sdi'

    # ---------- Mode A: Create fresh WinPE with ADK copype ----------
    if ($Mode -eq 'A') {
        $adkRoot   = "${env:ProgramFiles(x86)}\Windows Kits\10\Assessment and Deployment Kit"
        $winpeRoot = Join-Path $adkRoot 'Windows Preinstallation Environment'
        $copype    = Join-Path $winpeRoot 'copype.cmd'
        if (-not (Test-Path $copype)) {
            $adkRoot   = "${env:ProgramFiles}\Windows Kits\10\Assessment and Deployment Kit"
            $winpeRoot = Join-Path $adkRoot 'Windows Preinstallation Environment'
            $copype    = Join-Path $winpeRoot 'copype.cmd'
        }
        if (-not (Test-Path $copype)) {
            throw "ADK copype.cmd not found. Install Windows ADK + WinPE Addon, or use -Mode B."
        }

        $workDir = Join-Path $ROOT '_pe_build\copype_work'
        if (Test-Path $workDir) { Remove-Item -Recurse -Force $workDir }
        New-Item -ItemType Directory -Path $workDir -Force | Out-Null

        Write-Step "Mode A: running copype.cmd (amd64)..."
        & cmd /c "`"$copype`" amd64 `"$workDir`""
        if ($LASTEXITCODE -ne 0) { throw "copype.cmd failed (exit $LASTEXITCODE)" }

        $srcWimInWork = Join-Path $workDir 'winpe.wim'
        Copy-Item -LiteralPath $srcWimInWork -Destination $outWim -Force

        # copype's winpe.wim is writable, no need to clear ReadOnly
    } else {
        # ---------- Mode B: Mount existing WIM ----------
        if (-not (Test-Path -LiteralPath $SourceWim)) {
            throw "Source boot.wim not found: $SourceWim"
        }

        Write-Step "Mode B: copying source WIM to output..."
        Copy-Item -LiteralPath $SourceWim -Destination $outWim -Force

        # CRITICAL FIX: Copy-Item preserves ReadOnly attribute from source WIM,
        # causing DISM mount to fail with 0xc1510111
        $attrs = (Get-Item -LiteralPath $outWim).Attributes
        if ($attrs -band [System.IO.FileAttributes]::ReadOnly) {
            (Get-Item -LiteralPath $outWim).Attributes = $attrs -bxor [System.IO.FileAttributes]::ReadOnly
            Write-OK "Cleared ReadOnly attribute on output WIM"
        }
        Write-OK "Source copied: $SourceWim -> $outWim"

        # Verify source WIM integrity
        Write-Step "Verifying WIM structure with dism /Get-WimInfo..."
        & dism /Get-WimInfo /WimFile:$outWim 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dism /Get-WimInfo failed on $outWim - source may be corrupted"
        }
        Write-OK "WIM structure OK"

        Write-Step "Mode B: mounting WIM (Index 1) -> $MountDir"
        # Call dism directly so PowerShell handles argument quoting
        # (array splatting treats embedded quotes as literal characters)
        & dism /Mount-Wim /WimFile:$outWim /Index:1 /MountDir:$MountDir
        if ($LASTEXITCODE -ne 0) {
            throw "DISM /Mount-Wim failed (exit $LASTEXITCODE)"
        }

        # Verify mount actually succeeded (DISM may silently fail with exit=0)
        if (-not (Test-MountSucceeded $MountDir)) {
            throw "DISM reported success but mount dir has no Windows\System32\cmd.exe - mount did not actually happen"
        }
        $mountSucceeded = $true
        Write-OK "WIM mounted successfully"
    }

    # ---------- Verify PE builtin tools (design spec V3 check) ----------
    Write-Step "Verifying PE builtin tools (V3 check)..."
    $requiredTools = @(
        'Windows\System32\wpeinit.exe',
        'Windows\System32\wpeutil.exe',
        'Windows\System32\choice.exe',
        'Windows\System32\cmd.exe',
        'Windows\System32\diskpart.exe',
        'Windows\System32\dism.exe',
        'Windows\System32\bcdboot.exe'
    )
    $missing = @()
    foreach ($t in $requiredTools) {
        $full = Join-Path $MountDir $t
        if (-not (Test-Path -LiteralPath $full)) {
            $missing += $t
        }
    }

    $injectedChoice = $false
    if ($missing.Count -gt 0) {
        # If only choice.exe is missing and user allows injection, copy from local System32
        $localChoice = "$env:SystemRoot\System32\choice.exe"
        if (-not $SkipChoiceInject -and ($missing -contains 'Windows\System32\choice.exe') -and (Test-Path -LiteralPath $localChoice)) {
            $tgt = Join-Path $MountDir 'Windows\System32\choice.exe'
            Copy-Item -LiteralPath $localChoice -Destination $tgt -Force
            $missing = $missing | Where-Object { $_ -ne 'Windows\System32\choice.exe' }
            $injectedChoice = $true
            Write-OK "Injected choice.exe from $localChoice (Microsoft system binary for 10s escape window)"
        }

        if ($missing.Count -gt 0) {
            Write-Warn2 "Missing PE builtin tools (this WIM is a slimmed PE):"
            $missing | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
            Write-Warn2 "Startnet.cmd may fail. Recommend using a fuller WinPE WIM (e.g. from ADK copype)."
        } else {
            Write-OK "All required PE builtin tools now present (after optional injection)"
        }
    } else {
        Write-OK "All required PE builtin tools present"
    }

    # ---------- Overwrite Startnet.cmd ----------
    $targetStartnet = Join-Path $MountDir 'Windows\System32\Startnet.cmd'
    $targetDir      = Split-Path -Parent $targetStartnet
    if (-not (Test-Path $targetDir)) {
        # We should never reach here if mount succeeded
        if (-not $mountSucceeded) {
            throw "Target dir does not exist ($targetDir) - mount likely failed silently. Aborting before any Copy-Item."
        }
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    Write-Step "Injecting Startnet.cmd..."
    Copy-Item -LiteralPath $StartnetSrc -Destination $targetStartnet -Force

    # Hash verification (design spec V3.1: byte-level consistency)
    $srcHash = Get-FileSHA256 $StartnetSrc
    $dstHash = Get-FileSHA256 $targetStartnet
    if ($srcHash -ne $dstHash) {
        throw "Startnet.cmd hash mismatch after copy: src=$srcHash dst=$dstHash"
    }
    Write-OK "Startnet.cmd hash verified: $srcHash"

    # ---------- Unmount WIM (commit changes) ----------
    Write-Step "Unmounting WIM with commit..."
    & dism /Unmount-Wim /MountDir:$MountDir /Commit
    if ($LASTEXITCODE -ne 0) {
        throw "DISM /Unmount-Wim /Commit failed (exit $LASTEXITCODE)"
    }
    $mountSucceeded = $false
    Write-OK "WIM committed"

    # ---------- Post-commit verification ----------
    Write-Step "Post-commit verification (dism /Get-WimInfo)..."
    & dism /Get-WimInfo /WimFile:$outWim 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Post-commit /Get-WimInfo failed - boot.wim may be corrupted"
    }
    Write-OK "boot.wim parseable after commit"

    # ---------- Copy boot.sdi (3MB fixed from ADK) ----------
    $sdiSourceFound = $false
    if (-not $SourceSdi) {
        $guess = Join-Path (Split-Path -Parent $SourceWim) 'boot.sdi'
        if (Test-Path -LiteralPath $guess) { $SourceSdi = $guess }
    }
    if ($SourceSdi -and (Test-Path -LiteralPath $SourceSdi)) {
        Write-Step "Copying boot.sdi from: $SourceSdi"
        Copy-Item -LiteralPath $SourceSdi -Destination $outSdi -Force
        Write-OK "boot.sdi copied"
        $sdiSourceFound = $true
    } else {
        Write-Warn2 "boot.sdi source not provided/skipped."
        Write-Warn2 "For production: copy boot.sdi from ADK install (3MB fixed)."
        Write-Warn2 "If you have a previous boot.sdi, pass via -SourceSdi <path>."
    }

    # ---------- Generate manifest ----------
    Write-Step "Generating manifest..."
    $wimHash  = Get-FileSHA256 $outWim
    $wimSize  = (Get-Item -LiteralPath $outWim).Length
    $wimSizeMB = [math]::Round($wimSize / 1048576, 1)

    $sdiHash = $null
    $sdiSize = 0
    $sdiSizeMB = 0
    if ($sdiSourceFound -and (Test-Path -LiteralPath $outSdi)) {
        $sdiHash = Get-FileSHA256 $outSdi
        $sdiSize = (Get-Item -LiteralPath $outSdi).Length
        $sdiSizeMB = [math]::Round($sdiSize / 1048576, 2)
    }

    $startnetHash = Get-FileSHA256 $StartnetSrc

    $lines = @(
        "[zs_boot_assets_v1]",
        "generated=$([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))",
        "mode=$Mode",
        "sha256=$wimHash=boot.wim",
        "size_bytes=$wimSize",
        "size_mb=$wimSizeMB"
    )
    if ($sdiHash) {
        $lines += "sha256=$sdiHash=boot.sdi"
        $lines += "sdi_size_bytes=$sdiSize"
        $lines += "sdi_size_mb=$sdiSizeMB"
    }
    $lines += "sha256=$startnetHash=Startnet.cmd"
    if ($injectedChoice) {
        $lines += "extra_injected=choice.exe (from $env:SystemRoot\System32\choice.exe, Microsoft system binary for 10s escape window)"
    }

    Set-Content -LiteralPath $Manifest -Value $lines -Encoding UTF8

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Phase 1 boot.wim build SUCCESS" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Output dir:   $OutDir" -ForegroundColor Cyan
    Write-Host "  boot.wim:     $wimSizeMB MB  sha256=$wimHash" -ForegroundColor Gray
    if ($sdiHash) {
        Write-Host "  boot.sdi:     $sdiSizeMB MB  sha256=$sdiHash" -ForegroundColor Gray
    }
    Write-Host "  Startnet.cmd: sha256=$startnetHash" -ForegroundColor Gray
    Write-Host "  Manifest:     $Manifest" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Next step: copy boot.wim and boot.sdi to server's PE assets folder" -ForegroundColor Yellow
    Write-Host "             and update server/app/model/PeVersion.php with new sha256." -ForegroundColor Yellow

    exit 0
}
catch {
    Write-Err $_.Exception.Message
    Write-Err $_.ScriptStackTrace

    # Failure fallback: if mount is still active, attempt discard unmount
    if ($mountSucceeded -and (Test-Path $MountDir)) {
        Write-Warn2 "Attempting to discard mounted WIM at $MountDir..."
        & dism /Unmount-Wim /MountDir:$MountDir /Discard 2>&1 | Out-Null
    }
    exit 1
}
finally {
    # Do not force-delete MountDir (helps debugging). Next run cleans it at start.
}
