<?php
return [
    "controller_suffix"  => true,
    "debug"              => env("APP_DEBUG", false),
    "default_return_type" => "json",
    "default_filter"     => "trim",
    "exception_handle"   => \app\exception\HttpExceptionHandler::class,
    "show_error_msg"     => true,
];
