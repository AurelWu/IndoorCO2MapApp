using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Bluetooth
{
    internal partial class BLEDeviceManager : INotifyPropertyChanged
    {
        private static readonly Lazy<BLEDeviceManager> _instance = new(() => new BLEDeviceManager());
        internal static BLEDeviceManager Instance => _instance.Value;

        private readonly IBluetoothLE _ble;
        internal readonly IAdapter _adapter;

        internal ObservableCollection<BluetoothDeviceModel> Devices { get; } = [];

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsScanning)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<BluetoothDeviceModel>? DeviceDiscovered;

        private BLEDeviceManager()
        {
            _ble = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;
        }

        internal async Task StartScanningAsync(int scanDurationMs = 20000, bool clearBeforeScan = true, CO2MonitorType filter = CO2MonitorType.None)
        {
            //TODO: if we have a deviceNameFilter set we can probably cancel once we find it
            if (clearBeforeScan)
                Devices.Clear();

            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            //check permissions

            var bluetoothHelper = BluetoothPlatformProvider.CreateOrUse();
            var btAllowed = bluetoothHelper.CheckPermissions();
            if (!btAllowed) return;

            // On iOS, CBCentralManager starts as Unknown and takes ~1-2s to settle to PoweredOn.
            // Poll up to 5s so the first scan at cold start is not silently skipped.
            await WaitForBluetoothReadyAsync(TimeSpan.FromSeconds(5));

            if (!bluetoothHelper.CheckIfBTEnabled()) return;

            IsScanning = true;

            using var cts = new CancellationTokenSource(scanDurationMs);

            async void Handler(object? sender, DeviceEventArgs e)
            {
                try
                {
                    Logger.WriteToLog($"BLEDeviceManager|Scan: saw '{e.Device.Name}' ({e.Device.Id})", LogMode.Verbose);

                    var detectedType = await CO2MonitorProviderFactory.DetectFromNameOrAdvertisementAsync(
                        e.Device,
                        _adapter
                    );

                    // Guard against stale additions: detection may complete after the scan
                    // ended and Devices was already cleared for the next scan.
                    if (cts.IsCancellationRequested) return;

                    if (!detectedType.HasValue || (filter & detectedType.Value) == 0)
                    {
                        Logger.WriteToLog($"BLEDeviceManager|Scan: '{e.Device.Name}' rejected (detectedType={detectedType?.ToString() ?? "null"}, filter={filter})", LogMode.Verbose);
                        return;
                    }

                    var deviceModel = new BluetoothDeviceModel(e.Device) { DetectedType = detectedType };

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Deduplicate by device ID — Plugin.BLE may produce different IDevice
                        // instances for the same physical device across repeated advertisement events.
                        if (Devices.Any(d => d.Device.Id == e.Device.Id))
                            return;

                        Logger.WriteToLog($"BLEDeviceManager|Scan: adding '{e.Device.Name}' as {detectedType}");
                        Devices.Add(deviceModel);
                        DeviceDiscovered?.Invoke(this, deviceModel);
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteToLog("Error in BLE device discovered handler: " + ex.ToString());
                }
            }

            _adapter.DeviceDiscovered -= Handler;
            _adapter.DeviceDiscovered += Handler;
            try
            {
                await _adapter.StartScanningForDevicesAsync(cts.Token);
            }
            catch (Exception e)
            {
                Logger.WriteToLog("Error when calling _adapter.StartScanningForDevicesAsync:" + e.ToString());
            }
            finally
            {
                if (_adapter.IsScanning)
                    await _adapter.StopScanningForDevicesAsync();
                _adapter.DeviceDiscovered -= Handler;
                IsScanning = false;
            }
        }

        private static async Task WaitForBluetoothReadyAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var state = CrossBluetoothLE.Current.State;
                Logger.WriteToLog($"BLEDeviceManager|WaitForBT: state={state}", LogMode.Verbose);
                if (state == BluetoothState.On)
                    return;
                if (state == BluetoothState.Off || state == BluetoothState.Unavailable)
                {
                    Logger.WriteToLog("BLEDeviceManager|WaitForBT: BT is off/unavailable, stopping early");
                    return;
                }
                await Task.Delay(500);
            }
            Logger.WriteToLog("BLEDeviceManager|WaitForBT: timed out");
        }

        internal async Task<bool> ConnectDeviceAsync(IDevice device)
        {
            try
            {
                if (!_adapter.ConnectedDevices.Contains(device))
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                    await _adapter.ConnectToDeviceAsync(device, cancellationToken: cts.Token);
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.WriteToLog("ConnectDeviceAsync timed out after 12s");
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("ConnectDeviceAsync failed: " + ex.Message);
                return false;
            }
        }
    }
}
