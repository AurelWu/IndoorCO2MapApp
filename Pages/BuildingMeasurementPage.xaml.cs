using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DataUpload;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using System.Diagnostics;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class BuildingMeasurementPage : AppPage
    {
        private TriState _doorsWindowsState = TriState.Unknown;
        private TriState _ventilationState = TriState.Unknown;
        private IDispatcherTimer? _countdownTimer;
        private int _secondsUntilUpdate;
        private CancellationTokenSource? _submitDelayCts;
        private bool _programmaticSliderUpdate;
        private double? _pendingTrimLow;
        private double? _pendingTrimHigh;
        private bool _autoFollowMax = true;

        public BuildingMeasurementPage()
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

            // Clears chart so previous recording doesn't show
            lineChartView.Clear();

            _pendingTrimLow = null;
            _pendingTrimHigh = null;
            _autoFollowMax = true;

            _programmaticSliderUpdate = true;
            TrimSlider.Minimum = 0;
            TrimSlider.Maximum = 20;
            TrimSlider.LowerValue = 0;
            TrimSlider.UpperValue = 20;
            _programmaticSliderUpdate = false;

            // UI-safe async initialization
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;
                await UpdateChartAsync();

                var rec = RecordingManager.Instance.ActiveRecording;
                if (rec != null
                    && double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("trimLow"),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tLow)
                    && double.TryParse(rec.AdditionalDataByParameter.GetValueOrDefault("trimHigh"),
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tHigh))
                {
                    _pendingTrimLow = tLow;
                    _pendingTrimHigh = tHigh;
                    await UpdateChartAsync();
                }
            });

            // TODO: check activeRecording for recoveryValues to set UI
            var activeRec = RecordingManager.Instance.ActiveRecording;
            if (activeRec == null) return;
            TriState windowState = activeRec.DoorWindowState;
            TriState ventilationState = activeRec.VentilationState;
            string customNotes = activeRec.CustomNotes;

            if (windowState == TriState.Unknown)
            {
                DoorsWindowsUnknownRb.IsChecked = true;
                DoorsWindowsNoRb.IsChecked = false;
                DoorsWindowsYesRb.IsChecked = false;
            }
            else if (windowState == TriState.No)
            {
                DoorsWindowsUnknownRb.IsChecked = false;
                DoorsWindowsNoRb.IsChecked = true;
                DoorsWindowsYesRb.IsChecked = false;
            }
            else if (windowState == TriState.Yes)
            {
                DoorsWindowsUnknownRb.IsChecked = false;
                DoorsWindowsNoRb.IsChecked = false;
                DoorsWindowsYesRb.IsChecked = true;
            }

            if (ventilationState == TriState.Unknown)
            {
                VentilationUnknownRb.IsChecked = true;
                VentilationNoRb.IsChecked = false;
                VentilationYesRb.IsChecked = false;
            }
            else if (ventilationState == TriState.No)
            {
                VentilationUnknownRb.IsChecked = false;
                VentilationNoRb.IsChecked = true;
                VentilationYesRb.IsChecked = false;
            }
            else if (ventilationState == TriState.Yes)
            {
                VentilationUnknownRb.IsChecked = false;
                VentilationNoRb.IsChecked = false;
                VentilationYesRb.IsChecked = true;
            }

            NoteEditor.Text = customNotes;
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
            double sliderWidth = targetWidth - 25;

            lineChartView.WidthRequest = targetWidth;
            TrimSlider.WidthRequest = sliderWidth;
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
                        TrimSlider.Maximum = data.Count - 1;

                        if (_pendingTrimHigh.HasValue)
                        {
                            int targetHigh = (int)_pendingTrimHigh.Value;
                            if (data.Count - 1 >= targetHigh)
                            {
                                TrimSlider.UpperValue = targetHigh;
                                _pendingTrimHigh = null;
                                _autoFollowMax = false;
                            }
                            else
                            {
                                TrimSlider.UpperValue = data.Count - 1;
                            }
                        }
                        else if (_autoFollowMax || TrimSlider.UpperValue > data.Count - 1)
                        {
                            TrimSlider.UpperValue = data.Count - 1;
                        }

                        if (_pendingTrimLow.HasValue)
                        {
                            int targetLow = (int)_pendingTrimLow.Value;
                            if (targetLow <= (int)TrimSlider.UpperValue)
                            {
                                TrimSlider.LowerValue = targetLow;
                                _pendingTrimLow = null;
                            }
                        }
                    }
                }
                finally
                {
                    _programmaticSliderUpdate = false;
                }

                lineChartView.SetData(
                    data,
                    (int)TrimSlider.LowerValue,
                    (int)TrimSlider.UpperValue
                );
            });

            UpdateSubmitButtonState();
        }

        private void OnTrimChanged(object sender, EventArgs e)
        {
            if (_programmaticSliderUpdate || TrimSlider == null || !RecordingManager.Instance.IsRecording) return;
            _autoFollowMax = false;
            RecordingManager.Instance.UpdateTrimSnapshot(TrimSlider.LowerValue, TrimSlider.UpperValue);
            TemporarilyDisableSubmit();
            _ = UpdateChartAsync();
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
                ResetPageForNewMeasurement();
                await NavigateAsync("///home");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CancelMeasurementAsync();
            return true;
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CancelMeasurementAsync().SafeFireAndForget("OnCancelClicked|CancelMeasurementAsync");
        }

        private void OnSubmitRecordingClicked(object sender, EventArgs e)
        {
            SubmitRecordingAsync().SafeFireAndForget("OnSubmitRecordingClicked|SubmitRecordingAsync");
        }

        private async Task SubmitRecordingAsync()
        {
            if (UserSettings.Instance.ConfirmUpload)
            {
                bool answer = await DisplayAlertAsync(
                    "Submit Measurement",
                    "Are you sure you want to submit the measurement",
                    "Yes",
                    "No"
                );

                if (!answer)
                    return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.IsEnabled = false);

            string originalButtonText = SubmitButton.Text;
            string customNote = NoteEditor.Text?.Trim() ?? "";

            await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.Text = "Submitting data...");

            try
            {
                var rec = RecordingManager.Instance.ActiveRecording;
                if (rec == null) return;

                string submissionId = Converter.GenerateSubmissionId();

                var builder = new APISubmissionBuilder(
                    rec,
                    trimMin: (int)TrimSlider.LowerValue,
                    trimMax: (int)TrimSlider.UpperValue
                );

                var submission = builder
                    .WithOpenWindowsDoors(_doorsWindowsState)
                    .WithVentilationSystem(_ventilationState)
                    .WithNotes(customNote)
                    .WithSubmissionId(submissionId)
                    .Build();

                await Co2ApiGatewayClient.SubmitAsync(
                    submission.ToJson(),
                    SubmissionMode.Building
                );

                if (UserSettings.Instance.EnableHistory)
                {
                    int trimMin = (int)TrimSlider.LowerValue;
                    int trimMax = (int)TrimSlider.UpperValue;
                    var trimmed = rec.MeasurementData
                        .Skip(trimMin)
                        .Take(trimMax - trimMin + 1)
                        .ToList();

                    var persistentRecording = new PersistentRecording
                    {
                        DateTime = rec.RecordingStart,
                        LocationName = rec.LocationName,
                        NWRId = rec.NwrId,
                        NWRType = rec.NwrType,
                        Latitude = rec.Latitude,
                        Longitude = rec.Longitude,
                        AvgCO2 = trimmed.Average(x => x.Ppm),
                        Values = string.Join(";", trimmed.Select(x => x.Ppm)),
                        DoorWindowState = _doorsWindowsState,
                        VentilationState = _ventilationState,
                        CustomNotes = customNote,
                        SensorType = rec.CO2MonitorType,
                        SubmissionId = submissionId,
                    };

                    await App.HistoryDatabase.SaveRecordingAsync(persistentRecording);
                }

                MainPage.PendingSuccessBanner = true;
                await RecordingManager.Instance.StopRecordingAsync();
                ResetPageForNewMeasurement();
                await NavigateAsync("///home");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(
                    "Upload Failed",
                    $"Something went wrong while submitting your data.\n\nDetails: {ex.Message}",
                    "OK"
                );
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

        private void ClearChart()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                lineChartView.Clear();
                SubmitButton.IsEnabled = false;
                SubmitButton.Text = Localisation.SubmitRecordingButtonNeedData;
            });
        }

        private void OnDoorsWindowsChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;

            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _doorsWindowsState = state;
                RecordingManager.Instance.UpdateRecoverySnapshot(
                    _doorsWindowsState,
                    _ventilationState,
                    NoteEditor.Text
                );
                TemporarilyDisableSubmit();
            }
        }

        private void OnVentilationChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;

            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _ventilationState = state;
                RecordingManager.Instance.UpdateRecoverySnapshot(
                    _doorsWindowsState,
                    _ventilationState,
                    NoteEditor.Text
                );
                TemporarilyDisableSubmit();
            }
        }

        private void ResetPageForNewMeasurement()
        {
            _doorsWindowsState = TriState.Unknown;
            _ventilationState = TriState.Unknown;

            DoorsWindowsUnknownRb.IsChecked = true;
            DoorsWindowsYesRb.IsChecked = false;
            DoorsWindowsNoRb.IsChecked = false;

            VentilationUnknownRb.IsChecked = true;
            VentilationYesRb.IsChecked = false;
            VentilationNoRb.IsChecked = false;

            TrimSlider.Minimum = 0;
            TrimSlider.Maximum = 0;
            TrimSlider.UpperValue = 0;
            TrimSlider.LowerValue = 0;
            TrimSlider.ForceLayout();

            NoteEditor.Text = string.Empty;

            ClearChart();

            MeasuredLocationLabel.Text =
                RecordingManager.Instance.CurrentLocationDisplay ?? string.Empty;

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;

            SubmitButton.IsEnabled = false;
            SubmitButton.Text = Localisation.SubmitRecordingButton;
        }

        private void OnCustomNotesChanged(object sender, TextChangedEventArgs e)
        {
            RecordingManager.Instance.UpdateRecoverySnapshot(
                _doorsWindowsState,
                _ventilationState,
                NoteEditor.Text
            );
            TemporarilyDisableSubmit();
        }
    }
}
