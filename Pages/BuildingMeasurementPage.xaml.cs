using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.DataUpload;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.PersistentData;


namespace IndoorCO2MapAppV2.Pages
{
    public partial class BuildingMeasurementPage : AppPage
    {
        private CancellationTokenSource _trimCts;

        private TriState _doorsWindowsState = TriState.Unknown;
        private TriState _ventilationState = TriState.Unknown;

        public BuildingMeasurementPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

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

            //TODO: check activeRecording for recoveryValues to set UI
            var activeRec = RecordingManager.Instance.ActiveRecording!; //this should never be null, if it is then there is a bug somewhere needing fixing
            TriState windowState = activeRec.DoorWindowState;
            TriState ventilationState = activeRec.VentilationState;
            string customNotes = activeRec.CustomNotes;
            if (windowState == TriState.Unknown)
            {
                DoorsWindowsUnknownRb.IsChecked = true;
                DoorsWindowsNoRb.IsChecked = false;
                DoorsWindowsYesRb.IsChecked = false;
            }
            else if(windowState == TriState.No)
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
            // debounce rapid slider changes
            _trimCts?.Cancel();
            _trimCts = new CancellationTokenSource();
            var token = _trimCts.Token;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(150, token);
                    if (!token.IsCancellationRequested)
                        await UpdateChartAsync();
                }
                catch (TaskCanceledException) { }
            });
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
                RecordingManager.Instance.StopRecordingAsync().SafeFireAndForget();
                ResetPageForNewMeasurement();
                Preferences.Set("RecordingState", string.Empty);
                await NavigateAsync("///home");
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CancelMeasurementAsync().SafeFireAndForget();
        }

        private void OnSubmitRecordingClicked(object sender, EventArgs e)
        {
            SubmitRecordingAsync().SafeFireAndForget();
        }

        private async Task SubmitRecordingAsync() //this maybe should be in the RecordingManager eventually...
        {
            if(UserSettings.Instance.ConfirmUpload)
            {
                bool answer = await DisplayAlertAsync(
                "Submit Measurement",
                "Are you sure you want to submit the measurement",
                "Yes",
                "No"
                );

                if (!answer)
                {
                    return;
                }
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
                string json = submission.ToJson();

                await Co2ApiGatewayClient.SubmitAsync(json, SubmissionMode.Building);

                if(UserSettings.Instance.EnableHistory)
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
                Preferences.Set("RecordingState", string.Empty); //order is important, must be after ResetPageForNewMeasurement, resetting changes the UI elements which triggers snapshot update (which maybe should be done differently to avoid such issues)
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
            // Make sure this runs on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                lineChartView.Clear(); // <-- your chart control should have a Clear() or similar method
                SubmitButton.IsEnabled = false;
                SubmitButton.Text = Localisation.SubmitRecordingButtonNeedData.Replace("{0}", "0");
            });
        }

        private void OnDoorsWindowsChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // Only handle when a RadioButton becomes checked

            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _doorsWindowsState = state;
                System.Diagnostics.Debug.WriteLine($"Doors/Windows: {_doorsWindowsState}");
                RecordingManager.Instance.UpdateRecoverySnapshot(_doorsWindowsState, _ventilationState, NoteEditor.Text);
            }
        }

        private void OnVentilationChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;

            if (sender is RadioButton rb && rb.Value is TriState state)
            {
                _ventilationState = state;
                System.Diagnostics.Debug.WriteLine($"Ventilation: {_ventilationState}");
                RecordingManager.Instance.UpdateRecoverySnapshot(_doorsWindowsState, _ventilationState, NoteEditor.Text);
            }
        }

        private void ResetPageForNewMeasurement()
        {
            // Reset TriStates
            _doorsWindowsState = TriState.Unknown;
            _ventilationState = TriState.Unknown;

            // Reset RadioButtons
            DoorsWindowsUnknownRb.IsChecked = true;
            DoorsWindowsYesRb.IsChecked = false;
            DoorsWindowsNoRb.IsChecked = false;

            VentilationUnknownRb.IsChecked = true;
            VentilationYesRb.IsChecked = false;
            VentilationNoRb.IsChecked = false;


            // Reset UI elements
            TrimSlider.Minimum = 0;
            TrimSlider.UpperValue = 0;
            TrimSlider.LowerValue = 0;

            TrimSlider.ForceLayout();

            NoteEditor.Text = string.Empty;

            ClearChart();


            MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay ?? string.Empty;


            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;


            SubmitButton.IsEnabled = false;            
            SubmitButton.Text = Localisation.SubmitRecordingButton;
        }

        private void OnCustomNotesChanged(object sender, TextChangedEventArgs e)
        {
            RecordingManager.Instance.UpdateRecoverySnapshot(_doorsWindowsState, _ventilationState, NoteEditor.Text);
        }
    }
}
