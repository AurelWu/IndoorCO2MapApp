using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DataUpload;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using System.Globalization;
using System.Linq;
#if !WINDOWS
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling;
#endif

namespace IndoorCO2MapAppV2.Pages
{
    public partial class TransitMeasurementPage : AppPage
    {
        private TriState _windowsState = TriState.Unknown;
        private TriState _ventilationState = TriState.Unknown;
        private List<LocationData> _endpointStations = [];
        private IDispatcherTimer? _countdownTimer;
        private int _secondsUntilUpdate;
        private CancellationTokenSource? _submitDelayCts;
        private bool _programmaticSliderUpdate;

        private readonly TransitSearchViewModel _changeRouteVm = new();
        private bool _changeRouteExpanded;
#if !WINDOWS
        private Mapsui.UI.Maui.MapControl? _changeRouteMapControl;
#endif

        public TransitMeasurementPage()
        {
            InitializeComponent();
            VersionLabel.Text = $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;

            if (RecordingManager.Instance.IsRecording)
                _ = RecordingManager.Instance.TriggerImmediateUpdateAsync();

            _secondsUntilUpdate = 30;
            UpdateSensorInfoLabel();
            StartCountdownTimer();

            lineChartView.Clear();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;

                var activeRec = RecordingManager.Instance.ActiveRecording;
                if (activeRec != null)
                {
                    NoteEditor.Text = activeRec.CustomNotes;
                    _windowsState = activeRec.DoorWindowState;
                    _ventilationState = activeRec.VentilationState;

                    WindowsUnknownRb.IsChecked = _windowsState == TriState.Unknown;
                    WindowsYesRb.IsChecked    = _windowsState == TriState.Yes;
                    WindowsNoRb.IsChecked     = _windowsState == TriState.No;

                    VentilationUnknownRb.IsChecked = _ventilationState == TriState.Unknown;
                    VentilationYesRb.IsChecked    = _ventilationState == TriState.Yes;
                    VentilationNoRb.IsChecked     = _ventilationState == TriState.No;

                    if (activeRec.AdditionalDataByParameter.TryGetValue("endpointName", out var epName)
                        && !string.IsNullOrEmpty(epName))
                    {
                        double.TryParse(activeRec.AdditionalDataByParameter.GetValueOrDefault("endpointLat", "0"),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out double epLat);
                        double.TryParse(activeRec.AdditionalDataByParameter.GetValueOrDefault("endpointLon", "0"),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out double epLon);
                        activeRec.AdditionalDataByParameter.TryGetValue("endpointType", out var epType);
                        long.TryParse(activeRec.AdditionalDataByParameter.GetValueOrDefault("endpointId", "0"), out long epId);

                        var recovered = new LocationData(epType ?? "node", epId, epName, epLat, epLon, epLat, epLon);
                        _endpointStations = [recovered];
                        EndpointPicker.ItemsSource = _endpointStations;
                        EndpointPicker.SelectedItem = recovered;
                        bool isFav = UserSettings.Instance.FavouriteLocationKeys.Contains(recovered.FavouriteKey);
                        EndpointStarLabel.TextColor = isFav ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");
                    }
                }

                await UpdateChartAsync();

                var rec = RecordingManager.Instance.ActiveRecording;
                if (rec != null
                    && double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("trimLow"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double tLow)
                    && double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("trimHigh"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double tHigh))
                {
                    _programmaticSliderUpdate = true;
                    TrimSlider.LowerValue = Math.Clamp((int)tLow, TrimSlider.Minimum, TrimSlider.Maximum);
                    TrimSlider.UpperValue = Math.Clamp((int)tHigh, TrimSlider.Minimum, TrimSlider.Maximum);
                    _programmaticSliderUpdate = false;
                }

                // ---- Change-route section setup ----
                ChangeRouteCard.IsVisible = UserSettings.Instance.ShowChangeRouteInRecording;
                if (!UserSettings.Instance.ShowChangeRouteInRecording) return;
                _changeRouteVm.ShowRoutePreview = UserSettings.Instance.ShowRoutePreview;
                RouteModeFilter.Items = new List<string>(_changeRouteVm.ModeFilterOptions);
                RouteModeFilter.SelectedItem = _changeRouteVm.ModeFilter;
                RouteModeFilter.SelectionChanged -= OnRouteModeFilterChanged;
                RouteModeFilter.SelectionChanged += OnRouteModeFilterChanged;

                _changeRouteVm.RefreshRoutes(preserveSelection: false);
                ChangeRoutePicker.ItemsSource = _changeRouteVm.FilteredRoutes;

                var activeRec2 = RecordingManager.Instance.ActiveRecording;
                if (activeRec2 != null && activeRec2.AdditionalDataByParameter.TryGetValue("routeName", out var currentRoute))
                    CurrentRouteLabel.Text = currentRoute;

                if (activeRec2 != null &&
                    double.TryParse(activeRec2.AdditionalDataByParameter.GetValueOrDefault("startLat", "0"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double sLat) &&
                    double.TryParse(activeRec2.AdditionalDataByParameter.GetValueOrDefault("startLon", "0"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double sLon) &&
                    sLat != 0)
                    _changeRouteVm.SetSearchCoordinates(sLat, sLon);

                _changeRouteVm.PropertyChanged -= OnChangeRouteVmPropertyChanged;
                _changeRouteVm.PropertyChanged += OnChangeRouteVmPropertyChanged;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _countdownTimer?.Stop();
            _submitDelayCts?.Cancel();
            _submitDelayCts?.Dispose();
            _submitDelayCts = null;
            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
        }

        private void TemporarilyDisableSubmit()
        {
            _submitDelayCts?.Cancel();
            _submitDelayCts?.Dispose();
            _submitDelayCts = new CancellationTokenSource();
            var token = _submitDelayCts.Token;
            SubmitButton.IsEnabled = false;
            Task.Run(async () =>
            {
                try
                {
                    var end = DateTime.UtcNow.AddMilliseconds(2000);
                    while (true)
                    {
                        double remaining = (end - DateTime.UtcNow).TotalSeconds;
                        if (remaining <= 0) break;
                        int label = (int)Math.Ceiling(remaining);
                        MainThread.BeginInvokeOnMainThread(() =>
                            SubmitButton.Text = $"{Localisation.SubmitRecordingButton} ({label}s)");
                        await Task.Delay(100, token);
                    }
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _submitDelayCts = null;
                        SubmitButton.Text = Localisation.SubmitRecordingButton;
                        UpdateSubmitButtonState();
                    });
                }
                catch (OperationCanceledException) { }
            });
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            double targetWidth = width * 0.80;
            lineChartView.WidthRequest = targetWidth;
            TrimSlider.WidthRequest = targetWidth - 25;
            TrimSlider.ForceLayout();
        }

