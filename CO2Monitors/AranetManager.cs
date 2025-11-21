using IndoorCO2MapAppV2.Bluetooth;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    //TODO => Add method to calculate StartIndex of History based on current time, device interval, and start of recording
    internal class AranetManager : BaseCO2MonitorManager
    {
        // UUIDs
        public static readonly Guid SERVICE_UUID = Guid.Parse("0000FCE0-0000-1000-8000-00805f9b34fb");
        public static readonly Guid OLD_VERSION_SERVICE_UUID = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");

        public static readonly Guid LIVE_CHARACTERISTICS_UUID = Guid.Parse("f0cd3001-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid TOTAL_READINGS_CHARACTERISTIC_UUID = Guid.Parse("f0cd2001-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid WRITE_CHARACTERISTIC_UUID = Guid.Parse("f0cd1402-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid HISTORY_V2_CHARACTERISTIC_UUID = Guid.Parse("f0cd2005-95da-4f4b-9ac8-aa55d312af0c");

        public ICharacteristic? LiveCharacteristic { get; private set; }
        public ICharacteristic? TotalDataPointsCharacteristic { get; private set; }
        public ICharacteristic? WriterCharacteristic { get; private set; }
        public ICharacteristic? HistoryV2Characteristic { get; private set; }

        public static bool IsOldVersion { get; private set; }
        public static string SensorVersion { get; private set; } = "";

        public static IDevice? ActiveDevice { get; private set; }

        /// <summary>
        /// Fully initialize the Aranet sensor:
        /// 1. Find correct service
        /// 2. Discover characteristics
        /// </summary>
        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            BLEDeviceManager.ActiveMonitorManager = this;
            // Find new-version service, fall back to old
            var service =
                await TryGetServiceAsync(device, SERVICE_UUID) ??
                await TryGetServiceAsync(device, OLD_VERSION_SERVICE_UUID);

            if (service == null)
            {
                Console.WriteLine("Aranet service not found.");
                return false;
            }

            IsOldVersion = service.Id == OLD_VERSION_SERVICE_UUID;

            return await DiscoverCharacteristicsAsync(service);
        }

        /// <summary>
        /// Discover all required characteristics in provided service.
        /// </summary>
        private async Task<bool> DiscoverCharacteristicsAsync(IService service)
        {
            LiveCharacteristic =
                await TryGetCharacteristicAsync(service, LIVE_CHARACTERISTICS_UUID);
            TotalDataPointsCharacteristic =
                await TryGetCharacteristicAsync(service, TOTAL_READINGS_CHARACTERISTIC_UUID);
            WriterCharacteristic =
                await TryGetCharacteristicAsync(service, WRITE_CHARACTERISTIC_UUID);
            HistoryV2Characteristic =
                await TryGetCharacteristicAsync(service, HISTORY_V2_CHARACTERISTIC_UUID);

            bool ok =
                LiveCharacteristic != null &&
                TotalDataPointsCharacteristic != null &&
                WriterCharacteristic != null &&
                HistoryV2Characteristic != null;

            if (!ok)
                Console.WriteLine("Initialization incomplete: missing one or more characteristics.");
            Console.WriteLine("Initialization complete: all characteristics found");
            return ok;
        }

        /// <summary>
        /// Read current CO2 from live characteristic.
        /// </summary>
        public override async Task<int> ReadCurrentCO2Async()
        {
            if (LiveCharacteristic == null || !LiveCharacteristic.CanRead)
                return 0;

            var result = await LiveCharacteristic.ReadAsync();

            var data = result.data;
            return data.Length >= 2 ? (data[1] << 8) | data[0] : 0;
        }

        /// <summary>
        /// Read total number of stored datapoints (if available).
        /// </summary>
        public async Task<int?> ReadTotalDataPointsAsync()
        {
            if (TotalDataPointsCharacteristic == null || !TotalDataPointsCharacteristic.CanRead)
                return null;

            var result = await TotalDataPointsCharacteristic.ReadAsync();
            var data = result.data;
            return data.Length >= 2 ? (data[1] << 8) | data[0] : null;
        }

        /// <summary>
        /// Read CO2 history block from sensor.
        /// </summary>
        public override async Task<ushort[]> ReadHistoryAsync(ushort startIndex)
        {
            if (WriterCharacteristic == null || HistoryV2Characteristic == null)
                return [];

            if (!WriterCharacteristic.CanWrite || !HistoryV2Characteristic.CanRead)
                return [];

            byte[] packet = CreateCO2HistoryRequestPacket(startIndex);
            await WriterCharacteristic.WriteAsync(packet);

            await Task.Delay(35); // give sensor time

            var result = await HistoryV2Characteristic.ReadAsync();
            var data = result.data;

            if (data.Length < 10)
                return [];

            byte count = data[9];
            ushort[] values = new ushort[count];

            for (int i = 0; i < count; i++)
                values[i] = BitConverter.ToUInt16(data, 10 + i * 2);

            return values;
        }

        /// <summary>
        /// Create CO2 history request packet.
        /// </summary>
        public static byte[] CreateCO2HistoryRequestPacket(ushort startIndex)
        {
            using var memoryStream = new MemoryStream();
            byte header = 0x61;
            byte co2ID = 0x04;
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(header);       // Write 1 byte
                binaryWriter.Write(co2ID);   // Write 1 byte
                binaryWriter.Write(startIndex);        // Write 2 bytes (little-endian by default)
            }

            byte[] data = memoryStream.ToArray();

            //System.Diagnostics.Debug.WriteLine("Sent data: " + BitConverter.ToString(data));
            return memoryStream.ToArray();
        }


    }
}
