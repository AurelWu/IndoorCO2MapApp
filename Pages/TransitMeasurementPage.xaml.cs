using IndoorCO2MapAppV2.DataUpload;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class TransitMeasurementPage : AppPage
    {
        private TriState _windowsState = TriState.Unknown;
        private TriState _ventilationState = TriState.Unknown;
        private List<LocationData> _endpointStations = [];

        public TransitMeasurementPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;

            lineChartView.Clear();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;
                await UpdateChartAsync();
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
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
            MainThread.BeginInvokeOnMainThread(async () => await UpdateChartAsync());
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
                bool wasAtMax = TrimSlider.UpperValue >= TrimSlider.Maximum;
                TrimSlider.Maximum = data.Count - 1;
                if (wasAtMax || TrimSlider.UpperValue > data.Count - 1)
                    TrimSlider.UpperValue = data.Count - 1;

                lineChartView.SetData(data, (int)TrimSlider.LowerValue, (int)TrimSlider.UpperValue);
            });

            UpdateSubmitButtonState();
        }

        private void OnTrimChanged(object sender, EventArgs e)
        {
            _ = UpdateChartAsync();
        }

        private void UpdateSubmitButtonState()
        {
            var rec = RecordingManager.Instance.ActiveRecording;
            if (rec == null || rec.MeasurementData == null || TrimSlider == null)
            {
                SubmitButton.IsEnabled = false;
                return;
            }

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
                _windowsState = state;
        }

        private void OnVentilationChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;
            if (sender is RadioButton rb && rb.Value is TriState state)
                _ventilationState = state;
        }

        private void OnCustomNotesChanged(object sender, TextChangedEventArgs e) { }

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
                double lat = 51.332506, lon = 12.373544;
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
                    EndpointPicker.ItemsSource = _endpointStations;
                    if (_endpointStations.Count > 0)
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
                var submission = TransitSubmissionData.FromRecording(
                    rec,
                    trimMin: (int)TrimSlider.LowerValue,
                    trimMax: (int)TrimSlider.UpperValue,
                    notes: customNote,
                    endpoint: endpoint);

                Logger.WriteToLog("TransitMeasurementPage|SubmitRecordingAsync: " + submission.ToJson(), minimumLogMode: IndoorCO2MapAppV2.Enumerations.LogMode.Verbose);

                await Co2ApiGatewayClient.SubmitAsync(submission.ToJson(), SubmissionMode.Transit);

                if (UserSettings.Instance.EnableHistory)
                {
                    var persistentRecording = new PersistentRecording
                    {
                        DateTime = rec.RecordingStart,
                        LocationName = rec.LocationName,
                        NWRId = rec.NwrId,
                        NWRType = rec.NwrType,
                        Latitude = rec.Latitude,
                        Longitude = rec.Longitude,
                        AvgCO2 = rec.MeasurementData.Average(x => x.Ppm),
                        Values = string.Join(";", rec.MeasurementData.Select(x => x.Ppm)),
                        DoorWindowState = _windowsState,
                        VentilationState = _ventilationState,
                        CustomNotes = customNote,
                        SensorType = rec.CO2MonitorType,
                    };
                    await App.HistoryDatabase.SaveRecordingAsync(persistentRecording);
                }

                MainPage.PendingSuccessBanner = true;
                await RecordingManager.Instance.StopRecordingAsync();
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
    }
}
