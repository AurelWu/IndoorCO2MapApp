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

            // Start from the manager's current state rather than empty. The manager outlives
            // the page: after the app is closed and reopened while the process lives on, a new
            // page builds a new view model that otherwise believes no sensor is selected while
            // one is still connected — so the picker couldn't show it and recording couldn't start.
            SelectedDevice = _monitorManager.SelectedDevice;
            CurrentCO2 = _monitorManager.CurrentCO2;
            MeasurementInterval = _monitorManager.UpdateInterval;
            Co2History = _monitorManager.Co2History;
            IsScanning = _monitorManager.IsScanning;

            _monitorManager.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(CO2MonitorManager.IsScanning):
                        IsScanning = _monitorManager.IsScanning;
                        break;
                    case nameof(CO2MonitorManager.CurrentCO2):
                        CurrentCO2 = _monitorManager.CurrentCO2;
                        IsSmartHomeWarningVisible =
                            (_monitorManager.ActiveCO2MonitorProvider as IndoorCO2MapAppV2.CO2Monitors.AranetProvider)
                            ?.IsSmartHomeDisabled ?? false;
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
                    case nameof(CO2MonitorManager.ActiveCO2MonitorProvider):
                        // IsDeviceConnected is computed from the manager, so announce it here —
                        // the start buttons and status line depend on it.
                        OnPropertyChanged(nameof(IsDeviceConnected));
                        OnPropertyChanged(nameof(SelectedDeviceStatusText));
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

        public bool HasSelectedDevice => SelectedDevice != null;

        public string SelectedDeviceStatusText
        {
            get
            {
                if (IsScanning)
                    return Localisation.ScanningStatusLabel;

                if (SelectedDevice != null)
                {
                    // Not connected (yet, or the connect failed): don't show numbers that look
                    // live. A failed reconnect used to leave the last CO2 value on screen.
                    if (!IsDeviceConnected || (CurrentCO2 == 0 && MeasurementInterval == 0))
                        return Localisation.SensorWaitingForData;
                    return Localisation.CO2LevelsLabel + CurrentCO2 + " | " + Localisation.UpdateInterval + MeasurementInterval + "s";
                }

                return Localisation.NoSensorFoundStatusLabel;
            }
        }

        public Color StatusDotColor =>
            SelectedDevice != null ? Color.FromArgb("#4CAF50") :
            IsScanning             ? Color.FromArgb("#512BD4") :
                                     Color.FromArgb("#9E9E9E");

        public async Task StartScanAsync(CO2MonitorType filter, bool clearBeforeScan = true, int scanDurationMs = 20000, CancellationToken cancellationToken = default)
        {
            _monitorManager.ZeroOutCO2Values();
            await _monitorManager.StartScanAsync(filter, clearBeforeScan, scanDurationMs, cancellationToken);
        }

        [ObservableProperty] private bool isSmartHomeWarningVisible;

        public async Task SelectDeviceAsync(BluetoothDeviceModel device)
        {
            if (device == null) return;

            // Only a genuine switch invalidates the cached readings. Reselecting the current
            // sensor (timer reconnect, recovery) used to zero UpdateInterval regardless, and a
            // recording tick landing in that window read 5-minute history as 1-minute data.
            // Compared by Id: a rescan can wrap the same sensor in a new device object.
            if (_monitorManager.SelectedDevice?.Id != device.Id)
            {
                _monitorManager.ZeroOutCO2Values();
                IsSmartHomeWarningVisible = false;
            }
            await _monitorManager.SelectDeviceAsync(device);
            await RefreshLiveCO2Async();
            await RefreshUpdateIntervalAsync();
            //RefreshHistoryAsync(10).SafeFireAndForget(); // used to check if we can successfully get the history (but actual check still TODO), should trigger bonding request on mobiles.

            // On slow devices (e.g. Android 12) the GATT stack may not be fully settled
            // after the initial read, leaving CurrentCO2 = 0. Retry until we get a value
            // or the user selects a different device.
            for (int i = 0; i < 5 && CurrentCO2 == 0 && !IsSmartHomeWarningVisible && SelectedDevice == device; i++)
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
            IsSmartHomeWarningVisible =
                (_monitorManager.ActiveCO2MonitorProvider as IndoorCO2MapAppV2.CO2Monitors.AranetProvider)
                ?.IsSmartHomeDisabled ?? false;
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
            OnPropertyChanged(nameof(HasSelectedDevice));
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
