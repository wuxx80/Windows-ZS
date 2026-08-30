<?php
return [
    "key"      => env("JWT.KEY", "zs_installer_default_key"),
    "expire"   => env("JWT.EXPIRE", 86400),
    "algo"     => "HS256",
];
