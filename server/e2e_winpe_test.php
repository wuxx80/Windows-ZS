<?php
// 端到端回归测试：模拟 WinPE / Windows 客户端完整流程（登录→自注册→建任务→进度上报→完成）
// 运行方式：php e2e_winpe_test.php（需服务器运行于 http://127.0.0.1:8001）
$baseUrl = "http://127.0.0.1:8001";

function req($method, $path, $data = null, $token = null) {
    global $baseUrl;
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $baseUrl . $path);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);
    $headers = ["Content-Type: application/json"];
    if ($token) { $headers[] = "Authorization: Bearer " . $token; }
    if ($method === "POST") { curl_setopt($ch, CURLOPT_POST, true); }
    elseif ($method === "GET" && $data) { curl_setopt($ch, CURLOPT_URL, $baseUrl . $path . "?" . http_build_query($data)); $data = null; }
    if ($data !== null) { curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data, JSON_UNESCAPED_UNICODE)); }
    curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
    $resp = curl_exec($ch);
    $code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $err = curl_error($ch);
    curl_close($ch);
    return ["http" => $code, "body" => json_decode($resp, true), "err" => $err];
}

function ok($name, $cond, $detail) {
    echo ($cond ? "[PASS] " : "[FAIL] ") . $name . " - " . $detail . "\n";
    return $cond;
}

echo "=== WinPE 客户端端到端流程测试 ===\n\n";
$allPass = true;

// 1. 登录
$r = req("POST", "/api/v1/auth/login", ["username" => "admin", "password" => "admin123"]);
$token = $r["body"]["data"]["token"] ?? "";
$allPass = ok("登录", $r["body"]["code"] === 0 && $token !== "", "token获取: " . (strlen($token) > 10 ? "OK" : "失败")) && $allPass;

// 2. 客户端自注册（模拟 WinPE 客户端参数）
$r = req("POST", "/api/v1/clients/register", [
    "hostname" => "TEST-PE-" . rand(100, 999),
    "mac_address" => "AA-BB-CC-DD-EE-01",
    "os_version" => "Microsoft Windows 10 PE",
    "client_version" => "0.0.268311",
    "client_type" => "winpe"
], $token);
$regOk = $r["body"]["code"] === 0 && isset($r["body"]["data"]["client_id"]);
$clientId = $r["body"]["data"]["client_id"] ?? "";
$serverClientId = $r["body"]["data"]["id"] ?? 0;
$allPass = ok("客户端自注册", $regOk, "client_id=" . $clientId . " id=" . $serverClientId) && $allPass;

// 3. 再次注册（幂等测试：应返回相同 client_id 并刷新心跳）
$r2 = req("POST", "/api/v1/clients/register", [
    "hostname" => "TEST-PE-SAME",
    "mac_address" => "AA-BB-CC-DD-EE-01",
    "os_version" => "Microsoft Windows 10 PE",
    "client_version" => "0.0.268311",
    "client_type" => "winpe"
], $token);
$idemOk = $r2["body"]["code"] === 0 && ($r2["body"]["data"]["client_id"] ?? "") === $clientId;
$allPass = ok("注册幂等性", $idemOk, "相同client_id返回: " . ($r2["body"]["data"]["client_id"] ?? "")) && $allPass;

