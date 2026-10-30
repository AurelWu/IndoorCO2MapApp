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

        // Added later: sqlite-net's CreateTable adds the column to existing databases, and rows
        // cached before that simply have no category.
        public string? Category { get; set; }
    }
}
