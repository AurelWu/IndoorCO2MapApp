using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.PersistentData;
using Plugin.BLE.Abstractions;
using System.Collections.ObjectModel;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    public partial class CO2MonitorManager : ObservableObject
    {
        private static readonly Lazy<CO2MonitorManager> _instance = new(() => new CO2MonitorManager());
        public static CO2MonitorManager Instance => _instance.Value;

        private readonly BLEDeviceManager _ble;

        public List<CO2MonitorType> _monitorTypes;

        // Single lock that serializes ALL sensor operations so concurrent
        // UI triggers (RefreshLiveCO2Async called 4-5x at startup etc.)
        // never overlap and hammer the sensor simultaneously.
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private CO2MonitorManager()
        {
            _ble = BLEDeviceManager.Instance;
            Devices = _ble.Devices;

            _monitorTypes = [.. MonitorTypes.SearchStringByMonitorTypeDebugMode.Keys];
            SelectedMonitorType = _monitorTypes.FirstOrDefault();

            _ble.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_ble.IsScanning))
                    IsScanning = _ble.IsScanning;
            };
        }

#pragma warning disable IDE0079
#pragma warning disable MVVMTK0045

        [ObservableProperty] private BaseCO2MonitorProvider? activeCO2MonitorProvider;
        [ObservableProperty] private BluetoothDeviceModel? selectedDevice;
        [ObservableProperty] private CO2MonitorType selectedMonitorType;
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private int currentCO2;
        [ObservableProperty] private int updateInterval = -1;
        [ObservableProperty] private List<ushort> co2History = [];

