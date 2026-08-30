<?php
namespace think;
require __DIR__ . "/../vendor/autoload.php";
$app = new App();
$app->setRuntimePath(__DIR__ . "/../runtime/");
$http = $app->http;
$response = $http->run();
$response->send();
$http->end($response);