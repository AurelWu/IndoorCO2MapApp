using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using System.Diagnostics;
using System.Globalization;
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

        public bool NeedsTrimRestore { get; set; } = false;

        private volatile int _snapshotGeneration = 0;

        bool inkBirdRecoveryDone = false;
#if ANDROID
        private Android.OS.PowerManager.WakeLock? _wakeLock;
#endif

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
                startTime -= 2 * 60 * 1000; // grab last 2 sensor readings immediately
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


            _snapshotGeneration++;
            NeedsTrimRestore = false;
            ActiveRecording = rec;
            SaveRecoverySnapshot(rec, deviceID);
            _cts?.Cancel();
            _cts?.Dispose();
            _timer?.Dispose();
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(30)); //updates every 30 seconds.

            _ = RunLoopAsync(_cts.Token);
#if ANDROID
            AcquireWakeLockAndStartService();
#endif
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
            _cts?.Dispose();
            _cts = null;
            _timer?.Dispose();
            _timer = null;

            _snapshotGeneration++;
            NeedsTrimRestore = false;
            ActiveRecording = null;
            Preferences.Remove("RecordingState");
            CurrentSnapShot = new RecordingRecoverySnapshot();
#if ANDROID
            ReleaseWakeLockAndStopService();
#endif
        }

        public Task TriggerImmediateUpdateAsync()
        {
            if (!IsRecording) return Task.CompletedTask;
            return ReadAndStoreLatestAsync();
        }

        // ----------------------------------------------------------------------
        // MAIN LOOP
        // ----------------------------------------------------------------------
        private async Task RunLoopAsync(CancellationToken token)
        {
            Logger.WriteToLog("RunLoopAsync called", LogMode.Verbose);
            try
            {
                // Immediate first read — don't wait for the first 30-second tick
                await ReadAndStoreLatestAsync();

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
            var recording = ActiveRecording;
            if (recording == null)
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long minutes = (now - recording.RecordingStart) / 60000;

            if (minutes < 1)
                return;

            await _monitor.RefreshHistoryAsync((ushort)Math.Min(minutes, 480));

            // Re-check after the await — recording could have been stopped
            if (ActiveRecording == null)
            {
                Logger.WriteToLog("ReadAndStoreLatestAsync| ActiveRecording is null after RefreshHistory - returning", LogMode.Verbose);
                return;
            }

            var hist = _monitor.Co2History;
            if (hist == null || hist.Count == 0)
                return;

            if (recording.MeasurementData != null)
            {
                //=> needs to handle Inkbird Recovery setup
                if(recording.CO2MonitorType == CO2MonitorType.InkbirdIAMT1.ToString() && !inkBirdRecoveryDone)
                {
                    var m = _monitor.ActiveCO2MonitorProvider as InkbirdProvider;
                    var recData = recording.MeasurementData;
                    m.assembledCO2History = new List<ushort>();
                    foreach(var r in recData)
                    {
                        m.assembledCO2History.Add(r.Ppm);
                    }
                    inkBirdRecoveryDone = true;
                    hist = m.assembledCO2History;
                }
                recording.MeasurementData.Clear();
                Logger.WriteToLog("ReadAndStoreLatestAsync| clearing MeasurementData", LogMode.Verbose);
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
                recording.MeasurementData.Add(new CO2Reading(v, offset, DateTime.Now));
                offset += interval;
            }
            Logger.WriteToLog("ReadAndStoreLatestAsync |Before MeasurementDataUpdated?.Invoke()", LogMode.Verbose);
            SaveRecoverySnapshot(recording, recording.MonitorID);
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
                    return $"{Localisation.RecordingLocationLabel}{ActiveRecording.LocationName}";

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
        public async Task TryRecoverRecordingAfterDeviceReadyAsync(RecordingRecoverySnapshot snapshot, string deviceId, bool forceDeviceId = false)
        {
            // Auto-recovery requires the ready device to match the saved one.
            // Manual resume (forceDeviceId) trusts the user's explicit sensor choice —
            // needed because BLE ids can change across app restarts (notably on iOS).
            if (snapshot == null || (!forceDeviceId && snapshot.MonitorDeviceId != deviceId))
                return;

            string effectiveDeviceId = forceDeviceId ? deviceId : snapshot.MonitorDeviceId;

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
                MonitorID = effectiveDeviceId,
                DoorWindowState = snapshot.DoorWindowState,
                VentilationState = snapshot.VentilationState,
                CustomNotes = snapshot.CustomNote,                
                
            };

            foreach (var kv in snapshot.AdditionalDataByParameter)
                ActiveRecording.AdditionalDataByParameter.TryAdd(kv.Key, kv.Value);

            NeedsTrimRestore = ActiveRecording.AdditionalDataByParameter.ContainsKey("trimHigh");

            if(ActiveRecording.CO2MonitorType == CO2MonitorType.InkbirdIAMT1.ToString())
            {
                ActiveRecording.MeasurementData = snapshot.CO2Values;
            }

            //TODO: if Inkbird then we also set the Measurementdata to what we had.
            

            Logger.WriteToLog("Recovered recording after sensor ready.");

            // Start periodic loop (make sure you dispose previous _cts if any)
            _cts?.Cancel();
            _cts?.Dispose();
            _timer?.Dispose();
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            _ = RunLoopAsync(_cts.Token);
#if ANDROID
            AcquireWakeLockAndStartService();
#endif

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
                CO2Values = [.. recording.MeasurementData],
                DoorWindowState = recording.DoorWindowState,
                VentilationState = recording.VentilationState,
                CustomNote = recording.CustomNotes,
                IsTransitRecording = recording.AdditionalDataByParameter.ContainsKey("routeID"),
                AdditionalDataByParameter = new Dictionary<string, string>(recording.AdditionalDataByParameter)
            };

            CurrentSnapShot = snapshot;
            int gen = _snapshotGeneration;
            Task.Run(() =>
            {
                if (_snapshotGeneration != gen) return;
                Preferences.Set("RecordingState", JsonSerializer.Serialize(snapshot));
            });
        }

