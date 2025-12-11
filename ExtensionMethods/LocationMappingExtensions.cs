using IndoorCO2MapAppV2.Spatial;

namespace IndoorCO2MapAppV2.ExtensionMethods
{
    public static class LocationMappingExtensions
    {
        public static LocationData ToLocationData(this CachedLocation c, double userLat, double userLon)
        {
            return new LocationData(
                c.Type,
                c.ID,
                c.Name,
                c.Latitude,
                c.Longitude,
                userLat,
                userLon
            );
        }
    }
}