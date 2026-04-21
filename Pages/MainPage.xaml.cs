using CommunityToolkit.Maui.Views;
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Popups;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Extensions;

#if !WINDOWS
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling;
#endif

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
        private int _transitSearchRange = 250;
#if !WINDOWS
        private Mapsui.UI.Maui.MapControl? _routePreviewMap;
#endif


        public MainPage()
        {
            InitializeComponent();
            _mainPageViewModel = new MainPageViewModel();
            BindingContext = _mainPageViewModel;

            CO2MonitorPicker.ItemsSource = _mainPageViewModel.Sensor.Devices.Select(d => d.DisplayName).ToList();
            CO2MonitorPicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;

            sortAlphabetical = UserSettings.Instance.SortBuildingsAlphabetical;

            _mainPageViewModel.Transit.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TransitSearchViewModel.SelectedRouteGeometry) ||
                    e.PropertyName == nameof(TransitSearchViewModel.ShowRoutePreview) ||
                    e.PropertyName == nameof(TransitSearchViewModel.SelectedStation))
                    MainThread.BeginInvokeOnMainThread(RebuildRoutePreview);
            };
        }

        private void RebuildRoutePreview()
        {
            var geometry = _mainPageViewModel.Transit.SelectedRouteGeometry;
            if (!_mainPageViewModel.Transit.ShowRoutePreview || geometry == null)
            {
                RoutePreviewContainer.Content = null;
                return;
            }
#if !WINDOWS
            if (geometry.Points.Count < 2)
            {
                RoutePreviewContainer.Content = null;
                return;
            }

            var map = new Mapsui.Map();
            map.Widgets.Clear();
            map.Navigator.RotationLock = true;
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            // Parse route color (fallback purple)
            var routeColor = Mapsui.Styles.Color.FromArgb(255, 81, 43, 212);
            if (!string.IsNullOrEmpty(geometry.Color))
            {
                try
                {
                    var hex = geometry.Color.TrimStart('#');
                    if (hex.Length == 6)
                    {
                        int r = Convert.ToInt32(hex[..2], 16);
                        int g = Convert.ToInt32(hex[2..4], 16);
                        int b = Convert.ToInt32(hex[4..6], 16);
                        routeColor = Mapsui.Styles.Color.FromArgb(255, r, g, b);
                    }
                }
                catch { }
            }

            // Route polyline via NTS LineString
            var coords = geometry.Points
                .Select(p =>
                {
                    var (mx, my) = SphericalMercator.FromLonLat(p.Lon, p.Lat);
                    return new NetTopologySuite.Geometries.Coordinate(mx, my);
                }).ToArray();

            var line = new NetTopologySuite.Geometries.GeometryFactory().CreateLineString(coords);
            var routeFeature = new GeometryFeature { Geometry = line };
            routeFeature.Styles.Add(new Mapsui.Styles.VectorStyle
            {
                Line = new Mapsui.Styles.Pen(routeColor, 3)
            });
            map.Layers.Add(new MemoryLayer { Name = "Route", Features = [routeFeature], Style = null });

            // Start station pin
            var startStation = _mainPageViewModel.Transit.SelectedStation;
            if (startStation != null)
            {
                var (sx, sy) = SphericalMercator.FromLonLat(startStation.Longitude, startStation.Latitude);
                var startPin = new PointFeature(new MPoint(sx, sy));
                startPin.Styles.Add(new Mapsui.Styles.SymbolStyle
                {
                    SymbolType  = Mapsui.Styles.SymbolType.Ellipse,
                    Fill        = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 81, 43, 212)),
                    Outline     = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 0.6
                });
                map.Layers.Add(new MemoryLayer { Name = "Station", Features = [startPin], Style = null });
            }

            // Fit to route bounds with padding
            var minLon = geometry.Points.Min(p => p.Lon);
            var maxLon = geometry.Points.Max(p => p.Lon);
            var minLat = geometry.Points.Min(p => p.Lat);
            var maxLat = geometry.Points.Max(p => p.Lat);
            var (x0, y0) = SphericalMercator.FromLonLat(minLon, minLat);
            var (x1, y1) = SphericalMercator.FromLonLat(maxLon, maxLat);
            double padX = (x1 - x0) * 0.05;
            double padY = (y1 - y0) * 0.05;
            map.Navigator.ZoomToBox(new MRect(x0 - padX, y0 - padY, x1 + padX, y1 + padY), MBoxFit.Fit);

            _routePreviewMap = new Mapsui.UI.Maui.MapControl { Map = map, IsEnabled = false };
            RoutePreviewContainer.Content = _routePreviewMap;
#if ANDROID
            _routePreviewMap.HandlerChanged += (s, e) => SetupAndroidMapTouchInterception();
#endif
#else
            RoutePreviewContainer.Content = geometry != null ? new Label
            {
                Text = $"Route: {geometry.Points.Count} points (map not available on Windows)",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = 12
            } : null;
#endif
        }

