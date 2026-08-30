<?php
return [
    "port" => env("WEBSOCKET.PORT", 2346),
    "heartbeat_interval" => 30,
    "max_connections" => 1000,
];
