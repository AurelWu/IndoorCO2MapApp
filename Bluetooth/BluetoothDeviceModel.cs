using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions.Contracts;
using System.Text;

namespace IndoorCO2MapAppV2.Bluetooth
{
    public record BluetoothDeviceModel(IDevice Device)
    {
        public string Name => Device.Name ?? "Unknown";
        public string Id => Device.Id.ToString();

        /// <summary>Set by BLEDeviceManager after detection; used for display only.</summary>
        public CO2MonitorType? DetectedType { get; set; }

        /// <summary>Human-readable label: prepends a friendly type name when the device name is cryptic.</summary>
        public string DisplayName
        {
            get
            {
                string raw = Device.Name ?? "Unknown";
                if (DetectedType is CO2MonitorType t &&
                    MonitorTypes.SearchStringByMonitorType.TryGetValue(t, out var label) &&
                    !raw.Contains(label, StringComparison.OrdinalIgnoreCase))
                    return $"{label} ({raw})";
                return raw;
            }
        }
        public int Rssi => Device.Rssi;


        /// <summary>
        /// Returns a formatted string of all advertisement records
        /// </summary>
        public string AdvertisementData
        {
            get
            {
                if (Device.AdvertisementRecords == null || !Device.AdvertisementRecords.Any())
                    return "(no advertisement data)";

                var sb = new StringBuilder();

                foreach (var record in Device.AdvertisementRecords)
                {
                    sb.AppendLine($"{record.Type}: {BitConverter.ToString(record.Data)}");

                    // Optional: decode some common fields
                    if (record.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.ManufacturerSpecificData)
                    {
                        sb.AppendLine("  -> Manufacturer specific data: " + BitConverter.ToString(record.Data));
                    }
                    else if (record.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.UuidsComplete16Bit ||
                             record.Type == Plugin.BLE.Abstractions.AdvertisementRecordType.UuidsComplete128Bit)
                    {
                        sb.AppendLine("  -> Service UUIDs: " + BitConverter.ToString(record.Data));
                    }
                }

                return sb.ToString();
            }
        }
    }
}
