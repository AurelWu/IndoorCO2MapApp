using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class MainPage : AppPage
    {
        private readonly SensorViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = new SensorViewModel();
            BindingContext = _viewModel;

            CO2MonitorPicker.ItemsSource = _viewModel.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;
        }

        // Sync picker selection with viewmodel
        private void DevicePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (CO2MonitorPicker.SelectedIndex >= 0)
            {
                var device = _viewModel.Devices[CO2MonitorPicker.SelectedIndex];
                _viewModel.SelectDeviceAsync(device).SafeFireAndForget();
            }
        }

        private void OnRefreshSensorListClicked(object? sender, EventArgs e)
        {
            SearchBluetoothDevicesAsync().SafeFireAndForget();
        }

        private async Task SearchBluetoothDevicesAsync()
        {
            await _viewModel.StartScanAsync(_viewModel.SelectedMonitorType);
            CO2MonitorPicker.ItemsSource = _viewModel.Devices.Select(d => d.Name).ToList();
        }

        //private void OnNavigateClicked(object sender, EventArgs e)
        //{
        //    if (sender is Button button && button.CommandParameter is string route)
        //    {
        //        NavigateAsync(route).SafeFireAndForget();
        //    }
        //}

        //private static async Task NavigateAsync(string route)
        //{
        //    await Shell.Current.GoToAsync(route);
        //}
    }
}
