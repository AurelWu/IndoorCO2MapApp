using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.UIUtility
{
    using System;
    using Microsoft.Maui.Controls;

    public class UnixTimeToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long unixTime)
            {
                // Unix time is seconds since 1970-01-01
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            else if (value is DateTime dt)
            {
                return dt.ToString("yyyy-MM-dd HH:mm");
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
