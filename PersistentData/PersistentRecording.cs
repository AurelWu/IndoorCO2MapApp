using System;
using System.Collections.Generic;
using System.Text;
using IndoorCO2MapAppV2.Enumerations;
using SQLite;

namespace IndoorCO2MapAppV2.PersistentData
{

    // right now we have way too many different Recording classes ... we need to clean that up at some point or at least just have it as derived classes whereever differences appear
    public class PersistentRecording
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public long DateTime { get; set; }
        public string LocationName { get; set; } = "";

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public long? NWRId { get; set; }
        public string NWRType { get; set; } = "";

        public double AvgCO2 { get; set; }
        public string Values { get; set; } = "";

        // Context metadata — SQLite-net adds these columns automatically on first run
        public TriState DoorWindowState { get; set; }
        public TriState VentilationState { get; set; }
        public string CustomNotes { get; set; } = "";
        public string SensorType { get; set; } = "";
    }
}
