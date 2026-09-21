using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinAudioBooster.Converters
{
    public class GainToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
            {
                if (percent <= 100.0)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")); // Зелений (0-100%)
                
                if (percent <= 200.0)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")); // Помаранчевий (101-200%)

                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")); // Червоний (>200%)
            }

            return new SolidColorBrush(Colors.ForestGreen);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}