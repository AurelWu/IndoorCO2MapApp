using IndoorCO2MapAppV2.Enumerations;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal static class CO2MonitorProviderFactory
    {
        public static BaseCO2MonitorProvider? CreateProvider(CO2MonitorType type)
        {
            return type switch
            {
                CO2MonitorType.Aranet4 => new AranetProvider(),
                CO2MonitorType.Airvalent => new AirvalentProvider(),
                CO2MonitorType.InkbirdIAMT1 => new InkbirdProvider(),
                CO2MonitorType.AirSpotHealth => new AirspotProvider(),
                _ => null
            };
        }

        public static CO2MonitorType? DetectFromName(string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return null;

            foreach (var kv in MonitorTypes.MonitorTypeBySearchString)
            {
                if (deviceName.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            return null;
        }
    }
}
