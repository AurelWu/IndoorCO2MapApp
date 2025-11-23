using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Enumerations
{
    [Flags]
    public enum CO2MonitorType
    {
        None = 0,
        Aranet4 = 1 << 0,
        Airvalent = 1 << 1,
        InkbirdIAMT1 = 1 << 2,
        AirSpotHealth = 1 << 3,
        AllMonitors = Aranet4 | Airvalent | InkbirdIAMT1 | AirSpotHealth
    }
}
