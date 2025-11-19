using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AranetManager
    {
        public static Guid AranetServiceUUID = Guid.Parse("0000FCE0-0000-1000-8000-00805f9b34fb");
        public static Guid OldAranetServiceUUID = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");

        public static string sensorVersion = "";
        public static bool outdatedVersion = false;

        public static Guid Aranet_CharacteristicUUID = Guid.Parse("f0cd3001-95da-4f4b-9ac8-aa55d312af0c"); //Characteristic which has the data we need
        public static Guid ARANET_WRITE_CHARACTERISTIC_UUID = Guid.Parse("f0cd1402-95da-4f4b-9ac8-aa55d312af0c");
        public static Guid ARANET_HISTORY_V2_CHARACTERISTIC_UUID = Guid.Parse("f0cd2005-95da-4f4b-9ac8-aa55d312af0c");
        public static Guid ARANET_TOTAL_READINGS_CHARACTERISTIC_UUID = Guid.Parse("f0cd2001-95da-4f4b-9ac8-aa55d312af0c");

        public static Guid AranetVersionServiceUUID = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
        public static Guid ARANET_VersionNumber_CHARACTERISTIC_UUID = Guid.Parse("00002a26-0000-1000-8000-00805f9b34fb");


        public static byte[] CreateCO2HistoryRequestPacket(ushort startIndex)
        {
            using MemoryStream memoryStream = new();
            byte header = 0x61;
            byte co2ID = 0x04;
            using (BinaryWriter binaryWriter = new(memoryStream))
            {
                binaryWriter.Write(header);       // Write 1 byte
                binaryWriter.Write(co2ID);   // Write 1 byte
                binaryWriter.Write(startIndex);        // Write 2 bytes (little-endian by default)
            }

            byte[] data = memoryStream.ToArray();
            return memoryStream.ToArray();
        }
    }
}
