<?php
$db = new PDO('mysql:host=127.0.0.1;dbname=zs_installer', 'root', 'maoge123');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
function colInfo(PDO $db, string $table, array $cols): array {
    $in = implode(',', array_map(fn($c) => "'" . $c . "'", $cols));
    return $db->query("SELECT COLUMN_NAME,COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='zs_installer' AND TABLE_NAME='$table' AND COLUMN_NAME IN ($in)")->fetchAll(PDO::FETCH_ASSOC);
}
foreach ([['zs_tasks',['status','duration','cancelled_at','cancelled_by']], ['zs_clients',['approved_at','approved_by','last_heartbeat','first_ip']]] as [$t,$cols]) {
    echo "=== $t ===" . PHP_EOL;
    foreach (colInfo($db,$t,$cols) as $c) echo $c['COLUMN_NAME'] . " : " . $c['COLUMN_TYPE'] . PHP_EOL;
}