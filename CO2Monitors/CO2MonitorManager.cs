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

        public async Task StartScanAsync(CO2MonitorType filter, bool clearBeforeScan = true)
        {
            await _ble.StartScanningAsync(
                clearBeforeScan: clearBeforeScan,
                filter: filter);
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

                if (device?.Device == null)
                    return;

                SelectedDevice = device;

                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: connecting to {device.DisplayName}...");
                var connected = await _ble.ConnectDeviceAsync(device.Device);
                Logger.WriteToLog($"CO2MonitorManager|SelectDeviceAsync: connected={connected}");
                if (!connected)
                    return;

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

        public async Task RefreshUpdateIntervalAsync()
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null)
                return;

            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider == null) return;
                UpdateInterval = await ActiveCO2MonitorProvider.ReadUpdateIntervalSafeAsync();
            }
            finally
            {
                _opLock.Release();
            }
        }

        public async Task RefreshHistoryAsync(ushort minutes)
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null)
                return;

            await _opLock.WaitAsync();
            try
            {
                if (ActiveCO2MonitorProvider == null) return;
                var hist = await ActiveCO2MonitorProvider.ReadHistorySafeAsync(
                    minutes,
                    CO2MonitorManager.Instance.UpdateInterval);
                if (hist != null)
                    Co2History = [.. hist];
            }
            finally
            {
                _opLock.Release();
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
            }
            finally
            {
                _opLock.Release();
            }
        }
    }
}