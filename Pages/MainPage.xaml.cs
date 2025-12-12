using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System.Globalization;
using System.Threading.Tasks;

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

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            bool recovered = await TryRecoverRecordingAsync();

            //only do it at startup once
            if (!_initialRefreshDone && !recovered)
            {
                _initialRefreshDone = true;

                OnRefreshSensorListClicked(RefreshButton, EventArgs.Empty);
            }
        }

        protected async Task<bool> TryRecoverRecordingAsync()
        {
            ManualResumeButton.IsVisible = false;
            var recoveryService = RecoveryManager.Instance;
            recoveryService.Initialize(BLEDeviceManager.Instance._adapter, RecordingManager.Instance);

            var snapshot = recoveryService.LoadSnapshot();
            if (snapshot == null) return false;
            
            bool recovered = await recoveryService.TryAutoRecoverAsync(_mainPageViewModel.Sensor); //this sets the activeRecording to the saved state //TODO: also seed active sensor to correct one
            if (!recovered)
            {
                // Show Manual Resume button
                ManualResumeButton.IsVisible = true;
                return false;
                // Show UI: automatic recovery failed.
                // Show Manual Resume Button => will use active Recording but with current sensor... might need to write that to the active recording
            }

            await NavigateAsync("///building");
            return true;
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

        private void OnGetCachedLocationsClicked(object sender, EventArgs e)
        {
            LoadCachedLocationsAsync().SafeFireAndForget();
        }

        private async Task LoadCachedLocationsAsync()
        {
            await _mainPageViewModel.BuildingSearch.GetGpsAsync();
            // Make sure we have user location for distance calculation
            if (!_mainPageViewModel.BuildingSearch.HasValidGPS)
            {
                _mainPageViewModel.BuildingSearch.Status = "No valid GPS data yet.";
                return;
            }

            double userLat = _mainPageViewModel.BuildingSearch.Latitude!.Value;
            double userLon = _mainPageViewModel.BuildingSearch.Longitude!.Value;

            // Load cached locations from SQLite
            var cachedLocations = await App.LocationCacheDb.GetAllAsync(userLat, userLon);

            // Update the LocationStore
            LocationStore.Instance.SetBuildingLocations(cachedLocations);

            // Refresh UI list
            _mainPageViewModel.BuildingSearch.RefreshBuildings();            
        }

        private async void OnManualResumeClicked(object sender, EventArgs e)
        {
            var recoveryService = RecoveryManager.Instance;
            var snapshot = recoveryService.LoadSnapshot();
            if (snapshot == null) return;

            // Use current selected sensor
            var selectedDevice = _mainPageViewModel.Sensor.SelectedDevice;
            if (selectedDevice == null)
            {
                await DisplayAlertAsync("No Sensor", "Please select a sensor first.", "OK");
                return;
            }

            await _mainPageViewModel.Sensor.SelectDeviceAsync(selectedDevice);
            await RecordingManager.Instance.TryRecoverRecordingAfterDeviceReadyAsync(snapshot, selectedDevice.Id.ToString());

            ManualResumeButton.IsVisible = false;

            await NavigateAsync("///building");
        }
    }
}
