using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE;
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

        internal async Task StartScanningAsync(int scanDurationMs = 15000, bool clearBeforeScan = true, CO2MonitorType filter = CO2MonitorType.None, string deviceNameFilter ="")
        {
            //TODO: if we have a deviceNameFilter set we can probably cancel once we find it
            if (clearBeforeScan)
                Devices.Clear();

            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            //check permissions            

            var bluetoothHelper = BluetoothPlatformProvider.CreateOrUse();
            var btOn = bluetoothHelper.CheckIfBTEnabled();
            var btAllowed = bluetoothHelper.CheckPermissions();

            if (!btOn) return;
            if (!btAllowed) return;

            IsScanning = true;

            using var cts = new CancellationTokenSource(scanDurationMs);

            void Handler(object? sender, DeviceEventArgs e)
            {
                if (string.IsNullOrWhiteSpace(e.Device.Name)) return;
                if(!e.Device.Name.Contains(deviceNameFilter)) return;

                var detectedType = CO2MonitorProviderFactory.DetectFromName(e.Device.Name);
                if (detectedType.HasValue && (filter & detectedType.Value) != 0)
                {
                    var deviceModel = new BluetoothDeviceModel(e.Device);
                    if (!Devices.Contains(deviceModel))
                    {
                        Devices.Add(deviceModel);
                        DeviceDiscovered?.Invoke(this, deviceModel);
                    }
                }
            }
            
            _adapter.DeviceDiscovered -= Handler; //safeguard probably not strictly needed
            _adapter.DeviceDiscovered += Handler;
            await _adapter.StopScanningForDevicesAsync(); //might be needed if something still running (or might not be needed)
            try { await _adapter.StartScanningForDevicesAsync(cts.Token); }
            catch (Exception e)
            {
                Logger.WriteToLog("Error when calling _adapter.StartScanningForDevicesAsync:" + e.ToString());
                _adapter.DeviceDiscovered -= Handler;
                IsScanning = false;
            }

            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            _adapter.DeviceDiscovered -= Handler;
            IsScanning = false;
        }

        internal async Task<bool> ConnectDeviceAsync(IDevice device)
        {
            try
            {
                if (!_adapter.ConnectedDevices.Contains(device))
                    await _adapter.ConnectToDeviceAsync(device);
                return true;
            }
            catch { return false; }
        }
    }
}
