using IndoorCO2MapAppV2.Pages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
        internal bool OpenWindowsDoors { get; set; }
        internal bool VentilationSystem { get; set; }
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

        public string ToJson(int rangeSliderMin, int rangeSliderMax)
        {
            JObject json = new JObject();
            int arraySize = ((rangeSliderMax + 1) - rangeSliderMin);
            string[] ppmArray = new string[arraySize];
            string[] timestampArray = new string[arraySize];

            if (rangeSliderMin + 1 > MeasurementData.Count)
            {
                throw new IndexOutOfRangeException("RangeSliderMin +1 > SensorData Array - this should not happen");
            }

            if (rangeSliderMax > MeasurementData.Count)
            {
                throw new IndexOutOfRangeException("RangeSliderMax +1 > SensorData Array - this should not happen");
            }

            int arrayIndex = 0;
            for (int i = rangeSliderMin; i <= rangeSliderMax; i++)
            {
                CO2Reading data = MeasurementData[i];
                ppmArray[arrayIndex] = data.Ppm.ToString();
                timestampArray[arrayIndex] = data.RelativeTimeStamp.ToString();
                arrayIndex++;
            }

            //OpenWindowsDoors = MainPage.hasOpenWindowsDoors;
            //VentilationSystem = MainPage.hasVentilationSystem;
            //AdditionalNotes = MainPage.MainPageSingleton.GetNotesEditorText();


            json["d"] = "TestRunNewApp" + System.Random.Shared.Next(0, 100000);
            json["p"] = NwrType;
            json["i"] = NwrID;
            json["n"] = NwrName;
            json["b"] = StartTime;
            json["x"] = NwrLongitude; // x = lon
            json["y"] = NwrLatitude; // y = lat
            json["w"] = OpenWindowsDoors;
            json["v"] = VentilationSystem;
            json["o"] = OccupancyLevel;
            json["a"] = AdditionalNotes;
            json["c"] = string.Join(";", ppmArray);
            json["t"] = string.Join(";", timestampArray);

            return json.ToString();
        }
    }

    public static class Converter
    {
        public static string ArrayToString(string[] array, string separator)
        {
            return string.Join(separator, array);
        }
    }
}