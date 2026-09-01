# ZS 无人值守装机 · 完美落地版设计文档 v1

> 生成日期：2026-09-01
> 适用场景：**A. 单机电在线链路**（用户在正常 Windows 里下单 → 自动重启进 PE → 全离线自动装机 → 重启进新系统 → 自动装软件/优化）
> 上一版失败原因：假设 PE 端始终能联网，且要求用户先做 U 盘刻录再改 BIOS 启动顺序；这两个前提在 50%+ 的真实硬件上不成立。
> 本版核心变化：**所有装机资源在 Windows 下单时就 100% 下完 + 校验完写到非系统分区，然后通过 BCD 一次性启动项直接从硬盘加载 PE WIM，PE 端不假设任何网络存在。**

***

***

> ## ⚠️ R7 架构 · 本文件为当前唯一有效设计（2026-09-02 生效）
>
> - **废弃**：所有设计文档/旧架构/旧主链路中记载的「Windows 下单 → U盘 / ISO / PXE → PE 端联网认领 waiting 任务」方案。全部不再作为编码依据。
>
> - **启用**：本文档定义的「BCD bootsequence 一次性启动 + 硬盘 boot.wim ramdisk + ZS\_Task 全离线任务目录 + Startnet.cmd 10s 逃生窗 + ZS\_Agent 八阶段流水线 + SetupComplete.cmd 首次启动」主链路。
>
> - **boot.wim / boot.sdi 来源**：均由运维 / 开发机使用微软官方 **Windows ADK + WinPE Addon** 预先打包；**不调用用户电脑本地的 ADK / copype**；用户端 Windows\_Client 在 P1 阶段仅把这两个文件当作二进制资源从服务器 HTTP 下载。boot.sdi 与 ADK 安装目录下的原版完全一致（SHA-256 白名单）。
>
> - **不使用任何第三方 PE 发行版的品牌名 / 作者名 / 商标名**；仅使用通用微软官方命令（bcdedit / dism / diskpart / bcdboot / wpeutil / oscdimg 等）。
>
> - **「别人易语言写的/」目录仅作为技术对比学习资料，内容不纳入本项目、不写入六件套、不作为任何编码依据。**

### 当前代码真实差距（必须先过 Phase 0，再编码 Phase 1\~6）

| 差距                                                                                                                                             | 状态                                          | 对应文档                       |
| ---------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------- | -------------------------- |
| P0-1 RoleController.php + Role 路由 缺失                                                                                                           | ❌ 未实现                                       | 开发计划表.md §5 / 项目理解报告.md §三 |
| P0-2 users.html role:string ⇆ UserController role\_id:int 契约不一致                                                                                | ❌ 未修复                                       | 开发计划表.md §5                |
| P0-3 WinPE\_Agent 未入 ZS\_Installer.slnx / 未入 Git / nullable 警告                                                                                 | ❌ 未实现                                       | 开发计划表.md §5                |
| P0-4 AuthMiddleware / LogMiddleware 未注册（config/middleware.php 仅 Cors）                                                                          | ❌ 未实现                                       | 开发计划表.md §5                |
| P0-5 5 个测试脚手架（\_b5\_logic\_test / \_b5\_ui\_harness / \_iso\_logic\_test / \_offline\_logic\_test / \_offline\_ui\_harness）磁盘已删、Git delete 未提交 | ❌ 未整理                                       | 开发计划表.md §5                |
| P0-6 Git 工作树脏（28 modified / 8 deleted / 6 untracked）                                                                                           | ❌ 未 commit                                  | 版本更新记录.md §R7              |
| P1 Windows 端 ZS\_Task 目录 + task.ini 16字段 + zs\_manifest.key 写入                                                                                 | ❌ 仅本文档定义                                    | 开发计划表.md §7                |
| P2 BCD bootsequence 一次性启动项注入 + 30s 倒计时                                                                                                         | ❌ 仅本文档定义                                    | 开发计划表.md §7                |
| P3 Startnet.cmd 盘符遍历 + 10s 倒计时逃生窗 + 手动装机菜单                                                                                                     | ❌ 仅本文档定义                                    | 开发计划表.md §8                |
| P4 ZS\_Agent §6 八阶段流水线（固件双判/分区尾端验证等 6 项增强未实现）                                                                                                  | ⚠️ WinPE\_Client\Services 有部分封装，需迁移合并 + 加验证 | 开发计划表.md §9                |
| P5 SetupComplete.cmd 模板渲染 + 写入新系统 + 自清理开关                                                                                                      | ❌ 仅本文档定义                                    | 开发计划表.md §10               |

***

## 0. 设计目标与验收标准

**单点击通过率目标：** 在一台**全新拆封**的 Windows 11 OEM 机器（UEFI+GPT+中文系统+没装任何工具）上，做到：

1. 用户只操作一次：打开 Windows\_Client → 登录 → 选 Win11 专业版镜像 → 点"一键装机" → 确认
2. 之后 20\~40 分钟（取决于镜像体积和硬盘速度），任何环节**不需要再按键盘或鼠标**
3. 最终停在：新系统桌面已打开 + 已装好列在 task.ini 中的所有软件 + 已执行优化项
4. 同时满足**安全可中断**：任何关键步骤（BCD 注入后、PE 启动后、部署中）都有明确的逃生方式，不会出现"格式化到一半用户只能傻等"的死路。

***

## 1. 顶层架构（已由用户 2026-09-01 确认）

### 1.1 旧设计为什么失败

