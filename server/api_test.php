<?php
$baseUrl = "http://127.0.0.1:8001";
$token = "";
$results = [];
$passed = 0;
$failed = 0;

function test($name, $callback) {
    global $results, $passed, $failed;
    try {
        $result = $callback();
        $isPass = ($result["status"] === "pass");
        if ($isPass) { $passed++; } else { $failed++; }
        $results[] = ["name" => $name, "status" => $isPass ? "PASS" : "FAIL", "detail" => $result["detail"] ?? ""];
        if (!$isPass) { echo "  FAIL: $name - " . ($result["detail"] ?? "") . "\n"; }
    } catch (Exception $e) {
        $failed++;
        $results[] = ["name" => $name, "status" => "FAIL", "detail" => "Exception: " . $e->getMessage()];
        echo "  FAIL: $name - Exception: " . $e->getMessage() . "\n";
    }
}

function httpRequest($method, $path, $data = null, $customToken = null) {
    global $baseUrl, $token;
    $url = $baseUrl . $path;
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);
    $headers = [];
    $useToken = $customToken !== null ? $customToken : $token;
    if ($useToken) { $headers[] = "Authorization: Bearer " . $useToken; }
    if ($method === "POST" || $method === "PUT" || $method === "DELETE") {
        if ($method === "POST") { curl_setopt($ch, CURLOPT_POST, true); }
        elseif ($method === "PUT") { curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "PUT"); }
        elseif ($method === "DELETE") { curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "DELETE"); }
        if ($data !== null) {
            $postData = http_build_query($data);
            curl_setopt($ch, CURLOPT_POSTFIELDS, $postData);
            $headers[] = "Content-Type: application/x-www-form-urlencoded";
        }
    } elseif ($method === "GET" && $data !== null) {
        $url .= "?" . http_build_query($data);
        curl_setopt($ch, CURLOPT_URL, $url);
    }
    curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
    $response = curl_exec($ch);
    $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $error = curl_error($ch);
    curl_close($ch);
    return ["http_code" => $httpCode, "body" => $response, "error" => $error];
}

function parseResponse($resp) {
    if ($resp["error"]) { return ["code" => -1, "message" => $resp["error"], "http_code" => $resp["http_code"]]; }
    $body = json_decode($resp["body"], true);
    if (!$body) { return ["code" => -2, "message" => "Invalid JSON: " . substr($resp["body"], 0, 200), "http_code" => $resp["http_code"]]; }
    $body["http_code"] = $resp["http_code"];
    return $body;
}

function assertSuccess($resp, $msg) {
    $parsed = parseResponse($resp);
    $isSuccess = isset($parsed["code"]) && $parsed["code"] === 0;
    return ["status" => $isSuccess ? "pass" : "fail", "detail" => $msg . " - Got: " . ($parsed["message"] ?? "unknown") . " (code=" . ($parsed["code"] ?? "N/A") . ")"];
}

echo "=== ZS Installer API 全面测试 ===\n\n";

echo "--- 1. 认证模块 ---\n";
test("登录 - 正确凭据", function() {
    global $token;
    $resp = httpRequest("POST", "/api/v1/auth/login", ["username" => "admin", "password" => "admin123"]);
    $parsed = parseResponse($resp);
    if (isset($parsed["data"]["token"])) { $token = $parsed["data"]["token"]; }
    return assertSuccess($resp, "登录成功");
});

test("登录 - 空用户名", function() {
    $resp = httpRequest("POST", "/api/v1/auth/login", ["username" => "", "password" => "test"]);
    $parsed = parseResponse($resp);
    return ["status" => ($parsed["code"] === 1001) ? "pass" : "fail", "detail" => "空用户名应返回1001, 实际: " . ($parsed["code"] ?? "N/A")];
});

test("登录 - 错误密码", function() {
    $resp = httpRequest("POST", "/api/v1/auth/login", ["username" => "admin", "password" => "wrong"]);
    $parsed = parseResponse($resp);
    return ["status" => ($parsed["code"] !== 0) ? "pass" : "fail", "detail" => "错误密码应返回非0, 实际: " . ($parsed["code"] ?? "N/A")];
});

test("获取个人信息", function() {
    $resp = httpRequest("GET", "/api/v1/auth/profile");
    $parsed = parseResponse($resp);
    $hasUser = isset($parsed["data"]["username"]) && $parsed["data"]["username"] === "admin";
    return ["status" => $hasUser ? "pass" : "fail", "detail" => "用户名=admin: " . ($hasUser ? "匹配" : "不匹配")];
});

