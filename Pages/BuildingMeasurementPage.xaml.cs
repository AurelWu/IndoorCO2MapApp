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
        private IDispatcherTimer? _trimDebounceTimer;

        private TriState _doorsWindowsState = TriState.Unknown;
        private TriState _ventilationState = TriState.Unknown;

        public BuildingMeasurementPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            InitializeTrimDebounce();

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;

            // Clears chart so previous recording doesn't show
            lineChartView.Clear();

            // UI-safe async initialization
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;
                await UpdateChartAsync();
            });

            // TODO: check activeRecording for recoveryValues to set UI
            var activeRec = RecordingManager.Instance.ActiveRecording!;
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

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;

            if (_trimDebounceTimer != null)
            {
                _trimDebounceTimer.Stop();
                _trimDebounceTimer = null;
            }
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

        private void InitializeTrimDebounce()
        {
            if (_trimDebounceTimer != null)
                return;

            _trimDebounceTimer = Dispatcher.CreateTimer();
            _trimDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
            _trimDebounceTimer.IsRepeating = false;

            _trimDebounceTimer.Tick += (_, __) =>
            {
                _trimDebounceTimer.Stop();
                _ = UpdateChartAsync();
            };
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
                await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.IsEnabled = false);
                return;
            }

            var data = rec.MeasurementData;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TrimSlider.Maximum = data.Count - 1;
                if (TrimSlider.UpperValue > data.Count - 1)
                    TrimSlider.UpperValue = data.Count - 1;

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
            if (_trimDebounceTimer == null)
                return;

            _trimDebounceTimer.Stop();
            _trimDebounceTimer.Start();
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
                RecordingManager.Instance.StopRecordingAsync()
                    .SafeFireAndForget("CancelMeasurementAsync|StopRecordingAsync");

                ResetPageForNewMeasurement();
                Preferences.Set("RecordingState", string.Empty);
                await NavigateAsync("///home");
            }
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

            await MainThread.InvokeOnMainThreadAsync(() => SubmitButton.Text = "Submitting...");

            try
            {
                var rec = RecordingManager.Instance.ActiveRecording;
                if (rec == null) return;

                var builder = new APISubmissionBuilder(
                    rec,
                    trimMin: (int)TrimSlider.LowerValue,
                    trimMax: (int)TrimSlider.UpperValue
                );

                var submission = builder
                    .WithOpenWindowsDoors(_doorsWindowsState)
                    .WithVentilationSystem(_ventilationState)
                    .WithNotes(customNote)
                    .Build();

                await Co2ApiGatewayClient.SubmitAsync(
                    submission.ToJson(),
                    SubmissionMode.Building
                );

                if (UserSettings.Instance.EnableHistory)
                {
                    var persistentRecording = new PersistentRecording
                    {
                        DateTime = rec.RecordingStart,
                        LocationName = rec.LocationName,
                        NWRId = rec.NwrId,
                        NWRType = rec.NwrType,
                        AvgCO2 = rec.MeasurementData.Average(x => x.Ppm),
                        Values = string.Join(";", rec.MeasurementData.Select(x => x.Ppm))
                    };

                    await App.HistoryDatabase.SaveRecordingAsync(persistentRecording);
                }

                await DisplayAlertAsync(
                    "Upload Complete",
                    "Your measurement was successfully submitted.",
                    "OK"
                );

                ResetPageForNewMeasurement();
                Preferences.Set("RecordingState", string.Empty);
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
                    SubmitButton.Text = string.Format(
                        Localisation.SubmitRecordingButtonNeedData,
                        trimmed.Count
                    );
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
                SubmitButton.Text =
                    Localisation.SubmitRecordingButtonNeedData.Replace("{0}", "0");
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
        }
    }
}
