using System.Windows;
using System.Windows.Input;
using Windows_Client.Models;
using Windows_Client.Services;

namespace Windows_Client
{
    /// <summary>
    /// 用户登录 / 注册对话框：登录成功或注册成功即返回结果，由 MainViewModel 建立会话。
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly ApiService _api;
        private readonly string _serverUrl;
        private bool _isRegisterMode;

        /// <summary>登录/注册成功后填充，供调用方建立会话</summary>
        public LoginResult? Result { get; private set; }

        public LoginWindow(ApiService api, string serverUrl)
        {
            InitializeComponent();
            _api = api;
            _serverUrl = serverUrl;
            _api.SetBaseUrl(_serverUrl);
            Loaded += (_, _) => UsernameBox.Focus();
        }

        private bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                _isRegisterMode = value;
                ConfirmPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                TitleText.Text = value ? "注册 ZS 装机助手" : "登录 ZS 装机助手";
                SubmitBtn.Content = value ? "注 册" : "登 录";
                var activeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 90, 172));
                LoginModeBtn.Background = value ? System.Windows.Media.Brushes.Transparent : activeBrush;
                RegisterModeBtn.Background = value ? activeBrush : System.Windows.Media.Brushes.Transparent;
                StatusText.Text = "";
            }
        }

        private void OnLoginModeClick(object sender, RoutedEventArgs e) => IsRegisterMode = false;

        private void OnRegisterModeClick(object sender, RoutedEventArgs e) => IsRegisterMode = true;

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Result = null;
                DialogResult = false;
            }
            else if (e.Key == Key.Enter)
            {
                _ = SubmitAsync();
            }
        }

        private async void OnSubmitClick(object sender, RoutedEventArgs e)
        {
            await SubmitAsync();
        }

        private async System.Threading.Tasks.Task SubmitAsync()
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "请输入用户名和密码";
                return;
            }

            if (IsRegisterMode)
            {
                if (password.Length < 6)
                {
                    StatusText.Text = "密码长度不能少于6位";
                    return;
                }
                if (ConfirmPasswordBox.Password != password)
                {
                    StatusText.Text = "两次输入的密码不一致";
                    return;
                }
            }

            SubmitBtn.IsEnabled = false;
            StatusText.Foreground = System.Windows.Media.Brushes.White;
            StatusText.Text = IsRegisterMode ? "正在注册..." : "正在登录...";
            try
            {
                var result = IsRegisterMode
                    ? await _api.RegisterAsync(username, password, NicknameBox.Text.Trim())
                    : await _api.LoginAsync(username, password);

                if (result.IsSuccess && result.Data != null)
                {
                    Result = result.Data;
                    DialogResult = true;
                }
                else
                {
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 179, 71));
                    StatusText.Text = result.Message;
                }
            }
            finally
            {
                SubmitBtn.IsEnabled = true;
            }
        }

        // 无边框窗口拖拽
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
