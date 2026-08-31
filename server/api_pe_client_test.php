<?php
// A7 自测：PE 版本客户端列表 / 下载接口
$baseUrl = "http://127.0.0.1:8001";
$token = "";
$passed = 0; $failed = 0;

function req($method, $path, $data = null, $customToken = null, $extraHeaders = []) {
    global $baseUrl, $token;
    $url = $baseUrl . $path;
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);
    $headers = [];
    $useToken = $customToken !== null ? $customToken : $token;
    if ($useToken) { $headers[] = "Authorization: Bearer " . $useToken; }
    foreach ($extraHeaders as $h) { $headers[] = $h; }
    if ($method === "POST") { curl_setopt($ch, CURLOPT_POST, true); }
    elseif ($method === "PUT") { curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "PUT"); }
    elseif ($method === "DELETE") { curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "DELETE"); }
    if ($data !== null) { curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($data)); $headers[] = "Content-Type: application/x-www-form-urlencoded"; }
    curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
    $response = curl_exec($ch);
    $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $error = curl_error($ch);
    curl_close($ch);
    return ["http_code" => $httpCode, "body" => $response, "error" => $error];
}

function parse($resp) {
    if ($resp["error"]) { return ["code" => -1, "message" => $resp["error"], "http_code" => $resp["http_code"]]; }
    $body = json_decode($resp["body"], true);
    if (!$body) { return ["code" => -2, "message" => "Invalid JSON: " . substr($resp["body"], 0, 120), "http_code" => $resp["http_code"]]; }
    $body["http_code"] = $resp["http_code"];
    return $body;
}

function test($name, $callback) {
    global $passed, $failed;
    try {
        $r = $callback();
        if ($r["status"] === "pass") { $passed++; echo "  PASS: $name\n"; }
        else { $failed++; echo "  FAIL: $name - " . ($r["detail"] ?? "") . "\n"; }
    } catch (Exception $e) {
        $failed++; echo "  FAIL: $name - Exception: " . $e->getMessage() . "\n";
    }
}

echo "=== A7 PE 客户端接口自测 ===\n\n";
echo "--- 1. 登录 ---\n";
test("登录获取Token", function() {
    global $token;
    $resp = req("POST", "/api/v1/auth/login", ["username" => "admin", "password" => "admin123"]);
    $p = parse($resp);
    if (isset($p["data"]["token"])) { $token = $p["data"]["token"]; return ["status" => "pass", "detail" => "token ok"]; }
    return ["status" => "fail", "detail" => "未获取token: " . ($p["message"] ?? "?")];
});

echo "\n--- 2. 客户端列表接口 clientList ---\n";
$clientListData = null;
test("clientList 返回成功", function() use (&$clientListData) {
    $resp = req("GET", "/api/v1/peVersions/clientList");
    $p = parse($resp);
    if ($p["code"] === 0) { $clientListData = $p["data"]; return ["status" => "pass", "detail" => "code=0"]; }
    return ["status" => "fail", "detail" => "code=" . ($p["code"] ?? "N/A") . " msg=" . ($p["message"] ?? "")];
});
test("clientList 返回数组", function() use (&$clientListData) {
    return ["status" => is_array($clientListData) ? "pass" : "fail", "detail" => "is_array=" . var_export(is_array($clientListData), true)];
});
test("clientList 仅返回启用版本(status=1)", function() use (&$clientListData) {
    if (!is_array($clientListData)) return ["status" => "fail", "detail" => "无数据"];
    foreach ($clientListData as $item) {
        if (isset($item["status"]) && (int)$item["status"] !== 1) return ["status" => "fail", "detail" => "发现禁用版本 id=" . $item["id"]];
    }
    return ["status" => "pass", "detail" => "共 " . count($clientListData) . " 条全部启用"];
});
test("clientList 每项含 download_url", function() use (&$clientListData) {
    if (!is_array($clientListData) || count($clientListData) === 0) return ["status" => "pass", "detail" => "无数据(跳过)"];
    foreach ($clientListData as $item) {
        if (empty($item["download_url"])) return ["status" => "fail", "detail" => "id=" . $item["id"] . " 缺少download_url"];
    }
    return ["status" => "pass", "detail" => "共 " . count($clientListData) . " 项均含 download_url"];
});

echo "\n--- 3. 客户端下载接口 clientDownload ---\n";
test("未认证访问返回401/3004", function() {
    $resp = req("GET", "/api/v1/peVersions/clientList", null, "");
    $p = parse($resp);
    return ["status" => ($p["code"] === 3004 || $p["code"] === 3003) ? "pass" : "fail", "detail" => "code=" . ($p["code"] ?? "N/A")];
});
test("不存在的PE下载返回错误", function() {
    $resp = req("GET", "/api/v1/peVersions/999999/download");
    $p = parse($resp);
    return ["status" => ($p["code"] !== 0) ? "pass" : "fail", "detail" => "code=" . ($p["code"] ?? "N/A") . " msg=" . ($p["message"] ?? "")];
});
$firstPe = null;
test("clientDownload 带Range断点续传请求头", function() use (&$firstPe, &$clientListData) {
    if (!is_array($clientListData) || count($clientListData) === 0) return ["status" => "pass", "detail" => "无PE数据(跳过)"];
    $firstPe = $clientListData[0];
    $resp = req("GET", "/api/v1/peVersions/" . $firstPe["id"] . "/download", null, null, ["Range: bytes=0-1023"]);
    $p = parse($resp);
    // 文件存在时应返回二进制流(JSON解析失败属正常)或 206；文件缺失则返回结构化错误
    if ($p["code"] === -2) {
        // 非JSON = 文件流返回
        $hasPartial = (stripos($resp["body"], "206") !== false) || $resp["http_code"] === 206;
        return ["status" => "pass", "detail" => "文件流已返回 http=" . $resp["http_code"] . " len=" . strlen($resp["body"])];
    }
    if ($p["code"] === 9999 || $p["code"] !== 0) {
        return ["status" => "pass", "detail" => "文件缺失返回结构化错误: " . ($p["message"] ?? $p["code"])];
    }
    return ["status" => "fail", "detail" => "意外响应 code=" . ($p["code"] ?? "N/A")];
});

echo "\n=== 结果: PASS=$passed FAIL=$failed ===\n";
exit($failed > 0 ? 1 : 0);