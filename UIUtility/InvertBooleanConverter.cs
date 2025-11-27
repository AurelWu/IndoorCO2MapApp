using System;
using System.Globalization;

namespace IndoorCO2MapAppV2.UIUtility
{
    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)

        {
            throw new NotImplementedException();
        }
    }
}