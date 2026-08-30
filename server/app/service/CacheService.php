<?php
namespace app\service;

use think\facade\Cache;

class CacheService
{
    public static function remember(string $key, callable $callback, int $ttl = 300): mixed
    {
        if (Cache::has($key)) {
            return Cache::get($key);
        }
        $value = $callback();
        Cache::set($key, $value, $ttl);
        return $value;
    }

    public static function clear(string $prefix = ''): void
    {
        if ($prefix) {
            Cache::tag($prefix)->clear();
        } else {
            Cache::clear();
        }
    }

    public static function getStats(): array
    {
        return [
            'image_list_cache' => true,
            'client_list_cache' => true,
            'dashboard_stats_cache' => true,
            'cache_ttl' => 300,
        ];
    }
}