        private void OnMeasurementUpdated()
        {
            _secondsUntilUpdate = 30;
            MainThread.BeginInvokeOnMainThread(async () => await UpdateChartAsync());
        }

        private void StartCountdownTimer()
        {
            _countdownTimer?.Stop();
            _countdownTimer = Dispatcher.CreateTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += OnCountdownTick;
            _countdownTimer.Start();
        }

        private void OnCountdownTick(object? sender, EventArgs e)
        {
            if (_secondsUntilUpdate > 0) _secondsUntilUpdate--;
            NextUpdateLabel.Text = $"{Localisation.RecordingNextUpdateLabel} {_secondsUntilUpdate}s";
            UpdateSensorInfoLabel();
        }

        private bool _isInstantUpdating;

        private void OnInstantUpdateClicked(object sender, EventArgs e)
        {
            if (_isInstantUpdating) return;
            DoInstantUpdateAsync().SafeFireAndForget("OnInstantUpdateClicked");
        }

        private async Task DoInstantUpdateAsync()
        {
            _isInstantUpdating = true;
            InstantUpdateButton.IsEnabled = false;
            _secondsUntilUpdate = 0;
            try
            {
                await RecordingManager.Instance.TriggerImmediateUpdateAsync();
            }
            finally
            {
                _isInstantUpdating = false;
                InstantUpdateButton.IsEnabled = true;
            }
        }

