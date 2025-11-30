using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.UIUtility
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            //TODO: Color-Impaired friendly option
            return (value is bool b && b) ? Colors.Green : Colors.Red; 
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
