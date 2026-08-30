<?php
return [
    "default"      => "file",
    "level"        => [],
    "channels"     => [
        "file" => [
            "type"  => "File",
            "path"  => __DIR__ . "/../runtime/log",
            "level" => [],
        ],
    ],
];
