using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Bluetooth
{
    public partial class BLEDeviceManager : INotifyPropertyChanged
    {
        private static readonly Lazy<BLEDeviceManager> _instance = new(() => new BLEDeviceManager());
        public static BLEDeviceManager Instance => _instance.Value;

        private readonly IBluetoothLE _ble;
        private readonly IAdapter _adapter;

        public ObservableCollection<BluetoothDeviceModel> Devices { get; } = [];

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

        public event EventHandler<BluetoothDeviceModel>? DeviceDiscovered;
        public event PropertyChangedEventHandler? PropertyChanged;

        private BLEDeviceManager()
        {
            _ble = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;
            _adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
        }

        private void Adapter_DeviceDiscovered(object? sender, DeviceEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Device.Name))
            {
                var deviceModel = new BluetoothDeviceModel(e.Device);
                if (!Devices.Contains(deviceModel))
                {
                    Devices.Add(deviceModel);
                    DeviceDiscovered?.Invoke(this, deviceModel);
                }
            }
        }

        public async Task StartScanningAsync(int scanDurationMs = 10000, bool clearBeforeScan = true, string? filter = null)
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
                if (!string.IsNullOrEmpty(e.Device.Name) &&
                    (string.IsNullOrEmpty(filter) || e.Device.Name.Contains(filter)))
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