#if ANDROID
        private void SetupAndroidMapTouchInterception()
        {
            if (_routePreviewMap?.Handler?.PlatformView is not Android.Views.View nativeView)
                return;
            nativeView.Touch += (s, args) =>
            {
                switch (args.Event?.ActionMasked)
                {
                    case Android.Views.MotionEventActions.Down:
                    case Android.Views.MotionEventActions.PointerDown:
                        nativeView.Parent?.RequestDisallowInterceptTouchEvent(true);
                        break;
                    case Android.Views.MotionEventActions.Up:
                    case Android.Views.MotionEventActions.Cancel:
                        nativeView.Parent?.RequestDisallowInterceptTouchEvent(false);
                        break;
                }
                args.Handled = false;
            };
        }
#endif

        private async Task SearchBuildingsAsync()
        {
            await _mainPageViewModel.BuildingSearch.SearchBuildingsAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            pageActive = true;
            // Sync the route-preview setting so it updates when user navigates back from Settings
            _mainPageViewModel.Transit.ShowRoutePreview = UserSettings.Instance.ShowRoutePreview;

            // Request permissions FIRST so BT is authorised before recovery/scan runs.
            // On iOS the CBCentralManager starts as Unknown until permission is granted;
            // recovery and scan attempts before that point silently find nothing.
            // _permissionsRequested ensures this block only runs once on first launch.
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

                // Immediately attempt GPS now that permission has been granted/confirmed,
                // rather than waiting for the first loop iteration (up to 15 seconds).
                try { await _mainPageViewModel.BuildingSearch.GetGpsAsync(); } catch { }
            }

            bool recovered = false;
            try
            {
                RecoveryOverlay.IsVisible = true;
                recovered = await TryRecoverRecordingAsync();
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"TryRecoverRecordingAsync failed: {ex.Message}");
                ManualResumeButton.IsVisible = true;
            }
            finally
            {
                RecoveryOverlay.IsVisible = false;
            }
            _mainPageViewModel.Settings.EnablePreRecording = false;
            StartCo2TimerOnce();
            _ = _mainPageViewModel.RefreshHasRecordingsAsync();

            if (PendingSuccessBanner)
            {
                PendingSuccessBanner = false;
                _ = ShowSuccessBannerAsync();
            }

            // GAP 3: only do the initial sensor scan when there was no recording to recover.
            // If recovery was attempted (whether it succeeded or failed), the BLE throttle
            // may already be close to its limit — skip the scan to avoid competing.
            // ManualResumeButton.IsVisible == true means recovery was tried but failed.
            // Also skip if the sensor is already connected (e.g. returning from a recovered recording
            // that was just submitted/aborted) — the live timer handles refresh from here.
            if (!_initialRefreshDone && !recovered && !ManualResumeButton.IsVisible
                && !_mainPageViewModel.Sensor.IsDeviceConnected)
            {
                _initialRefreshDone = true;
                OnRefreshSensorListClicked(RefreshButton, EventArgs.Empty);
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

            var snapshot2 = recoveryManager.LoadSnapshot();
            await NavigateAsync(snapshot2?.IsTransitRecording == true ? "///transit" : "///building");
            return true;
        }


        private void OnTransitSearchRangeChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;
            if (_mainPageViewModel == null) return;
            if (sender == TransitRadioButton250m) _transitSearchRange = 250;
            if (sender == TransitRadioButton500m) _transitSearchRange = 500;
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

        private void OnSmartHomeInfoTapped(object sender, EventArgs e)
        {
#if !WINDOWS
            this.ShowPopupAsync(new SmarthomeInfoPopUp()).SafeFireAndForget("OnSmartHomeInfoTapped");
#endif
        }

        //TODO: add filter from settings (not just type but also explicit string)
        private async Task RefreshSensorListAsync()
        {
            await _mainPageViewModel.Sensor.StartScanAsync(_mainPageViewModel.Sensor.SelectedMonitorType);

            // Auto-retry once if nothing found — sensor may not have been advertising.
            // Wait 3 s before retrying to avoid Android BLE scan throttle (5 starts / 30 s).
            if (_mainPageViewModel.Sensor.Devices.Count == 0)
            {
                Logger.WriteToLog("RefreshSensorListAsync: no devices found, retrying scan...");
                await CommunityToolkit.Maui.Alerts.Toast.Make("No sensors found, retrying scan…").Show();
                await Task.Delay(3000);
                await _mainPageViewModel.Sensor.StartScanAsync(
                    _mainPageViewModel.Sensor.SelectedMonitorType,
                    clearBeforeScan: false);
            }

            var sf = UserSettings.Instance.SensorFilter?.Trim() ?? "";
            _filteredDevices = _mainPageViewModel.Sensor.Devices
                .Where(d => string.IsNullOrEmpty(sf) ||
                            d.Name.Contains(sf, StringComparison.OrdinalIgnoreCase))
                .ToList();
            CO2MonitorPicker.ItemsSource = _filteredDevices.Select(d => d.DisplayName).ToList();

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
            await _mainPageViewModel.Transit.SearchTransitAsync(lat, lon, _transitSearchRange);
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

        private void OnOpenUrlClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string url)
                Launcher.OpenAsync(new Uri(url)).SafeFireAndForget("OnOpenUrlClicked");
        }

    }
}
