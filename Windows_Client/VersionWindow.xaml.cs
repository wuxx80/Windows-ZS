using System.Windows;

namespace Windows_Client
{
    public partial class VersionWindow : Window
    {
        public VersionWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
