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
        private readonly DebugSensorPageViewModel _viewModel;

        public DebugSensorPage()
        {
            InitializeComponent();

            // Create the viewmodel and set as binding context
            _viewModel = new DebugSensorPageViewModel();
            BindingContext = _viewModel;

            // Bind device list
            BluetoothDevicesList.ItemsSource = _viewModel.Devices;

            // Populate monitor type picker
            MonitorTypePicker.ItemsSource = _viewModel.MonitorOptions
                .Select(mt => MonitorTypes.SearchStringByMonitorTypeDebugMode[mt])
                .ToList();
            MonitorTypePicker.SelectedIndex = 0;

            // Event handlers
            BluetoothDevicesList.SelectionChanged += BluetoothDevicesList_SelectionChanged;
            MonitorTypePicker.SelectedIndexChanged += MonitorTypePicker_SelectedIndexChanged;
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
            _viewModel.StartScanAsync(_viewModel.SelectedMonitorType).SafeFireAndForget();
        }

        private void OnRetrieveDataFromMonitorClicked(object sender, EventArgs e)
        {
            //_viewModel.RetrieveCO2Async().SafeFireAndForget();
            //_viewModel.RetrieveUpdateIntervalAsync().SafeFireAndForget();
            _viewModel.RetrieveHistoryAsync(20).SafeFireAndForget();
            //TODO: also read History and maybe wait for one to finish before other instead of fire&forget at same time?
        }
    }
}
