using System.IO;
using System.Text;

namespace Windows_Client.Services
{
    /// <summary>
    /// 本地登录会话存储：将 token 与用户名保存到用户配置目录，
    /// 客户端下次启动可自动恢复登录（免重复输入账号密码）。
    /// token 为服务端签发，失效后由接口返回「Token 已过期」并引导重新登录。
    /// </summary>
    public static class SessionStore
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZS_Installer");

        private static string TokenFile => Path.Combine(Dir, "session.token");
        private static string UsernameFile => Path.Combine(Dir, "session.user");

        public static void Save(string token, string username)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(TokenFile, token, Encoding.UTF8);
                File.WriteAllText(UsernameFile, username, Encoding.UTF8);
            }
            catch
            {
                // 保存失败不影响登录流程（仅下次需重新登录）
            }
        }

        public static string? LoadToken()
        {
            try
            {
                return File.Exists(TokenFile) ? File.ReadAllText(TokenFile, Encoding.UTF8).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        public static string? LoadUsername()
        {
            try
            {
                return File.Exists(UsernameFile) ? File.ReadAllText(UsernameFile, Encoding.UTF8).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(TokenFile)) File.Delete(TokenFile);
                if (File.Exists(UsernameFile)) File.Delete(UsernameFile);
            }
            catch
            {
                // 清理失败可忽略
            }
        }
    }
}
