<?php
return [
    "default"         => "mysql",
    "connections"     => [
        "mysql" => [
            "type"         => "mysql",
            "hostname"     => env("DATABASE.HOSTNAME", "127.0.0.1"),
            "database"     => env("DATABASE.DATABASE", "zs_installer"),
            "username"     => env("DATABASE.USERNAME", "root"),
            "password"     => env("DATABASE.PASSWORD", ""),
            "hostport"     => env("DATABASE.HOSTPORT", "3306"),
            "charset"      => env("DATABASE.CHARSET", "utf8mb4"),
            "prefix"       => env("DATABASE.PREFIX", "zs_"),
            "fields_strict" => true,
            "break_reconnect" => true,
        ],
    ],
];
