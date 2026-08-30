<?php
$sql = file_get_contents('d:/Users/Desktop/Windows-ZS/database/migration_closure.sql');
$db = new PDO('mysql:host=127.0.0.1;dbname=zs_installer', 'root', 'maoge123', [PDO::MYSQL_ATTR_MULTI_STATEMENTS => true]);
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
try {
    $db->exec($sql);
    echo "MIGRATION OK" . PHP_EOL;
} catch (PDOException $e) {
    echo "MIGRATION ERROR: " . $e->getMessage() . PHP_EOL;
    exit(1);
}