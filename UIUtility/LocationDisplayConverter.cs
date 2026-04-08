using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IndoorCO2MapAppV2.UIUtility
{
    public class LocationDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LocationData loc)
                return "";

            string name = loc.Name ?? "(no name)";
            bool isFav = UserSettings.Instance.FavouriteLocationKeys.Contains(loc.FavouriteKey);
            string prefix = isFav ? "★ " : "";
            return $"{prefix}{name} — {loc.Distance:F0} m";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
