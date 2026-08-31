using System.Windows;
using System.Windows.Input;

namespace Windows_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnInstallClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.OpenInstallWizardCommand.Execute(null);
        }

        private void OnUsbClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.OpenUDiskCommand.Execute(null);
        }

        private void OnToolsClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.OpenToolsCommand.Execute(null);
        }

        private void OnSoftwareClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.NavigateCommand.Execute("Software");
        }
    }
}