using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinPE_Client.Helpers
{
    public class ViewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString() == parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToConnectTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "已连接" : "连接";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "#E8F5E9" : "#FFF3E0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>分区方案单选：int == 0（自动分区）</summary>
    public class IndexZeroToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i == 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? 0 : Binding.DoNothing;
    }

    /// <summary>分区方案单选：int == 1（保留现有分区）</summary>
    public class IndexOneToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i == 1;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? 1 : Binding.DoNothing;
    }

    /// <summary>分区方案单选：int == 2（自定义分区）</summary>
    public class IndexTwoToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i == 2;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? 2 : Binding.DoNothing;
    }

    /// <summary>字符串颜色（#RRGGBB / #AARRGGBB）转画刷（用于致命/警告等状态色）</summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var hex = s.TrimStart('#');
                    if (hex.Length == 6)
                    {
                        byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                        byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                        byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, r, g, b));
                    }
                    if (hex.Length == 8)
                    {
                        byte a = System.Convert.ToByte(hex.Substring(0, 2), 16);
                        byte r = System.Convert.ToByte(hex.Substring(2, 2), 16);
                        byte g = System.Convert.ToByte(hex.Substring(4, 2), 16);
                        byte b = System.Convert.ToByte(hex.Substring(6, 2), 16);
                        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
                    }
                }
                catch { }
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}