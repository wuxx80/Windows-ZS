<?php
namespace app\controller\admin;

use app\model\UnattendTemplate;

/**
 * 无人值守模板管理
 * 表结构对齐 zs_unattend_templates: name / description / template_type / config(JSON) / xml_content / is_default
 * generateXml 按模板 config 渲染真实 Windows unattend.xml，供 WinPE 部署后写入 Panther\unattend.xml
 */
class UnattendController extends BaseController
{
    public function index()
    {
        $keyword = input('keyword');
        $templateType = input('template_type');

        $query = UnattendTemplate::order('id', 'desc');

        if ($keyword) {
            $query->where('name|description', 'like', '%' . $keyword . '%');
        }
        if ($templateType) {
            $query->where('template_type', $templateType);
        }

        return $this->paginate($query);
    }

    public function create()
    {
        $templateType = input('template_type', 'standard');
        $config = input('config/a', []);

        $data = [
            'name' => input('name'),
            'description' => input('description'),
            'template_type' => in_array($templateType, ['standard', 'domain', 'kiosk', 'custom']) ? $templateType : 'standard',
            'config' => $config,
            'is_default' => (int) (bool) input('is_default', 0),
            'created_by' => $this->userId,
        ];

        if (empty($data['name'])) {
            return $this->error('param_error', '模板名称不能为空');
        }

        $data['xml_content'] = self::renderXml($templateType, $config);

        $template = UnattendTemplate::create($data);
        if ($data['is_default']) {
            UnattendTemplate::where('id', '<>', $template->id)->update(['is_default' => 0]);
        }
        return $this->success($template, '创建成功');
    }

    public function edit($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('template_not_found');
        }

        $data = [];
        $name = input('name');
        if ($name !== null) { $data['name'] = $name; }
        $desc = input('description');
        if ($desc !== null) { $data['description'] = $desc; }
        $type = input('template_type');
        if ($type !== null) {
            $data['template_type'] = in_array($type, ['standard', 'domain', 'kiosk', 'custom']) ? $type : 'standard';
        }
        $config = input('config/a', null);
        if ($config !== null) {
            $data['config'] = $config;
            $data['xml_content'] = self::renderXml($data['template_type'] ?? $template->template_type, $config);
        }
        $isDefault = input('is_default');
        if ($isDefault !== null) {
            $data['is_default'] = (int) (bool) $isDefault;
        }

