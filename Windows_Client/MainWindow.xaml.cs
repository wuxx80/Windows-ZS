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
            vm?.NavigateCommand.Execute("Install");
        }

        private void OnUsbClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.NavigateCommand.Execute("Usb");
        }

        private void OnToolsClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.NavigateCommand.Execute("Tools");
        }

        private void OnSoftwareClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            vm?.NavigateCommand.Execute("Software");
        }
    }
}