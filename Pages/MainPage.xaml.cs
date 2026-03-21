using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
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
        public static bool PendingSuccessBanner { get; set; }

        private readonly MainPageViewModel _mainPageViewModel;
        private bool _initialRefreshDone = false;

        private bool sortAlphabetical = false;

        private IDispatcherTimer? _co2liveValueUpdateTimer;

        private bool pageActive = true;


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
            pageActive = true;
            bool recovered = await TryRecoverRecordingAsync();
            _mainPageViewModel.Settings.EnablePreRecording = false;
            StartCo2TimerOnce();
            _ = _mainPageViewModel.RefreshHasRecordingsAsync();

            if (PendingSuccessBanner)
            {
                PendingSuccessBanner = false;
                _ = ShowSuccessBannerAsync();
            }

            //only do it at startup once
            if (!_initialRefreshDone && !recovered)
            {
                _initialRefreshDone = true;
                OnRefreshSensorListClicked(RefreshButton, EventArgs.Empty);
            }
        }

        private async Task ShowSuccessBannerAsync()
        {
            SuccessBanner.TranslationY = -100;
            SuccessBanner.IsVisible = true;
            await SuccessBanner.TranslateTo(0, 0, 300, Easing.CubicOut);
            await Task.Delay(2500);
            await SuccessBanner.TranslateTo(0, -100, 300, Easing.CubicIn);
            SuccessBanner.IsVisible = false;
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            pageActive = false;
        }

        protected async Task<bool> TryRecoverRecordingAsync()
        {
            ManualResumeButton.IsVisible = false;
            var recoveryManager = RecoveryManager.Instance;
            recoveryManager.Initialize(BLEDeviceManager.Instance._adapter, RecordingManager.Instance);

            var snapshot = recoveryManager.LoadSnapshot();
            if (snapshot == null ) return false;
            
            bool recovered = await recoveryManager.TryAutoRecoverAsync(_mainPageViewModel.Sensor); //this sets the activeRecording to the saved state //TODO: also seed active sensor to correct one
            if (!recovered)
            {
                Logger.WriteToLog("Automatic Recovery failed, showing manual resume button");
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
                _mainPageViewModel.Sensor.SelectDeviceAsync(device).SafeFireAndForget("DevicePicker_SelectedIndexChanged|_mainPageViewModel.Sensor.SelectDeviceAsync");
            }
        }

        private void OnRefreshSensorListClicked(object sender, EventArgs e)
        {
            RefreshSensorListAsync().SafeFireAndForget("OnRefreshSensorListClicked|RefreshSensorListAsync");
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
                _mainPageViewModel.Sensor.SelectDeviceAsync(firstDevice).SafeFireAndForget("RefreshSensorListAsync|_mainPageViewModel.Sensor.SelectDeviceAsync");
            }
        }

        private void OnSearchBuildingsClicked(object sender, EventArgs e)
        {
            SearchBuildingsAsync().SafeFireAndForget("OnSearchBuildingsClicked|SearchBuildingsAsync");
        }

        private void OnGetCachedLocationsClicked(object sender, EventArgs e)
        {
            LoadCachedLocationsAsync().SafeFireAndForget("OnGetCachedLocationsClicked|LoadCachedLocationsAsync");
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

        private void StartCo2TimerOnce()
        {
            if (_co2liveValueUpdateTimer != null)
                return;

            _co2liveValueUpdateTimer = Dispatcher.CreateTimer();
            _co2liveValueUpdateTimer.Interval = TimeSpan.FromSeconds(15);
            _co2liveValueUpdateTimer.Tick += OnCo2TimerTick;
            _co2liveValueUpdateTimer.Start();
        }

        private async void OnCo2TimerTick(object? sender, EventArgs e)
        {
            
            if (!pageActive) return;
            Logger.WriteToLog("OnCo2TimerTick Called && is after active page check return");
            var sensorVm = _mainPageViewModel.Sensor;
            if (sensorVm.SelectedDevice == null)
                return;

            try
            {
                await sensorVm.RefreshLiveCO2Async();
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"CO₂ Live update failed: {ex}", minimumLogMode: Enumerations.LogMode.Default);
            }
        }


    }
}
