<?php
// 通用辅助函数

if (!function_exists('json_success')) {
    function json_success($data = null, string $msg = '操作成功')
    {
        return json(['code' => 0, 'message' => $msg, 'data' => $data, 'timestamp' => time()]);
    }
}

if (!function_exists('json_error')) {
    function json_error(string $code = 'error', string $msg = '')
    {
        $status = config('status.' . $code, ['code' => 9999, 'message' => '未知错误']);
        if ($msg) $status['message'] = $msg;
        return json($status);
    }
}

if (!function_exists('generate_order_no')) {
    function generate_order_no(string $prefix = ''): string
    {
        return $prefix . date('YmdHis') . str_pad(mt_rand(0, 999999), 6, '0', STR_PAD_LEFT);
    }
}

if (!function_exists('format_bytes')) {
    function format_bytes(int $bytes): string
    {
        if ($bytes >= 1073741824) return round($bytes / 1073741824, 2) . ' GB';
        if ($bytes >= 1048576) return round($bytes / 1048576, 2) . ' MB';
        if ($bytes >= 1024) return round($bytes / 1024, 2) . ' KB';
        return $bytes . ' B';
    }
}

if (!function_exists('mask_sensitive')) {
    function mask_sensitive(string $value, int $showFront = 1, int $showEnd = 1): string
    {
        $len = mb_strlen($value);
        if ($len <= $showFront + $showEnd) return $value;
        $maskLen = $len - $showFront - $showEnd;
        return mb_substr($value, 0, $showFront) . str_repeat('*', $maskLen) . mb_substr($value, -$showEnd);
    }
}

if (!function_exists('get_client_ip')) {
    function get_client_ip(): string
    {
        return request()->ip();
    }
}
