using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
   


    internal class AirvalentProvider : BaseCO2MonitorProvider
    {
        private static readonly Guid AirvalentServiceUUID = Guid.Parse("B81C94A4-6B2B-4D41-9357-0C8229EA02DF");
        private static readonly Guid AirvalentUpdateIntervalCharacteristic = Guid.Parse("b1c48eea-4f5c-44f7-9797-73e0ce294881");
        private static readonly Guid AirvalentHistoryCharacteristic = Guid.Parse("426d4fa2-50ea-4a8d-b88c-c58b3e78f857");
        private static readonly Guid AirvalentHistoryPointerCharacteristic = Guid.Parse("cdbde84d-2dc6-46e4-8d6b-f3ababf560aa");
        private static readonly Guid AirvalentDataChunkCountCharacteristic = Guid.Parse("a6cf90e4-7ec0-46b2-a90a-5c2580f85a43"); // 0 => just current data in History Characteristic is available (not sure yet what happens if we set the airvalentHistoryPointerCharacteristic to a value where no data is yet? does it return old data / random data / crash / ignore? 
        //↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑↑
        //First 8 Bytes are some headers/meta data, after that 8 Bytes per minute: Layout:
        //14 Bit: CO2 Value
        //10 Bit: Temperature 
        //24 Bit: unknown / probably Humidity and something else? or unused?)
        //16 Bit: Timer/Counter, Byte 6 increments by 15 every minute, Byte 7 increases whenever Byte 7 overflows (maybe Bit 5 is also timer/counter?)

        private IService? _service;
        ICharacteristic? _airValentUpdateInterval;
        ICharacteristic? _airValentHistory;
        ICharacteristic? _airValentHistoryPointer;
        ICharacteristic? _airValentChunkCounter;
        private int _latestCO2FromHistory = -1;



        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            //CO2MonitorManager.Instance.ActiveCO2MonitorProvider = this;

            _service = await TryGetServiceAsync(device, AirvalentServiceUUID); 

            if (_service == null)
            {
                Debug.WriteLine("Airvalent service not found.");
                return false;
            }

            return await DiscoverCharacteristicsAsync(_service);
        }

        protected override async Task<int> DoReadCurrentCO2Async()
        {
            // After a history read the pointer is left pointing at an old chunk, so direct
            // reads return a stuck historical value. Prefer the freshest value extracted
            // during the last DoReadHistoryAsync call if one exists.
            if (_latestCO2FromHistory > 0) return _latestCO2FromHistory;

            if (!IsGattValid()) return -1;

            try
            {
                // Fallback: used only before the first history read (pointer not yet set)
                var reply = await _airValentHistory.ReadAsync();
                var bytes = reply.data.ToList();

                if (bytes.Count <= 8) return -1;

                bytes.RemoveRange(0, 8);

                int lastIndex = bytes.Count - 8;
                byte co2byte2shift = (byte)(bytes[lastIndex + 1] << 2);
                co2byte2shift = (byte)(co2byte2shift >> 2);
                byte[] co2bytes = new byte[2] { bytes[lastIndex + 0], co2byte2shift };
                ushort co2Value = BitConverter.ToUInt16(co2bytes, 0);

                return co2Value;
            }
            catch
            {
                return -1;
            }
        }

        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort amountOfMinutes, int sensorUpdateInterval)
        {
            if (!IsGattValid()) return null;

            try
            {
                // 1️ Read chunk count
                var chunkReply = await _airValentChunkCounter.ReadAsync();
                byte[] chunkBytes = chunkReply.data;
                ushort chunkCount = BitConverter.ToUInt16(chunkBytes, 0);

                // 2️ Set history pointer if chunks exist
                if (chunkCount > 0 && _airValentHistoryPointer != null)
                {
                    var msg = AirvalentSetHistoryPointerMsgData();
                    await _airValentHistoryPointer.WriteAsync(msg);
                }

                // 3️ Read history chunks
                var historyBytesList = new List<byte>();
                for (int i = 0; i < (chunkCount > 0 ? 2 : 1); i++)
                {
                    var reply = await _airValentHistory.ReadAsync();
                    var bytes = reply.data.ToList();
                    if (bytes.Count > 8) bytes.RemoveRange(0, 8); // skip header
                    historyBytesList.AddRange(bytes);
                }

                // 4️ Convert to CO2 values
                var co2Values = new List<ushort>();
                for (int i = 0; i < historyBytesList.Count; i += 8)
                {
                    byte co2byte2shift = (byte)(historyBytesList[i + 1] << 2);
                    co2byte2shift = (byte)(co2byte2shift >> 2);
                    byte[] co2bytes = new byte[2] { historyBytesList[i + 0], co2byte2shift };
                    co2Values.Add(BitConverter.ToUInt16(co2bytes, 0));
                }

                // 5️ Calculate elapsed intervals based on sensorUpdateInterval
                int elapsedIntervals = amountOfMinutes;
                switch (sensorUpdateInterval)
                {
                    case 120:
                        elapsedIntervals /= 2;
                        break;
                    case 300:
                        elapsedIntervals /= 5;
                        break;
                    case 600:
                        elapsedIntervals /= 10;
                        break;
                    case 900:
                        elapsedIntervals /= 15;
                        break;
                }

                if (elapsedIntervals <= 0) elapsedIntervals = 1;

                // 6️ Take the last N values
                if (elapsedIntervals > co2Values.Count)
                    elapsedIntervals = co2Values.Count;

                var result = co2Values.Skip(co2Values.Count - elapsedIntervals).Take(elapsedIntervals).ToArray();
                if (result.Length > 0) _latestCO2FromHistory = result[result.Length - 1];
                return result;
            }
            catch
            {
                return null;
            }
        }


        public static byte[] AirvalentSetHistoryPointerMsgData() //sets it to the last but one data array (the newest already completely filled one)
        {
            using (var memoryStream = new MemoryStream())
            {
                byte b1 = 0xbf;
                byte b2 = 0x04;
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(b1);
                    binaryWriter.Write(b2);
                }
                byte[] data = memoryStream.ToArray();
                return memoryStream.ToArray();
            }
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            if (_airValentUpdateInterval == null) return -1;
            var reply = await _airValentUpdateInterval.ReadAsync();
            byte[] intervalBytes = reply.data;
            ushort interval = BitConverter.ToUInt16(intervalBytes, 0);
            return interval;
        }

        protected override bool IsGattValid()
        {
            return
                _airValentUpdateInterval != null &&
                _airValentHistory != null &&
                _airValentHistoryPointer != null &&
                _airValentChunkCounter != null;
        }

        /// <summary>
        /// Discover all required characteristics in provided service.
        /// </summary>
        private async Task<bool> DiscoverCharacteristicsAsync(IService service)
        {
            _airValentUpdateInterval =
                await TryGetCharacteristicAsync(service, AirvalentUpdateIntervalCharacteristic);
            _airValentHistory =
                await TryGetCharacteristicAsync(service, AirvalentHistoryCharacteristic);
            _airValentHistoryPointer =
                await TryGetCharacteristicAsync(service, AirvalentHistoryPointerCharacteristic);
            _airValentChunkCounter =
                await TryGetCharacteristicAsync(service, AirvalentDataChunkCountCharacteristic);

            bool ok =
                _airValentUpdateInterval != null &&
                _airValentHistory != null &&
                _airValentHistoryPointer != null &&
                _airValentChunkCounter != null;

            if (!ok)
                Debug.WriteLine("Initialization incomplete: missing one or more characteristics.");
            Debug.WriteLine("Initialization complete: all characteristics found");
            return ok;
        }

        private async Task<int> GetChunkCountAsync()
        {
            if (_airValentUpdateInterval == null) return -1;
            var reply = await _airValentChunkCounter.ReadAsync();
            byte[] chunkCounterBytes = reply.data;
            int chunkCount = BitConverter.ToUInt16(chunkCounterBytes, 0);
            return chunkCount;
        }
    }
}
