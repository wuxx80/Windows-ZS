<?php
return [
    "max_size"      => env("UPLOAD.MAX_SIZE", 21474836480),
    "extensions"    => explode(",", env("UPLOAD.EXTENSIONS", "wim,iso,esd,swm,gho,zip,rar,7z")),
    "path"          => env("UPLOAD.PATH", "/data/images"),
    "chunk_size"    => 5242880,
    "allow_types"   => [
        "wim" => "application/octet-stream",
        "iso" => "application/x-iso9660-image",
        "esd" => "application/octet-stream",
        "swm" => "application/octet-stream",
        "gho" => "application/octet-stream",
    ],
];
