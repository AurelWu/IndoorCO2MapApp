using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Bluetooth
{
    internal partial class BLEDeviceManager : INotifyPropertyChanged
    {
        private static readonly Lazy<BLEDeviceManager> _instance = new(() => new BLEDeviceManager());
        internal static BLEDeviceManager Instance => _instance.Value;

        private readonly IBluetoothLE _ble;
        internal readonly IAdapter _adapter;

        internal static BaseCO2MonitorManager? ActiveMonitorManager = null;

        internal ObservableCollection<BluetoothDeviceModel> Devices { get; } = [];

        private bool _isScanning;
        internal bool IsScanning
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

        public event EventHandler<BluetoothDeviceModel>? DeviceDiscovered;
        public event PropertyChangedEventHandler? PropertyChanged;

        private BLEDeviceManager()
        {
            _ble = CrossBluetoothLE.Current; // this should never be null on devices which have Bluetooth ( I think)
            _adapter = CrossBluetoothLE.Current.Adapter; // this should never be null on devices which have Bluetooth ( I think)

        }

        internal async Task StartScanningAsync(int scanDurationMs = 10000, bool clearBeforeScan = true, CO2MonitorType filter = CO2MonitorType.None)
        {
            if (clearBeforeScan)
                Devices.Clear();

            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            IsScanning = true;

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(scanDurationMs);

            void Handler(object? sender, DeviceEventArgs e)
            {
                if (string.IsNullOrEmpty(e.Device.Name))
                    return;

                // Try to find a matching monitor type
                CO2MonitorType deviceType = CO2MonitorType.None;
                foreach (var kv in MonitorTypes.MonitorTypeBySearchString)
                {
                    if (e.Device.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        deviceType = kv.Value;
                        break;
                    }
                }

                // Only add devices that match the current flags
                if ((filter & deviceType) != 0)
                {
                    var deviceModel = new BluetoothDeviceModel(e.Device);
                    if (!Devices.Contains(deviceModel))
                    {
                        Devices.Add(deviceModel);
                        DeviceDiscovered?.Invoke(this, deviceModel);
                    }
                }
            }

            _adapter.DeviceDiscovered += Handler;

            try
            {
                await _adapter.StartScanningForDevicesAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Normal when scan times out
            }

            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            _adapter.DeviceDiscovered -= Handler;

            IsScanning = false;
        }
    }
}
