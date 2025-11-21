using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IndoorCO2MapAppV2.Enumerations;

namespace IndoorCO2MapAppV2.UIUtility
{
    internal static class EnumStateTextProvider
    {
        private static readonly Dictionary<(Enum Value, string Context), string> _texts =
            new()
            {
                {(CO2MonitorType.Aranet4,"Picker"),"Aranet 4" },
                {(CO2MonitorType.Airvalent,"Picker"),"Airvalent" },
                {(CO2MonitorType.AirSpotHealth,"Picker"),"Airspot Health" },
                {(CO2MonitorType.InkbirdIAMT1,"Picker"),"Inkbird IAM-T1" },

            //{ (ScanState.Idle, "Button"), "Start scanning" }, //TODO define enum state + context combinations to provide Texts for UI
            //{ (ScanState.Scanning, "Button"), "Stop scanning" },
            //{ (ScanState.Error, "Button"), "Retry" },
            //
            //{ (ScanState.Idle, "StatusBar"), "Not scanning" },
            //{ (ScanState.Scanning, "StatusBar"), "Searching for devices..." },
            //{ (ScanState.Error, "StatusBar"), "An error occurred" },
            };

        public static string Get(Enum value, string context)
        {
            return _texts.TryGetValue((value, context), out var str)
                ? str
                : value.ToString(); // fallback
        }
    }
}
