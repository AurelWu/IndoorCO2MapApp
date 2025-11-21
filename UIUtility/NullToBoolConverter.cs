using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IndoorCO2MapAppV2.UIUtility
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value != null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
