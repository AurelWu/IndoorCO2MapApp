using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AirspotDataPage
    {
        public int PageID { get; private set; }
        public List<long> Timestamps { get; } = [];
        public List<int> CO2Values { get; } = [];
        public bool FinishedPage { get; private set; } = true;

        public AirspotDataPage(byte[] data)
        {
            ParseData(data);
        }

        private void ParseData(byte[] data)
        {
            if(data.Length!= 135)
            {
                Logger.WriteToLog("Airspot History Data package with length deviating from normal 135 byte");
            }
            FinishedPage = true;
            int offset = 4;

            for (int i = 0; i < 16; i++)
            {
                uint timestamp =
                    (uint)((data[offset] << 24) |
                           (data[offset + 1] << 16) |
                           (data[offset + 2] << 8) |
                            data[offset + 3]);

                offset += 4;

                int co2 = (data[offset] << 8) | data[offset + 1];
                offset += 2;

                byte msgType = data[offset];      // 00 = CO2 ; 02 = TimeSync
                byte padding = data[offset + 1]; // unused
                offset += 2;

                if (timestamp == 0xFFFFFFFF)
                    FinishedPage = false;

                // Only store entries with status 0x00
                if (msgType == 0x00 && co2 > 200)
                {
                    Timestamps.Add(timestamp);
                    CO2Values.Add(co2);
                }
            }
            int id = (data[offset] << 8) | data[offset + 1];
            PageID = id;
        }
    }

}
