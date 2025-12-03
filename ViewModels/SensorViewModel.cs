using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
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

        public string SelectedDeviceStatusText
        {
            get
            {
                if (IsScanning)
                    return Localisation.ScanningStatusLabel;

                if (SelectedDevice != null)
                    return Localisation.CO2LevelsLabel + CurrentCO2;

                return Localisation.NoSensorFoundStatusLabel;
            }
        }

        public async Task StartScanAsync(CO2MonitorType filter)
        {
            await _monitorManager.StartScanAsync(filter);
        }

        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            if (device == null) return;
            await _monitorManager.SelectDeviceAsync(device);
            await RefreshLiveCO2Async();
        }

        public async Task RefreshLiveCO2Async()
        {
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
        }

        partial void OnSelectedDeviceChanged(BluetoothDeviceModel? value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
        }

        partial void OnCurrentCO2Changed(int value)
        {
            OnPropertyChanged(nameof(SelectedDeviceStatusText));
        }
    }
}
