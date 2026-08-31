using System.Windows;
using Windows_Client.ViewModels;

namespace Windows_Client
{
    public partial class SoftwareWindow : Window
    {
        public SoftwareWindow()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SoftwareWindowViewModel vm)
                vm.RequestClose += Close;
        }
    }
}
