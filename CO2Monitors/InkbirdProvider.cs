using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class InkbirdProvider : BaseCO2MonitorProvider
    {
        public override Task<bool> InitializeAsync(IDevice device)
        {
            throw new NotImplementedException();
        }

        protected override async Task<int> DoReadCurrentCO2Async()
        {
            return -1;            
        }


        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort startIndex)
        {
            return new ushort[0];
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            return -1;
        }

        protected override bool IsGattValid()
        {
            return false;
        }
    }
}
