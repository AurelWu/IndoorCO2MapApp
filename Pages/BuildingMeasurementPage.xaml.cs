using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class BuildingMeasurementPage : AppPage
    {
        public BuildingMeasurementPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            

            RecordingManager.Instance.MeasurementDataUpdated += OnMeasurementUpdated;            
            UpdateChart();
            UpdateSubmitButtonState();
            MeasuredLocationLabel.Text = RecordingManager.Instance.CurrentLocationDisplay;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
        }

        private void OnMeasurementUpdated()
        {            
            MainThread.BeginInvokeOnMainThread(UpdateChart);
        }

        private void UpdateChart()
        {
            UpdateSubmitButtonState();
            var rec = RecordingManager.Instance.ActiveRecording;
            if (rec == null)
                return;

            var data = rec.MeasurementData;
            if (data == null || data.Count == 0)
                return;

            // Update slider range
            TrimSilder.Maximum = data.Count - 1;

            // Keep trim valid
            if (TrimSilder.UpperValue > data.Count - 1)
                TrimSilder.UpperValue = data.Count - 1;

            // Update chart
            lineChartView.SetData(
                data,
                (int)TrimSilder.LowerValue,
                (int)TrimSilder.UpperValue
            );

            SubmitButton.IsEnabled = data.Count >= 5;
        }

        private void OnTrimChanged(object sender, EventArgs e)
        {
            UpdateChart();
            UpdateSubmitButtonState();
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
                RecordingManager.Instance
                    .StopRecordingAsync()
                    .SafeFireAndForget();

                await NavigateAsync("///home");
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CancelMeasurementAsync().SafeFireAndForget();
        }

        private async void OnSubmitRecordingClicked(object sender, EventArgs e)
        {
            var rec = RecordingManager.Instance.ActiveRecording;

            if (rec == null)
            {
                await DisplayAlert("Error", "No recording active.", "OK");
                return;
            }



            int trimStart = (int)TrimSilder.LowerValue;
            int trimEnd = (int)TrimSilder.UpperValue;

            var trimmed = rec.MeasurementData
                .Skip(trimStart)
                .Take(trimEnd - trimStart + 1)
                .ToList();

            // Stub: this is where submission later goes
            await SubmitRecordingStubAsync(trimmed);

            //await DisplayAlert(
            //    Localisation.SubmitRecordingSuccessTitle,
            //    Localisation.SubmitRecordingSuccessMessage,
            //    "OK"
            //);

            // Stop recording and go home
            RecordingManager.Instance.StopRecordingAsync().SafeFireAndForget();
            await NavigateAsync("///home");
        }

        private Task SubmitRecordingStubAsync(List<CO2Reading> trimmed)
        {
            // TODO: implement actual upload later
            System.Diagnostics.Debug.WriteLine($"Submitting {trimmed.Count} data points...");
            return Task.CompletedTask;
        }

        private void UpdateSubmitButtonState()
        {
            var rec = RecordingManager.Instance.ActiveRecording;

            if (TrimSilder == null) return;
            int trimStart = (int)TrimSilder.LowerValue;
            int trimEnd = (int)TrimSilder.UpperValue;

            var trimmed = rec.MeasurementData
                .Skip(trimStart)
                .Take(trimEnd - trimStart + 1)
                .ToList();


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
        }
    }
}
