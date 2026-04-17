using IndoorCO2MapAppV2.Pages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using IndoorCO2MapAppV2.CO2Monitors;

namespace IndoorCO2MapAppV2.Recording
{
/// <summary>
/// Used for Transmitting to the Backend API Gateway - for now its the same as the old one
/// </summary>
    public class BuildingSubmissionData 
    {
        internal string SensorType { get; set; }
        internal string SensorID { get; set; }
        internal string NwrType { get; set; }
        internal long NwrID { get; set; }
        internal string NwrName { get; set; }
        internal double NwrLatitude { get; set; }
        internal double NwrLongitude { get; set; }
        internal long StartTime { get; set; }
        internal List<CO2Reading> MeasurementData { get; set; }
        internal string OpenWindowsDoors { get; set; } = "noData";
        internal string VentilationSystem { get; set; } = "noData";
        internal string OccupancyLevel { get; set; }
        internal string AdditionalNotes { get; set; }

        public BuildingSubmissionData(string sensorType, string sensorID, string nwrType, long nwrID, string nwrName, double nwrLat, double nwrLon, long startTime, bool isRecovery)
        {
            SensorType = sensorType;
            SensorID = sensorID;
            NwrType = nwrType;
            NwrID = nwrID;
            NwrName = nwrName;
            StartTime = startTime;
            NwrLatitude = nwrLat;
            NwrLongitude = nwrLon;
            OccupancyLevel = "undefined";
            AdditionalNotes = String.Empty;
            //if (MainPage.MainPageSingleton._NotesEditor.Text != null && isRecovery)
            //{
            //    AdditionalNotes = MainPage.MainPageSingleton._NotesEditor.Text;
            //}
            MeasurementData = new List<CO2Reading>();
        }

        public string ToJson()
        {
            int count = MeasurementData.Count;

            string[] ppmArray = new string[count];
            string[] timestampArray = new string[count];

            for (int i = 0; i < count; i++)
            {
                ppmArray[i] = MeasurementData[i].Ppm.ToString(System.Globalization.CultureInfo.InvariantCulture);
                timestampArray[i] = MeasurementData[i].RelativeTimeStamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            JObject json = new JObject
            {
                ["d"] = "TestRunNewApp_" + SensorType + "_" + SensorID,
                ["p"] = NwrType,
                ["i"] = NwrID,
                ["n"] = NwrName,
                ["b"] = StartTime,
                ["x"] = NwrLongitude,
                ["y"] = NwrLatitude,
                ["w"] = OpenWindowsDoors,
                ["v"] = VentilationSystem,
                ["o"] = OccupancyLevel,
                ["a"] = AdditionalNotes,
                ["c"] = string.Join(";", ppmArray),
                ["t"] = string.Join(";", timestampArray)
            };

            return json.ToString();
        }

    }

    public static class Converter
    {
        public static string ArrayToString(string[] array, string separator)
        {
            return string.Join(separator, array);
        }

        public static string GenerateSubmissionId(int length = 24)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var bytes = RandomNumberGenerator.GetBytes(length);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }
    }
}