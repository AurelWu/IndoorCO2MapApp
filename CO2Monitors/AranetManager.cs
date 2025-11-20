using System;
using System.IO;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AranetManager
    {
        // Service and characteristic UUIDs
        public static readonly Guid SERVICE_UUID = Guid.Parse("0000FCE0-0000-1000-8000-00805f9b34fb");
        public static readonly Guid OLD_VERSION_SERVICE_UUID = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");

        public static readonly Guid LIVE_CHARACTERISTICS_UUID = Guid.Parse("f0cd3001-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid TOTAL_READINGS_CHARACTERISTIC_UUID = Guid.Parse("f0cd2001-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid WRITE_CHARACTERISTIC_UUID = Guid.Parse("f0cd1402-95da-4f4b-9ac8-aa55d312af0c");
        public static readonly Guid HISTORY_V2_CHARACTERISTIC_UUID = Guid.Parse("f0cd2005-95da-4f4b-9ac8-aa55d312af0c");

        public static readonly Guid VERSION_SERVICE_UUID = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
        public static readonly Guid VERSIONNUMBER_CHARACTERISTIC_UUID = Guid.Parse("00002a26-0000-1000-8000-00805f9b34fb");

        public static string SensorVersion { get; set; } = "";
        public static bool IsOldVersion { get; set; } = false;

        public ICharacteristic? LiveCharacteristic { get; private set; }
        public ICharacteristic? TotalDataPointsCharacteristic { get; private set; }
        public ICharacteristic? WriterCharacteristic { get; private set; }
        public ICharacteristic? HistoryV2Characteristic { get; private set; }

        private const int RetryCount = 3;
        private const int RetryDelayMs = 100;

        /// <summary>
        /// Initialize Aranet characteristics asynchronously with retry logic.
        /// </summary>
        public async Task<bool> InitializeAsync(IService service)
        {
            if (service == null)
            {
                Console.WriteLine("Service is null. Cannot initialize AranetManager.");
                return false;
            }

            // Fetch characteristics with retries
            LiveCharacteristic = await TryGetCharacteristicAsync(service, LIVE_CHARACTERISTICS_UUID);
            if (LiveCharacteristic == null) Console.WriteLine("Warning: Live characteristic not found.");

            TotalDataPointsCharacteristic = await TryGetCharacteristicAsync(service, TOTAL_READINGS_CHARACTERISTIC_UUID);
            if (TotalDataPointsCharacteristic == null) Console.WriteLine("Warning: Total data points characteristic not found.");

            WriterCharacteristic = await TryGetCharacteristicAsync(service, WRITE_CHARACTERISTIC_UUID);
            if (WriterCharacteristic == null) Console.WriteLine("Warning: Writer characteristic not found.");

            HistoryV2Characteristic = await TryGetCharacteristicAsync(service, HISTORY_V2_CHARACTERISTIC_UUID);
            if (HistoryV2Characteristic == null) Console.WriteLine("Warning: History V2 characteristic not found.");

            // Return true only if all characteristics were found
            bool allFound = LiveCharacteristic != null &&
                            TotalDataPointsCharacteristic != null &&
                            WriterCharacteristic != null &&
                            HistoryV2Characteristic != null;

            if (!allFound)
                Console.WriteLine("Initialization incomplete: not all characteristics were found.");

            return allFound;
        }

        /// <summary>
        /// Retry getting characteristic multiple times to handle transient BLE issues.
        /// </summary>
        private static async Task<ICharacteristic?> TryGetCharacteristicAsync(IService service, Guid uuid)
        {
            for (int i = 0; i < RetryCount; i++)
            {
                var characteristic = await service.GetCharacteristicAsync(uuid);
                if (characteristic != null) return characteristic;
                await Task.Delay(RetryDelayMs);
            }
            return null;
        }

        /// <summary>
        /// Read current CO2 value. Returns null if characteristic unavailable or read fails.
        /// </summary>
        public async Task<int?> ReadCO2Async()
        {
            if (LiveCharacteristic == null || !LiveCharacteristic.CanRead) return null;

            var result = await LiveCharacteristic.ReadAsync();
            var data = result.data;
            if (data.Length >= 2)
                return (data[1] << 8) | data[0];

            return null;
        }

        /// <summary>
        /// Read total data points from the sensor. Returns null if unavailable.
        /// </summary>
        public async Task<int?> ReadTotalDataPointsAsync()
        {
            if (TotalDataPointsCharacteristic == null || !TotalDataPointsCharacteristic.CanRead) return null;

            var result = await TotalDataPointsCharacteristic.ReadAsync();
            var data = result.data;
            if (data.Length >= 2)
                return (data[1] << 8) | data[0];

            return null;
        }

        /// <summary>
        /// Read history V2 from sensor starting at startIndex. Returns empty array if unavailable.
        /// </summary>
        public async Task<ushort[]> ReadHistoryV2Async(ushort startIndex)
        {
            if (WriterCharacteristic == null || HistoryV2Characteristic == null) return [];
            if (!WriterCharacteristic.CanWrite || !HistoryV2Characteristic.CanRead) return [];

            byte[] requestData = CreateCO2HistoryRequestPacket(startIndex);

            await WriterCharacteristic.WriteAsync(requestData);
            await Task.Delay(35); // give sensor time

            var result = await HistoryV2Characteristic.ReadAsync();
            var data = result.data;

            if (data.Length < 10) return [];

            byte count = data[9];
            ushort[] co2Values = new ushort[count];
            for (int i = 0; i < count; i++)
                co2Values[i] = BitConverter.ToUInt16(data, 10 + i * 2);

            return co2Values;
        }

        /// <summary>
        /// Create CO2 history request packet for Aranet4 sensor.
        /// </summary>
        public static byte[] CreateCO2HistoryRequestPacket(ushort startIndex)
        {
            using MemoryStream memoryStream = new();
            byte header = 0x61;
            byte co2ID = 0x04;

            using (BinaryWriter binaryWriter = new(memoryStream))
            {
                binaryWriter.Write(header);       // 1 byte
                binaryWriter.Write(co2ID);        // 1 byte
                binaryWriter.Write(startIndex);   // 2 bytes, little-endian
            }

            return memoryStream.ToArray();
        }
    }
}
