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

            // On iOS the CBCentralManager starts as Unknown and needs time to reach PoweredOn.
            // Poll up to 8 seconds before giving up — scan attempts before BT is ready find nothing.
            await WaitForBluetoothReadyAsync(TimeSpan.FromSeconds(8));

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                Logger.WriteToLog($"Recovery attempt {attempt} for device {deviceIdString}");

                var device = await TryFindDeviceAsync(deviceIdString);
                if (device != null)
                {
                    await sensorViewModel.SelectDeviceAsync(new Bluetooth.BluetoothDeviceModel(device));
                    await _recordingManager.TryRecoverRecordingAfterDeviceReadyAsync(snapshot, deviceIdString);
                    return true;
                }

                Preferences.Set("RecordingRecoveryAttempts", attempt);
                await Task.Delay(DelayBetweenAttemptsMs);
            }

            return false;
        }

        // ------------------------------------------------------------------------------
        // BLUETOOTH READINESS
        // ------------------------------------------------------------------------------

        /// <summary>
        /// Polls until the BLE adapter reports PoweredOn, or the timeout elapses.
        /// On iOS the CBCentralManager starts as Unknown and takes 1-2s to settle.
        /// </summary>
        private static async Task WaitForBluetoothReadyAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var state = CrossBluetoothLE.Current.State;
                Logger.WriteToLog($"RecoveryManager|WaitForBT: state={state}");
                if (state == BluetoothState.On)
                    return;
                // If it's definitively off/unavailable, stop waiting early
                if (state == BluetoothState.Off || state == BluetoothState.Unavailable)
                {
                    Logger.WriteToLog("RecoveryManager|WaitForBT: BT is off/unavailable, not waiting further");
                    return;
                }
                await Task.Delay(500);
            }
            Logger.WriteToLog("RecoveryManager|WaitForBT: timed out waiting for BT");
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
