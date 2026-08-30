<?php
$p = 'd:/Users/Desktop/Windows-ZS/server/e2e_winpe_test.php';
$s = file_get_contents($p);
$old = '// 23. 取消 waiting 任务
$r = req("POST", "/api/v1/tasks/" . $waitTaskId . "/cancel", null, $token);
$cancelOk = $r["body"]["code"] === 0 && ($r["body"]["data"]["status"] ?? "") === "cancelled";
$allPass = ok("取消waiting任务", $cancelOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;';
$new = '// 23. 取消 waiting 任务（新建一个 waiting 任务再取消，验证 waiting→cancelled 闭环）
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
$allPass = ok("取消running任务", $cancelRunOk, "status=" . ($r["body"]["data"]["status"] ?? "")) && $allPass;';
if (strpos($s, $old) !== false) {
    $s = str_replace($old, $new, $s);
    file_put_contents($p, $s);
    echo "e2e updated\n";
} else {
    echo "not found\n";
    $i = strpos($s, '取消waiting任务');
    echo substr($s, max(0,$i-300), 500) . "\n";
}