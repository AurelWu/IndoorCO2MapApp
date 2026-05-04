using SQLite;

namespace IndoorCO2MapAppV2.Spatial
{
    public class CachedTransitLine
    {
        [PrimaryKey]
        public string Key { get; set; } = "";
        public string VehicleType { get; set; } = "";
        public long ID { get; set; }
        public string NWRType { get; set; } = "";
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
