<?php
$p = 'd:/Users/Desktop/Windows-ZS/server/app/controller/admin/TaskController.php';
$s = file_get_contents($p);
$old = "if (!in_array(\$task->status, ['pending', 'waiting'])) {
            return \$this->error('task_cancel_not_allowed');
        }";
$new = "if (!in_array(\$task->status, ['pending', 'waiting', 'running'])) {
            return \$this->error('task_cancel_not_allowed', '当前状态不允许取消');
        }";
if (strpos($s, $old) !== false) {
    $s = str_replace($old, $new, $s);
    file_put_contents($p, $s);
    echo "cancel fixed\n";
} else {
    echo "not found\n";
    $i = strpos($s, 'task_cancel_not_allowed');
    echo substr($s, max(0,$i-200), 300) . "\n";
}