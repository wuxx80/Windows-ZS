<?php
namespace app\controller\admin;

class DeviceController extends BaseController
{
    public function disks()
    {
        $disks = [];
        if (PHP_OS_FAMILY === 'Windows') {
            exec('wmic logicaldisk get DeviceID,Size,DriveType,FileSystem,VolumeName 2>&1', $output, $ret);
            if ($ret === 0) {
                $lines = array_filter(explode("\n", implode("\n", $output)));
                $header = null;
                foreach ($lines as $i => $line) {
                    $line = trim($line);
                    if ($i === 0) { $header = preg_split('/\\s{2,}/', $line); continue; }
                    if (empty($line)) continue;
                    $vals = preg_split('/\\s{2,}/', $line);
                    $drive = [];
                    foreach ($header as $j => $h) {
                        $drive[strtolower($h)] = $vals[$j] ?? '';
                    }
                    $driveType = (int)
                    ($drive['drivetype'] ?? 0);
                    if ($driveType === 3) {
                        $disks[] = [
                            'device' => $drive['deviceid'] ?? '',
                            'size' => (int)($drive['size'] ?? 0),
                            'fstype' => $drive['filesystem'] ?? '',
                            'label' => $drive['volumename'] ?? '',
                            'type' => 'local',
                        ];
                    }
                }
            }
        }
        return $this->success($disks);
    }
}