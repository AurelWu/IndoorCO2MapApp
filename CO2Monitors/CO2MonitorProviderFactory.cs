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
        /// Detects CO2 monitor type using name, advertisement UUIDs, or GATT services (after connection)
        /// </summary>
        /// <param name="device">The connected or scanned BLE device</param>
        /// <param name="bleAdapter">Plugin.BLE adapter, needed only if checking GATT services</param>
        /// <returns>The detected CO2 monitor type, or null if unknown</returns>
        public static async Task<CO2MonitorType?> DetectFromNameOrAdvertisementAsync(
    IDevice device,
    IAdapter? bleAdapter = null)
        {
            // 1️⃣ Name-based detection
            var byName = DetectFromName(device.Name);
            if (byName != null)
                return byName;

            // 2️⃣ Advertisement service UUIDs (scan-time)
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
                        return type;
                }
            }

            if (device.State != DeviceState.Connected)
            {
                Logger.WriteToLog($"Device not connected: {device.Name}");
                return null;
            }

            if (bleAdapter != null)
            {
                try
                {

                    var gattServices = await device.GetServicesAsync();
                    foreach (var service in gattServices)
                    {
                        if (MonitorTypes.MonitorTypeByServiceUuid.TryGetValue(service.Id, out var type))
                            return type;
                    }

                }
                catch (Exception ex)
                {
                    Logger.WriteToLog("Exception during CO2monitorProviderFactory when trying to get Services of Device for identification | Device Name: " + device.Name ?? "unkown devicee");
                }
            }

            return null;
        }
    }
}
