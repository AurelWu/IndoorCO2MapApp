using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.Resources.Strings;
using System.Diagnostics;
using System.Text.Json;

namespace IndoorCO2MapAppV2.Recording
{
    public sealed partial class RecordingManager : ObservableObject
    {
        private static readonly Lazy<RecordingManager> _instance =
            new(() => new RecordingManager());
        public static RecordingManager Instance => _instance.Value;

        private readonly CO2MonitorManager _monitor = CO2MonitorManager.Instance;
        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private BuildingRecording? activeRecording;

        public bool IsRecording => ActiveRecording != null;

        public event Action? MeasurementDataUpdated;

        public static RecordingRecoverySnapshot CurrentSnapShot { get; set; } = new RecordingRecoverySnapshot();

        bool inkBirdRecoveryDone = false;

        private RecordingManager() { }

        // ----------------------------------------------------------------------
        // START RECORDING
        // ----------------------------------------------------------------------
        public async Task StartRecordingAsync(string nwrType, long nwrID, double latitude, double longitude, string locationName, string monitorType, string deviceID, bool prerecording)
        {
            if (IsRecording)

            {
                Logger.WriteToLog("Already recording, overwriting current one with fresh one.");
            }

            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            

            if(prerecording)
            {
                startTime -= 15 * 60 * 1000; // 15 minutes hardcoded is okay here, we dont really want to change that
            }
            else
            {
                startTime -= 1 * 60 * 1000; //even without preRecording we start 1 minute early to grab currenty sensor reading. //maybe we even change that to 2
            }

            var rec = new BuildingRecording
            {
                NwrId = nwrID,
                NwrType = nwrType,
                LocationName = locationName,
                Latitude = latitude,
                Longitude = longitude,
                RecordingStart = startTime,
                MeasurementData = new(),
                CO2MonitorType = monitorType,
                DoorWindowState = TriState.Unknown,
                VentilationState = TriState.Unknown,
                CustomNotes = "",
                AdditionalDataByParameter = new(),
                MonitorID = deviceID,
            };


            ActiveRecording = rec;
            SaveRecoverySnapshot(rec, deviceID);
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(30)); //updates every 30 seconds.

            _ = RunLoopAsync(_cts.Token);
        }

        // ----------------------------------------------------------------------
        // STOP RECORDING
        // ----------------------------------------------------------------------
        public async Task StopRecordingAsync()
        {
            Logger.WriteToLog("StopRecordingAsync called", LogMode.Verbose);
            if (!IsRecording)
                return;

            _cts?.Cancel();
            _cts = null;

            ActiveRecording = null;
            Preferences.Remove("RecordingState");
        }

