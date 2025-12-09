using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Resources.Strings;
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

        private RecordingManager() { }

        // ----------------------------------------------------------------------
        // START RECORDING
        // ----------------------------------------------------------------------
        public async Task StartRecordingAsync(string nwrType, long nwrID, double latitude, double longitude, string locationName, string monitorType)
        {
            if (IsRecording)

            {
                Logger.WriteToLog("Already recording, overwriting current one with fresh one.");
            }

            var rec = new BuildingRecording
            {
                NwrId = nwrID,
                NwrType = nwrType,
                LocationName = locationName,
                Latitude = latitude,
                Longitude =longitude,
                RecordingStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MeasurementData = new(),
                CO2MonitorType = monitorType,
                AdditionalDataByParameter = new()
            };

            ActiveRecording = rec;

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(30)); //updates every 30 seconds.

            _ = RunLoopAsync(_cts.Token);
        }

        // ----------------------------------------------------------------------
        // STOP RECORDING
        // ----------------------------------------------------------------------
        public async Task StopRecordingAsync()
        {
            if (!IsRecording)
                return;

            _cts?.Cancel();
            _cts = null;

            ActiveRecording = null;

        }

        // ----------------------------------------------------------------------
        // MAIN LOOP
        // ----------------------------------------------------------------------
        private async Task RunLoopAsync(CancellationToken token)
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(token))
                {
                    await ReadAndStoreLatestAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // normal exit
            }
        }

        // ----------------------------------------------------------------------
        // READ HISTORY & STORE POINTS
        // ----------------------------------------------------------------------
        private async Task ReadAndStoreLatestAsync()
        {
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
                ActiveRecording.MeasurementData.Clear();
            }
            if (ActiveRecording == null) return;
            foreach (var v in hist)
            {
                ActiveRecording.MeasurementData.Add(new CO2Reading(v, 0, DateTime.Now));
            }
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
    }
}
