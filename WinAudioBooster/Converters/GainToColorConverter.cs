using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinAudioBooster.Converters
{
    /// <summary>
    /// Змінює колір відображення Gain залежно від рівня підсилення:
    /// 0–6 dB   → зелений
    /// 6–12 dB  → помаранчевий
    /// 12–20 dB → червоний
    /// </summary>
    public class GainToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double gain)
            {
                if (gain <= 6)
                {
                    return new SolidColorBrush(Colors.ForestGreen);
                }
                if (gain <= 12)
                {
                    return new SolidColorBrush(Colors.DarkOrange);
                }
                return new SolidColorBrush(Colors.Crimson);
            }

            return new SolidColorBrush(Colors.ForestGreen);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}