        private void UpdateSensorInfoLabel()
        {
            var device = CO2MonitorManager.Instance.SelectedDevice;
            var co2 = CO2MonitorManager.Instance.CurrentCO2;
            string name = device?.DisplayName ?? "-";
            string co2Text = co2 > 0 ? $"{co2}ppm" : "-";
            SensorInfoLabel.Text = $"{name} | Current CO2: {co2Text}";
        }

        private async Task UpdateChartAsync()
        {
            var rec = RecordingManager.Instance.ActiveRecording;
            if (rec == null || rec.MeasurementData == null || rec.MeasurementData.Count == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (SubmitButton != null)
                        SubmitButton.IsEnabled = false;
                });
                return;
            }

            var data = rec.MeasurementData;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (TrimSlider == null || lineChartView == null) return;
                _programmaticSliderUpdate = true;
                try
                {
                    if (data.Count >= 2)
                    {
                        bool wasAtMax = TrimSlider.UpperValue >= TrimSlider.Maximum;
                        TrimSlider.Maximum = data.Count - 1;
                        if (wasAtMax || TrimSlider.UpperValue > data.Count - 1)
                            TrimSlider.UpperValue = data.Count - 1;
                    }
                }
                finally
                {
                    _programmaticSliderUpdate = false;
                }

