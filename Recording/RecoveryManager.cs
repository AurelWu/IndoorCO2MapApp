using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ViewModels;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Text.Json;

namespace IndoorCO2MapAppV2.Recording
{
    public class RecoveryManager
    {
        // --- SINGLETON ---
        private static readonly Lazy<RecoveryManager> _instance =
            new(() => new RecoveryManager());

        public static RecoveryManager Instance => _instance.Value;

        // --- FIELDS (set once at startup) ---
        private IAdapter? _adapter;
        private RecordingManager? _recordingManager;

        private RecoveryManager() { } // PRIVATE CONSTRUCTOR

        /// <summary>
        /// Must be called once on app startup to supply dependencies.
        /// </summary>
        public void Initialize(IAdapter adapter, RecordingManager recordingManager)
        {
            _adapter = adapter;
            _recordingManager = recordingManager;
        }

        private const int MaxAttempts = 3;
        private const int DelayBetweenAttemptsMs = 3000;

        // ------------------------------------------------------------------------------
        // SNAPSHOT LOADING
        // ------------------------------------------------------------------------------

        public RecordingRecoverySnapshot? LoadSnapshot()
        {
            try
            {
                var json = Preferences.Get("RecordingState", "");
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<RecordingRecoverySnapshot>(json);
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"RecoveryManager|LoadSnapshot failed: {ex.Message}");
                return null;
            }
        }

        public void ClearSnapshot() => Preferences.Remove("RecordingState");

        // ------------------------------------------------------------------------------
        // AUTO RECOVERY
        // ------------------------------------------------------------------------------

        public async Task<bool> TryAutoRecoverAsync(SensorViewModel sensorViewModel)
        {
            if (_adapter == null || _recordingManager == null)
                throw new InvalidOperationException("RecoveryManager not initialized. Call Initialize() first.");

            var snapshot = LoadSnapshot();
            if (snapshot == null) return false;

            var deviceIdString = snapshot.MonitorDeviceId;
            if (string.IsNullOrWhiteSpace(deviceIdString)) return false;

            // GAP 2: allow ~2s for Android to tear down GATT handles from the killed process.
            // Without this, GetSystemConnectedOrPairedDevices() may return a stale "connected"
            // device whose GATT handles are no longer valid, causing silent read failures.
            await Task.Delay(2000);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                Logger.WriteToLog($"Recovery attempt {attempt} for device {deviceIdString}");

                var device = await TryFindDeviceAsync(deviceIdString);
                if (device != null)
                {
                    await sensorViewModel.SelectDeviceAsync(new Bluetooth.BluetoothDeviceModel(device));

                    // GAP 1: SelectDeviceAsync can fail silently (provider stays null on connect
                    // timeout or InitializeAsync failure). Don't declare success unless the
                    // provider is actually live — retry instead.
                    if (CO2Monitors.CO2MonitorManager.Instance.ActiveCO2MonitorProvider == null)
                    {
                        Logger.WriteToLog($"Recovery attempt {attempt}: provider null after SelectDeviceAsync, retrying");
                        Preferences.Set("RecordingRecoveryAttempts", attempt);
                        await Task.Delay(DelayBetweenAttemptsMs);
                        continue;
                    }

                    await _recordingManager.TryRecoverRecordingAfterDeviceReadyAsync(snapshot, deviceIdString);
                    return true;
                }

                Preferences.Set("RecordingRecoveryAttempts", attempt);
                await Task.Delay(DelayBetweenAttemptsMs);
            }

            return false;
        }

        // ------------------------------------------------------------------------------
        // DEVICE DISCOVERY
        // ------------------------------------------------------------------------------

        private async Task<IDevice?> TryFindDeviceAsync(string expectedId)
        {
            if (_adapter == null)
                throw new InvalidOperationException("RecoveryManager not initialized.");

            // check connected/paired first
            var connected = _adapter.GetSystemConnectedOrPairedDevices();
            var match = connected.FirstOrDefault(d =>
                d.Id.ToString().Equals(expectedId, StringComparison.OrdinalIgnoreCase)
                || (d.Name?.Equals(expectedId, StringComparison.OrdinalIgnoreCase) ?? false));

            if (match != null) return match;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            IDevice? found = null;

            void Handler(object? s, DeviceEventArgs a)
            {
                try
                {
                    var dev = a.Device;
                    if (dev == null) return;

                    if (dev.Id.ToString().Equals(expectedId, StringComparison.OrdinalIgnoreCase)
                        || (dev.Name?.Equals(expectedId, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        found = dev;
                        cts.Cancel();
                    }
                }
                catch (Exception ex) { Logger.WriteToLog("RecoveryManager handler error: " + ex.Message, LogMode.Verbose); }
            }

            _adapter.DeviceDiscovered += Handler;

            try
            {
                await _adapter.StartScanningForDevicesAsync(cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.WriteToLog($"RecoveryManager|StartScanning failed: {ex.Message}"); }
            finally
            {
                _adapter.DeviceDiscovered -= Handler;
                try { await _adapter.StopScanningForDevicesAsync(); } catch (Exception ex) { Logger.WriteToLog("RecoveryManager StopScanning failed: " + ex.Message, LogMode.Verbose); }
            }

            return found;
        }
    }
}
