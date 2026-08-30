using System.Windows;
using System.Windows.Input;

namespace Windows_Client
{
    /// <summary>一键装机六步向导窗口（设计文档《一键装机交互设计》实现）</summary>
    public partial class InstallWizardWindow : Window
    {
        public InstallWizardWindow()
        {
            InitializeComponent();
        }

        /// <summary>标题栏拖拽移动窗口</summary>
        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch { }
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}