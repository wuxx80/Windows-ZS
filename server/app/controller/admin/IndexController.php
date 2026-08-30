<?php
namespace app\controller\admin;

use app\model\Image;
use app\model\Client;
use app\model\Task;
use think\facade\Db;

class IndexController extends BaseController
{
    public function index()
    {
        $todayStart = date("Y-m-d 00:00:00");
        $todayEnd = date("Y-m-d 23:59:59");

        $todayInstalls = Task::whereBetween("created_at", [$todayStart, $todayEnd])
            ->where("status", "completed")
            ->count();

        $totalInstalls = Task::where("status", "completed")
            ->count();

        $totalImages = Image::count();
        $onlineClients = Client::where("status", "online")->count();
        $pendingClients = Client::where("status", "pending")->count();

        $recentTasks = Task::order("created_at", "desc")
            ->limit(10)
            ->select()
            ->toArray();

        $storageUsage = Db::name("images")
            ->field("SUM(file_size) as total_size, COUNT(*) as total_files")
            ->find();

                // 单次查询7天趋势，避免循环N+1
        $rawTrend = Db::name("tasks")
            ->field("DATE(created_at) as date, COUNT(*) as count")
            ->where("status", "completed")
            ->where("created_at", ">=", date("Y-m-d", strtotime("-6 days")))
            ->group("DATE(created_at)")
            ->select()
            ->toArray();
        $trendMap = [];
        foreach ($rawTrend as $t) {
            $trendMap[$t["date"]] = (int) $t["count"];
        }
        $installTrend = [];
        for ($i = 6; $i >= 0; $i--) {
            $date = date("Y-m-d", strtotime("-" . $i . " days"));
            $installTrend[] = [
                "date" => $date,
                "count" => $trendMap[$date] ?? 0,
            ];
        }

        return $this->success([
            "today_installs" => $todayInstalls,
            "total_installs" => $totalInstalls,
            "total_images" => $totalImages,
            "online_clients" => $onlineClients,
            "pending_clients" => $pendingClients,
            "recent_tasks" => $recentTasks,
            "storage_usage" => [
                "total_size" => (int) ($storageUsage["total_size"] ?? 0),
                "total_files" => (int) ($storageUsage["total_files"] ?? 0),
            ],
            "install_trend" => $installTrend,
        ]);
    }
}
