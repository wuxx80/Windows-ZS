<?php
namespace app\controller\admin;

use app\model\Task;
use app\model\Client;
use app\model\Image;
use app\model\WorkOrder;
use think\facade\Db;

class ReportController extends BaseController
{
    public function installReport()
    {
        $startDate = input('start_date', date('Y-m-d', strtotime('-30 days')));
        $endDate = input('end_date', date('Y-m-d'));

        $dailyStats = Task::whereBetween('created_at', [$startDate . ' 00:00:00', $endDate . ' 23:59:59'])
            ->field('DATE(created_at) as date, COUNT(*) as total, SUM(CASE WHEN status = "completed" THEN 1 ELSE 0 END) as success, SUM(CASE WHEN status = "failed" THEN 1 ELSE 0 END) as failed')
            ->group('DATE(created_at)')
            ->select()
            ->toArray();

        $summary = Task::whereBetween('created_at', [$startDate . ' 00:00:00', $endDate . ' 23:59:59'])
            ->field('COUNT(*) as total, SUM(CASE WHEN status = "completed" THEN 1 ELSE 0 END) as success, SUM(CASE WHEN status = "failed" THEN 1 ELSE 0 END) as failed')
            ->find();

        return $this->success([
            'daily' => $dailyStats,
            'summary' => $summary,
            'start_date' => $startDate,
            'end_date' => $endDate,
        ]);
    }

    public function clientReport()
    {
        $total = Client::count();
        $online = Client::where('status', 'online')->count();
        $offline = Client::where('status', 'offline')->count();
        $pending = Client::where('status', 'pending')->count();
        $blocked = Client::where('status', 'blocked')->count();

        $osDistribution = Client::field('os_version, COUNT(*) as count')
            ->group('os_version')
            ->select()
            ->toArray();

        $versionDistribution = Client::field('client_version, COUNT(*) as count')
            ->group('client_version')
            ->select()
            ->toArray();

        return $this->success([
            'total' => $total,
            'online' => $online,
            'offline' => $offline,
            'pending' => $pending,
            'blocked' => $blocked,
            'os_distribution' => $osDistribution,
            'version_distribution' => $versionDistribution,
        ]);
    }

    public function imageRanking()
    {
        $ranking = Image::field('id, name, format, os_type, download_count, install_count, file_size')
            ->order('install_count', 'desc')
            ->limit(20)
            ->select()
            ->toArray();

        return $this->success([
            'ranking' => $ranking,
            'total' => Image::count(),
        ]);
    }

    public function orderReport()
    {
        $startDate = input('start_date', date('Y-m-d', strtotime('-30 days')));
        $endDate = input('end_date', date('Y-m-d'));

        $dailyOrders = Task::whereBetween('created_at', [$startDate . ' 00:00:00', $endDate . ' 23:59:59'])
            ->field('DATE(created_at) as date, COUNT(*) as count')
            ->group('DATE(created_at)')
            ->select()
            ->toArray();

        $statusStats = Task::field('status, COUNT(*) as count')
            ->group('status')
            ->select()
            ->toArray();

        return $this->success([
            'daily' => $dailyOrders,
            'status_stats' => $statusStats,
            'start_date' => $startDate,
            'end_date' => $endDate,
        ]);
    }

    public function workOrderReport()
    {
        $startDate = input('start_date', date('Y-m-d', strtotime('-30 days')));
        $endDate = input('end_date', date('Y-m-d'));

        $statusStats = WorkOrder::field('status, COUNT(*) as count')
            ->group('status')
            ->select()
            ->toArray();

        $typeStats = WorkOrder::field('type, COUNT(*) as count')
            ->group('type')
            ->select()
            ->toArray();

        $resolution = WorkOrder::where('status', 'completed')
            ->whereBetween('created_at', [$startDate . ' 00:00:00', $endDate . ' 23:59:59'])
            ->field('AVG(TIMESTAMPDIFF(HOUR, created_at, updated_at)) as avg_hours')
            ->find();

        return $this->success([
            'status_stats' => $statusStats,
            'type_stats' => $typeStats,
            'avg_resolution_hours' => round($resolution['avg_hours'] ?? 0, 1),
            'start_date' => $startDate,
            'end_date' => $endDate,
        ]);
    }
}