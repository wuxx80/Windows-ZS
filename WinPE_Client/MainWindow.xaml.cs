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

        // U盘制作：打开四步向导（真实写盘，带安全护栏）
        private void OnUsbClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm?.OpenUDiskCommand.CanExecute(null) == true)
                Vm.OpenUDiskCommand.Execute(null);
        }

        // 工具大全：打开工具大全子窗口；绿色软件属后续开发阶段
        private void OnToolsClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm?.OpenToolsCommand.CanExecute(null) == true)
                Vm.OpenToolsCommand.Execute(null);
        }

        private void OnSoftwareClick(object sender, MouseButtonEventArgs e)
        {
            Vm?.NotifyNotImplementedCommand.Execute("绿色软件");
        }
    }
}