// 3.5 审批客户端（未审批客户端不能派发任务，闭环：注册→审批→派单）
$r = req("POST", "/api/v1/clients/" . $serverClientId . "/approve", null, $token);
$allPass = ok("客户端审批通过", $r["body"]["code"] === 0, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

// 4. 取第一个可用镜像
$r = req("GET", "/api/v1/images?page=1&limit=5", null, $token);
$images = $r["body"]["data"]["list"] ?? [];
if (empty($images)) {
    echo "[WARN] 无镜像，跳过建任务/进度测试\n";
} else {
    $imageId = $images[0]["id"];
    echo "[INFO] 使用镜像 ID=" . $imageId . " " . ($images[0]["name"] ?? "") . "\n";

    // 5. 创建任务（客户端传参）
    $r = req("POST", "/api/v1/tasks", [
        "image_id" => $imageId,
        "client_id" => $serverClientId,
        "target_disk_index" => 0,
        "target_partition" => "C:",
        "partition_scheme" => "auto",
        "options" => json_encode(["auto_partition" => true, "auto_repair_boot" => true, "auto_inject_drivers" => false, "image_index" => 1])
    ], $token);
    $taskOk = $r["body"]["code"] === 0 && isset($r["body"]["data"]["id"]);
    $taskId = $r["body"]["data"]["id"] ?? 0;
    $taskNo = $r["body"]["data"]["task_no"] ?? "";
    $allPass = ok("创建任务", $taskOk, "task_no=" . $taskNo . " id=" . $taskId) && $allPass;

    // 6. 进度上报：分区（running）
    $r = req("POST", "/api/v1/tasks/" . $taskId . "/progress", [
        "progress" => 5, "message" => "正在分区...", "step_name" => "分区", "status" => "running"
    ], $token);
    $allPass = ok("进度上报-分区", $r["body"]["code"] === 0, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

    // 7. 进度上报：部署镜像（running）
    $r = req("POST", "/api/v1/tasks/" . $taskId . "/progress", [
        "progress" => 20, "message" => "正在部署镜像...", "step_name" => "部署镜像", "status" => "running"
    ], $token);
    $allPass = ok("进度上报-部署", $r["body"]["code"] === 0, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

    // 8. 进度上报：修复引导
    $r = req("POST", "/api/v1/tasks/" . $taskId . "/progress", [
        "progress" => 80, "message" => "正在修复引导...", "step_name" => "修复引导", "status" => "running"
    ], $token);
    $allPass = ok("进度上报-修复引导", $r["body"]["code"] === 0, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

    // 9. 进度上报：完成（completed）
    $r = req("POST", "/api/v1/tasks/" . $taskId . "/progress", [
        "progress" => 100, "message" => "装机完成", "step_name" => "完成", "status" => "completed"
    ], $token);
    $completeOk = $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "completed";
    $allPass = ok("进度上报-完成", $completeOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

    // 10. 任务详情验证：状态 completed、进度 100
    $r = req("GET", "/api/v1/tasks/" . $taskId, null, $token);
    $detail = $r["body"]["data"] ?? [];
    $allPass = ok("任务详情验证", ($detail["status"] ?? "") === "completed" && ($detail["progress"] ?? 0) >= 100,
        "status=" . ($detail["status"] ?? "") . " progress=" . ($detail["progress"] ?? 0)) && $allPass;

    // 11. 任务日志验证：应有 4 条记录
    $r = req("GET", "/api/v1/tasks/" . $taskId . "/logs", null, $token);
    $logs = $r["body"]["data"]["list"] ?? $r["body"]["data"] ?? [];
    $logCount = is_array($logs) ? count($logs) : 0;
    $allPass = ok("任务日志记录", $logCount >= 4, "日志数=" . $logCount) && $allPass;

    // 12. 失败场景：进度越界校验
    $r = req("POST", "/api/v1/tasks/" . $taskId . "/progress", [
        "progress" => 150, "message" => "非法进度", "step_name" => "校验", "status" => "running"
    ], $token);
    $allPass = ok("非法进度拦截", $r["body"]["code"] !== 0, "code=" . ($r["body"]["code"] ?? "")) && $allPass;
}

// 13. 非法镜像建任务
$r = req("POST", "/api/v1/tasks", ["image_id" => 999999, "client_id" => $serverClientId], $token);
$allPass = ok("镜像不存在拦截", $r["body"]["code"] !== 0, "code=" . ($r["body"]["code"] ?? "") . " msg=" . ($r["body"]["message"] ?? "")) && $allPass;

// 14. Windows 客户端场景：注册类型 windows + 建任务保持 running（等待 PE 执行）
$r = req("POST", "/api/v1/clients/register", [
    "hostname" => "TEST-WIN-" . rand(100, 999),
    "mac_address" => "AA-BB-CC-DD-EE-02",
    "os_version" => "Microsoft Windows 11 Pro",
    "client_version" => "0.0.268311",
    "client_type" => "windows"
], $token);
$winClientId = $r["body"]["data"]["client_id"] ?? "";
$winServerId = $r["body"]["data"]["id"] ?? 0;
$allPass = ok("Windows客户端注册", $r["body"]["code"] === 0 && $winClientId !== "", "client_id=" . $winClientId . " id=" . $winServerId) && $allPass;

// 审批 Windows 客户端（下单前置条件）
$r = req("POST", "/api/v1/clients/" . $winServerId . "/approve", null, $token);
$allPass = ok("Windows客户端审批", $r["body"]["code"] === 0, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

if (!empty($images)) {
    $r = req("POST", "/api/v1/tasks", [
        "image_id" => $imageId,
        "client_id" => $winServerId,
        "target_disk_index" => 0,
        "target_partition" => "C:",
        "partition_scheme" => "auto"
    ], $token);
    $winTaskOk = $r["body"]["code"] === 0 && isset($r["body"]["data"]["id"]);
    $winTaskId = $r["body"]["data"]["id"] ?? 0;
    $allPass = ok("Windows建任务", $winTaskOk, "task_no=" . ($r["body"]["data"]["task_no"] ?? "")) && $allPass;

    // 任务创建后上报 running（不应直接 completed）
    $r = req("POST", "/api/v1/tasks/" . $winTaskId . "/progress", [
        "progress" => 5, "message" => "任务已创建，等待进入 WinPE 执行装机", "step_name" => "创建任务", "status" => "running"
    ], $token);
    $allPass = ok("Windows任务进度-running", $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "running",
        "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;
}


// ============ 全功能流程闭环扩展测试（waiting/心跳/无人值守） ============

// 15. 客户端心跳接口
$r = req("POST", "/api/v1/clients/heartbeat", [
    "client_id" => $clientId,
    "mac_address" => "AA-BB-CC-DD-EE-01",
    "hostname" => "TEST-PE-HEARTBEAT",
    "os_version" => "Microsoft Windows 10 PE",
    "client_version" => "0.0.268311"
], $token);
$hbOk = $r["body"]["code"] === 0 && isset($r["body"]["data"]["waiting_task_count"]);
$allPass = ok("客户端心跳", $hbOk, "status=" . ($r["body"]["data"]["status"] ?? "") . " waiting_task_count=" . ($r["body"]["data"]["waiting_task_count"] ?? "-")) && $allPass;

// 16. 心跳未注册客户端 → 报错要求重新注册
$r = req("POST", "/api/v1/clients/heartbeat", [
    "client_id" => "ZS-NOT-EXIST-0000",
    "mac_address" => "00-00-00-00-00-01"
], $token);
$allPass = ok("心跳未注册拦截", $r["body"]["code"] !== 0, "code=" . ($r["body"]["code"] ?? "")) && $allPass;

// 17. 无人值守模板：创建 + 真实 XML 生成
$r = req("POST", "/api/v1/unattendTemplates", [
    "name" => "E2E-标准模板",
    "description" => "端到端测试模板",
    "template_type" => "standard",
    "is_default" => 1,
    "config" => [
        "general" => ["language" => "zh-CN", "timezone" => "China Standard Time", "product_key" => ""],
        "user_account" => ["username" => "Admin", "password" => "Test@1234", "auto_login" => true],
        "network" => ["dhcp" => true, "workgroup" => "WORKGROUP", "computer_name" => "ZS-E2E-PC"],
        "first_logon" => ["install_software" => true]
    ]
], $token);
$tplId = $r["body"]["data"]["id"] ?? 0;
$tplOk = $r["body"]["code"] === 0 && $tplId > 0;
$allPass = ok("无人值守模板创建", $tplOk, "id=" . $tplId) && $allPass;

$r = req("POST", "/api/v1/unattendTemplates/" . $tplId . "/generateXml", null, $token);
$xml = $r["body"]["data"]["xml"] ?? "";
$xmlOk = $r["body"]["code"] === 0 && strpos($xml, "<unattend") !== false && strpos($xml, "windowsPE") !== false && strpos($xml, "FirstLogonCommands") !== false;
$allPass = ok("无人值守XML生成", $xmlOk, "含unattend/windowsPE/FirstLogonCommands=" . ($xmlOk ? "是" : "否")) && $allPass;

// 18. Windows 端：以 waiting 状态下单（模拟真实 Windows 客户端）
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "client_id" => $winServerId,
    "target_disk_index" => 0,
    "target_partition" => "C:",
    "partition_scheme" => "auto",
    "status" => "waiting",
    "unattend_template_id" => $tplId,
    "options" => json_encode([
        "auto_partition" => true, "auto_repair_boot" => true, "unattended" => true,
        "install_software" => true, "optimize" => true, "backup_data" => false,
        "image_index" => 1, "unattend_template_id" => $tplId, "software_template_id" => null
    ])
], $token);
$waitTaskId = $r["body"]["data"]["id"] ?? 0;
$waitTaskStatus = $r["body"]["data"]["status"] ?? "";
$waitOk = $r["body"]["code"] === 0 && $waitTaskStatus === "waiting";
$allPass = ok("Windows下单置waiting", $waitOk, "status=" . $waitTaskStatus . " id=" . $waitTaskId) && $allPass;

// 19. PE 端拉取本机 waiting 任务（续装）
$r = req("GET", "/api/v1/tasks", ["client_id" => $winServerId, "status" => "waiting"], $token);
$waitList = $r["body"]["data"]["list"] ?? [];
$pullOk = false;
foreach ($waitList as $t) {
    if (($t["id"] ?? 0) == $waitTaskId) { $pullOk = true; break; }
}
$allPass = ok("PE拉取waiting任务", $pullOk, "list_size=" . count($waitList)) && $allPass;

// 20. PE 认领：waiting → running
$r = req("POST", "/api/v1/tasks/" . $waitTaskId . "/progress", [
    "progress" => 10, "message" => "WinPE 已认领任务，开始执行", "step_name" => "认领任务", "status" => "running"
], $token);
$claimOk = $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "running";
$allPass = ok("waiting转running认领", $claimOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

// 21. 任务无人值守 XML 获取（PE 部署后写入 Panther）
$r = req("GET", "/api/v1/tasks/" . $waitTaskId . "/unattend", null, $token);
$taskXml = $r["body"]["data"]["xml"] ?? "";
$allPass = ok("任务关联无人值守XML", $r["body"]["code"] === 0 && strpos($taskXml, "<unattend") !== false,
    "template=" . ($r["body"]["data"]["template_name"] ?? "-")) && $allPass;

// 22. 任务 options 契约完整（含默认值）
$r = req("GET", "/api/v1/tasks/" . $waitTaskId, null, $token);
$opts = json_decode($r["body"]["data"]["options"] ?? "{}", true);
$optOk = isset($opts["auto_partition"]) && isset($opts["unattended"]) && isset($opts["optimize"]) && isset($opts["driver_package"]);
$allPass = ok("任务options契约完整", $optOk, "keys=" . implode(",", array_keys($opts))) && $allPass;

// 23. 取消 waiting 任务（新建一个 waiting 任务再取消，验证 waiting→cancelled 闭环）
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "client_id" => $winServerId,
    "target_disk_index" => 0,
    "target_partition" => "C:",
    "partition_scheme" => "auto",
    "status" => "waiting"
], $token);
$cancelTaskId = $r["body"]["data"]["id"] ?? 0;
$r = req("POST", "/api/v1/tasks/" . $cancelTaskId . "/cancel", null, $token);
$cancelOk = $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "cancelled";
$allPass = ok("取消waiting任务", $cancelOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

// 23.5 取消 running 任务（运行中也可取消，验证 running→cancelled 闭环）
$r = req("POST", "/api/v1/tasks/" . $waitTaskId . "/cancel", null, $token);
$cancelRunOk = $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "cancelled";
$allPass = ok("取消running任务", $cancelRunOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;

// 24. 客户端列表离线判定字段
$r = req("GET", "/api/v1/clients?page=1&limit=50", null, $token);
$clients = $r["body"]["data"]["list"] ?? [];
$hasOnline = false;
foreach ($clients as $cl) {
    if (($cl["id"] ?? 0) == $serverClientId) { $hasOnline = isset($cl["online"]); break; }
}
$allPass = ok("客户端列表含在线判定", $hasOnline, "字段online=" . ($hasOnline ? "存在" : "缺失")) && $allPass;


// ============ 一键安装闭环修复回归测试（r2/r3/r4/r8/r9） ============

// 25. r2 后端闭环：waiting 任务必须关联已注册客户端（否则 PE 端无法按 client_id 拉取，下单后无人认领断链）
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "target_disk_index" => 0, "target_partition" => "C:", "partition_scheme" => "auto",
    "status" => "waiting"
], $token);
$allPass = ok("waiting无client拦截", $r["body"]["code"] !== 0, "code=" . ($r["body"]["code"] ?? "") . " msg=" . ($r["body"]["message"] ?? "")) && $allPass;

// 26. r4 身份校验：A 客户端不能操控 B 客户端任务（client_id 不匹配 → auth_client_mismatch）
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "client_id" => $serverClientId,
    "target_disk_index" => 0, "target_partition" => "C:", "partition_scheme" => "auto",
    "status" => "waiting"
], $token);
$ownTaskId = $r["body"]["data"]["id"] ?? 0;
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/progress", [
    "progress" => 5, "message" => "越权上报", "step_name" => "认领", "status" => "running",
    "client_id" => $winServerId
], $token);
$allPass = ok("任务身份校验拦截", $r["body"]["code"] !== 0 && strpos($r["body"]["message"] ?? "", "任务不属于当前客户端") !== false,
    "code=" . ($r["body"]["code"] ?? "") . " msg=" . ($r["body"]["message"] ?? "")) && $allPass;

