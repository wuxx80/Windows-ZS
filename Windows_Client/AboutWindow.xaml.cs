using System.Windows;
using Windows_Client.ViewModels;

namespace Windows_Client
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private MainViewModel? Vm => DataContext as MainViewModel;

        private void OnContactClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.OpenContactCommand.CanExecute(null) == true)
                Vm.OpenContactCommand.Execute(null);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
