using System.Collections.Generic;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    /// <summary>
    /// Defines friendly names and search strings for supported BLE monitors
    /// </summary>
    public static class MonitorTypes
    {
        // Production: only real monitor types
        public static readonly Dictionary<string, string> SearchStringByMonitorType = new()
        {
            { "Aranet4", "Aranet4" },
            { "Airvalent", "Airvalent" },
            { "Inkbird IAM-T1", "IAM-T1" },
            { "Airspot Health", "Airspot" }
        };

        // Debug: includes "All Devices"
        public static readonly Dictionary<string, string> SearchStringByMonitorTypeDebugMode = new()
        {
            { "All Devices", "" },
            { "Aranet4", "Aranet4" },
            { "Airvalent", "Airvalent" },
            { "Inkbird IAM-T1", "IAM-T1" },
            { "Airspot Health", "Airspot" }
        };
    }
}