        $template->save($data);
        if (!empty($data['is_default'])) {
            UnattendTemplate::where('id', '<>', $template->id)->update(['is_default' => 0]);
        }
        return $this->success($template, '更新成功');
    }

    public function delete($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('template_not_found');
        }
        $template->delete();
        return $this->success(null, '删除成功');
    }

    /**
     * 预览：返回模板 config 与已生成的 XML
     */
    public function preview($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('template_not_found');
        }
        return $this->success([
            'id' => $template->id,
            'name' => $template->name,
            'template_type' => $template->template_type,
            'config' => $template->config,
            'xml_content' => $template->xml_content,
        ]);
    }

    /**
     * 重新生成 XML（按当前 config），并保存到 xml_content
     */
    public function generateXml($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('template_not_found');
        }

        $xml = self::renderXml($template->template_type, $template->config);
        $template->xml_content = $xml;
        $template->save();

        return $this->success([
            'id' => $template->id,
            'name' => $template->name,
            'xml' => $xml,
        ], 'XML 生成成功');
    }

    public function validate($id)
    {
        $template = UnattendTemplate::find($id);
        if (!$template) {
            return $this->error('template_not_found');
        }

        $errors = [];
        $xml = $template->xml_content ?: self::renderXml($template->template_type, $template->config);

        if (empty($xml)) {
            $errors[] = '模板 XML 为空';
        }
        if (strpos($xml, '<?xml') === false) {
            $errors[] = 'XML 缺少声明';
        }
        if (strpos($xml, '<unattend') === false) {
            $errors[] = 'XML 缺少 unattend 根元素';
        }
        if (strpos($xml, '<settings pass="windowsPE"') === false) {
            $errors[] = 'XML 缺少 windowsPE 配置段';
        }

        return $this->success([
            'valid' => empty($errors),
            'errors' => $errors,
        ], empty($errors) ? '验证通过' : '验证失败');
    }

    /**
     * 渲染真实 Windows unattend.xml
     * @param string $type   standard / domain / kiosk / custom
     * @param array  $config 配置（general/user_account/network/disk/components/first_logon/security）
     */
    public static function renderXml(string $type, array $config): string
    {
        $general = $config['general'] ?? [];
        $user = $config['user_account'] ?? [];
        $network = $config['network'] ?? [];
        $disk = $config['disk'] ?? [];
        $components = $config['components'] ?? [];
        $firstLogon = $config['first_logon'] ?? [];

        $language = htmlspecialchars($general['language'] ?? 'zh-CN', ENT_QUOTES);
        $timezone = htmlspecialchars($general['timezone'] ?? 'China Standard Time', ENT_QUOTES);
        $username = htmlspecialchars($user['username'] ?? 'Admin', ENT_QUOTES);
        $password = htmlspecialchars($user['password'] ?? '', ENT_QUOTES);
        $autoLogin = !empty($user['auto_login']);
        $computerName = htmlspecialchars($network['computer_name'] ?? 'ZS-PC', ENT_QUOTES);
        $workgroup = htmlspecialchars($network['workgroup'] ?? 'WORKGROUP', ENT_QUOTES);
        $domain = htmlspecialchars($network['domain'] ?? '', ENT_QUOTES);
        $domainUser = htmlspecialchars($network['domain_user'] ?? '', ENT_QUOTES);
        $domainPassword = htmlspecialchars($network['domain_password'] ?? '', ENT_QUOTES);
        $joinDomain = $type === 'domain' && !empty($network['domain']);

        // OOBE 阶段首次登录命令（装软件 / 执行优化脚本），由 PE 生成 SetupComplete.cmd 时引用
        $firstLogonCommands = [];
        if (!empty($firstLogon['install_software'])) {
            $firstLogonCommands[] = '%SystemDrive%\\ZSInstall\\SetupComplete.cmd';
        }
        $customCommands = $firstLogon['commands'] ?? [];
        if (is_array($customCommands)) {
            foreach ($customCommands as $cmd) {
                if ($cmd) $firstLogonCommands[] = $cmd;
            }
        }

        $xml = '<?xml version="1.0" encoding="utf-8"?>' . "\n";
        $xml .= '<unattend xmlns="urn:schemas-microsoft-com:unattend">' . "\n";

        // ==== windowsPE 段：语言/键盘/分区（配置中未指定则省略分区，由 PE 按任务分区方案执行） ====
        $xml .= '    <settings pass="windowsPE">' . "\n";
        $xml .= '        <component name="Microsoft-Windows-International-Core-WinPE" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
        $xml .= '            <SetupUILanguage><UILanguage>' . $language . '</UILanguage></SetupUILanguage>' . "\n";
        $xml .= '            <InputLocale>' . ($general['input_locale'] ?? '0804:00000804') . '</InputLocale>' . "\n";
        $xml .= '            <SystemLocale>' . $language . '</SystemLocale>' . "\n";
        $xml .= '            <UILanguage>' . $language . '</UILanguage>' . "\n";
        $xml .= '            <UserLocale>' . $language . '</UserLocale>' . "\n";
        $xml .= '        </component>' . "\n";
        $xml .= '        <component name="Microsoft-Windows-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
        $xml .= '            <UserData>' . "\n";
        $xml .= '                <AcceptEula>true</AcceptEula>' . "\n";
        if ($username) {
            $xml .= '                <FullName>' . $username . '</FullName>' . "\n";
            $xml .= '                <Organization>ZS Studio</Organization>' . "\n";
        }
        $xml .= '                <ProductKey><Key>' . htmlspecialchars($general['product_key'] ?? '', ENT_QUOTES) . '</Key></ProductKey>' . "\n";
        $xml .= '            </UserData>' . "\n";
        $xml .= '        </component>' . "\n";
        $xml .= '    </settings>' . "\n";

        // ==== specialize 段：计算机名/时区/网络/加入域 ====
        $xml .= '    <settings pass="specialize">' . "\n";
        $xml .= '        <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
        if ($computerName) {
            $xml .= '            <ComputerName>' . $computerName . '</ComputerName>' . "\n";
        }
        $xml .= '            <TimeZone>' . $timezone . '</TimeZone>' . "\n";
        $xml .= '        </component>' . "\n";
        if ($joinDomain) {
            $xml .= '        <component name="Microsoft-Windows-UnattendedJoin" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
            $xml .= '            <Identification>' . "\n";
            $xml .= '                <Credentials><Domain>' . $domain . '</Domain><Username>' . $domainUser . '</Username><Password>' . $domainPassword . '</Password></Credentials>' . "\n";
            $xml .= '                <JoinDomain>' . $domain . '</JoinDomain>' . "\n";
            $xml .= '            </Identification>' . "\n";
            $xml .= '        </component>' . "\n";
        } else {
            $xml .= '        <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
            $xml .= '            <RegisteredOrganization>ZS Studio</RegisteredOrganization>' . "\n";
            $xml .= '            <RegisteredOwner>' . $username . '</RegisteredOwner>' . "\n";
            $xml .= '        </component>' . "\n";
        }
        $xml .= '    </settings>' . "\n";

        // ==== oobeSystem 段：OOBE 跳过 + 本地账号 + 首次登录命令 ====
        $xml .= '    <settings pass="oobeSystem">' . "\n";
        $xml .= '        <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
        $xml .= '            <InputLocale>' . ($general['input_locale'] ?? '0804:00000804') . '</InputLocale>' . "\n";
        $xml .= '            <SystemLocale>' . $language . '</SystemLocale>' . "\n";
        $xml .= '            <UILanguage>' . $language . '</UILanguage>' . "\n";
        $xml .= '            <UserLocale>' . $language . '</UserLocale>' . "\n";
        $xml .= '        </component>' . "\n";
        $xml .= '        <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">' . "\n";
        $xml .= '            <OOBE>' . "\n";
        $xml .= '                <HideEULAPage>true</HideEULAPage>' . "\n";
        $xml .= '                <HideLocalAccountScreen>true</HideLocalAccountScreen>' . "\n";
        $xml .= '                <HideOnlineAccountScreens>true</HideOnlineAccountScreens>' . "\n";
        $xml .= '                <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>' . "\n";
        $xml .= '                <NetworkLocation>Work</NetworkLocation>' . "\n";
        $xml .= '                <ProtectYourPC>3</ProtectYourPC>' . "\n";
        $xml .= '                <SkipMachineOOBE>true</SkipMachineOOBE>' . "\n";
        $xml .= '                <SkipUserOOBE>true</SkipUserOOBE>' . "\n";
        $xml .= '            </OOBE>' . "\n";
        $xml .= '            <UserAccounts>' . "\n";
        $xml .= '                <LocalAccounts>' . "\n";
        $xml .= '                    <LocalAccount wcm:action="add">' . "\n";
        $xml .= '                        <Name>' . $username . '</Name>' . "\n";
        $xml .= '                        <DisplayName>' . $username . '</DisplayName>' . "\n";
        $xml .= '                        <Group>Administrators</Group>' . "\n";
        $xml .= '                        <Password><Value>' . $password . '</Value><PlainText>true</PlainText></Password>' . "\n";
        $xml .= '                    </LocalAccount>' . "\n";
        $xml .= '                </LocalAccounts>' . "\n";
        $xml .= '            </UserAccounts>' . "\n";
        if ($autoLogin) {
            $xml .= '            <AutoLogon>' . "\n";
            $xml .= '                <Password><Value>' . $password . '</Value><PlainText>true</PlainText></Password>' . "\n";
            $xml .= '                <Enabled>true</Enabled>' . "\n";
            $xml .= '                <Username>' . $username . '</Username>' . "\n";
            $xml .= '            </AutoLogon>' . "\n";
        }
        if (!empty($firstLogonCommands)) {
            $xml .= '            <FirstLogonCommands>' . "\n";
            $seq = 0;
            foreach ($firstLogonCommands as $cmd) {
                $xml .= '                <SynchronousCommand wcm:action="add">' . "\n";
                $xml .= '                    <Order>' . $seq . '</Order>' . "\n";
                $xml .= '                    <CommandLine>' . htmlspecialchars($cmd, ENT_QUOTES) . '</CommandLine>' . "\n";
                $xml .= '                </SynchronousCommand>' . "\n";
                $seq++;
            }
            $xml .= '            </FirstLogonCommands>' . "\n";
        }
        $xml .= '        </component>' . "\n";
        $xml .= '    </settings>' . "\n";

        $xml .= '    <cpi:offlineImage cpi:source="wim:c:/sources/install.wim#Windows 11 Pro" xmlns:cpi="urn:schemas-microsoft-com:cpi" />' . "\n";
        $xml .= '</unattend>';

        return $xml;
    }
}