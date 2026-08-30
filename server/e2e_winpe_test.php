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

echo "\n=== 结果: " . ($allPass ? "全部通过" : "存在失败") . " ===\n";
exit($allPass ? 0 : 1);
