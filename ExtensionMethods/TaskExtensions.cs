using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace IndoorCO2MapAppV2.ExtensionMethods
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Safely fire-and-forget a Task, writing errors to console and log.
        /// </summary>
        public static void SafeFireAndForget(this Task task, string sender ="")
        {
            _ = task.ContinueWith(async t =>
            {
                if (t.IsFaulted)
                {
                    // Log all inner exceptions
                    foreach (var ex in t.Exception!.InnerExceptions)
                    {
                        //Debug.WriteLine($"Task from {sender} failed: {ex.Message}");
                        Logger.WriteToLog($"Task from {sender} failed: {ex.Message}",LogMode.Verbose);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
