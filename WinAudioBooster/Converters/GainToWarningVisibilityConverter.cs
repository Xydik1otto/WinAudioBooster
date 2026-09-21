using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinAudioBooster.Converters
{
    /// <summary>
    /// Відображає попередження про високий Gain, якщо значення перевищує порог (10 dB).
    /// </summary>
    public class GainToWarningVisibilityConverter : IValueConverter
    {
        private const double WarningThresholdDb = 10.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double gain)
            {
                return gain > WarningThresholdDb
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}