using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IndoorCO2MapAppV2.Enumerations;
using Microsoft.VisualStudio.Utilities;

namespace IndoorCO2MapAppV2.DebugTools
{
    internal static class Logger
    {
        public static LogMode logMode = LogMode.Verbose;
        public static CircularBuffer<string> circularBuffer = new(500000);
        public static bool writeAlsoToConsole = true;
        public static bool LogDetailedGPSCoordinates = false; //if false it will round to 1° - just to see if GPS works at all, if true it will log the precise position, default will be false
        public static bool includeSender = true;
        //maybe add Enum for different Logging Levels

        public static void WriteToLog(string text, LogMode minimumLogMode=LogMode.Default, string sender = "")
        {
            DateTime dateTime = DateTime.Now;
            string textWithTimeStamp = text + " | " + dateTime.ToString();
            if (includeSender) textWithTimeStamp += " | " + sender;
            circularBuffer.Add(textWithTimeStamp);           
            if (writeAlsoToConsole)
            {
                Debug.WriteLine(textWithTimeStamp);
            }
        }

        public static void WriteLogToFile(string fileName)
        {
#if WINDOWS
            string path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            File.WriteAllLines(path, circularBuffer);
#endif
        }
    }
}
