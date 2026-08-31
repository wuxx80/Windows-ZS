<?php
namespace app\controller\api;

use app\BaseController;
use think\facade\Db;

/**
 * 站点信息公开接口（客户端首页品牌信息，无需登录即可获取）
 * 读取 zs_settings 表中 group=site 的配置项，供客户端展示：
 * 左上角品牌（Logo 文字 / 主标题 / 副标题 / 标语）、
 * 边框项（版权 / 版本 / 联系 / 关于）。
 */
class SiteController extends BaseController
{
    /**
     * 站点信息默认值（settings 表中无记录时的兜底）
     */
    private $defaults = [
        'site_logo_text' => 'ZS',
        'site_title'     => '装机助手 · PE',
        'site_subtitle'  => 'ZS Install Assistant | www.zs-install.com',
        'site_tagline'   => '简单 · 高效 · 一站式系统维护',
        'site_website'   => 'www.zs-install.com',
        'site_copyright' => '© 2026 ZS 装机助手 版权所有',
        'site_version'   => 'v0.0.268311',
        'site_contact'   => "客服邮箱: support@zs-install.com\nQQ 群: 10000001\n客服电话: 400-000-0000",
        'site_about'     => "ZS 装机助手是一款集一键系统重装、U盘启动盘制作、系统维护工具、绿色软件安装于一体的系统维护助手。\n\n支持无人值守全自动装机，重启进入 WinPE 后自动认领任务并完成分区、部署、驱动注入、引导修复与首次登录优化，全程无需值守。",
    ];

    public function info()
    {
        $rows = Db::name('settings')
            ->where('group', 'site')
            ->field('key,value')
            ->select()
            ->toArray();

        $data = $this->defaults;
        foreach ($rows as $row) {
            // 仅接受已定义键，避免配置脏数据外泄
            if (array_key_exists($row['key'], $data) && $row['value'] !== null && $row['value'] !== '') {
                $data[$row['key']] = $row['value'];
            }
        }

        // 兼容旧配置：主标题为空时回退到 basic.site_name
        if (empty($data['site_title'])) {
            $siteName = Db::name('settings')->where('key', 'site_name')->value('value');
            if ($siteName) {
                $data['site_title'] = $siteName;
            }
        }

        return json([
            'code' => 0,
            'message' => 'ok',
            'data' => $data,
            'timestamp' => time(),
        ]);
    }
}
