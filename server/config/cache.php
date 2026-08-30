<?php
return [
    "default" => "file",
    "stores"  => [
        "file" => [
            "type"   => "File",
            "path"   => __DIR__ . "/../runtime/cache",
            "prefix" => "zs_",
            "expire" => 0,
        ],
        "redis" => [
            "type"   => "redis",
            "host"   => env("REDIS.HOSTNAME", "127.0.0.1"),
            "port"   => env("REDIS.PORT", "6379"),
            "password" => env("REDIS.PASSWORD", ""),
            "select" => env("REDIS.SELECT", 0),
            "prefix" => "zs_",
            "expire" => 0,
        ],
    ],
];