using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace IndoorCO2MapAppV2.Pages
{
    //TODO: LocationPicker => picking something doesnt actually do anything yet I think, need to store

    public partial class MainPage : AppPage
    {
        private readonly MainPageViewModel _mainPageViewModel;
        private bool _initialRefreshDone = false;

        private bool sortAlphabetical = false;                                                                                          

        public MainPage()
        {
            InitializeComponent();
            _mainPageViewModel = new MainPageViewModel();
            BindingContext = _mainPageViewModel;

            CO2MonitorPicker.ItemsSource = _mainPageViewModel.Sensor.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;

            sortAlphabetical = UserSettings.Instance.SortBuildingsAlphabetical;

        }

        private async Task SearchBuildingsAsync()
        {
            await _mainPageViewModel.BuildingSearch.GetGpsAsync();
            await _mainPageViewModel.BuildingSearch.SearchBuildingsAsync();

        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Optional: prevent multiple auto-refreshes if navigating back & forth
            if (!_initialRefreshDone)
            {
                _initialRefreshDone = true;

                // Call the same method the button uses
                OnRefreshSensorListClicked(RefreshButton, EventArgs.Empty);
            }
        }

        private void OnSearchRangeChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // only when checked

            if (sender == RadioButton100m)
                _mainPageViewModel.BuildingSearch.Range=100;

            if (sender == RadioButton250m)
                _mainPageViewModel.BuildingSearch.Range = 250;
        }

        private void DevicePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int index = CO2MonitorPicker.SelectedIndex;

            if (index >= 0 && index < _mainPageViewModel.Sensor.Devices.Count)
            {
                var device = _mainPageViewModel.Sensor.Devices[index];
                _mainPageViewModel.Sensor.SelectDeviceAsync(device).SafeFireAndForget();
            }
        }

        private void OnRefreshSensorListClicked(object sender, EventArgs e)
        {
            RefreshSensorListAsync().SafeFireAndForget();
        }

        //TODO: add filter from settings (not just type but also explicit string)
        private async Task RefreshSensorListAsync()
        {
            await _mainPageViewModel.Sensor.StartScanAsync(_mainPageViewModel.Sensor.SelectedMonitorType);

            var deviceNames = _mainPageViewModel.Sensor.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.ItemsSource = deviceNames;

            if (deviceNames.Count > 0)
            {
                CO2MonitorPicker.SelectedIndex = 0;
                var firstDevice = _mainPageViewModel.Sensor.Devices[0];
                _mainPageViewModel.Sensor.SelectDeviceAsync(firstDevice).SafeFireAndForget();
            }
        }

        private void OnSearchBuildingsClicked(object sender, EventArgs e)
        {
            SearchBuildingsAsync().SafeFireAndForget();
        }
    }
}
