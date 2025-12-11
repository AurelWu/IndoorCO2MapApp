using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace IndoorCO2MapAppV2.Spatial
{
    public class CachedLocation
    {
        [PrimaryKey]
        public string Key { get; set; }  

        public string Type { get; set; }
        public long ID { get; set; }

        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
