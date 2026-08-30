<?php
return [
    "enabled"   => env("BACKUP.ENABLED", true),
    "keep_days" => env("BACKUP.KEEP_DAYS", 7),
    "path"      => env("BACKUP.PATH", "/data/backup"),
];
