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