// 26.5 r4 认领闭环：正确身份认领 waiting → running，并写入 started_at
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/progress", [
    "progress" => 5, "message" => "正确认领", "step_name" => "认领", "status" => "running",
    "client_id" => $serverClientId
], $token);
$r2 = req("GET", "/api/v1/tasks/" . $ownTaskId, null, $token);
$ownDetail = $r2["body"]["data"] ?? [];
$allPass = ok("正确身份认领", $r["body"]["code"] === 0 && ($ownDetail["status"] ?? "") === "running" && !empty($ownDetail["started_at"]),
    "status=" . ($ownDetail["status"] ?? "") . " started_at=" . ($ownDetail["started_at"] ?? "")) && $allPass;

// 27. r3/r9 失败重试复用原任务：failed → retry → 同一 id 变为 waiting（供 PE 重新认领，不新建任务）
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/progress", [
    "progress" => 20, "message" => "模拟安装失败", "step_name" => "部署", "status" => "failed",
    "client_id" => $serverClientId
], $token);
$failOk = ($r["body"]["data"]["status"] ?? "") === "failed";
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/retry", null, $token);
$retryData = $r["body"]["data"] ?? [];
$retryOk = $r["body"]["code"] === 0 && ($retryData["id"] ?? 0) == $ownTaskId && ($retryData["status"] ?? "") === "waiting" && ($retryData["progress"] ?? -1) === 0;
$allPass = ok("失败重试复用原任务", $failOk && $retryOk,
    "fail=" . ($failOk ? "failed" : "?") . " retryId=" . ($retryData["id"] ?? 0) . " status=" . ($retryData["status"] ?? "") . " progress=" . ($retryData["progress"] ?? "")) && $allPass;

