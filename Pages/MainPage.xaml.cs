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
        private bool _permissionsRequested = false;

        private bool sortAlphabetical = false;

        private IDispatcherTimer? _co2liveValueUpdateTimer;
        private CancellationTokenSource? _gpsCts;
        private List<BluetoothDeviceModel> _filteredDevices = [];

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

            if (!_permissionsRequested)
            {
                _permissionsRequested = true;

                var locationService = LocationServicePlatformProvider.CreateOrUse();
                if (!await locationService.HasLocationPermissionAsync())
                    await locationService.RequestLocationPermissionAsync();

                var btHelper = BluetoothPlatformProvider.CreateOrUse();
                if (!btHelper.CheckPermissions())
                    await btHelper.RequestPermissionsAsync();

                await StatusViewModel.Instance.RefreshNowAsync();
            }

            _gpsCts?.Cancel();
            _gpsCts = new CancellationTokenSource();
            _ = GpsRefreshLoopAsync(_gpsCts.Token);
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
            _gpsCts?.Cancel();
            _gpsCts = null;
        }

        private async Task GpsRefreshLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await _mainPageViewModel.BuildingSearch.GetGpsAsync(); } catch { }
                int delaySecs = _mainPageViewModel.BuildingSearch.HasValidGPS ? 60 : 15;
                try { await Task.Delay(TimeSpan.FromSeconds(delaySecs), ct); }
                catch (OperationCanceledException) { break; }
            }
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
            if (_mainPageViewModel == null) return; // fires during InitializeComponent before assignment

            if (sender == RadioButton100m)
                _mainPageViewModel.BuildingSearch.Range=100;

            if (sender == RadioButton250m)
                _mainPageViewModel.BuildingSearch.Range = 250;
        }

        private void DevicePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int index = CO2MonitorPicker.SelectedIndex;

            if (index >= 0 && index < _filteredDevices.Count)
            {
                var device = _filteredDevices[index];
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

            // Auto-retry once if nothing found — sensor may not have been advertising
            if (_mainPageViewModel.Sensor.Devices.Count == 0)
            {
                Logger.WriteToLog("RefreshSensorListAsync: no devices found, retrying scan...");
                await CommunityToolkit.Maui.Alerts.Toast.Make("No sensors found, retrying scan…").Show();
                await _mainPageViewModel.Sensor.StartScanAsync(
                    _mainPageViewModel.Sensor.SelectedMonitorType,
                    clearBeforeScan: false);
            }

            var sf = UserSettings.Instance.SensorFilter?.Trim() ?? "";
            _filteredDevices = _mainPageViewModel.Sensor.Devices
                .Where(d => string.IsNullOrEmpty(sf) ||
                            d.Name.Contains(sf, StringComparison.OrdinalIgnoreCase))
                .ToList();
            CO2MonitorPicker.ItemsSource = _filteredDevices.Select(d => d.Name).ToList();

            if (_filteredDevices.Count > 0)
            {
                CO2MonitorPicker.SelectedIndex = 0;
                _mainPageViewModel.Sensor.SelectDeviceAsync(_filteredDevices[0]).SafeFireAndForget("RefreshSensorListAsync|_mainPageViewModel.Sensor.SelectDeviceAsync");
            }
        }

        private void OnSearchBuildingsClicked(object sender, EventArgs e)
        {
            SearchBuildingsAsync().SafeFireAndForget("OnSearchBuildingsClicked|SearchBuildingsAsync");
        }

        private void OnSearchTransitClicked(object sender, EventArgs e)
        {
            SearchTransitAsync().SafeFireAndForget("OnSearchTransitClicked|SearchTransitAsync");
        }

        private async Task SearchTransitAsync()
        {
            await _mainPageViewModel.BuildingSearch.GetGpsAsync();
            if (!_mainPageViewModel.BuildingSearch.HasValidGPS)
            {
                _mainPageViewModel.Transit.Status = "No valid GPS data yet.";
                return;
            }
            double lat = _mainPageViewModel.BuildingSearch.Latitude!.Value;
            double lon = _mainPageViewModel.BuildingSearch.Longitude!.Value;
            await _mainPageViewModel.Transit.SearchTransitAsync(lat, lon, 250);
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
            int overrideMeters = UserSettings.Instance.CacheRangeOverrideMeters;
            int range = overrideMeters > 0
                ? overrideMeters
                : _mainPageViewModel.BuildingSearch.Range;

            // Load cached locations from SQLite and filter to the selected range
            var cachedLocations = await App.LocationCacheDb.GetAllAsync(userLat, userLon);
            var filtered = cachedLocations.Where(loc => loc.Distance <= range).ToHashSet();

            // Update the LocationStore
            LocationStore.Instance.SetBuildingLocations(filtered);

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
                if (!sensorVm.IsDeviceConnected)
                {
                    // Provider dropped (e.g. GATT error 133) — reconnect the same device.
                    Logger.WriteToLog("OnCo2TimerTick: provider gone, reconnecting...");
                    await sensorVm.SelectDeviceAsync(sensorVm.SelectedDevice);
                    return;
                }
                await sensorVm.RefreshLiveCO2Async();
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"CO₂ Live update failed: {ex}", minimumLogMode: Enumerations.LogMode.Default);
            }
        }


    }
}
