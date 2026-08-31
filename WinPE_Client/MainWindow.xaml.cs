using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        // 窗口加载完成：启动炫彩动效 → 自动连接服务器 → 自注册 → 检测待执行任务 → 启动心跳（WinPE 续装闭环）
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartBackgroundAnimation();
            if (Vm != null)
                await Vm.Initialize();
        }

        /// <summary>
        /// 启动动态炫彩背景：5 个彩色光斑缓慢漂移 + 主渐变色持续流动 + 星尘粒子闪烁 + 入口按钮呼吸光晕 + 中央内容入场动画。
        /// </summary>
        private void StartBackgroundAnimation()
        {
            // 5 个彩色光斑各自沿 X/Y 不同周期正弦往返，组合出不规则漂移轨迹
            AnimateOrb(Orb1Trans, 46, -34, 7, 9);
            AnimateOrb(Orb2Trans, -52, 38, 9, 7);
            AnimateOrb(Orb3Trans, 30, 48, 8, 10);
            AnimateOrb(Orb4Trans, -36, -44, 11, 6);
            AnimateOrb(Orb5Trans, 42, 32, 6, 12);

            // 主渐变底色缓慢呼吸：双色对向流动，让整个背景“活”起来
            var flow = new ColorAnimation
            {
                From = Color.FromRgb(0x15, 0x23, 0x3F),
                To = Color.FromRgb(0x2E, 0x5A, 0xAC),
                Duration = TimeSpan.FromSeconds(9),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            BgStop0.BeginAnimation(GradientStop.ColorProperty, flow);
            var flow2 = new ColorAnimation
            {
                From = Color.FromRgb(0x2E, 0x5A, 0xAC),
                To = Color.FromRgb(0x1F, 0x3A, 0x75),
                Duration = TimeSpan.FromSeconds(9),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            BgStop2.BeginAnimation(GradientStop.ColorProperty, flow2);

            // 4 个入口按钮的呼吸光晕：从中心缓缓扩散并淡出，营造“脉冲”活力感
            AnimateHalo(HaloInstall, HaloInstallScale, 2.6);
            AnimateHalo(HaloUsb, HaloUsbScale, 2.9);
            AnimateHalo(HaloTools, HaloToolsScale, 3.2);
            AnimateHalo(HaloSoft, HaloSoftScale, 2.4);

            // 星尘粒子：散布的细小光点缓慢漂移 + 明暗闪烁
            CreateParticles();

            // 中央内容入场动画：淡入 + 上浮，柔和不呆板
            ContentPanel.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.55))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ContentPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            var rise = new DoubleAnimation(26, 0, TimeSpan.FromSeconds(0.6))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ContentTrans.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        /// <summary>让指定光斑在 X/Y 两个方向各自做慢速正弦往返漂移（时长不同 → 轨迹不规则）</summary>
        private static void AnimateOrb(TranslateTransform trans, double toX, double toY, double secondsX, double secondsY)
        {
            var animX = new DoubleAnimation(trans.X, toX, TimeSpan.FromSeconds(secondsX))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var animY = new DoubleAnimation(trans.Y, toY, TimeSpan.FromSeconds(secondsY))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            trans.BeginAnimation(TranslateTransform.XProperty, animX);
            trans.BeginAnimation(TranslateTransform.YProperty, animY);
        }

        /// <summary>让入口按钮的圆形光晕做“脉冲扩散”动画：缓缓放大同时淡出，循环往复</summary>
        private static void AnimateHalo(System.Windows.Shapes.Ellipse halo, ScaleTransform scale, double seconds)
        {
            var duration = TimeSpan.FromSeconds(seconds);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var scaleAnim = new DoubleAnimation(1.0, 1.4, duration)
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            var fadeAnim = new DoubleAnimation(1.0, 0.0, duration)
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };
            halo.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        /// <summary>
        /// 生成一批星尘粒子：细小光点随机散布，各自缓慢漂移并做明暗闪烁（呼吸感），
        /// 全部异步错峰启动，营造夜空星光的“活”感。
        /// </summary>
        private void CreateParticles()
        {
            // 固定种子保证每次启动布局一致，避免随机抖动
            var palette = new[]
            {
                Color.FromRgb(0x7E, 0xB8, 0xFF),
                Color.FromRgb(0x9C, 0x6B, 0xFF),
                Color.FromRgb(0x4F, 0xCE, 0xA8),
                Color.FromRgb(0xFF, 0xB8, 0x6C),
                Color.FromRgb(0xFF, 0x7B, 0xB0),
                Color.FromRgb(0xE6, 0xF0, 0xFF)
            };
            var rnd = new Random(20260831);
            for (int i = 0; i < 20; i++)
            {
                var x = 20 + rnd.NextDouble() * 900;
                var y = 20 + rnd.NextDouble() * 580;
                var size = 2.5 + rnd.NextDouble() * 4;
                var color = palette[rnd.Next(palette.Length)];

                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(color),
                    Opacity = 0.4 + rnd.NextDouble() * 0.4,
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
                };
                var drift = new TranslateTransform(0, 0);
                dot.RenderTransform = drift;
                System.Windows.Controls.Canvas.SetLeft(dot, x);
                System.Windows.Controls.Canvas.SetTop(dot, y);
                ParticleLayer.Children.Add(dot);

                // 漂移：小幅往返
                var dx = (rnd.NextDouble() - 0.5) * 40;
                var dy = (rnd.NextDouble() - 0.5) * 30;
                var driftSec = 6 + rnd.NextDouble() * 6;
                AnimateOrb(drift, dx, dy, driftSec, driftSec + 1.5);

                // 闪烁：明暗呼吸（错峰 BeginTime）
                var twinkle = new DoubleAnimation(dot.Opacity, 0.05 + rnd.NextDouble() * 0.15, TimeSpan.FromSeconds(2.5 + rnd.NextDouble() * 3))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                    BeginTime = TimeSpan.FromMilliseconds(rnd.Next(0, 1200))
                };
                dot.BeginAnimation(UIElement.OpacityProperty, twinkle);
            }
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
