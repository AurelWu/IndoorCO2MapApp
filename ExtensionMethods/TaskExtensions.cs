using IndoorCO2MapAppV2.Debug;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.ExtensionMethods
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Safely fire-and-forget a Task, optionally showing an alert on error.
        /// </summary>
        public static void SafeFireAndForget(this Task task)
        {
            _ = task.ContinueWith(async t =>
            {
                if (t.IsFaulted)
                {
                    // Log all inner exceptions
                    foreach (var ex in t.Exception!.InnerExceptions)
                    {
                        Console.WriteLine($"Task failed: {ex.Message}");
                        Logger.WriteToLog(ex.Message);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