#pragma warning restore MVVMTK0045
#pragma warning restore IDE0079

        public ObservableCollection<BluetoothDeviceModel> Devices { get; }

        public async Task StartScanAsync(CO2MonitorType filter, bool clearBeforeScan = true, int scanDurationMs = 20000, CancellationToken cancellationToken = default)
        {
            await _ble.StartScanningAsync(
                scanDurationMs: scanDurationMs,
                clearBeforeScan: clearBeforeScan,
                filter: filter,
                cancellationToken: cancellationToken);
        }

        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            // Skip only if already connected to this device — allow reconnect when provider is gone.
            if (SelectedDevice == device && ActiveCO2MonitorProvider != null) return;

            await _opLock.WaitAsync();
            try
            {
                // Re-check inside the lock: a concurrent call that got here first may have
                // already reconnected successfully (e.g. after a timeout drained the queue).
                if (SelectedDevice == device && ActiveCO2MonitorProvider != null)
                    return;

                if (ActiveCO2MonitorProvider != null)
                {
                    await ActiveCO2MonitorProvider.DisposeAsync();
                    ActiveCO2MonitorProvider = null;
                }

                // Whatever value we hold belongs to a connection that no longer exists; it is
                // re-read below once this one succeeds. Left in place, a failed connect kept it
                // on screen — and kept the start buttons enabled.
                CurrentCO2 = 0;

                if (device?.Device == null)
                    return;

                SelectedDevice = device;

                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: connecting to {device.DisplayName}...");
                var connected = await _ble.ConnectDeviceAsync(device.Device);
                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: connected={connected}");
                if (!connected)
                    return;

#if ANDROID
                // Give the Android GATT stack time to settle after connection —
                // without this, GetServiceAsync fails immediately on the first attempt.
                // Not needed on iOS: CoreBluetooth service discovery is event-driven, and
                // TryGetServiceAsync retries anyway if the stack isn't ready.
                await Task.Delay(500);
#endif

                // Use type already set during scan — avoids any post-connection GATT check
                var type = device.DetectedType
                    ?? CO2MonitorProviderFactory.DetectFromName(device.Device.Name)
                    ?? SelectedMonitorType;

                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: provider type={type}", LogMode.Verbose);
                ActiveCO2MonitorProvider = CO2MonitorProviderFactory.CreateProvider(type);
                if (ActiveCO2MonitorProvider == null)
                    return;

                // InitializeAsync is called ONCE here when connecting.
                // Refresh methods below must NOT call it again.
                bool ok;
                try
                {
                    ok = await ActiveCO2MonitorProvider.InitializeAsync(device.Device);
                }
                catch (Exception ex)
                {
                    Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: InitializeAsync threw: {ex.Message}");
                    await ActiveCO2MonitorProvider.DisposeAsync();
                    ActiveCO2MonitorProvider = null;
                    return;
                }
                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: initialized={ok}");
                if (!ok)
                {
                    await ActiveCO2MonitorProvider.DisposeAsync();
                    ActiveCO2MonitorProvider = null;
                    return;
                }

                CurrentCO2 = await ActiveCO2MonitorProvider.ReadCurrentCO2SafeAsync();
                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: initial CO2={CurrentCO2}");
            }
            finally
            {
                _opLock.Release();
            }
        }

        public void ZeroOutCO2Values()
        {
            CurrentCO2 = 0;
            UpdateInterval = 0;
            ClearHistory();
        }

        /// <summary>
        /// Drops the cached history without touching <see cref="UpdateInterval"/>.
        /// This buffer outlives any single recording and is only overwritten on a
        /// *successful* read, so a new recording must clear it explicitly — otherwise
        /// a failed first read leaves the previous recording's curve in place and the
        /// new recording adopts it. Do not use ZeroOutCO2Values for that: it also
        /// resets UpdateInterval, which the recording loop needs to space samples.
        /// </summary>
        public void ClearHistory()
        {
            Co2History = [];
        }

        public async Task RefreshLiveCO2Async()
        {
            Logger.WriteToLog("CO2MonitorManager|RefreshLiveCO2Async called", LogMode.Verbose);

            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null)
                return;

            // FIX: no InitializeAsync here — session is already established by
            // SelectDeviceAsync. Calling it on every refresh was the source of
            // repeated BLE subscription attempts that confused the sensor.
            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider == null) return;
                CurrentCO2 = await ActiveCO2MonitorProvider.ReadCurrentCO2SafeAsync();
            }
            finally
            {
                _opLock.Release();
            }
        }

        /// <summary>
        /// Re-reads the sensor's measurement interval. Returns false if it could not be
        /// read, in which case the previous value is kept: the providers report failure
        /// as -1 (connection lost) or 0 (Aranet read error), and storing either would
        /// make the recording loop fall through to its 1-minute default and mis-space
        /// every submitted sample.
        /// </summary>
        public async Task<bool> RefreshUpdateIntervalAsync()
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null)
                return false;

            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider == null) return false;
                int interval = await ActiveCO2MonitorProvider.ReadUpdateIntervalSafeAsync();
                if (interval <= 0)
                {
                    Logger.WriteToLog($"CO2MonitorManager|RefreshUpdateIntervalAsync: read failed ({interval}), keeping {UpdateInterval}s");
                    return false;
                }

                UpdateInterval = interval;
                return true;
            }
            finally
            {
                _opLock.Release();
            }
        }

        /// <summary>
        /// Returns false when the history could not be read (no provider, or the
        /// connection could not be revalidated). Callers must not treat
        /// <see cref="Co2History"/> as current in that case — it still holds the last
        /// successful read, which may belong to an earlier recording.
        /// </summary>
        public async Task<bool> RefreshHistoryAsync(ushort minutes)
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null)
                return false;

            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider == null) return false;
                var hist = await ActiveCO2MonitorProvider.ReadHistorySafeAsync(
                    minutes,
                    CO2MonitorManager.Instance.UpdateInterval);
                if (hist == null)
                    return false;

                Co2History = [.. hist];
                if (hist.Length > 0)
                    CurrentCO2 = hist[hist.Length - 1];
                return true;
            }
            finally
            {
                _opLock.Release();
            }
        }

        private BluetoothDeviceModel? _suspendedDevice;

        /// <summary>
        /// Closes the sensor connection when the app goes to the background, remembering
        /// the device so <see cref="ResumeConnectionAsync"/> can bring it back. Without
        /// this the GATT link dangles while the app is asleep and the stale handle
        /// obstructs the next connect. Callers must skip this while recording.
        /// </summary>
        public async Task SuspendConnectionAsync()
        {
            var device = SelectedDevice;
            if (device == null) return;

            Logger.WriteToLog($"CO2MonitorManager|SuspendConnectionAsync: releasing {device.DisplayName}");
            _suspendedDevice = device;
            await DisconnectAsync();
        }

        /// <summary>
        /// Reconnects the device released by <see cref="SuspendConnectionAsync"/>.
        /// A cached handle can go stale over a long background period; if the reconnect
        /// fails the provider simply stays null and the usual "no sensor" path applies.
        /// </summary>
        public async Task ResumeConnectionAsync()
        {
            var device = _suspendedDevice;
            _suspendedDevice = null;

            if (device == null || ActiveCO2MonitorProvider != null) return;

            Logger.WriteToLog($"CO2MonitorManager|ResumeConnectionAsync: reconnecting {device.DisplayName}");
            await SelectDeviceAsync(device);

            // GATT error 133 straight after returning to the foreground is common on Android and
            // usually transient; one retry after a short pause clears most of them. Only if the
            // user hasn't picked another sensor meanwhile — retrying then would switch back.
            if (ActiveCO2MonitorProvider == null && SelectedDevice?.Id == device.Id)
            {
                Logger.WriteToLog($"CO2MonitorManager|ResumeConnectionAsync: reconnect failed, retrying once");
                await Task.Delay(1000);
                if (ActiveCO2MonitorProvider == null && SelectedDevice?.Id == device.Id)
                    await SelectDeviceAsync(device);
            }
        }

        public async Task DisconnectAsync()
        {
            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();

                ActiveCO2MonitorProvider = null;
                SelectedDevice = null;
                CurrentCO2 = 0;  // see SelectDeviceAsync — a value with no connection behind it
            }
            finally
            {
                _opLock.Release();
            }
        }
    }
}