#if ANDROID
        private void AcquireWakeLockAndStartService()
        {
            if (_wakeLock?.IsHeld == true)
                _wakeLock.Release();
            var pm = (Android.OS.PowerManager?)Android.App.Application.Context
                .GetSystemService(Android.Content.Context.PowerService);
            _wakeLock = pm?.NewWakeLock(Android.OS.WakeLockFlags.Partial, "IndoorCO2:Recording");
            _wakeLock?.Acquire();
        }

        private void ReleaseWakeLockAndStopService()
        {
            if (_wakeLock?.IsHeld == true)
                _wakeLock.Release();
            _wakeLock = null;
        }
#endif

        public void UpdateTrimSnapshot(double low, double high)
        {
            if (!IsRecording || ActiveRecording == null) return;
            ActiveRecording.AdditionalDataByParameter["trimLow"]  = low.ToString(CultureInfo.InvariantCulture);
            ActiveRecording.AdditionalDataByParameter["trimHigh"] = high.ToString(CultureInfo.InvariantCulture);
            SaveRecoverySnapshot(ActiveRecording, ActiveRecording.MonitorID);
        }

        public void UpdateEndpointSnapshot(LocationData? endpoint)
        {
            if (!IsRecording || ActiveRecording == null) return;
            var d = ActiveRecording.AdditionalDataByParameter;
            if (endpoint != null)
            {
                d["endpointName"] = endpoint.Name;
                d["endpointLat"]  = endpoint.Latitude.ToString(CultureInfo.InvariantCulture);
                d["endpointLon"]  = endpoint.Longitude.ToString(CultureInfo.InvariantCulture);
                d["endpointType"] = endpoint.Type;
                d["endpointId"]   = endpoint.ID.ToString();
            }
            else
            {
                foreach (var key in new[] { "endpointName", "endpointLat", "endpointLon", "endpointType", "endpointId" })
                    d.Remove(key);
            }
            SaveRecoverySnapshot(ActiveRecording, ActiveRecording.MonitorID);
        }

        public void UpdateRouteSnapshot(string routeId, string routeName)
        {
            if (!IsRecording || ActiveRecording == null) return;
            var d = ActiveRecording.AdditionalDataByParameter;
            d["routeID"]   = routeId;
            d["routeName"] = routeName;
            if (d.TryGetValue("startName", out var startName))
                ActiveRecording.LocationName = $"{routeName} ({startName})";
            else
                ActiveRecording.LocationName = routeName;
            SaveRecoverySnapshot(ActiveRecording, ActiveRecording.MonitorID);
        }

        public void UpdateRecoverySnapshot(TriState doorWindowstate, TriState ventilationState, string customNote)
        {
            if (!IsRecording) return;
            CurrentSnapShot.DoorWindowState = doorWindowstate;
            CurrentSnapShot.VentilationState = ventilationState;
            CurrentSnapShot.CustomNote = customNote;
            if (ActiveRecording != null)
            {
                ActiveRecording.DoorWindowState = doorWindowstate;
                ActiveRecording.VentilationState = ventilationState;
                ActiveRecording.CustomNotes = customNote;
            }
            var snap = CurrentSnapShot;
            Task.Run(() => Preferences.Set("RecordingState", JsonSerializer.Serialize(snap)));
        }
    }
}
