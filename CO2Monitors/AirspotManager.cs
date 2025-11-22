using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AirspotManager : BaseCO2MonitorManager
    {
        public override Task<bool> InitializeAsync(IDevice device)
        {
            throw new NotImplementedException();
        }

        protected override Task<int> DoReadCurrentCO2Async()
        {
            throw new NotImplementedException();
        }

        protected override Task<ushort[]?> DoReadHistoryAsync(ushort startIndex)
        {
            throw new NotImplementedException();
        }

        protected override bool IsGattValid()
        {
            throw new NotImplementedException();
        }
    }
}
