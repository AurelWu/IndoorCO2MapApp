using IndoorCO2MapAppV2.CO2Monitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AirSpotDataPage
    {
        public int PageID { get; private set; }
        public List<long> Timestamps { get; } = [];
        public List<int> CO2Values { get; } = [];
        public bool FinishedPage { get; private set; } = true;

        public AirSpotDataPage(byte[] data)
        {
            ParseData(data);
        }

        private void ParseData(byte[] data)
        {
            FinishedPage = true;
            int offset = 4;

            for (int i = 0; i < 16; i++)
            {
                uint timestamp = (uint)((data[offset] << 24) |
                                        (data[offset + 1] << 16) |
                                        (data[offset + 2] << 8) |
                                         data[offset + 3]);
                Timestamps.Add(timestamp);

                if (timestamp == 0xFFFFFFFF)
                    FinishedPage = false;

                offset += 4;

                int co2 = (data[offset] << 8) | data[offset + 1];
                CO2Values.Add(co2);
                offset += 2;

                offset += 2; // skip unused ushort

                // Optional debug:
                // DateTime time = new DateTime(2000, 1, 1).AddSeconds(timestamp);
                // Console.WriteLine($"Entry {i}: Time={time}, CO2={co2} ppm");
            }

            PageID = (data[offset] << 8) | data[offset + 1];
        }
    }

}