                lineChartView.SetData(data, (int)TrimSlider.LowerValue, (int)TrimSlider.UpperValue);
            });

            UpdateSubmitButtonState();
        }

        private void OnTrimChanged(object sender, EventArgs e)
        {
            if (_programmaticSliderUpdate || TrimSlider == null || !RecordingManager.Instance.IsRecording) return;
            RecordingManager.Instance.UpdateTrimSnapshot(TrimSlider.LowerValue, TrimSlider.UpperValue);
            TemporarilyDisableSubmit();
            _ = UpdateChartAsync();
        }

        private void UpdateSubmitButtonState()
        {
            if (_submitDelayCts != null && !_submitDelayCts.IsCancellationRequested) return;
            var rec = RecordingManager.Instance.ActiveRecording;
            if (rec == null || rec.MeasurementData == null || TrimSlider == null || SubmitButton == null)
                return;

            int trimStart = (int)TrimSlider.LowerValue;
            int trimEnd = (int)TrimSlider.UpperValue;

            var trimmed = rec.MeasurementData
                .Skip(trimStart)
                .Take(trimEnd - trimStart + 1)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (trimmed.Count < 5)
                {
                    SubmitButton.IsEnabled = false;
                    SubmitButton.Text = Localisation.SubmitRecordingButtonNeedData;
                }
                else
                {
                    SubmitButton.IsEnabled = true;
                    SubmitButton.Text = Localisation.SubmitRecordingButton;
                }
            });
        }

        private void OnWindowsChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;
            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _windowsState = state;
                RecordingManager.Instance.UpdateRecoverySnapshot(_windowsState, _ventilationState, NoteEditor.Text ?? "");
                TemporarilyDisableSubmit();
            }
        }

        private void OnVentilationChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;
            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _ventilationState = state;
                RecordingManager.Instance.UpdateRecoverySnapshot(_windowsState, _ventilationState, NoteEditor.Text ?? "");
                TemporarilyDisableSubmit();
            }
        }

        private void OnCustomNotesChanged(object sender, TextChangedEventArgs e)
        {
            RecordingManager.Instance.UpdateRecoverySnapshot(
                _windowsState,
                _ventilationState,
                NoteEditor.Text ?? "");
            TemporarilyDisableSubmit();
        }

        private void OnEndpointPickerSelectionChanged(object sender, EventArgs e)
        {
            var loc = EndpointPicker.SelectedItem as LocationData;
            if (loc == null)
            {
                EndpointStarLabel.TextColor = Color.FromArgb("#BDBDBD");
                RecordingManager.Instance.UpdateEndpointSnapshot(null);
                return;
            }
            bool isFav = UserSettings.Instance.FavouriteLocationKeys.Contains(loc.FavouriteKey);
            EndpointStarLabel.TextColor = isFav ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");
            RecordingManager.Instance.UpdateEndpointSnapshot(loc);
        }

        private void OnEndpointStarTapped(object sender, EventArgs e)
        {
            var loc = EndpointPicker.SelectedItem as LocationData;
            if (loc == null) return;
            var key = loc.FavouriteKey;
            var keys = new List<string>(UserSettings.Instance.FavouriteLocationKeys);
            if (!keys.Remove(key)) keys.Add(key);
            UserSettings.Instance.FavouriteLocationKeys = keys;
            bool isFav = keys.Contains(key);
            EndpointStarLabel.TextColor = isFav ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");

            // Re-sort endpoint list with favourites first
            if (_endpointStations.Count == 0) return;
            var sorted = _endpointStations
                .Where(s => UserSettings.Instance.FavouriteLocationKeys.Contains(s.FavouriteKey))
                .Concat(_endpointStations.Where(s => !UserSettings.Instance.FavouriteLocationKeys.Contains(s.FavouriteKey)))
                .ToList();
            EndpointPicker.ItemsSource = sorted;
            var stillSelected = sorted.FirstOrDefault(s => s.FavouriteKey == key);
            if (stillSelected != null)
                EndpointPicker.SelectedItem = stillSelected;
        }

        private void OnSearchEndpointClicked(object sender, EventArgs e)
            => SearchEndpointAsync().SafeFireAndForget("TransitMeasurementPage|OnSearchEndpointClicked");

        private async Task SearchEndpointAsync()
        {
            EndpointSearchIndicator.IsVisible = true;
            EndpointSearchIndicator.IsRunning = true;
            EndpointStatusLabel.IsVisible = false;
            SearchEndpointButton.IsEnabled = false;
            try
            {
#if WINDOWS
                double lat = 51.3406, lon = 12.3747;
#else
                var locationService = LocationServicePlatformProvider.CreateOrUse();
                var loc = await locationService.GetCurrentLocationAsync();
                if (loc == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        EndpointStatusLabel.Text = "Could not get GPS position.";
                        EndpointStatusLabel.IsVisible = true;
                    });
                    return;
                }
                double lat = loc.Latitude, lon = loc.Longitude;
