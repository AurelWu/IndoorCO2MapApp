using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using System;
using System.Globalization;
using System.Linq;

namespace IndoorCO2MapAppV2.UIUtility
{
    public class TransitRouteDisplayConverter : IValueConverter
    {
        public static double CurrentSearchLat { get; set; }
        public static double CurrentSearchLon { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TransitLineData route)
                return "";

            double rLat = Math.Round(CurrentSearchLat, 2);
            double rLon = Math.Round(CurrentSearchLon, 2);
            bool isFav = UserSettings.Instance.FavouriteTransitRoutes
                .Any(f => f.RouteId == route.ID && f.Lat == rLat && f.Lon == rLon);
            return isFav ? $"★ {route.ShortenedName}" : route.ShortenedName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
