using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using Windows_Client;
using Windows_Client.Services;
using Windows_Client.ViewModels;

// U盘制作闭环 UI 验证：步骤③复选框 + 完成页 成功/失败/取消 分色与重试按钮
namespace B5UiHarness
{
    internal static class Program
    {
        private static readonly object Gate = new();
        private static int Pass, Fail;

        private static void Ok(string m) { lock (Gate) { Pass++; Console.WriteLine("  PASS: " + m); } }
        private static void Ng(string m) { lock (Gate) { Fail++; Console.WriteLine("  FAIL: " + m); } }

        [STAThread]
        private static int Main()
        {
            var api = new ApiService();
            var svc = new UDiskService();
            var vm = new UDiskViewModel(api, svc, "http://127.0.0.1", AppContext.BaseDirectory);
            var win = new UDiskWindow { DataContext = vm };
            win.Show();
            Task.Run(() => Monitor(win, vm));
            Dispatcher.Run();
            Console.WriteLine("=== 结果: PASS=" + Pass + " FAIL=" + Fail + " ===");
            return Fail > 0 ? 1 : 0;
        }

        private static void Monitor(Window win, UDiskViewModel vm)
        {
            try
            {
                Thread.Sleep(2500);
                var root = AutomationElement.RootElement;
                var w = root.FindFirst(TreeScope.Children, new PropertyCondition(
                    AutomationElement.NameProperty, "ZS 装机助手 - U盘制作"));
                if (w == null) { Ng("未找到 UDisk 窗口"); return; }
                Ok("UDisk 窗口已打开");

                // 步骤③ 复选框
                vm.CurrentStep = 2;
                Thread.Sleep(800);
                var cbs = w.FindAll(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.CheckBox));
                bool tools = false, client = false, customize = false;
                foreach (AutomationElement cb in cbs)
                {
                    var n = cb.Current.Name;
                    if (n.Contains("内置工具大全")) tools = true;
                    if (n.Contains("装机助手客户端")) client = true;
                    if (n.Contains("PE 定制")) customize = true;
                }
                if (tools) Ok("步骤③ 找到「同时写入内置工具大全」复选框"); else Ng("步骤③ 缺少「内置工具大全」复选框");
                if (client) Ok("步骤③ 找到「装机助手客户端」复选框"); else Ng("步骤③ 缺少「装机助手客户端」复选框");
                if (customize) Ok("步骤③ 找到「PE 定制」复选框"); else Ng("步骤③ 缺少「PE 定制」复选框");

                bool HasButton(string name)
                {
                    var b = w.FindFirst(TreeScope.Descendants, new PropertyCondition(
                        AutomationElement.NameProperty, name));
                    return b != null;
                }
                bool HasText(string text)
                {
                    var cond = new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                        new PropertyCondition(AutomationElement.NameProperty, text));
                    return w.FindFirst(TreeScope.Descendants, cond) != null;
                }

                // 失败态：❌ + 失败原因 + 重新制作
                win.Dispatcher.Invoke(() =>
                {
                    vm.CurrentStep = 3;
                    vm.ResultKind = "failed";
                    vm.StatusText = "制作失败";
                    vm.FailReason = "模拟失败原因: 磁盘被占用";
                    vm.IsFinished = true;
                });
                Thread.Sleep(800);
                if (HasText("制作失败") && HasButton("重新制作"))
                    Ok("失败态：显示失败原因 + 重新制作按钮");
                else Ng("失败态缺少失败提示或重新制作按钮");

                // 点击重新制作 → 复位到执行页
                win.Dispatcher.Invoke(() => vm.RetryCommand.Execute(null));
                Thread.Sleep(600);
                if (!vm.IsFinished && vm.ResultKind == "" && vm.CurrentStep == 3)
                    Ok("重新制作复位成功（清除结果态，回到执行页）");
                else Ng("重新制作复位失败");

                // 成功态：✅ + 完成提示 + 无重新制作
                win.Dispatcher.Invoke(() =>
                {
                    vm.ResultKind = "success";
                    vm.StatusText = "U盘制作完成";
                    vm.IsFinished = true;
                });
                Thread.Sleep(800);
                if (HasText("U盘制作完成") && HasText("可以安全拔出 U 盘") && !HasButton("重新制作"))
                    Ok("成功态：显示完成提示，无重新制作按钮");
                else Ng("成功态渲染错误");

                // 取消态：⏹ + 未写入数据 + 重新制作
                win.Dispatcher.Invoke(() =>
                {
                    vm.ResultKind = "canceled";
                    vm.StatusText = "已取消制作";
                    vm.IsFinished = true;
                });
                Thread.Sleep(800);
                if (HasText("已取消制作") && HasText("未写入任何数据，可返回重新制作") && HasButton("重新制作"))
                    Ok("取消态：显示未写入提示 + 重新制作按钮");
                else Ng("取消态渲染错误");
            }
            catch (Exception ex) { Ng("监控异常: " + ex.Message); }
            finally
            {
                try
                {
                    win.Dispatcher.Invoke(() =>
                    {
                        win.Close();
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    });
                }
                catch { }
            }
        }
    }
}