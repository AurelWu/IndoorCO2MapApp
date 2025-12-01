using IndoorCO2MapAppV2.CO2Monitors;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.PersistentData
{
    internal class BuildingRecording
    {
        long nwrid;
        string nwrtype;
        long recordingStart;
        List<CO2Reading> measurementData;
        Dictionary<string, string> additionalDataByParameter;
    }
}
