<?php
namespace app\service;

use think\facade\Db;

class BackupService
{
    public static function backup(): array
    {
        $backupPath = config('backup.path', '/data/backup');
        if (!is_dir($backupPath)) {
            mkdir($backupPath, 0755, true);
        }

        $fileName = 'backup_' . date('Ymd_His') . '.sql';
        $filePath = $backupPath . '/' . $fileName;

        $tables = Db::query('SHOW TABLES');
        $sql = "-- ZS 装机助手 Database Backup\n-- Date: " . date('Y-m-d H:i:s') . "\n\n";

        foreach ($tables as $row) {
            $table = current($row);
            $sql .= "DROP TABLE IF EXISTS `{$table}`;\n";
            $create = Db::query("SHOW CREATE TABLE `{$table}`");
            $sql .= $create[0]['Create Table'] . ";\n\n";

            $rows = Db::table($table)->select();
            if (count($rows) > 0) {
                $sql .= "INSERT INTO `{$table}` VALUES \n";
                $values = [];
                foreach ($rows as $row) {
                    $vals = array_map(function($v) {
                        return is_null($v) ? 'NULL' : "'" . addslashes($v) . "'";
                    }, array_values((array)$row));
                    $values[] = '(' . implode(',', $vals) . ')';
                }
                $sql .= implode(",\n", $values) . ";\n\n";
            }
        }

        file_put_contents($filePath, $sql);

        $keepDays = config('backup.keep_days', 7);
        $expire = time() - $keepDays * 86400;
        foreach (glob($backupPath . '/backup_*.sql') as $oldFile) {
            if (filemtime($oldFile) < $expire) {
                unlink($oldFile);
            }
        }

        return [
            'file_name' => $fileName,
            'file_path' => $filePath,
            'file_size' => filesize($filePath),
            'created_at' => date('Y-m-d H:i:s'),
        ];
    }

    public static function list(): array
    {
        $backupPath = config('backup.path', '/data/backup');
        $files = glob($backupPath . '/backup_*.sql');
        $list = [];
        foreach ($files as $file) {
            $list[] = [
                'file_name' => basename($file),
                'file_path' => $file,
                'file_size' => filesize($file),
                'created_at' => date('Y-m-d H:i:s', filemtime($file)),
            ];
        }
        rsort($list);
        return $list;
    }
}
