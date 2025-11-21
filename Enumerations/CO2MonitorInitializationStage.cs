using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.Enumerations
{
    //to be used in the CO2 Manager classes to keep track during initialisation and to be exposed as property to UI via Bindings
    public enum CO2MonitorInitializationStage
    {
        Uninitialized,
        Connecting,
        FindingService,
        DiscoveringCharacteristics,
        Completed,
        Failed
    }
}
