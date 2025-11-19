using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Util
{
    internal class Helpers
    {
        static int ConvertSecondsToFullMinutes(int seconds)
        {
            return (int)Math.Ceiling(seconds / 60.0);
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return Convert.ToHexString(ba);
        }
    }
}