test("无Token访问", function() {
    $resp = httpRequest("GET", "/api/v1/dashboard", null, "");
    $parsed = parseResponse($resp);
    return ["status" => ($parsed["code"] === 3004) ? "pass" : "fail", "detail" => "无Token应返回3004, 实际: " . ($parsed["code"] ?? "N/A")];
});

echo "\n--- 2. 仪表盘 ---\n";
test("仪表盘数据", function() {
    $resp = httpRequest("GET", "/api/v1/dashboard");
    $parsed = parseResponse($resp);
    $hasData = isset($parsed["data"]) && is_array($parsed["data"]);
    return ["status" => $hasData ? "pass" : "fail", "detail" => $hasData ? "数据返回正常" : "缺少data字段"];
});

echo "\n--- 3. 镜像管理 ---\n";
test("镜像列表", function() {
    $resp = httpRequest("GET", "/api/v1/images");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

test("创建镜像 - 缺少必填字段", function() {
    $resp = httpRequest("POST", "/api/v1/images", ["name" => ""]);
    $parsed = parseResponse($resp);
    return ["status" => ($parsed["code"] === 1001) ? "pass" : "fail", "detail" => "缺少必填字段应返回1001, 实际: " . ($parsed["code"] ?? "N/A")];
});

$GLOBALS["testImageId"] = 0;
test("创建镜像 - 正常创建", function() {
    $resp = httpRequest("POST", "/api/v1/images", ["name" => "测试镜像", "filename" => "test.wim", "format" => "wim", "file_size" => 1024, "os_type" => "Windows 10", "os_version" => "22H2", "os_arch" => "x64", "os_edition" => "专业版", "description" => "API测试创建"]);
    $parsed = parseResponse($resp);
    if ($parsed["code"] === 0 && isset($parsed["data"]["id"])) { $GLOBALS["testImageId"] = $parsed["data"]["id"]; }
    return assertSuccess($resp, "创建镜像");
});

test("获取镜像详情", function() {
    $id = $GLOBALS["testImageId"] ?? 0;
    if (!$id) { return ["status" => "fail", "detail" => "没有测试镜像ID"]; }
    $resp = httpRequest("GET", "/api/v1/images/" . $id);
    return assertSuccess($resp, "获取镜像详情");
});

test("获取不存在的镜像详情", function() {
    $resp = httpRequest("GET", "/api/v1/images/99999");
    $parsed = parseResponse($resp);
    return ["status" => ($parsed["code"] !== 0) ? "pass" : "fail", "detail" => "不存在的镜像应返回非0, 实际code=" . ($parsed["code"] ?? "N/A")];
});

test("删除镜像", function() {
    $id = $GLOBALS["testImageId"] ?? 0;
    if (!$id) { return ["status" => "pass", "detail" => "没有测试镜像需要删除"]; }
    $resp = httpRequest("DELETE", "/api/v1/images/" . $id);
    return assertSuccess($resp, "删除镜像");
});

echo "\n--- 4. 客户端管理 ---\n";
test("客户端列表", function() {
    $resp = httpRequest("GET", "/api/v1/clients");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

$GLOBALS["testVersionId"] = 0;
test("创建客户端版本", function() {
    $resp = httpRequest("POST", "/api/v1/clientVersions", ["version" => "1.0.0-test", "description" => "API测试版本", "file_url" => "http://test.com/client.exe", "file_size" => 1024000, "md5" => md5("test"), "force_update" => 0, "status" => "enabled"]);
    $parsed = parseResponse($resp);
    if ($parsed["code"] === 0 && isset($parsed["data"]["id"])) { $GLOBALS["testVersionId"] = $parsed["data"]["id"]; }
    return assertSuccess($resp, "创建客户端版本");
});

echo "\n--- 5. 工单管理 ---\n";
test("工单列表", function() {
    $resp = httpRequest("GET", "/api/v1/workOrders");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

$GLOBALS["testWorkOrderId"] = 0;
test("创建工单", function() {
    $resp = httpRequest("POST", "/api/v1/workOrders", ["title" => "API测试工单", "device_type" => "PC", "device_model" => "Dell OptiPlex", "priority" => "normal", "customer_name" => "测试客户", "customer_phone" => "13800138001"]);
    $parsed = parseResponse($resp);
    if ($parsed["code"] === 0 && isset($parsed["data"]["id"])) { $GLOBALS["testWorkOrderId"] = $parsed["data"]["id"]; }
    return assertSuccess($resp, "创建工单");
});

echo "\n--- 6. 客户管理 ---\n";
test("客户列表", function() {
    $resp = httpRequest("GET", "/api/v1/customers");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

$GLOBALS["testCustomerId"] = 0;
test("创建客户", function() {
    $resp = httpRequest("POST", "/api/v1/customers", ["name" => "API测试客户", "phone" => "13900139000", "company" => "测试公司", "source" => "api_test", "status" => "enabled"]);
    $parsed = parseResponse($resp);
    if ($parsed["code"] === 0 && isset($parsed["data"]["id"])) { $GLOBALS["testCustomerId"] = $parsed["data"]["id"]; }
    return assertSuccess($resp, "创建客户");
});

echo "\n--- 7. 日志管理 ---\n";
test("日志列表", function() {
    $resp = httpRequest("GET", "/api/v1/logs");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

test("日志类型统计", function() {
    $resp = httpRequest("GET", "/api/v1/logTypes");
    $parsed = parseResponse($resp);
    $hasData = isset($parsed["data"]) && is_array($parsed["data"]);
    return ["status" => $hasData ? "pass" : "fail", "detail" => $hasData ? "类型统计返回正常，共" . count($parsed["data"]) . "项" : "缺少data字段"];
});

echo "\n--- 8. 报表 ---\n";
test("安装报表", function() {
    $resp = httpRequest("GET", "/api/v1/reports/install");
    $parsed = parseResponse($resp);
    $hasData = isset($parsed["data"]) && is_array($parsed["data"]);
    return ["status" => $hasData ? "pass" : "fail", "detail" => $hasData ? "报表返回正常" : "缺少data字段"];
});

test("工单报表", function() {
    $resp = httpRequest("GET", "/api/v1/reports/workOrder");
    $parsed = parseResponse($resp);
    $hasData = isset($parsed["data"]) && is_array($parsed["data"]);
    return ["status" => $hasData ? "pass" : "fail", "detail" => $hasData ? "报表返回正常" : "缺少data字段"];
});

echo "\n--- 9. 系统设置 ---\n";
test("获取设置列表", function() {
    $resp = httpRequest("GET", "/api/v1/settings");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "设置返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 10. 通知 ---\n";
test("通知列表", function() {
    $resp = httpRequest("GET", "/api/v1/notifications");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

test("未读通知数", function() {
    $resp = httpRequest("GET", "/api/v1/notifications/unread_count");
    $parsed = parseResponse($resp);
    $hasCount = isset($parsed["data"]) && (isset($parsed["data"]["count"]) || isset($parsed["data"]["unread_count"]));
    return ["status" => $hasCount ? "pass" : "fail", "detail" => $hasCount ? "未读计数返回正常" : "缺少count字段"];
});

echo "\n--- 11. 软件管理 ---\n";
test("软件列表", function() {
    $resp = httpRequest("GET", "/api/v1/software");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

$GLOBALS["testSoftwareCategoryId"] = 0;
test("创建软件分类", function() {
    $resp = httpRequest("POST", "/api/v1/softwareCategories", ["name" => "API测试分类", "sort" => 1, "status" => "enabled"]);
    $parsed = parseResponse($resp);
    if ($parsed["code"] === 0 && isset($parsed["data"]["id"])) { $GLOBALS["testSoftwareCategoryId"] = $parsed["data"]["id"]; }
    return assertSuccess($resp, "创建软件分类");
});

echo "\n--- 12. 驱动管理 ---\n";
test("驱动列表", function() {
    $resp = httpRequest("GET", "/api/v1/drivers");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 13. 脚本管理 ---\n";
test("脚本列表", function() {
    $resp = httpRequest("GET", "/api/v1/scripts");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 14. PE管理 ---\n";
test("PE版本列表", function() {
    $resp = httpRequest("GET", "/api/v1/peVersions");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

test("PE定制列表", function() {
    $resp = httpRequest("GET", "/api/v1/peCustomize");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 15. PXE配置 ---\n";
test("PXE配置列表", function() {
    $resp = httpRequest("GET", "/api/v1/pxeConfigs");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 16. 网络部署 ---\n";
test("网络部署列表", function() {
    $resp = httpRequest("GET", "/api/v1/networkDeploys");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 17. 定时任务 ---\n";
test("定时任务列表", function() {
    $resp = httpRequest("GET", "/api/v1/scheduledTasks");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 18. 回收站 ---\n";
test("回收站列表", function() {
    $resp = httpRequest("GET", "/api/v1/recycleBin");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 19. Webhook日志 ---\n";
test("Webhook日志列表", function() {
    $resp = httpRequest("GET", "/api/v1/webhookLogs");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 20. 用户管理 ---\n";
test("用户列表", function() {
    $resp = httpRequest("GET", "/api/v1/users");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    $noPassword = true;
    if ($hasList && count($parsed["data"]["list"]) > 0) { $noPassword = !isset($parsed["data"]["list"][0]["password"]); }
    return ["status" => ($hasList && $noPassword) ? "pass" : "fail", "detail" => ($hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段") . ($noPassword ? "，密码字段已隐藏" : "，密码字段未隐藏！")];
});

test("用户详情", function() {
    $resp = httpRequest("GET", "/api/v1/users/2");
    $parsed = parseResponse($resp);
    $hasUser = isset($parsed["data"]["username"]);
    $noPassword = !isset($parsed["data"]["password"]);
    return ["status" => ($hasUser && $noPassword) ? "pass" : "fail", "detail" => ($hasUser ? "用户信息返回正常" : "缺少用户信息") . ($noPassword ? "，密码字段已隐藏" : "，密码字段未隐藏！")];
});

echo "\n--- 21. 镜像源管理 ---\n";
test("镜像源列表", function() {
    $resp = httpRequest("GET", "/api/v1/imageSources");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 22. 镜像标签 ---\n";
test("镜像标签列表", function() {
    $resp = httpRequest("GET", "/api/v1/imageTags");
    $parsed = parseResponse($resp);
    $hasList = isset($parsed["data"]["list"]) && is_array($parsed["data"]["list"]);
    return ["status" => $hasList ? "pass" : "fail", "detail" => $hasList ? "列表返回正常，共" . count($parsed["data"]["list"]) . "条" : "缺少list字段"];
});

echo "\n--- 23. 清理测试数据 ---\n";
test("删除测试客户端版本", function() {
    $id = $GLOBALS["testVersionId"] ?? 0;
    if (!$id) { return ["status" => "pass", "detail" => "没有测试版本需要删除"]; }
    $resp = httpRequest("DELETE", "/api/v1/clientVersions/" . $id);
    return assertSuccess($resp, "删除测试客户端版本");
});

test("删除测试工单", function() {
    $id = $GLOBALS["testWorkOrderId"] ?? 0;
    if (!$id) { return ["status" => "pass", "detail" => "没有测试工单需要删除"]; }
    $resp = httpRequest("DELETE", "/api/v1/workOrders/" . $id);
    return assertSuccess($resp, "删除测试工单");
});

test("删除测试客户", function() {
    $id = $GLOBALS["testCustomerId"] ?? 0;
    if (!$id) { return ["status" => "pass", "detail" => "没有测试客户需要删除"]; }
    $resp = httpRequest("DELETE", "/api/v1/customers/" . $id);
    return assertSuccess($resp, "删除测试客户");
});

test("删除测试软件分类", function() {
    $id = $GLOBALS["testSoftwareCategoryId"] ?? 0;
    if (!$id) { return ["status" => "pass", "detail" => "没有测试分类需要删除"]; }
    $resp = httpRequest("DELETE", "/api/v1/softwareCategories/" . $id);
    return assertSuccess($resp, "删除测试软件分类");
});

echo "\n\n=== 测试结果汇总 ===\n";
echo "总计: " . count($results) . " | 通过: $passed | 失败: $failed\n\n";
foreach ($results as $r) {
    $icon = $r["status"] === "PASS" ? chr(0xE2) . chr(0x9C) . chr(0x93) : chr(0xE2) . chr(0x9C) . chr(0x97);
    echo "$icon {$r["name"]}: {$r["status"]}";
    if ($r["status"] === "FAIL") { echo " - {$r["detail"]}"; }
    echo "\n";
}
echo "\n=== 测试完成 ===\n";
exit($failed > 0 ? 1 : 0);

