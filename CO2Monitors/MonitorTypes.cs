using IndoorCO2MapAppV2.Enumerations;
using System.Collections.Generic;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    /// <summary>
    /// Defines friendly names and search strings for supported BLE monitors
    /// </summary>
    internal static class MonitorTypes
    {
        // Production: only real monitor types
        internal static readonly Dictionary<CO2MonitorType, string> SearchStringByMonitorType = new()
        {
            { CO2MonitorType.AllMonitors, "All Monitors" }, // Only used for display
            { CO2MonitorType.Aranet4, "Aranet4" },
            { CO2MonitorType.Airvalent, "Airvalent" },
            { CO2MonitorType.InkbirdIAMT1, "IAM-T1" },
            { CO2MonitorType.AirSpotHealth, "Airspot" },
        };

        // Debug: includes "All Devices"
        internal static readonly Dictionary<CO2MonitorType, string> SearchStringByMonitorTypeDebugMode = new()
        {
            { CO2MonitorType.None, "" },
            { CO2MonitorType.AllMonitors, "All Monitors" }, // Only used for display
            { CO2MonitorType.Aranet4, "Aranet4" },
            { CO2MonitorType.Airvalent, "Airvalent" },
            { CO2MonitorType.InkbirdIAMT1, "IAM-T1" },
            { CO2MonitorType.AirSpotHealth, "Airspot"}, 
        };

        internal static readonly Dictionary<string, CO2MonitorType> MonitorTypeBySearchString = new()
        {
            { "Aranet4", CO2MonitorType.Aranet4 },
            { "Airvalent", CO2MonitorType.Airvalent },
            { "IAM-T1", CO2MonitorType.InkbirdIAMT1 },
            { "Airspot", CO2MonitorType.AirSpotHealth }
        };
    }
}