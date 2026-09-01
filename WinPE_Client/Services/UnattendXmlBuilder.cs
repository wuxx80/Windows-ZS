using System;
using System.IO;
using System.Text;
using System.Xml;
using WinPE_Client.Models;

namespace WinPE_Client.Services
{
    /// <summary>
    /// Unattend.xml 生成器 —— 设计文档 §6.6 Step 6c。
    /// 仅当 task.ini meta.oobe_mode=auto 时生成并写入 C:\Windows\Panther\Unattend.xml，
    /// 实现"OOBE 全跳过 + 自动创建本地管理员账户 + 时区 zh-CN + Win11 旁路联网 BypassNRO"。
    /// oobe_mode=manual 时返回空字符串，调用方应跳过写入，保留 OOBE 让用户操作。
    /// </summary>
    public static class UnattendXmlBuilder
    {
        // 默认本地管理员账户名 / 描述（密码留空，OOBE 不要求输入）
        private const string DefaultAdminName = "Admin";
        private const string DefaultAdminGroup = "Administrators";
        private const string DefaultAdminPassword = "";
        private const string DefaultTimeZone = "China Standard Time";

        /// <summary>
        /// 渲染 Unattend.xml 内容。
        /// </summary>
        /// <param name="task">task.ini 模型（读取 meta.oobe_mode）</param>
        /// <returns>oobe_mode=auto 返回完整 Unattend.xml 字符串；否则返回空字符串</returns>
        public static string Build(TaskIni task)
        {
            // 仅 oobe_mode=auto 时生成（设计 §6.6c）
            if (task?.Meta == null) return "";
            if (string.IsNullOrEmpty(task.Meta.OobeMode)
                || !task.Meta.OobeMode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            var settings = new StringBuilder();
            settings.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            settings.Append("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

            // ===== windowsPE pass：时区 + 系统语言 =====
            settings.Append("<settings pass=\"windowsPE\">");
            settings.Append("<component name=\"Microsoft-Windows-International-Core-WinPE\" "
                + "processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" "
                + "language=\"neutral\" versionScope=\"nonSxS\" "
                + "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" "
                + "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            settings.Append("<SetupUILanguage>");
            settings.Append("<UILanguage>zh-CN</UILanguage>");
            settings.Append("</SetupUILanguage>");
            settings.Append("<InputLocale>0804:</InputLocale>");
            settings.Append("<SystemLocale>zh-CN</SystemLocale>");
            settings.Append("<UILanguage>zh-CN</UILanguage>");
            settings.Append("<UILanguageFallback>zh-CN</UILanguageFallback>");
            settings.Append("<UserLocale>zh-CN</UserLocale>");
            settings.Append("</component>");
            settings.Append("</settings>");

            // ===== specialize pass：Win11 BypassNRO 旁路联网 + 禁用 Cortana =====
            // reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f
            settings.Append("<settings pass=\"specialize\">");
            settings.Append("<component name=\"Microsoft-Windows-Deployment\" "
                + "processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" "
                + "language=\"neutral\" versionScope=\"nonSxS\" "
                + "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" "
                + "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            settings.Append("<RunSynchronous>");
            settings.Append("<RunSynchronousCommand wcm:action=\"add\">");
            settings.Append("<Order>1</Order>");
            settings.Append("<Description>Bypass NRO for Windows 11</Description>");
            settings.Append("<Path>reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" "
                + "/v BypassNRO /t REG_DWORD /d 1 /f</Path>");
            settings.Append("</RunSynchronousCommand>");
            settings.Append("</RunSynchronous>");
            settings.Append("</component>");
            settings.Append("</settings>");

            // ===== oobeSystem pass：OOBE 全跳过 + 自动创建本地管理员 =====
            settings.Append("<settings pass=\"oobeSystem\">");

            // OOBE 全部跳过
            settings.Append("<component name=\"Microsoft-Windows-Shell-Setup\" "
                + "processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" "
                + "language=\"neutral\" versionScope=\"nonSxS\" "
                + "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" "
                + "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");

            // 时区
            settings.Append("<TimeZone>" + DefaultTimeZone + "</TimeZone>");

            // OOBE 跳过
            settings.Append("<OOBE>");
            settings.Append("<HideEULAPage>true</HideEULAPage>");
            settings.Append("<HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>");
            settings.Append("<HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
            settings.Append("<HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
            settings.Append("<NetworkLocation>Work</NetworkLocation>");
            settings.Append("<ProtectYourPC>3</ProtectYourPC>"); // 3=SkipExpressSetup 既不安装推荐也不安装重要
            settings.Append("<SkipMachineOOBE>true</SkipMachineOOBE>");
            settings.Append("<SkipUserOOBE>true</SkipUserOOBE>");
            settings.Append("</OOBE>");

            // 用户账户：自动创建本地管理员
            settings.Append("<UserAccounts>");
            settings.Append("<LocalAccounts>");
            settings.Append("<LocalAccount wcm:action=\"add\">");
            settings.Append("<Name>" + DefaultAdminName + "</Name>");
            settings.Append("<Group>" + DefaultAdminGroup + "</Group>");
            settings.Append("<DisplayName>" + DefaultAdminName + "</DisplayName>");
            settings.Append("<Description>Local admin auto-created by ZS unattended</Description>");
            if (!string.IsNullOrEmpty(DefaultAdminPassword))
            {
                settings.Append("<Password>");
                settings.Append("<Value>" + DefaultAdminPassword + "</Value>");
                settings.Append("<PlainText>true</PlainText>");
                settings.Append("</Password>");
            }
            settings.Append("</LocalAccount>");
            settings.Append("</LocalAccounts>");
            settings.Append("</UserAccounts>");

            // 自动登录到本地管理员账户
            settings.Append("<AutoLogon>");
            settings.Append("<Password>");
            settings.Append("<Value>" + DefaultAdminPassword + "</Value>");
            settings.Append("<PlainText>true</PlainText>");
            settings.Append("</Password>");
            settings.Append("<Enabled>true</Enabled>");
            settings.Append("<LogonCount>1</LogonCount>");
            settings.Append("<Username>" + DefaultAdminName + "</Username>");
            settings.Append("</AutoLogon>");

            settings.Append("</component>");
            settings.Append("</settings>");

            settings.Append("</unattend>");
            return settings.ToString();
        }

        /// <summary>
        /// 便捷重载：直接写入到目标系统的 C:\Windows\Panther\Unattend.xml。
        /// 仅当 oobe_mode=auto 时写入，否则跳过。返回写入的文件路径或 null。
        /// </summary>
        public static string? WriteToSystem(TaskIni task, string targetDrive)
        {
            var xml = Build(task);
            if (string.IsNullOrEmpty(xml)) return null;

            var drive = targetDrive.TrimEnd('\\');
            var pantherDir = Path.Combine(drive + "\\", "Windows", "Panther");
            Directory.CreateDirectory(pantherDir);
            var path = Path.Combine(pantherDir, "Unattend.xml");
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            return path;
        }
    }
}
