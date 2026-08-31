using System.Windows;
using System.Windows.Input;

namespace WinPE_Client
{
    public partial class UDiskWindow : Window
    {
        public UDiskWindow()
        {
            InitializeComponent();
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}