| 维度       | 旧设计假设（会失败）                                     | 新设计落地原则                                                       |
| -------- | ---------------------------------------------- | ------------------------------------------------------------- |
| PE 网络    | PE 启动后从服务器拉镜像/任务/驱动（网卡驱动常在 PE 里缺失）             | **PE 永远无网**：所有资源提前在 Windows 端下完校验完                            |
| 进入 PE 方式 | 用户先做 U 盘再进 BIOS 改启动顺序（90% 普通用户不会）              | **BCD 一次性启动项**：bcdedit /bootsequence，重启自动进，不留痕                |
| 资源时机     | 装机流程中逐步获取（中途断网全盘卡死）                            | **下单即完成准备**：ZS\_Task 目录资源齐全才能点"确认重启"                          |
| 回退机制     | 没考虑（格式化后出问题=砖）                                 | **三重逃生窗**：30s Windows 侧取消 / 10s PE 侧 M 键手动 / 任何命令失败停 cmd      |
| 启动入口     | WPF GUI 客户端在 PE 里需要 .NET 运行时+中文字体（实际会全变方块然后卡死） | **Startnet.cmd 直接调原生 .NET 8 AOT 单文件 EXE**：不依赖 PE 的 GUI 子系统和字体 |

### 1.2 为什么这么选 — 每个选型都有可验证的工程依据

