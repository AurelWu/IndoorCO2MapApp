using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    //TODO => Add method to calculate StartIndex of History based on current time, device interval, and start of recording
    internal class AranetProvider : BaseCO2MonitorProvider
    {
        // UUIDs
        private static readonly Guid SERVICE_UUID = Guid.Parse("0000FCE0-0000-1000-8000-00805f9b34fb");
        private static readonly Guid OLD_VERSION_SERVICE_UUID = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");

        private static readonly Guid LIVE_CHARACTERISTICS_UUID = Guid.Parse("f0cd3001-95da-4f4b-9ac8-aa55d312af0c");
        private static readonly Guid TOTAL_READINGS_CHARACTERISTIC_UUID = Guid.Parse("f0cd2001-95da-4f4b-9ac8-aa55d312af0c");
        private static readonly Guid WRITE_CHARACTERISTIC_UUID = Guid.Parse("f0cd1402-95da-4f4b-9ac8-aa55d312af0c");
        private static readonly Guid HISTORY_V2_CHARACTERISTIC_UUID = Guid.Parse("f0cd2005-95da-4f4b-9ac8-aa55d312af0c");

        private IService? _service;
        private ICharacteristic? _liveCharacteristic;
        private ICharacteristic? _totalDataPointsCharacteristic;
        private ICharacteristic? _writerCharacteristic;
        private ICharacteristic? _historyV2Characteristic;

        private static bool IsOldVersion { get; set; }

        //private static string SensorVersion { get; set; } = ""; //we might not need this at all the way we do it now
                
        /// <summary>
        /// Fully initialize the Aranet sensor:
        /// 1. Find correct service
        /// 2. Discover characteristics
        /// </summary>
        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            

            //CO2MonitorManager.Instance.ActiveCO2MonitorProvider = this;
            //if (IsGattValid() == true && errorReadingHappened == false) for Aranet we always just reinitialize as connections get closed aggressively
            // Find new-version service, fall back to old
            _service =
                await TryGetServiceAsync(device, SERVICE_UUID) ??
                await TryGetServiceAsync(device, OLD_VERSION_SERVICE_UUID);

            if (_service == null)
            {
                Debug.WriteLine("Aranet service not found.");
                return false;
            }

            IsOldVersion = _service.Id == OLD_VERSION_SERVICE_UUID;

            return await DiscoverCharacteristicsAsync(_service);
        }

        protected override bool IsGattValid()
        {
            return
                _service != null &&
                _liveCharacteristic != null &&
                _totalDataPointsCharacteristic != null &&
                _writerCharacteristic != null &&
                _historyV2Characteristic != null;
        }

        /// <summary>
        /// Discover all required characteristics in provided service.
        /// </summary>
        private async Task<bool> DiscoverCharacteristicsAsync(IService service)
        {
            _liveCharacteristic =
                await TryGetCharacteristicAsync(service, LIVE_CHARACTERISTICS_UUID);
            _totalDataPointsCharacteristic =
                await TryGetCharacteristicAsync(service, TOTAL_READINGS_CHARACTERISTIC_UUID);
            _writerCharacteristic =
                await TryGetCharacteristicAsync(service, WRITE_CHARACTERISTIC_UUID);
            _historyV2Characteristic =
                await TryGetCharacteristicAsync(service, HISTORY_V2_CHARACTERISTIC_UUID);

            bool ok =
                _liveCharacteristic != null &&
                _totalDataPointsCharacteristic != null &&
                _writerCharacteristic != null &&
                _historyV2Characteristic != null;

            if (!ok)
                Debug.WriteLine("Initialization incomplete: missing one or more characteristics.");
            Debug.WriteLine("Initialization complete: all characteristics found");
            return ok;
        }

        /// <summary>
        /// True when the sensor returned GATT error 2 (READ_NOT_PERMITTED),
        /// which means Smart Home Integration is disabled in the Aranet app.
        /// </summary>
        public bool IsSmartHomeDisabled { get; private set; }

        /// <summary>
        /// Read current CO2 from live characteristic.
        /// </summary>
        protected override async Task<int> DoReadCurrentCO2Async()
        {

            if (_liveCharacteristic == null || !_liveCharacteristic.CanRead)
                return 0;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await _liveCharacteristic.ReadAsync(cts.Token);

                // GATT status 2 = READ_NOT_PERMITTED → Smart Home Integration is disabled
                if (result.resultCode == 2)
                {
                    IsSmartHomeDisabled = true;
                    Logger.WriteToLog("AranetProvider|DoReadCurrentCO2Async: GATT READ_NOT_PERMITTED (2) — Smart Home Integration disabled");
                    return 0;
                }

                IsSmartHomeDisabled = false;
                var data = result.data;

                return data.Length >= 2
                    ? (data[1] << 8) | data[0]
                    : 0;
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("DoReadCurrentCO2Async failed: " + ex.Message, LogMode.Verbose);
                return 0;
            }
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            if (_liveCharacteristic == null || !_liveCharacteristic.CanRead)
                return 0;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await _liveCharacteristic.ReadAsync(cts.Token);
                var data = result.data;

                // Only read if data has enough bytes
                return data.Length >= 11
                    ? (data[10] << 8) | data[9]
                    : 0;
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("DoReadUpdateIntervalAsync failed: " + ex.Message, LogMode.Verbose);
                return 0;
            }
        }


        /// <summary>
        /// Read total number of stored datapoints (if available).
        /// </summary>
        public async Task<int?> ReadTotalDataPointsAsync()
        {
            if (!await EnsureConnectionIsValidAsync())
                return null;

            if (_totalDataPointsCharacteristic == null || !_totalDataPointsCharacteristic.CanRead)
                return null;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await _totalDataPointsCharacteristic.ReadAsync(cts.Token);
                var data = result.data;

                return data.Length >= 2
                    ? (data[1] << 8) | data[0]
                    : null;
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("ReadTotalDataPointsAsync failed: " + ex.Message, LogMode.Verbose);
                return null;
            }
        }

        /// <summary>
        /// Read CO2 history block from sensor. Returns null if anything goes wrong, returns empty array if the sensor returned data but data length is less than 10
        /// </summary>
        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort amountOfMinutes, int sensorUpdateInterval)
        {
            if (!await EnsureConnectionIsValidAsync())
                return null;

            if (_writerCharacteristic == null ||
                _historyV2Characteristic == null ||
                !_writerCharacteristic.CanWrite ||
                !_historyV2Characteristic.CanRead)
            {
                return null;
            }

            try
            {
                // 1. Read total number of data points
                int? totalDataPoints = await ReadTotalDataPointsAsync();
                if (totalDataPoints == null || totalDataPoints == 0)
                    return [];

                int elapsedIntervals = amountOfMinutes;
                if ( sensorUpdateInterval== 120)
                {
                    elapsedIntervals = elapsedIntervals / 2;
                }
                else if (sensorUpdateInterval == 300)
                {
                    elapsedIntervals = elapsedIntervals / 5;
                }
                else if (sensorUpdateInterval == 600)
                {
                    elapsedIntervals = elapsedIntervals / 10;
                }


                // 2. Determine start index (last N points)
                int pointsToRead = elapsedIntervals; // currently using amountOfMinutes as "last N"
                int startIndex = totalDataPoints.Value - pointsToRead;
                if (startIndex < 0)
                    startIndex = 0;

                Debug.WriteLine($"TotalDataPoints: {totalDataPoints}, StartIndex: {startIndex}, PointsToRead: {pointsToRead}");

                if (_writerCharacteristic.WriteType != Plugin.BLE.Abstractions.CharacteristicWriteType.Default)
                    _writerCharacteristic.WriteType = Plugin.BLE.Abstractions.CharacteristicWriteType.Default;

                // Paginated read — Aranet returns one BLE-MTU page per request;
                // keep requesting from the advancing index until all points are fetched.
                var allValues = new List<ushort>();
                int currentIndex = startIndex;
                int remaining = pointsToRead;

                while (remaining > 0)
                {
                    byte[] packet = CreateCO2HistoryRequestPacket((ushort)currentIndex);

                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await _writerCharacteristic.WriteAsync(packet, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"WriteAsync failed: {ex.Message}");
                        break;
                    }

                    await Task.Delay(35);

                    using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var pageResult = await _historyV2Characteristic.ReadAsync(readCts.Token);
                    var data = pageResult.data;

                    if (data.Length < 10) break;

                    byte count = data[9];
                    if (count == 0) break;

                    for (int i = 0; i < count; i++)
                        allValues.Add(BitConverter.ToUInt16(data, 10 + i * 2));

                    currentIndex += count;
                    remaining   -= count;
                }

                return allValues.Count > 0 ? allValues.ToArray() : [];
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("DoReadHistoryAsync failed: " + ex.Message, LogMode.Verbose);
                return null;
            }
        }


        /// <summary>
        /// Create CO2 history request packet.
        /// </summary>
        public static byte[] CreateCO2HistoryRequestPacket(ushort start)
        {
            using var memoryStream = new MemoryStream();
            byte header = 0x61;
            byte co2ID = 0x04;
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(header);       // Write 1 byte
                binaryWriter.Write(co2ID);   // Write 1 byte
                binaryWriter.Write(start);        // Write 2 bytes (little-endian by default) //
            }

            byte[] data = memoryStream.ToArray();

            //System.Diagnostics.Debug.WriteLine("Sent data: " + BitConverter.ToString(data));
            return memoryStream.ToArray();
        }
    }
}
