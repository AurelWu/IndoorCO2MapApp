using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.PersistentData;
using Microsoft.VisualBasic;
using Plugin.BLE.Abstractions;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    public partial class CO2MonitorManager : ObservableObject
    {
        private static readonly Lazy<CO2MonitorManager> _instance = new(() => new CO2MonitorManager());
        public static CO2MonitorManager Instance => _instance.Value;

        private readonly BLEDeviceManager _ble;

        public List<CO2MonitorType> _monitorTypes;

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


        public async Task StartScanAsync(CO2MonitorType filter)
        {           
            await _ble.StartScanningAsync(filter: filter, deviceNameFilter: UserSettings.Instance.SensorFilter);
        }

        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            if (SelectedDevice == device) return; //same as current device selected again, no need to do anything
            if (ActiveCO2MonitorProvider != null)
            {
                await ActiveCO2MonitorProvider.DisposeAsync();
                ActiveCO2MonitorProvider = null;
            }

            if (device?.Device == null)
                return;

            SelectedDevice = device;

            var advertisedServices = device.Device.AdvertisementRecords?
                   .Where(r =>
                       r.Type == AdvertisementRecordType.UuidsComplete128Bit ||
                       r.Type == AdvertisementRecordType.UuidsIncomplete128Bit)
                   .SelectMany(r => r.Data.To128BitGuids())
                   .ToList();

            var connected = await _ble.ConnectDeviceAsync(device.Device);
            if (!connected)
                return;
            
           

            var type = await CO2MonitorProviderFactory.DetectFromNameOrAdvertisementAsync(device.Device,BLEDeviceManager.Instance._adapter) ?? SelectedMonitorType;
            ActiveCO2MonitorProvider = CO2MonitorProviderFactory.CreateProvider(type);
            if (ActiveCO2MonitorProvider == null)
                return;

            bool ok = await ActiveCO2MonitorProvider.InitializeAsync(device.Device);
            if (!ok)
            {
                //Initialisation is called from RefreshLiveCO2Async, RefreshUpdateIntervalAsync(), and RefreshHistoryAsync
                if (ActiveCO2MonitorProvider!= null)
                {
                    await ActiveCO2MonitorProvider.DisposeAsync();
                    ActiveCO2MonitorProvider = null;
                    return;
                }
                return;
            }
            if(ActiveCO2MonitorProvider!=null)
            {
                CurrentCO2 = await ActiveCO2MonitorProvider.ReadCurrentCO2SafeAsync();
            }
            if(ActiveCO2MonitorProvider==null)
            {
                Logger.WriteToLog("ActiveCO2MonitorProvider currently Null after Initialisation");
            }
            
        }

        public void ZeroOutCO2Values()
        {
            CurrentCO2 = 0;
            Co2History = [];
        }

        public async Task RefreshLiveCO2Async()
        {
            Logger.WriteToLog("CO2MonitorManager |RefreshLiveCO2Async called", LogMode.Verbose);
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null) return;
            await ActiveCO2MonitorProvider.InitializeAsync(SelectedDevice.Device);
            CurrentCO2 = await ActiveCO2MonitorProvider.ReadCurrentCO2SafeAsync();
        }

        public async Task RefreshUpdateIntervalAsync()
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null) return;
            await ActiveCO2MonitorProvider.InitializeAsync(SelectedDevice.Device);
            UpdateInterval = await ActiveCO2MonitorProvider.ReadUpdateIntervalSafeAsync();
        }

        public async Task RefreshHistoryAsync(ushort minutes)
        {
            if (ActiveCO2MonitorProvider == null || SelectedDevice?.Device == null) return;
            await ActiveCO2MonitorProvider.InitializeAsync(SelectedDevice.Device);
            var hist = await ActiveCO2MonitorProvider.ReadHistorySafeAsync(minutes, CO2MonitorManager.Instance.UpdateInterval);
            if (hist != null) Co2History = [.. hist];            
        }

        public async Task DisconnectAsync()
        {
            if (ActiveCO2MonitorProvider is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();

            ActiveCO2MonitorProvider = null;
            SelectedDevice = null;
        }
    }
}