1. **离线任务目录 ZS\_Task/**：所有装机资源在用户下单阶段就 100% 下载完成，PE 端完全不依赖网络；使用独立根目录便于 PE 启动后快速盘符遍历定位
2. **BCD bootsequence + ramdisk**：从当前启动项复制模板自动继承 UEFI/BIOS 平台参数；bootsequence 保证只生效一次，PE 异常后自动回到原 Windows，避免永久污染启动菜单
3. **Startnet.cmd 入口 + wpeinit**：WinPE 默认启动执行入口，三种可选入口中兼容性最好（原生即支持，不需要额外注册表/ini 注入）
4. **choice.exe 10 秒倒计时逃生**：WinPE 原生自带命令，单进程实现倒计时+双按键分流，避免 start/pause 多进程竞态
5. **DISM Apply-Image + bcdboot**：微软原生部署链路，WIM/ESD 均支持；bcdboot 自动按固件类型写入对应引导目录与 BCD
6. **SetupComplete.cmd 首次进系统钩子**：OOBE 完成、首次登录前，以 SYSTEM 权限自动执行一次，执行完可自毁，是官方推荐的首次部署自动化入口

***

## 2. 任务目录结构

**放在哪里：** 所有资源放在**非系统分区**的 `\ZS_Task\` 根目录。选盘策略：跳过 C 盘，选剩余空间最大且 ≥ 镜像体积×2.5 的 NTFS 分区，盘符记为 `$TASK_DRIVE`，路径为 `$TASK_DRIVE:\ZS_Task\`。

**目录内容清单（10 项）：**

```
$TASK_DRIVE:\ZS_Task\
├── boot.wim               ← 通用 WinPE WIM（内置 Startnet.cmd 自启动入口，体积~350MB）
├── boot.sdi               ← ramdisk 所需的 SDI 文件（从 ADK 复制，固定 3MB）
├── system.esd             ← 系统镜像（WIM / ESD 均可，从服务器下载的 install.*）
├── drivers\               ← 驱动目录（可空。子目录按设备类型：\drivers\net \drivers\storage \drivers\usb …）
├── software\              ← 静默安装软件包（可空，每个软件 1 个子目录：\software\7z\ 内放 7z1900-x64.msi）
├── ZS_PE_Agent.exe        ← PE 端装机主程序（.NET 8 AOT 单文件，原生控制台，不需要 PE 里有 .NET 运行时）
├── task.ini               ← 任务配置（明文，便于审计）
├── zs_manifest.key        ← 所有文件的 SHA-256 校验清单（PE 端装机前必须 100% 通过）
├── pe_log.txt             ← （PE 阶段创建）部署流水日志，失败后可追溯
└── README_请勿删除.txt    ← 说明此目录重要（面向手动进 PE 的用户）
```

> **重要说明：boot.wim 和 boot.sdi 的来源**
> 这两个文件**不是在用户 Windows\_Client 本地构建**（构建需要安装 Windows ADK + WinPE Addon，约 6GB，普通用户不可能装）。
> 正确做法：**运维人员用 ADK 在服务器/开发机上预先打包出通用 boot.wim（内置 §5.1 的 Startnet.cmd）和 boot.sdi，作为二进制资产上传到服务器**；用户端 Windows\_Client 在 P1 阶段只是把这两个文件当作普通资源从服务器 HTTP 下载下来，不调用任何 ADK 工具。
> 打包 boot.wim 的流水线在 Phase 1 交付中实现，产出物是两个可复现哈希的二进制文件，更新版本号即可全网分发。

### 2.1 task.ini 字段设计（明文 ini）

```ini
; ============================================================
; ZS 无人值守装机任务配置（由 Windows_Client 在下单阶段生成）
; ============================================================

[meta]
version=1
created_at=2026-09-01 18:06:05
task_id=ZS-20260901-180605-DEMO
server_api=https://example.com/api    ; 可选：有网时回传进度，PE 无网则忽略
oobe_mode=manual                      ; auto=用 Unattend.xml 全自动跳过 / manual=保留 OOBE 让用户操作
first_boot_cleanup_zjzl=no            ; yes=首进系统后删 ZS_Task 目录 / no=保留以备下次装机复用

[target_disk]
disk_index=0                          ; 目标物理盘序号（0=第一块盘，Windows_Client 下单时让用户预览确认）
partition_mode=clean_whole_disk       ; clean_whole_disk=整盘重分区(推荐) / clean_c_only=只清 C 盘保留数据盘(待实现)

[partition_scheme]
table=auto                            ; auto=按固件自动判 GPT/MBR / force_gpt / force_mbr
esp_size_mb=500                       ; ESP 分区大小(仅 GPT)
msr_size_mb=16                        ; MSR 分区大小(仅 GPT)
recovery_size_mb=800                  ; 恢复分区大小(Win11 推荐)
system_letter=C                       ; 系统盘盘符
system_label=Windows
format_fs=ntfs
quick_format=yes

[system_image]
file=system.esd                       ; 对应 ZS_Task/ 中的文件名
index=6                               ; WIM/ESD 内的分卷号（6=专业版、4=家庭高级版，依具体镜像而定）
name=Windows 11 专业版 23H2

[drivers]
; 驱动注入策略：PE 端离线注入（默认开，提高首次进系统后 USB/网卡/显卡亮屏概率）
inject=yes
recurse=yes
force_unsigned=yes                    ; 强制注入无签名驱动（提高兼容，代价是进系统后可能弹签名告警）

[software]
; SetupComplete 阶段按顺序安装，每个 entry 对应 ZS_Task\software\<key>\ 下的文件
count=2
sw1_key=7z
sw1_name=7-Zip 19.00 x64
sw1_msi=7z1900-x64.msi
sw1_args=/qn /norestart
sw1_expect_exit=0
sw2_key=chrome
sw2_name=Google Chrome 最新版
sw2_exe=ChromeSetup.exe
sw2_args=--install --silent --system-level
sw2_expect_exit=0

[optimize]
; 首次进系统后的优化项（SetupComplete 中执行）
hibernation=off                       ; 关闭休眠，释放 C 盘空间
standby_timeout_ac=0                  ; 外接电源永不待机
standby_timeout_dc=30                 ; 电池 30 分钟待机
pagefile_auto=yes                     ; 自动托管页面文件
disable_telemetry=yes                 ; 关闭遥测
remove_cortana=yes                    ; 隐藏/卸载 Cortana（Win10 时代遗留，Win11 忽略即可）
```

### 2.2 zs\_manifest.key 校验清单格式

每行 `sha256_hex = 相对路径`，PE 端部署**第一条命令**就是遍历这个表，逐个校验；任何一行对不上 → 终止部署，报"文件损坏请重新下单"。

```ini
[zs_manifest_v1]
sha256=906A28A341B6A19E6157930D26B57FDD33AA9C17F4E0A36A88FEBA95478AD1BF=boot.wim
sha256=966D29A331B7A59E6457E77E57BF70D046DEE86AF799D5138A88BBEF49FBD6B9=boot.sdi
sha256=433D6C3D2F5BFFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7F=system.esd
sha256=6702C6F1730C29C8A30170743C117FFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7F=ZS_PE_Agent.exe
sha256=6C195407696E5ED7C05A2E2761555BFF7FFF7FFF7FFF7FFF7FFF7FFF7FFF7F=task.ini
sha256=E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F60718293A4B5=drivers/net/rtl8125.inf
;（所有 drivers/ 和 software/ 内文件逐一列出）…
```

***

## 3. 阶段 P1：预准备（Windows\_Client，正常系统内，需管理员权限）

**目标：** 把 10 项资源全部写到 `$TASK_DRIVE:\ZS_Task\`，校验通过后才允许点"确认重启"。

### 3.1 执行流程

1. **选盘**：枚举所有逻辑盘符，跳过 C 盘，按 NTFS + 可用空间 ≥ `预计下载体积*2.5` 过滤，选剩余空间最大的那个作为 `$TASK_DRIVE`；若没有则提示"磁盘空间不足，请释放至少 XX GB 后再试"，**不允许继续**。
2. **创建目录**：`New-Item -ItemType Directory -Path "$TASK_DRIVE:\ZS_Task" -Force`，同时写入 `README_请勿删除.txt`：`本目录是 ZS 装机系统的任务目录，删除会导致无人值守失败；装机完成如需清理空间可在"系统优化"中一键清理。`
3. **下载**：用 `HttpClient`（或 BITS `Start-BitsTransfer`）下载 boot.wim、boot.sdi、system.esd、驱动包、软件包、ZS\_PE\_Agent.exe。下载期间显示**实时进度+剩余时间**，支持"暂停/取消"（取消即整体回滚删 ZS\_Task 目录）。
4. **生成 task.ini**：由 Windows\_Client 根据用户在 UI 里选的镜像、分区、软件、优化选项，按 §2.1 的模板生成，保存为 UTF-8。
5. **计算并写入 zs\_manifest.key**：对每个文件跑 `Get-FileHash -Algorithm SHA256`，按 §2.2 格式写入 manifest。

### 3.2 验证点 V1（7 项，必须全部 True 才允许继续）

```powershell
@(
  (Test-Path "$TASK_DRIVE\ZS_Task\boot.wim"),
  (Test-Path "$TASK_DRIVE\ZS_Task\boot.sdi"),
  (Test-Path "$TASK_DRIVE\ZS_Task\system.esd"),
  (Test-Path "$TASK_DRIVE\ZS_Task\ZS_PE_Agent.exe"),
  (Test-Path "$TASK_DRIVE\ZS_Task\task.ini"),
  (Test-Path "$TASK_DRIVE\ZS_Task\zs_manifest.key"),
  ((& dism /Get-WimInfo /WimFile:"$TASK_DRIVE\ZS_Task\boot.wim" 2>$null | Select-String "Index" | Measure-Object).Count -ge 1)
) | ForEach-Object { if (-not $_) { throw "V1 验证失败" } }
```

### 3.3 回退 F1（P1 阶段取消 / 下载中断 / 校验不通过）

```powershell
Remove-Item -LiteralPath "$TASK_DRIVE\ZS_Task" -Recurse -Force -ErrorAction SilentlyContinue
```

只要还没进入 P2（还没碰 BCD），删了这个目录就是\*\* 100% 零残留\*\*，不影响原系统任何东西。

***

## 4. 阶段 P2：BCD 一次性启动项注入（需管理员权限）

### 4.1 完整命令模板（PowerShell）

```powershell
param([Parameter(Mandatory)][string]$TASK_DRIVE_LETTER_NO_COLON, [string]$WIM_REL_PATH = "\ZS_Task\boot.wim")
$ErrorActionPreference = "Stop"
$wimFullPath = "${TASK_DRIVE_LETTER_NO_COLON}:${WIM_REL_PATH}"

# 4.1.1 先备份 BCD（保留 7 天）
$backupPath = Join-Path $env:TEMP ("ZS_BCD_Backup_" + (Get-Date -Format "yyyyMMddHHmmss") + ".bcd")
& bcdedit /export $backupPath
if ($LASTEXITCODE -ne 0) { throw "BCD 备份失败，中止。" }

# 4.1.2 从 {current} 复制一份 → 继承原平台正确的 winload.exe 或 winload.efi、nx、locale 等参数
$copyResult = & bcdedit /copy "{current}" /d "ZS 无人值守 PE" 2>&1
if ($LASTEXITCODE -ne 0) { throw "bcdedit /copy 失败: $copyResult" }
$guidMatch = [regex]::Match(($copyResult | Out-String), '\{[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}\}')
if (-not $guidMatch.Success) { throw "无法解析 bcdedit /copy 返回的 GUID" }
$peGuid = $guidMatch.Value

# 4.1.3 配置启动项指向 ramdisk = 硬盘上的 boot.wim + 全局 {ramdiskoptions}
& bcdedit /set $peGuid device ("ramdisk=[{" + $TASK_DRIVE_LETTER_NO_COLON + ":}]" + $WIM_REL_PATH + ",{ramdiskoptions}")
if ($LASTEXITCODE -ne 0) { throw "bcdedit set device 失败" }
& bcdedit /set $peGuid osdevice ("ramdisk=[{" + $TASK_DRIVE_LETTER_NO_COLON + ":}]" + $WIM_REL_PATH + ",{ramdiskoptions}")
if ($LASTEXITCODE -ne 0) { throw "bcdedit set osdevice 失败" }
& bcdedit /set $peGuid systemroot "\Windows"
& bcdedit /set $peGuid winpe yes
& bcdedit /set $peGuid detecthal yes

# 4.1.4 全局 {ramdiskoptions}：指定 boot.sdi 位置（只需第一次配，重复配也幂等）
& bcdedit /set "{ramdiskoptions}" ramdisksdidevice ("partition=" + $TASK_DRIVE_LETTER_NO_COLON + ":")
& bcdedit /set "{ramdiskoptions}" ramdisksdipath "\ZS_Task\boot.sdi"

# 4.1.5 【关键】设为一次性启动项！此参数只影响下一次重启，不会永久污染启动菜单
& bcdedit /bootsequence $peGuid
if ($LASTEXITCODE -ne 0) { throw "bcdedit /bootsequence 失败" }

Write-Host "ZS PE 启动项注入成功，GUID = $peGuid"
Write-Host "  备份文件 = $backupPath"
Write-Host "  30 秒后系统将重启（可运行 shutdown /a 取消）"
& shutdown /r /f /t 30 /c "ZS 无人值守装机环境 30 秒后重启，如需取消请运行 shutdown /a"
```

### 4.2 为什么用 /copy {current} 而不是 /create /application osloader

因为 `/create` 方式下，`path` 参数（winload.efi 还是 winload.exe）必须手填，而 UEFI 和 BIOS 的 `path` 完全不同；填错就 0xc000000f 蓝屏。`/copy {current}` 是 Microsoft Answer 里官方推荐的做法 — 自动继承了当前机器正在用的所有平台参数，不用判断 UEFI/BIOS。

### 4.3 为什么用 /bootsequence 而不是 /default + /timeout

- `/default` 是永久改默认项，如果 PE 安装过程蓝屏了，用户下次重启还会再进 PE，恶性循环。

- `/bootsequence` 是官方的"一次性启动指定项"，启动完自动回到原来的 default，安全；如果 PE 蓝屏，下次就回到原 Windows（原 Windows 还在，因为 P2 没动任何分区数据）。

### 4.4 验证点 V2（注入后运行，3 项都要符合预期）

```powershell
# V2.1: 新条目里 device 和 osdevice 都是 ramdisk 指向 ZS_Task\boot.wim
$enumOut = & bcdedit /enum $peGuid
($enumOut | Select-String "ramdisk.*\\ZS_Task\\boot\.wim" | Measure-Object).Count -eq 2

# V2.2: {ramdiskoptions} 已经有 sdi 设备和 sdi 路径
$ramOut = & bcdedit /enum "{ramdiskoptions}"
($ramOut | Select-String "ramdisksdi" | Measure-Object).Count -ge 2

# V2.3: bootmgr 下 bootsequence 字段存在
$bootmgrOut = & bcdedit /enum "{bootmgr}"
($bootmgrOut | Select-String "bootsequence" | Measure-Object).Count -eq 1
```

### 4.5 回退 F2（P2 阶段 30 秒倒计时内取消 / 注入后立即后悔）

```powershell
shutdown /a
bcdedit /bootsequence
bcdedit /delete $peGuid /clean
Remove-Item -LiteralPath "$TASK_DRIVE\ZS_Task" -Recurse -Force -ErrorAction SilentlyContinue
```

***

## 5. 阶段 P3：PE 启动入口 + 10 秒逃生窗

所有入口逻辑都写在 **boot.wim 里的** **`\Windows\System32\Startnet.cmd`**，这个文件在 PE 启动时会被**自动执行**（微软默认机制）。

### 5.1 Startnet.cmd 完整内容

```batch
@echo off
setlocal EnableExtensions
title = ZS 无人值守装机环境 PE v1
color 1F
cls

echo ==============================================================================
echo    ZS 无人值守装机系统 v1        (PE 内部运行环境)
echo ==============================================================================
echo.
echo [ZS] 正在初始化即插即用、存储驱动和网络栈（无网不影响装机流程）...
wpeinit
echo.

:: ------------------------------------------------------------
:: 第一步：在所有可用盘符中找 \ZS_Task\task.ini，定位任务目录
:: ------------------------------------------------------------
set "TASK_DRIVE="
set "TASK_ROOT="
for %%P in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (
  if exist "%%P:\ZS_Task\task.ini" (
    set "TASK_DRIVE=%%P:"
    set "TASK_ROOT=%%P:\ZS_Task"
    goto :found_task
  )
)

:: 没找到任务文件 —— 直接进手动模式（最安全的回退）
echo [ZS][警告] 未在任何可访问分区发现 ZS_Task\task.ini。
echo      可能原因：(1) 没有下单直接进 PE  (2) ZS_Task 目录被误删  (3) 盘符分配异常
echo      已进入手动命令行模式，您可以备份数据 / 用 diskpart 手动操作 / 输入 wpeutil reboot 重启。
echo.
cmd.exe /k
exit /b 0

:found_task
echo [ZS] 发现任务目录: %TASK_ROOT%
echo.

:: ------------------------------------------------------------
:: 第二步：10 秒逃生窗（choice.exe 是 WinPE 原生自带）
:: ------------------------------------------------------------
echo  [选项说明]
echo    - 按 [X] 立即开始无人值守装机（倒计时结束后默认执行）
echo    - 按 [M] 进入手动模式（取消自动装机，保留命令行可操作）
echo.
echo  [倒计时] 10 秒后自动开始无人值守装机...
echo.
choice /c XM /t 10 /d X /m "ZS: 请选择"

if errorlevel 2 goto :manual_mode
if errorlevel 1 goto :auto_deploy

echo [ZS][错误] choice 返回 %ERRORLEVEL%，默认进入手动模式
goto :manual_mode

:manual_mode
echo.
echo ==============================================================================
echo    已切换至手动模式。关闭本窗口回到纯命令行提示符。
echo    可用命令提示：
echo      diskpart           —— 分区工具
echo      dism /?            —— 镜像和驱动工具
echo      bcdboot /?         —— 引导修复工具
echo      wpeutil reboot     —— 重启电脑
echo      notepad            —— 打开记事本查看日志
echo ==============================================================================
cmd.exe /k
exit /b 0

:auto_deploy
echo.
echo [ZS] 启动自动装机主程序 ZS_PE_Agent.exe ...
"%TASK_ROOT%\ZS_PE_Agent.exe" --auto --task "%TASK_ROOT%\task.ini" --manifest "%TASK_ROOT%\zs_manifest.key" --log "%TASK_ROOT%\pe_log.txt"

:: 主程序退出后的兜底
set "AGENT_EXIT=%ERRORLEVEL%"
echo.
echo ==============================================================================
echo    ZS_PE_Agent 已退出，退出码 = %AGENT_EXIT%。
echo    0 = 部署完成应重启；非 0 = 某处失败。
echo    完整日志位置: %TASK_ROOT%\pe_log.txt
echo ==============================================================================
if "%AGENT_EXIT%"=="0" (
  echo [ZS] 部署成功，按任意键将重启进入新系统 ...
  pause >nul
  wpeutil reboot
) else (
  echo [ZS][错误] 部署未完成，保持命令行以便救援。如需放弃并返回原系统，直接重启即可。
  cmd.exe /k
)
exit /b 0
```

### 5.2 逃生键为什么是这套 choice 配置

`choice /c XM /t 10 /d X` 的含义：

- 只接受两个按键：`X`、`M`（其他按键不生效，发出提示音）

- 如果 10 秒内没按，默认选 `X`

- 返回的 ERRORLEVEL：X=1，M=2（所以 `if errorlevel 2` 要写在前面，`if errorlevel N` 匹配的是 ≥ N）

比 `start /b + pause` 的双进程方案简单 10 倍，且 WinPE 原生有 choice.exe，不需要额外塞工具。

### 5.3 验证点 V3（打包 boot.wim 前的静态检查）

1. 用 DISM 挂 boot.wim → 确认 `MountDir\Windows\System32\Startnet.cmd` 的内容与 §5.1 完全一致（字节级对比哈希）
2. 确认 `MountDir\Windows\System32\wpeinit.exe`、`wpeutil.exe`、`choice.exe`、`cmd.exe`、`diskpart.exe`、`dism.exe`、`bcdboot.exe` 都存在（原版 WinPE 这些都有，不用自己加）
3. Unmount-Image /Commit 后，`dism /Get-WimInfo /WimFile:boot.wim` 能正常解析

### 5.4 失败回退 F3（PE 端三层兜底）

| 场景                       | 回退                                          |
| ------------------------ | ------------------------------------------- |
| 找不到 ZS\_Task 目录          | Startnet.cmd 最顶部的兜底 → 直接进 cmd.exe /k        |
| 10 秒内不想装机了               | 按 M → 手动模式 cmd.exe /k                       |
| ZS\_PE\_Agent 任何时候退出码非 0 | Startnet.cmd 末尾兜底 → 显示退出码+日志路径 → cmd.exe /k |
| ZS\_PE\_Agent 成功退出码 0    | 显式让用户按任意键确认后才 reboot（防止部署完成后直接重启而用户想先检查一下）  |

***

## 6. 阶段 P4：装机核心流水线（ZS\_PE\_Agent.exe 执行）

ZS\_PE\_Agent 是 .NET 8 AOT 单文件控制台 EXE，接受 4 个参数：`--auto`（无人值守）、`--task <path>`、`--manifest <path>`、`--log <path>`。

ZS\_PE\_Agent 内部按顺序跑以下 **8 个子阶段**，每个子阶段调用一个外部命令，外部命令 ExitCode≠0 立即：

1. 把错误信息 + 命令参数 + 时间戳写入日志
2. `Environment.Exit(非零)` → 触发 Startnet.cmd 兜底 → 停在 cmd。

### 6.0 前置子阶段：固件类型判定（GPT/MBR 分流）

PE 环境下真实硬件的注册表 `FirmwareType` 并不 100% 可靠（部分定制 PE 会把它清掉，少数 BIOS 的 ACPI 表上报也会错）。本设计采用**双重判定 + 相互印证**的两级策略，任意一级命中即可，两级冲突时以 diskpart 为准（因为 diskpart 读的是磁盘上真实写的分区表，是真正会被后续 clean/convert 用到的事实）。

#### 6.0.1 主判定：注册表（快速路径，0 IO）

```csharp
// 优先读注册表 FirmwareType
// 0x0 = Unknown, 0x1 = BIOS/MBR, 0x2 = UEFI/GPT（Win8+ 原生 PE 下此键稳定）
static FirmwareType DetectByRegistry()
{
    using var key = Microsoft.Win32.Registry.LocalMachine
        .OpenSubKey(@"System\CurrentControlSet\Control", writable: false);
    var raw = key?.GetValue("FirmwareType");
    if (raw is int v)
    {
        return v switch
        {
            0x1 => FirmwareType.Bios,
            0x2 => FirmwareType.Uefi,
            _   => FirmwareType.Unknown,
        };
    }
    // 备选键：某些 PE 下枚举 PE 固件环境变量
    var raw2 = Environment.GetEnvironmentVariable("FirmwareType");
    if (int.TryParse(raw2, out var v2))
        return v2 == 2 ? FirmwareType.Uefi : FirmwareType.Bios;
    return FirmwareType.Unknown;
}
```

#### 6.0.2 回退判定：diskpart 读取磁盘真实分区表（慢速但 100% 可信）

```csharp
static FirmwareType DetectByDiskpart(int targetDiskIndex)
{
    // 调用 diskpart，执行 "select disk N" + "detail disk"，抓取输出中 "Partition Style" 行
    // Win8+ 原生 WinPE 的 diskpart detail disk 输出包含：
    //   Partition Style: GUID Partition Table   ← 表示 GPT，固件必为 UEFI
    //   Partition Style: Master Boot Record     ← 表示 MBR，固件为 Legacy BIOS
    var scriptPath = Path.Combine(Path.GetTempPath(), "zs_detail_disk.txt");
    File.WriteAllText(scriptPath,
        $"select disk {targetDiskIndex}\r\ndetail disk\r\nexit\r\n");

    var pi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    using var proc = Process.Start(pi)!;
    var output = proc.StandardOutput.ReadToEnd();
    proc.WaitForExit();

    var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(l => l.StartsWith("Partition Style", StringComparison.OrdinalIgnoreCase));

    if (line == null) return FirmwareType.Unknown;
    if (line.Contains("GUID",      StringComparison.OrdinalIgnoreCase)) return FirmwareType.Uefi;
    if (line.Contains("Master Boot", StringComparison.OrdinalIgnoreCase)) return FirmwareType.Bios;
    return FirmwareType.Unknown;
}
```

#### 6.0.3 汇总判定逻辑（两种来源冲突处理）

```
if task.ini table != auto:
    直接按用户强制选 force_gpt / force_mbr 走（不做自动判定，跳过下面全部）

reg = DetectByRegistry()
disk = DetectByDiskpart(disk_index_from_task)

判定优先级：
  1. reg=Uefi AND disk=Uefi → 走 GPT 脚本（6.1a）
  2. reg=Bios AND disk=Bios → 走 MBR 脚本（6.1b）
  3. reg=Unknown AND disk=Uefi → 走 GPT（信 diskpart）
  4. reg=Unknown AND disk=Bios → 走 MBR（信 diskpart）
  5. 两者冲突（如 reg=Uefi 但 disk=MBR 或反之）
     → 这台机器正跑在 CSM 兼容模式，分区表和固件不一致，属于风险场景
     → 策略：以 diskpart 为准 + 日志中写红色警告 + 分区脚本末尾增加一条验证：
        GPT 分支最后再跑一次 "list partition" 校验真的有 ESP 分区；
        MBR 分支最后校验真的有 active 分区。
  6. 两者都 Unknown → 直接 Exit(2)，让 Startnet.cmd 停 cmd，报"无法判定固件类型"
     （真实硬件上这种情况极端罕见，出现意味着 diskpart 也读不到分区表，
      很可能是目标盘压根没初始化或数据线松动，硬往下执行 100% 会坏盘。）
```

**为什么把 diskpart 放在回退而不是主路径**：`diskpart /s` 要起一个子进程、要读磁盘，整个过程 200ms\~1s；注册表读 1ms。90% 的机器走主路径就够了，回退只在注册表读不到时触发，兼顾速度和可靠性。

### 6.1 子阶段 1 — 分区（diskpart /s 脚本）

**6.1a GPT / UEFI 脚本（diskpart /s）：**

```
select disk 0
clean
convert gpt
create partition efi size=500
format quick fs=fat32 label="ESP"
assign letter=S
create partition msr size=16
create partition primary size=800
format quick fs=ntfs label="Recovery"
set id="de94bba4-06d1-4d40-a16a-bfd50179d6ac"
gpt attributes=0x8000000000000001
create partition primary
format quick fs=ntfs label="Windows"
assign letter=C
:: ==== 尾端验证（对应 §6.0.3 第5项 CSM 冲突场景） ====
:: 确认真的存在 ESP 分区且是 FAT32
select partition 1
detail partition
list volume
exit
```

**6.1b MBR / BIOS 脚本（diskpart /s）：**

```
select disk 0
clean
convert mbr
create partition primary size=500
format quick fs=ntfs label="System Reserved"
active
assign letter=S
create partition primary
format quick fs=ntfs label="Windows"
assign letter=C
:: ==== 尾端验证（对应 §6.0.3 第5项 CSM 冲突场景） ====
:: 确认分区1真的被标记为 active（detail 输出会显示 "Active: Yes"）
select partition 1
detail partition
list volume
exit
```

Agent 执行完 diskpart /s 脚本后，必须解析 stdout 做一次结果验证：

- GPT 分支：`detail partition` 输出中必须包含 `Type : System` 或直接出现 `EFI` 字样，且 `list volume` 中必须有卷标为 `ESP`、文件系统为 `FAT`/`FAT32` 的一行。

- MBR 分支：`detail partition` 输出中必须包含 `Active: Yes`（或本地化后的 `活动: 是` / `Aktiv: Ja`）。

任何一条不满足 → 即使 diskpart 自己的 ExitCode=0，也要当作失败处理，`Environment.Exit(3)` → `回退 F4`。

### 6.2 子阶段 2 — 源文件完整性校验（zs\_manifest.key）

逐行解析 manifest，对每个文件重算 SHA256 对比。**任何一个不对立即 Exit(1)**。
同时检查 `task.ini` 中声明的 `[system_image] index`：

```
dism /Get-WimInfo /WimFile:"D:\ZS_Task\system.esd" /Index:<N>
```

### 6.3 子阶段 3 — 系统镜像展开

```
dism /Apply-Image /ImageFile:"<TASK_ROOT>\system.esd" /Index:<index_from_task> /ApplyDir:C:\
```

日志要抓进度百分比输出，解析后显示总进度。

### 6.4 子阶段 4 — 驱动离线注入

```
dism /Image:C:\ /Add-Driver /Driver:"<TASK_ROOT>\drivers" /Recurse /ForceUnsigned
```

只在 task.ini `[drivers] inject=yes` 且 drivers 目录非空时才跑。

### 6.5 子阶段 5 — 引导修复（写入 BCD 到 ESP / 保留分区）

```
# UEFI/GPT 模式：写引导到 ESP 分区（盘符 S）
bcdboot C:\Windows /s S: /f UEFI /l zh-cn

# BIOS/MBR 模式：写引导到 500MB 系统保留分区（盘符 S）
bcdboot C:\Windows /s S: /f BIOS /l zh-cn
```

### 6.6 子阶段 6 — 注入首次进系统自动化

**Step 6a：复制 software 目录**

```
xcopy /E /I /H /Y /Q "<TASK_ROOT>\software" "C:\Windows\Setup\Scripts\software\"
```

拷贝失败只写警告日志，不中断。

**Step 6b：生成 SetupComplete.cmd**

```batch
@echo off
setlocal EnableExtensions
set LOG=C:\ProgramData\ZS\first_boot.log
if not exist "C:\ProgramData\ZS" md "C:\ProgramData\ZS"
echo %date% %time% ZS SetupComplete 开始 >>%LOG%

:: === 静默安装软件（从 task.ini 逐个生成 start /wait 行）===
start "" /wait msiexec /i "C:\Windows\Setup\Scripts\software\7z\7z1900-x64.msi" /qn /norestart >>%LOG% 2>&1
start "" /wait "C:\Windows\Setup\Scripts\software\chrome\ChromeSetup.exe" --install --silent --system-level >>%LOG% 2>&1

:: === 系统优化 ===
powercfg /change standby-timeout-ac 0 >>%LOG% 2>&1
powercfg /change standby-timeout-dc 30 >>%LOG% 2>&1
powercfg /h off >>%LOG% 2>&1

:: === ZS_Task 清理策略（默认保留）===
:: if exist D:\ZS_Task RD /S /Q D:\ZS_Task

:: === 自毁：SetupComplete.cmd 只允许跑一次 ===
echo %date% %time% ZS SetupComplete 完成，自毁。 >>%LOG%
del "%~f0"
endlocal
exit /b 0
```

**Step 6c（oobe\_mode=auto 时才做）：写入 Unattend.xml 到** **`C:\Windows\Panther\Unattend.xml`**，关键配置：

- OOBE 全部跳过（HideEULAPage / SkipMachineOOBE / SkipUserOOBE / ProtectYourPC=3）

- 自动创建本地管理员账户

- TimeZone = "China Standard Time"

- InputLocale/SystemLocale/UILanguage 全 zh-CN

- Win11 旁路联网：`reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f`

### 6.7 子阶段 7 — 返回 0，控制权回 Startnet.cmd 让用户按任意键 reboot

### 6.8 回退 F4：任何命令 ExitCode != 0

写日志 → Environment.Exit(N) → Startnet.cmd 捕获 → 停在 cmd.exe /k。

***

## 7. 阶段 P5：首次进新系统 + SetupComplete 自动装软件优化

### 7.1 验证点 V5：SetupComplete 是否真的执行了

看 `C:\ProgramData\ZS\first_boot.log` 是否有"开始"和"完成，自毁"两行，且 `C:\Windows\Setup\Scripts\SetupComplete.cmd` 已经被删掉了。

### 7.2 已知问题

- SetupComplete 是 SYSTEM 权限运行，当前登录用户的开始菜单快捷方式可能没刷新（部分软件需要下次登录后才能在开始菜单看到）

- Win11 22H2+ 对强制跳过 OOBE 越来越严格，推荐默认 `oobe_mode=manual`，让用户保留自己注册 Microsoft Account / 创建本地账户的流程

***

## 8. 故障处理矩阵（全文汇总，便于运维查阅）

| 阶段              | 现象                                  | 根因           | 处理方式                                                    |
| --------------- | ----------------------------------- | ------------ | ------------------------------------------------------- |
| P2 30 秒倒计时      | 用户后悔                                | 正常取消         | `shutdown /a` + `bcdedit /bootsequence` 清空 + 删 ZS\_Task |
| P3 启动中          | PE 正在加载，用户不想装了                      | 还没进 Startnet | 等 30 秒进 10 秒倒计时后按 M                                     |
| P3 找不到 ZS\_Task | cmd 中报 "未在任何分区发现 ZS\_Task\task.ini" | 盘符错乱或被删      | 自动停 cmd；手动查看盘符后重部署                                      |
| P3 倒计时中         | 用户想取消                               | 正常逃生         | 按 M 进手动模式                                               |
| P4 diskpart 报错  | Agent 退出码非 0 停 cmd                  | 磁盘锁定/选错盘     | 手动 diskpart 检查，确认盘号后重试                                  |
| P4 镜像校验失败       | manifest 某行不一致                      | 下载损坏/坏道      | 记录文件名，回 Windows 重下                                      |
| P4 部署完蓝屏        | INACCESSIBLE\_BOOT\_DEVICE          | 存储驱动未注入      | 再进 PE 手动补驱动 dism /Add-Driver                            |
| P5 部分软件没装上      | SetupComplete 日志里 exit code 非 0     | 首次启动没网/静默参数错 | 手动重装，不影响系统                                              |

***

## 9. 分阶段交付计划（每个阶段独立可测试、可验收）

| 阶段                                            | 内容                                                                                                      | 交付标准                                              | 验证方法                                                                   |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------- | ---------------------------------------------------------------------- |
| **Phase 0 · 修复基建**                            | 补齐 RoleController / 修复 role vs role\_id 前后端契约 / LogMiddleware 注册 / WinPE\_Agent 加入 sln 与 Git / 恢复被删测试资产 | 后台用户和角色管理完整可用；Git 仓库新克隆下来缺啥都能找到                   | 后台创建角色+用户能登录；git status clean                                          |
| **Phase 1 · Boot.wim 制作流水线**                  | 用 ADK copype + DISM mount 注入 Startnet.cmd，产出通用 boot.wim（≈350MB） + boot.sdi                              | `dism /Get-WimInfo` 正常，Startnet.cmd 内容哈希与 §5.1 一致 | Hyper-V 二代(UEFI)和一代(BIOS) 都能从 BCD 启动项进 PE 并看到 ZS 标题+10秒倒计时             |
| **Phase 2 · 客户端 P1+P2 实现**                    | Windows\_Client 实现：选盘 → 下载 → 生成 task.ini/manifest → BCD 注入 → 30 秒倒计时重启                                  | 不连服务器也能跑通本地 test 资源流程                             | 虚拟机内跑 P1+P2 → 重启后自动进 Phase 1 做的 PE                                     |
| **Phase 3 · ZS\_PE\_Agent 全流水线实现**            | 实现 6.1\~6.7 八个子阶段，每个命令严格判 ExitCode，立刻停 cmd 兜底                                                           | 每个子阶段有独立单元测试，可通过 mock exit code 验证失败路径            | 虚拟机空盘跑：倒计时→分区→展开 100MB test wim→注入→写引导→注入 SetupComplete→返回 0→确认 C 盘有脚本 |
| **Phase 4 · P5 SetupComplete + Autounattend** | SetupComplete.cmd 生成器 + oobe\_mode=auto 的 Unattend.xml 生成器                                              | 日志+自毁都正常工作                                        | 部署完首次启动后检查 `C:\ProgramData\ZS\first_boot.log` + SetupComplete 已删除      |
| **Phase 5 · 真机端到端联调**                         | Phase 0\~4 全链路贯通                                                                                        | 除首次确认点击外，全流程零用户交互                                 | Intel 12/13 代 + AMD Ryzen 5000 两种常见真机平台各跑 1 次全流程                       |

***

## 10. 版本策略

- MAJOR=2（切换到本文档的离线新架构，原架构 MAJOR=1）

- MINOR：每个 Phase 完成后 +1

- PATCH：修复 bug 和微调命令参数

- 首个正式发布版本：**2.0.0**（Phase 5 联调通过后发布）

***

## 11. 与原项目模块的对应关系

| 原模块                       | 处理方式                                        | 要改什么                         |
| ------------------------- | ------------------------------------------- | ---------------------------- |
| server 后台（PHP + Layui）    | 保留（Phase 0 先修 RoleController 和 role\_id 契约） | 新增：镜像/驱动/软件包管理接口、task 生成接口   |
| Windows\_Client（WPF）      | 保留（原主逻辑替换）                                  | 原"一键装机"替换为 P1 下载 + P2 BCD 注入 |
| WinPE\_Client（WPF GUI）    | 降级为手动模式下可选小工具                               | 分区预览/软件勾选检查器，不做为主程序          |
| WinPE\_Agent（控制台 .NET 8）  | **保留并升级为 P4 核心执行者**                         | 实现 §6 八阶段严格 ExitCode 判定流水线   |
| \_pe\_iso\_build（ISO 构建器） | 保留降级为可选                                     | 继续支持制作离线 U 盘，但主链路不再依赖 ISO    |

***

*End of Spec v1.*
