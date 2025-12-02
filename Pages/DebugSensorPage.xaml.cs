using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Linq;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class DebugSensorPage : AppPage
    {
        private readonly SensorViewModel _viewModel;

        public DebugSensorPage()
        {
            InitializeComponent();

            // Create the viewmodel and set as binding context
            _viewModel = new SensorViewModel();
            BindingContext = _viewModel;

            // Bind device list
            //BluetoothDevicesList.ItemsSource = _viewModel.Devices;

            // Bind device list to Picker
            DevicePicker.ItemsSource = _viewModel.Devices.Select(d => d.Name).ToList();


            // Populate monitor type picker
            MonitorTypePicker.ItemsSource = _viewModel.MonitorOptions
                .Select(mt => MonitorTypes.SearchStringByMonitorTypeDebugMode[mt])
                .ToList();
            MonitorTypePicker.SelectedIndex = 0;

            // Event handlers
            //BluetoothDevicesList.SelectionChanged += BluetoothDevicesList_SelectionChanged;
            MonitorTypePicker.SelectedIndexChanged += MonitorTypePicker_SelectedIndexChanged;
            DevicePicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;
        }

        private void BluetoothDevicesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            HandleSelectionChangedAsync(e).SafeFireAndForget();
        }

        private async Task HandleSelectionChangedAsync(SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection?.Count > 0)
            {
                var device = e.CurrentSelection[0] as BluetoothDeviceModel;
                if (device != null)
                    await _viewModel.SelectDeviceAsync(device);
            }
            else
            {
                _viewModel.SelectedDevice = null;
            }
        }

        private void MonitorTypePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (MonitorTypePicker.SelectedIndex >= 0)
            {
                _viewModel.SelectedMonitorType = _viewModel.MonitorOptions[MonitorTypePicker.SelectedIndex];
            }
        }

        private void OnSearchBluetoothDevicesClicked(object sender, EventArgs e)
        {
            SearchBluetoothDevicesAsync().SafeFireAndForget();
        }

        private async Task SearchBluetoothDevicesAsync()
        {
            await _viewModel.StartScanAsync(_viewModel.SelectedMonitorType);

            var deviceNames = _viewModel.Devices.Select(d => d.Name).ToList();
            DevicePicker.ItemsSource = deviceNames;

            if (deviceNames.Count > 0)
            {
                DevicePicker.SelectedIndex = 0;

                var firstDevice = _viewModel.Devices[0];
                await _viewModel.SelectDeviceAsync(firstDevice);
            }
        }

        private void OnRetrieveDataFromMonitorClicked(object sender, EventArgs e)
        {
            _viewModel.RefreshLiveCO2Async().SafeFireAndForget();
            _viewModel.RefreshHistoryAsync(20).SafeFireAndForget();         
        }

        // Sync picker selection with viewmodel
        private void DevicePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (DevicePicker.SelectedIndex >= 0)
            {
                var device = _viewModel.Devices[DevicePicker.SelectedIndex];
                _viewModel.SelectDeviceAsync(device).SafeFireAndForget();
            }
        }
    }
}