        // ----------------------------------------------------------------------
        // MAIN LOOP
        // ----------------------------------------------------------------------
        private async Task RunLoopAsync(CancellationToken token)
        {
            Logger.WriteToLog("RunLoopAsync called", LogMode.Verbose);
            if (token.IsCancellationRequested)
            {
                Logger.WriteToLog("Recording Manager RunLoop CancellationToken IsCancellationRequested set to true");
            }
            try
            {
                while (await _timer!.WaitForNextTickAsync(token))
                {
                    Logger.WriteToLog("RunLoopAsync|in while loop before ReadAndStoreLatestAsync", LogMode.Verbose);
                    await ReadAndStoreLatestAsync();
                    Logger.WriteToLog("RunLoopAsync|in while loop after ReadAndStoreLatestAsync", LogMode.Verbose);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.WriteToLog("OperationCanceledException caught in RunLoopAsync", LogMode.Verbose);
            }
        }

        // ----------------------------------------------------------------------
        // READ HISTORY & STORE POINTS
        // ----------------------------------------------------------------------
        private async Task ReadAndStoreLatestAsync()
        {
            Logger.WriteToLog("ReadAndStoreLatestAsync() called", LogMode.Verbose);
            if (ActiveRecording == null)
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long minutes = (now - ActiveRecording.RecordingStart) / 60000;

            if (minutes < 1)
                return;

            //we read max 120 minutes for now
            await _monitor.RefreshHistoryAsync((ushort)Math.Min(minutes, 120));

            var hist = _monitor.Co2History;
            if (hist == null || hist.Count == 0)
                return;

            var dateTime = DateTimeOffset.UtcNow.DateTime;
            if(ActiveRecording!= null && ActiveRecording.MeasurementData!= null)
            {
                //=> needs to handle Inkbird Recovery setup
                if(activeRecording.CO2MonitorType == CO2MonitorType.InkbirdIAMT1.ToString() && !inkBirdRecoveryDone)
                {
                    var m = _monitor.ActiveCO2MonitorProvider as InkbirdProvider;
                    var recData = ActiveRecording.MeasurementData;
                    m.assembledCO2History = new List<ushort>();
                    foreach(var r in recData)
                    {
                        m.assembledCO2History.Add(r.Ppm);
                    }
                    inkBirdRecoveryDone = true;
                    hist = m.assembledCO2History;
                }
                ActiveRecording.MeasurementData.Clear();
                Logger.WriteToLog("ReadAndStoreLatestAsync| ActiveRecording!= null && ActiveRecording.MeasurementData!= null - clearing MeasurementData", LogMode.Verbose);
             
            }
            if (ActiveRecording == null)
            {
                Logger.WriteToLog("ReadAndStoreLatestAsync| ActiveRecording is null - returning",LogMode.Verbose);
                return;
            }

            int interval = 1;            
            if(CO2MonitorManager.Instance.UpdateInterval == 120)
            {
                interval = 2;
            }
            else if(CO2MonitorManager.Instance.UpdateInterval == 180)
            {
                interval = 3;
            }
            else if(CO2MonitorManager.Instance.UpdateInterval == 300)
            {
                interval = 5;
            }
            else if(CO2MonitorManager.Instance.UpdateInterval == 600)
            {
                interval = 10;
            }
            int offset = 0;
            
            foreach (var v in hist)
            {
                ActiveRecording.MeasurementData.Add(new CO2Reading(v, offset, DateTime.Now));
                offset += interval;
            }
            Logger.WriteToLog("ReadAndStoreLatestAsync |Before MeasurementDataUpdated?.Invoke()", LogMode.Verbose);
            SaveRecoverySnapshot(ActiveRecording!, ActiveRecording!.MonitorID);
            MeasurementDataUpdated?.Invoke();
        }

        // ----------------------------------------------------------------------
        // UI Helper Methods
        // ----------------------------------------------------------------------
        public string CurrentLocationDisplay
        {
            get
            {
                if (ActiveRecording == null)
                    return "[No Recording]";

                // Prefer user-friendly name if available
                if (!string.IsNullOrWhiteSpace(ActiveRecording.LocationName))
                    return $"Location: {Localisation.RecordingLocationLabel}{ActiveRecording.LocationName}";

                // Otherwise fall back to OSM type + ID
                return $"Location ID: {ActiveRecording.NwrType} {ActiveRecording.NwrId}";
            }
        }

        /// <summary>
        /// Recovery Mechanism
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        /// <summary>
        /// Restore an active recording once the given deviceId is known/ready.
        /// This no longer reads Preferences; the caller supplies the snapshot.
        /// </summary>
        public async Task TryRecoverRecordingAfterDeviceReadyAsync(RecordingRecoverySnapshot snapshot, string deviceId)
        {
            if (snapshot == null || snapshot.MonitorDeviceId != deviceId)
                return;

            // Restore active recording
            ActiveRecording = new BuildingRecording
            {
                NwrId = snapshot.NwrID,
                NwrType = snapshot.NwrType,
                LocationName = snapshot.LocationName,
                Latitude = snapshot.Latitude,
                Longitude = snapshot.Longitude,
                RecordingStart = snapshot.RecordingStart,
                MeasurementData = new(),
                CO2MonitorType = snapshot.MonitorType, //TODO: currently not actually the monitorType but the searchSettings which include "All Monitors" we want the specific make of it though eventually - not important for now but should be changed                
                MonitorID = snapshot.MonitorDeviceId,
                DoorWindowState = snapshot.DoorWindowState,
                VentilationState = snapshot.VentilationState,
                CustomNotes = snapshot.CustomNote,                
                
            };

            if(ActiveRecording.CO2MonitorType == CO2MonitorType.InkbirdIAMT1.ToString())
            {
                ActiveRecording.MeasurementData = snapshot.CO2Values;                
            }

            //TODO: if Inkbird then we also set the Measurementdata to what we had.
            

            Logger.WriteToLog("Recovered recording after sensor ready.");

            // Start periodic loop (make sure you dispose previous _cts if any)
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            _ = RunLoopAsync(_cts.Token);

            // Optionally: signal UI to navigate to recording page.
        }

        // Recovery Snapshot
        private void SaveRecoverySnapshot(BuildingRecording recording, string deviceId)
        {
            var snapshot = new RecordingRecoverySnapshot
            {
                RecordingStart = recording.RecordingStart,
                NwrID = recording.NwrId,
                NwrType = recording.NwrType,
                LocationName = recording.LocationName,
                Latitude = recording.Latitude,
                Longitude = recording.Longitude,
                MonitorType = recording.CO2MonitorType,
                MonitorDeviceId = deviceId,    
                CO2Values = recording.MeasurementData
                //DoorWindowState = 
            };

            CurrentSnapShot = snapshot;
            var json = JsonSerializer.Serialize(snapshot);
            Preferences.Set("RecordingState", json);
        }

        public void UpdateRecoverySnapshot(TriState doorWindowstate, TriState ventilationState, string customNote)
        {
            CurrentSnapShot.DoorWindowState = doorWindowstate;
            CurrentSnapShot.VentilationState = ventilationState;
            CurrentSnapShot.CustomNote = customNote;
            //TODO: this will set windowsDoorState / Ventilation / Custom Notes and TrimSlider Values ; should be called whenever any of these change
            var json = JsonSerializer.Serialize(CurrentSnapShot);
            Preferences.Set("RecordingState", json);
            
        }
    }
}
