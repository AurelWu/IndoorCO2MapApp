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

        protected override Task<int> DoReadCurrentCO2Async()
        {
            throw new NotImplementedException();
        }

        protected override Task<ushort[]?> DoReadHistoryAsync(ushort startIndex, int sensorUpdateInterval)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
