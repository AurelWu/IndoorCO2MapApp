using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.CO2Monitors
{


    internal abstract class BaseCO2MonitorManager
    {
        protected const int RetryCount = 3;
        protected const int RetryDelayMs = 100;

        protected int currentCO2Value = 0;

        /// <summary>
        /// Try reading a service with retries.
        /// </summary>
        protected static async Task<IService?> TryGetServiceAsync(IDevice device, Guid uuid)
        {
            for (int i = 0; i < RetryCount; i++)
            {
                var svc = await device.GetServiceAsync(uuid);
                if (svc != null)
                    return svc;

                await Task.Delay(RetryDelayMs);
            }
            return null;
        }

        /// <summary>
        /// Retry getting characteristic multiple times to handle transient BLE issues.
        /// </summary>
        protected static async Task<ICharacteristic?> TryGetCharacteristicAsync(IService service, Guid uuid)
        {
            for (int i = 0; i < RetryCount; i++)
            {
                var characteristic = await service.GetCharacteristicAsync(uuid);
                if (characteristic != null) return characteristic;
                await Task.Delay(RetryDelayMs);
            }
            return null;
        }

        public abstract Task<bool> InitializeAsync(IDevice device);
        public abstract Task<int> ReadCurrentCO2Async();

        public abstract Task<ushort[]> ReadHistoryAsync(ushort startIndex); //this should probably not provide startindex but just time since start of measurement and index calculation is device specific and also based on measurement interval etc.
    }
}
