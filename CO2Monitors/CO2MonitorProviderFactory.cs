using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

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

        /// <summary>
        /// Detects CO2 monitor type using device name and advertisement UUIDs (scan-time only).
        /// No GATT operations are performed — safe to call during BLE scanning.
        /// </summary>
        public static Task<CO2MonitorType?> DetectFromNameOrAdvertisementAsync(
            IDevice device,
            IAdapter? bleAdapter = null)
        {
            // 1. Name-based detection
            var byName = DetectFromName(device.Name);
            if (byName != null)
                return Task.FromResult<CO2MonitorType?>(byName);

            // 2. Advertisement service UUIDs (scan-time)
            var advertisedServices = device.AdvertisementRecords?
                .Where(r => r.Type == AdvertisementRecordType.UuidsComplete128Bit ||
                            r.Type == AdvertisementRecordType.UuidsIncomplete128Bit)
                .SelectMany(r => r.Data.To128BitGuids())
                .ToList();

            if (advertisedServices != null)
            {
                foreach (var service in advertisedServices)
                {
                    if (MonitorTypes.MonitorTypeByAdvertisementServiceUuid.TryGetValue(service, out var type))
                        return Task.FromResult<CO2MonitorType?>(type);
                }
            }

            return Task.FromResult<CO2MonitorType?>(null);
        }
    }
}
