<?php
namespace app\service;

class WebSocketService
{
    private static $redis = null;

    private static function getRedis(): object
    {
        if (self::$redis === null) {
            try {
                self::$redis = new \Redis();
                self::$redis->connect(
                    env('REDIS.HOSTNAME', '127.0.0.1'),
                    env('REDIS.PORT', 6379)
                );
                if ($pass = env('REDIS.PASSWORD', '')) {
                    self::$redis->auth($pass);
                }
                self::$redis->select(env('REDIS.SELECT', 0));
            } catch (\Exception $e) {
                return (object)['enabled' => false];
            }
        }
        return self::$redis;
    }

    public static function push(string $channel, array $data): void
    {
        try {
            $redis = self::getRedis();
            if ($redis && !isset($redis->enabled)) {
                $redis->publish('ws:' . $channel, json_encode($data));
            }
        } catch (\Exception $e) {
            // WebSocket unavailable, skip
        }
    }

    public static function pushTaskProgress(int $taskId, array $data): void
    {
        self::push('task:' . $taskId, [
            'event' => 'task_progress',
            'task_id' => $taskId,
            'data' => $data,
            'timestamp' => time(),
        ]);
    }

    public static function pushTaskStatus(int $taskId, string $status, array $extra = []): void
    {
        self::push('task:' . $taskId, array_merge([
            'event' => 'task_status',
            'task_id' => $taskId,
            'status' => $status,
            'timestamp' => time(),
        ], $extra));
    }

    public static function pushNotification(int $userId, array $data): void
    {
        self::push('user:' . $userId, [
            'event' => 'notification',
            'data' => $data,
            'timestamp' => time(),
        ]);
    }
}