#endif
                var (stations, _) = await PMTilesTransitService.Instance.SearchAsync(
                    lat, lon, 250);

                _endpointStations = stations;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var favKeys = UserSettings.Instance.FavouriteLocationKeys;
                    var sorted = _endpointStations
                        .Where(s => favKeys.Contains(s.FavouriteKey))
                        .Concat(_endpointStations.Where(s => !favKeys.Contains(s.FavouriteKey)))
                        .ToList();
                    EndpointPicker.ItemsSource = sorted;
                    if (sorted.Count > 0)
                        EndpointPicker.SelectedIndex = 0;
                    if (_endpointStations.Count == 0)
                    {
                        EndpointStatusLabel.Text = "No stops found nearby.";
                        EndpointStatusLabel.IsVisible = true;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("TransitMeasurementPage|SearchEndpointAsync failed: " + ex.Message);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EndpointStatusLabel.Text = "Search failed.";
                    EndpointStatusLabel.IsVisible = true;
                });
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    EndpointSearchIndicator.IsVisible = false;
                    EndpointSearchIndicator.IsRunning = false;
                    SearchEndpointButton.IsEnabled = true;
                });
            }
        }

        private void ResetEndpointPicker()
        {
            _endpointStations = [];
            EndpointPicker.ItemsSource = null;
            EndpointPicker.SelectedItem = null;
            EndpointStarLabel.TextColor = Color.FromArgb("#BDBDBD");
            EndpointStatusLabel.IsVisible = false;
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CancelMeasurementAsync().SafeFireAndForget("TransitMeasurementPage|OnCancelClicked|CancelMeasurementAsync");
        }

        private async Task CancelMeasurementAsync()
        {
            bool answer = await DisplayAlertAsync(
                "Cancel Measurement",
                "Are you sure you want to cancel and return to Home?",
                "Yes",
                "No"
            );

            if (answer)
            {
                await RecordingManager.Instance.StopRecordingAsync();
                ResetEndpointPicker();
                await NavigateAsync("///home");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CancelMeasurementAsync();
            return true;
        }

        private void OnSubmitRecordingClicked(object sender, EventArgs e)
        {
            SubmitRecordingAsync().SafeFireAndForget("TransitMeasurementPage|OnSubmitRecordingClicked|SubmitRecordingAsync");
        }

        private async Task SubmitRecordingAsync()
        {
            if (UserSettings.Instance.ConfirmUpload)
            {
                bool answer = await DisplayAlertAsync(
                    "Submit Measurement",
                    "Are you sure you want to submit the measurement?",
                    "Yes", "No");
                if (!answer) return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.IsEnabled = false);
            string originalButtonText = SubmitButton.Text;
            string customNote = NoteEditor.Text?.Trim() ?? "";

            await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.Text = "Submitting data...");

            try
            {
                var rec = RecordingManager.Instance.ActiveRecording;
                if (rec == null) return;

                var endpoint = EndpointPicker.SelectedItem as LocationData;
                string submissionId = Converter.GenerateSubmissionId();
                var submission = TransitSubmissionData.FromRecording(
                    rec,
                    trimMin: (int)TrimSlider.LowerValue,
                    trimMax: (int)TrimSlider.UpperValue,
                    notes: customNote,
                    endpoint: endpoint,
                    submissionId: submissionId);

                Logger.WriteToLog("TransitMeasurementPage|SubmitRecordingAsync: " + submission.ToJson(), minimumLogMode: IndoorCO2MapAppV2.Enumerations.LogMode.Verbose);

                await Co2ApiGatewayClient.SubmitAsync(submission.ToJson(), SubmissionMode.Transit);

                if (UserSettings.Instance.EnableHistory)
                {
                    // Build description: "Line 42 (Central Station => North Terminal)" or just "Line 42 (Central Station)"
                    string routePart = rec.LocationName.Contains(" (")
                        ? rec.LocationName.Substring(0, rec.LocationName.LastIndexOf(" ("))
                        : rec.LocationName;
                    string startName = rec.AdditionalDataByParameter.TryGetValue("startName", out var sn) ? sn : "";
                    string locationName = endpoint != null
                        ? $"{routePart} ({startName} => {endpoint.Name})"
                        : rec.LocationName;

                    int trimMin = (int)TrimSlider.LowerValue;
                    int trimMax = (int)TrimSlider.UpperValue;
                    var trimmed = rec.MeasurementData
                        .Skip(trimMin)
                        .Take(trimMax - trimMin + 1)
                        .ToList();

                    double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("startLat", "0"),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double startLat);
                    double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("startLon", "0"),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double startLon);

                    var persistentRecording = new PersistentRecording
                    {
                        DateTime = rec.RecordingStart,
                        LocationName = locationName,
                        NWRId = rec.NwrId,
                        NWRType = rec.NwrType,
                        Latitude = startLat != 0 ? startLat : rec.Latitude,
                        Longitude = startLon != 0 ? startLon : rec.Longitude,
                        AvgCO2 = trimmed.Average(x => x.Ppm),
                        Values = string.Join(";", trimmed.Select(x => x.Ppm)),
                        DoorWindowState = _windowsState,
                        VentilationState = _ventilationState,
                        CustomNotes = customNote,
                        SensorType = rec.CO2MonitorType,
                        DestinationLatitude   = endpoint?.Latitude,
                        DestinationLongitude  = endpoint?.Longitude,
                        DestinationName       = endpoint?.Name ?? "",
                        IsTransitRecording    = true,
                        SubmissionId         = submissionId,
                    };
                    await App.HistoryDatabase.SaveRecordingAsync(persistentRecording);
                }

                MainPage.PendingSuccessBanner = true;
                await RecordingManager.Instance.StopRecordingAsync();
                ResetEndpointPicker();
                await NavigateAsync("///home");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(
                    "Upload Failed",
                    $"Something went wrong while submitting your data.\n\nDetails: {ex.Message}",
                    "OK");
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SubmitButton.Text = originalButtonText;
                    SubmitButton.IsEnabled = true;
                });
            }
        }

        // ---- Change Transit Line ----

        private void OnChangeRouteVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TransitSearchViewModel.SelectedRouteGeometry) ||
                e.PropertyName == nameof(TransitSearchViewModel.IsLoadingRouteGeometry))
                MainThread.BeginInvokeOnMainThread(UpdateChangeRoutePreview);
        }

        private void OnChangeRouteToggleClicked(object sender, EventArgs e)
        {
            _changeRouteExpanded = !_changeRouteExpanded;
            ChangeRouteSection.IsVisible = _changeRouteExpanded;
            ChangeRouteToggleButton.Text = _changeRouteExpanded ? "▲" : "▼";
        }

        private void OnRouteModeFilterChanged(object? sender, string mode)
        {
            _changeRouteVm.ModeFilterChangedCommand.Execute(mode);
            RefreshChangeRoutePickerItems();
        }

        private void OnChangeRoutePickerChanged(object sender, EventArgs e)
        {
            if (ChangeRoutePicker.SelectedItem is not TransitLineData route) return;
            _changeRouteVm.SelectedRoute = route;
            UpdateChangeRouteStarColor();
        }

        private void OnChangeRouteStarTapped(object sender, EventArgs e)
        {
            _changeRouteVm.ToggleRouteFavouriteCommand.Execute(null);
            RefreshChangeRoutePickerItems();
            UpdateChangeRouteStarColor();
        }

        private void OnRouteFilterTextChanged(object sender, TextChangedEventArgs e)
        {
            _changeRouteVm.RouteFilterText = e.NewTextValue ?? "";
            RefreshChangeRoutePickerItems();
        }

        private void RefreshChangeRoutePickerItems()
        {
            ChangeRoutePicker.ItemsSource = null;
            ChangeRoutePicker.ItemsSource = _changeRouteVm.FilteredRoutes;
            if (_changeRouteVm.SelectedRoute != null &&
                _changeRouteVm.FilteredRoutes.Contains(_changeRouteVm.SelectedRoute))
                ChangeRoutePicker.SelectedItem = _changeRouteVm.SelectedRoute;
        }

        private void UpdateChangeRouteStarColor()
        {
            ChangeRouteStarLabel.TextColor = _changeRouteVm.IsRouteFavourited
                ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");
        }

        private void OnSearchRoutesClicked(object sender, EventArgs e)
            => SearchRoutesAsync().SafeFireAndForget("TransitMeasurementPage|OnSearchRoutesClicked");

        private async Task SearchRoutesAsync()
        {
            RouteSearchButton.IsEnabled = false;
            RouteSearchIndicator.IsVisible = true;
            RouteSearchIndicator.IsRunning = true;
            try
            {
#if WINDOWS
                double lat = 51.3406, lon = 12.3747;
#else
                var loc = await LocationServicePlatformProvider.CreateOrUse().GetCurrentLocationAsync();
                if (loc == null) return;
                double lat = loc.Latitude, lon = loc.Longitude;
#endif
                await _changeRouteVm.SearchTransitAsync(lat, lon, 250);
                RefreshChangeRoutePickerItems();
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    RouteSearchIndicator.IsVisible = false;
                    RouteSearchIndicator.IsRunning = false;
                    RouteSearchButton.IsEnabled = true;
                });
            }
        }

        private void OnConfirmRouteChangeClicked(object sender, EventArgs e)
        {
            var route = _changeRouteVm.SelectedRoute;
            if (route == null) return;
            RecordingManager.Instance.UpdateRouteSnapshot(route.ID.ToString(), route.Name);
            MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;
            CurrentRouteLabel.Text = route.Name;
            _changeRouteExpanded = false;
            ChangeRouteSection.IsVisible = false;
            ChangeRouteToggleButton.Text = "▼";
        }

        private void UpdateChangeRoutePreview()
        {
            RoutePreviewLoadingIndicator.IsVisible = _changeRouteVm.IsLoadingRouteGeometry;
            RoutePreviewLoadingIndicator.IsRunning = _changeRouteVm.IsLoadingRouteGeometry;

            var geometry = _changeRouteVm.SelectedRouteGeometry;
            if (!UserSettings.Instance.ShowRoutePreview || geometry == null || geometry.Points.Count < 2)
            {
                RoutePreviewBorder.IsVisible = false;
                ChangeRoutePreviewContainer.Content = null;
                return;
            }

#if !WINDOWS
            var map = new Mapsui.Map();
            map.Widgets.Clear();
            map.Navigator.RotationLock = true;
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

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

            var minLon = geometry.Points.Min(p => p.Lon);
            var maxLon = geometry.Points.Max(p => p.Lon);
            var minLat = geometry.Points.Min(p => p.Lat);
            var maxLat = geometry.Points.Max(p => p.Lat);
            var (x0, y0) = SphericalMercator.FromLonLat(minLon, minLat);
            var (x1, y1) = SphericalMercator.FromLonLat(maxLon, maxLat);
            double padX = (x1 - x0) * 0.05;
            double padY = (y1 - y0) * 0.05;
            map.Navigator.ZoomToBox(new MRect(x0 - padX, y0 - padY, x1 + padX, y1 + padY), MBoxFit.Fit);

            _changeRouteMapControl = new Mapsui.UI.Maui.MapControl { Map = map, IsEnabled = false };
            ChangeRoutePreviewContainer.Content = _changeRouteMapControl;
#if ANDROID
            _changeRouteMapControl.HandlerChanged += (s, ev) => SetupAndroidChangeRouteMapTouchInterception();
#endif
#else
            ChangeRoutePreviewContainer.Content = new Label
            {
                Text = $"Route: {geometry.Points.Count} points",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = 12
            };
#endif
            RoutePreviewBorder.IsVisible = true;
        }

#if ANDROID
        private void SetupAndroidChangeRouteMapTouchInterception()
        {
            if (_changeRouteMapControl?.Handler?.PlatformView is not Android.Views.View nativeView) return;
            nativeView.Touch += (s, args) =>
            {
                switch (args.Event?.ActionMasked)
                {
                    case Android.Views.MotionEventActions.Down:
                    case Android.Views.MotionEventActions.PointerDown:
                        nativeView.Parent?.RequestDisallowInterceptTouchEvent(true); break;
                    case Android.Views.MotionEventActions.Up:
                    case Android.Views.MotionEventActions.Cancel:
                        nativeView.Parent?.RequestDisallowInterceptTouchEvent(false); break;
                }
                args.Handled = false;
            };
        }
#endif
    }
}
