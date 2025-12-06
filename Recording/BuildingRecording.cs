using IndoorCO2MapAppV2.CO2Monitors;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.Recording
{
    public class BuildingRecording
    {
        public string CO2MonitorType { get; set; } = "";
        public long NwrId { get; set; }
        public string NwrType { get; set; } = "";
        public long RecordingStart { get; set; }

        public List<CO2Reading> MeasurementData { get; set; } = new();
        public Dictionary<string, string> AdditionalDataByParameter { get; set; } = new();
    }
}
