using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Resources.Strings;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class SensorViewModel : ObservableObject
    {
        private readonly CO2MonitorManager _monitorManager;

        public SensorViewModel()
        {
            _monitorManager = CO2MonitorManager.Instance;

            Devices = _monitorManager.Devices;
            MonitorOptions = _monitorManager._monitorTypes;
            SelectedMonitorType = MonitorOptions.FirstOrDefault();

            _monitorManager.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(CO2MonitorManager.IsScanning):
                        IsScanning = _monitorManager.IsScanning;
                        break;
                    case nameof(CO2MonitorManager.CurrentCO2):
                        CurrentCO2 = _monitorManager.CurrentCO2;
                        break;
                    case nameof(CO2MonitorManager.UpdateInterval):
                        MeasurementInterval = _monitorManager.UpdateInterval;
                        break;
                    case nameof(CO2MonitorManager.Co2History):
                        Co2History = _monitorManager.Co2History;
                        break;
                    case nameof(CO2MonitorManager.SelectedDevice):
                        SelectedDevice = _monitorManager.SelectedDevice;
                        break;
                    case nameof(CO2MonitorManager.SelectedMonitorType):
                        SelectedMonitorType = _monitorManager.SelectedMonitorType;
                        break;
                }
            };
        }

#pragma warning disable IDE0079
#pragma warning disable MVVMTK0045

        [ObservableProperty] private int currentCO2;
        [ObservableProperty] private int measurementInterval;
        [ObservableProperty] private List<ushort> co2History = [];
        [ObservableProperty] private BluetoothDeviceModel? selectedDevice;
        [ObservableProperty] private CO2MonitorType selectedMonitorType;
        [ObservableProperty] private bool isScanning;

#pragma warning restore MVVMTK0045
#pragma warning restore IDE0079

        internal ObservableCollection<BluetoothDeviceModel> Devices { get; }
        internal List<CO2MonitorType> MonitorOptions { get; }

        public bool IsDeviceConnected =>
            _monitorManager.ActiveCO2MonitorProvider != null && _monitorManager.SelectedDevice != null;

        public string SelectedDeviceStatusText
        {
            get
            {
                if (IsScanning)
                    return Localisation.ScanningStatusLabel;

                if (SelectedDevice != null)
                    return Localisation.CO2LevelsLabel + CurrentCO2 + " | " + Localisation.UpdateInterval + MeasurementInterval + "s";

                return Localisation.NoSensorFoundStatusLabel;
            }
        }

        public Color StatusDotColor =>
            SelectedDevice != null ? Color.FromArgb("#4CAF50") :
            IsScanning             ? Color.FromArgb("#512BD4") :
                                     Color.FromArgb("#9E9E9E");

        public async Task StartScanAsync(CO2MonitorType filter, bool clearBeforeScan = true)
        {
            _monitorManager.ZeroOutCO2Values();
            await _monitorManager.StartScanAsync(filter, clearBeforeScan);
        }

        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            _monitorManager.ZeroOutCO2Values();
            if (device == null) return;
            await _monitorManager.SelectDeviceAsync(device);
            await RefreshLiveCO2Async();
            await RefreshUpdateIntervalAsync();
            //RefreshHistoryAsync(10).SafeFireAndForget(); // used to check if we can successfully get the history (but actual check still TODO), should trigger bonding request on mobiles.

            // On slow devices (e.g. Android 12) the GATT stack may not be fully settled
            // after the initial read, leaving CurrentCO2 = 0. Retry until we get a value
            // or the user selects a different device.
            for (int i = 0; i < 5 && CurrentCO2 == 0 && SelectedDevice == device; i++)
            {
                Logger.WriteToLog($"SensorViewModel|SelectDeviceAsync retry {i + 1}/5: CO2 still 0, waiting 3s...");
                await Task.Delay(3000);
                if (SelectedDevice == device)
                {
                    await RefreshLiveCO2Async();
                    Logger.WriteToLog($"SensorViewModel|SelectDeviceAsync retry {i + 1}/5: CO2 after read = {CurrentCO2}");
                }
            }
        }

        public async Task RefreshLiveCO2Async()
        {
            Logger.WriteToLog("SensorViewModel |RefreshLiveCO2Async called", LogMode.Verbose);
            await _monitorManager.RefreshLiveCO2Async();
        }

        public async Task RefreshUpdateIntervalAsync()
        {
            await _monitorManager.RefreshUpdateIntervalAsync();
        }

        public async Task RefreshHistoryAsync(ushort minutes)
        {
            await _monitorManager.RefreshHistoryAsync(minutes);
        }

        public async Task DisconnectAsync()
        {
            await _monitorManager.DisconnectAsync();
        }

        partial void OnIsScanningChanged(bool value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
            OnPropertyChanged(nameof(StatusDotColor));
        }

        partial void OnSelectedDeviceChanged(BluetoothDeviceModel? value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
            OnPropertyChanged(nameof(StatusDotColor));
        }

        partial void OnCurrentCO2Changed(int value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
        }

        partial void OnMeasurementIntervalChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
        }
    }
}