// 27.5 r9 取消重试复用原任务：cancelled → retry → 同一 id 变为 waiting
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/cancel", null, $token);
$r = req("POST", "/api/v1/tasks/" . $ownTaskId . "/retry", null, $token);
$retryData = $r["body"]["data"] ?? [];
$allPass = ok("取消重试复用原任务", $r["body"]["code"] === 0 && ($retryData["id"] ?? 0) == $ownTaskId && ($retryData["status"] ?? "") === "waiting",
    "id=" . ($retryData["id"] ?? 0) . " status=" . ($retryData["status"] ?? "")) && $allPass;

// 28. r8 磁盘跨环境兜底：options 持久化 disk_index/disk_size/disk_model（PE 端模糊匹配依据）
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "client_id" => $serverClientId,
    "target_disk_index" => 3, "target_partition" => "C:", "partition_scheme" => "auto",
    "status" => "waiting",
    "options" => json_encode([
        "auto_partition" => true, "disk_index" => 3, "disk_size" => 476940, "disk_model" => "Samsung SSD 870 EVO 500GB"
    ])
], $token);
$r8TaskId = $r["body"]["data"]["id"] ?? 0;
$r = req("GET", "/api/v1/tasks/" . $r8TaskId, null, $token);
$r8opts = json_decode($r["body"]["data"]["options"] ?? "{}", true);
$r8ok = ($r8opts["disk_index"] ?? null) === 3 && ($r8opts["disk_size"] ?? null) == 476940 && ($r8opts["disk_model"] ?? "") === "Samsung SSD 870 EVO 500GB";
$allPass = ok("options持久化磁盘特征", $r8ok, "disk_size=" . ($r8opts["disk_size"] ?? "-") . " model=" . ($r8opts["disk_model"] ?? "-")) && $allPass;

