using System.Windows;
using System.Windows.Input;
using WinPE_Client.ViewModels;

namespace WinPE_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private MainViewModel? Vm => DataContext as MainViewModel;

        // 窗口加载完成：自动连接服务器 → 自注册 → 检测待执行任务 → 启动心跳（WinPE 续装闭环）
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
                await Vm.Initialize();
        }

        // 一键装机：打开六步向导（新配置 / 续装均可）
        private void OnInstallClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm?.OpenInstallWizardCommand.CanExecute(null) == true)
                Vm.OpenInstallWizardCommand.Execute(null);
        }

        // U盘制作 / 工具大全 / 绿色软件：设计文档已规划，属后续开发阶段
        private void OnUsbClick(object sender, MouseButtonEventArgs e)
        {
            Vm?.NotifyNotImplementedCommand.Execute("U盘制作");
        }

        private void OnToolsClick(object sender, MouseButtonEventArgs e)
        {
            Vm?.NotifyNotImplementedCommand.Execute("工具大全");
        }

        private void OnSoftwareClick(object sender, MouseButtonEventArgs e)
        {
            Vm?.NotifyNotImplementedCommand.Execute("绿色软件");
        }
    }
}
