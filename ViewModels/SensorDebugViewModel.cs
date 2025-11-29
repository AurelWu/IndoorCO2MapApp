using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class DebugSensorPageViewModel :ObservableObject
    {
        private readonly BLEDeviceManager _bluetoothManager;

        public DebugSensorPageViewModel()
        {
            _bluetoothManager = BLEDeviceManager.Instance;

            Devices = _bluetoothManager.Devices;
            MonitorOptions = [.. MonitorTypes.SearchStringByMonitorTypeDebugMode.Keys];
            SelectedMonitorType = MonitorOptions.FirstOrDefault();

            _bluetoothManager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_bluetoothManager.IsScanning))
                {
                    IsScanning = _bluetoothManager.IsScanning; // updates the observable property
                }
            };
        }

#pragma warning disable IDE0079 // supresses warning about supressed warning...
#pragma warning disable MVVMTK0045 // only affects Win RT which doesn't play nice with ObservableProperty apparently

        [ObservableProperty]
        private int currentCO2;

        [ObservableProperty]
        private int measurementInterval;

        [ObservableProperty]
        private List<ushort> co2History = [];

        [ObservableProperty]
        private BluetoothDeviceModel? selectedDevice;

        [ObservableProperty]
        private CO2MonitorType selectedMonitorType;

        [ObservableProperty]
        private bool isScanning;

#pragma warning restore MVVMTK0045
#pragma warning restore IDE0079


        internal ObservableCollection<BluetoothDeviceModel> Devices { get; }

        internal List<CO2MonitorType> MonitorOptions { get; }

        // Call this whenever device selection changes in UI
        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            if (device?.Device == null) return;

            SelectedDevice = device;

            await BLEDeviceManager.ConnectAsync(device.Device);

            if (BLEDeviceManager.ActiveMonitorManager != null)
            {
                await BLEDeviceManager.ActiveMonitorManager.InitializeAsync(device.Device);
                CurrentCO2 = await BLEDeviceManager.ActiveMonitorManager.ReadCurrentCO2SafeAsync();
            }
        }

        internal async Task StartScanAsync(CO2MonitorType filter)
        {
            await _bluetoothManager.StartScanningAsync(filter: filter);
        }

        public async Task RetrieveCO2Async()
        {
            if (SelectedDevice == null || BLEDeviceManager.ActiveMonitorManager == null) return;

            await BLEDeviceManager.ActiveMonitorManager.InitializeAsync(SelectedDevice.Device);
            CurrentCO2 = await BLEDeviceManager.ActiveMonitorManager.ReadCurrentCO2SafeAsync();
        }

        public async Task RetrieveUpdateIntervalAsync()
        {
            if (SelectedDevice == null || BLEDeviceManager.ActiveMonitorManager == null) return;

            await BLEDeviceManager.ActiveMonitorManager.InitializeAsync(SelectedDevice.Device);
            MeasurementInterval = await BLEDeviceManager.ActiveMonitorManager.ReadUpdateIntervalSafeAsync();
        }

        public async Task RetrieveHistoryAsync(ushort amountOfMinutes)
        {
            if (SelectedDevice == null || BLEDeviceManager.ActiveMonitorManager == null)
                return;

            await BLEDeviceManager.ActiveMonitorManager.InitializeAsync(SelectedDevice.Device);

            //TODO => calculate based on interval or timesteps in concrete implementations for now it just returns last n values ignoring the interval
            
            var history = await BLEDeviceManager.ActiveMonitorManager.ReadHistorySafeAsync(amountOfMinutes);

            if (history != null)
            {
                Co2History = [..history];
            }
        }
    }
}