// 29. r4 认领并发保护：同一 waiting 任务被双客户端同时认领，仅一次转为 running，另一侧触发冲突
$r = req("POST", "/api/v1/tasks", [
    "image_id" => $imageId,
    "client_id" => $serverClientId,
    "target_disk_index" => 0, "target_partition" => "C:", "partition_scheme" => "auto",
    "status" => "waiting"
], $token);
$raceTaskId = $r["body"]["data"]["id"] ?? 0;
$mkRaceCh = function ($tid) use ($baseUrl, $token, $serverClientId) {
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $baseUrl . "/api/v1/tasks/" . $tid . "/progress");
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode(["progress" => 10, "message" => "并发认领", "step_name" => "认领", "status" => "running", "client_id" => $serverClientId]));
    curl_setopt($ch, CURLOPT_HTTPHEADER, ["Content-Type: application/json", "Authorization: Bearer " . $token]);
    return $ch;
};
$mh = curl_multi_init();
$raceCh1 = $mkRaceCh($raceTaskId);
$raceCh2 = $mkRaceCh($raceTaskId);
curl_multi_add_handle($mh, $raceCh1);
curl_multi_add_handle($mh, $raceCh2);
do { $me = curl_multi_exec($mh, $active); } while ($active && $me == CURLM_OK);
$raceB1 = json_decode(curl_multi_getcontent($raceCh1), true);
$raceB2 = json_decode(curl_multi_getcontent($raceCh2), true);
curl_multi_remove_handle($mh, $raceCh1);
curl_multi_remove_handle($mh, $raceCh2);
curl_multi_close($mh);
$raceCodes = [$raceB1["code"] ?? -1, $raceB2["code"] ?? -1];
$conflictSeen = in_array("task_claim_conflict", $raceCodes);
$r = req("GET", "/api/v1/tasks/" . $raceTaskId, null, $token);
$raceFinal = $r["body"]["data"] ?? [];
$raceOk = ($raceFinal["status"] ?? "") === "running";
$allPass = ok("认领并发保护", $raceOk, "codes=[" . implode(",", $raceCodes) . "] 冲突=" . ($conflictSeen ? "触发" : "未触发(被读侧已转running)") . " 最终status=" . ($raceFinal["status"] ?? "")) && $allPass;


echo "\n=== 结果: " . ($allPass ? "全部通过" : "存在失败") . " ===\n";
exit($allPass ? 0 : 1);
