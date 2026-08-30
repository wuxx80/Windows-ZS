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

        $installTrend = [];
        for ($i = 6; $i >= 0; $i--) {
            $date = date("Y-m-d", strtotime("-" . $i . " days"));
            $count = Task::where("status", "completed")
                ->whereBetween("created_at", [$date . " 00:00:00", $date . " 23:59:59"])
                ->count();
            $installTrend[] = [
                "date" => $date,
                "count" => $count,
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
