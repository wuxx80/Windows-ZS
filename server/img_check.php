<?php
$db = new PDO('mysql:host=127.0.0.1;dbname=zs_installer', 'root', 'maoge123');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$r = $db->query("SELECT id,name,status,format FROM zs_images LIMIT 5")->fetchAll(PDO::FETCH_ASSOC);
echo "images=" . json_encode($r, JSON_UNESCAPED_UNICODE) . PHP_EOL;