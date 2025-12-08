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

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            double targetWidth = width * 0.80;      // 80% of screen width
            double sliderWidth = targetWidth - 25;  // minus 25px for the RangeSlider

            lineChartView.WidthRequest = targetWidth;
            TrimSlider.WidthRequest = sliderWidth;
            TrimSlider.ForceLayout();
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
            TrimSlider.Maximum = data.Count - 1;

            // Keep trim valid
            if (TrimSlider.UpperValue > data.Count - 1)
                TrimSlider.UpperValue = data.Count - 1;

            // Update chart
            lineChartView.SetData(
                data,
                (int)TrimSlider.LowerValue,
                (int)TrimSlider.UpperValue
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
           SubmitRecordingAsync().SafeFireAndForget();
        }

        private async Task SubmitRecordingAsync()
        {
            var rec = RecordingManager.Instance.ActiveRecording;

            var builder = new APISubmissionBuilder(
                rec,
                trimMin: (int)TrimSlider.LowerValue,
                trimMax: (int)TrimSlider.UpperValue
            );

            var submission = builder.Build();

            string json = submission.ToJson(
                (int)TrimSlider.LowerValue,
                (int)TrimSlider.UpperValue
            );

            await Co2ApiGatewayClient.SubmitAsync(json, SubmissionMode.Building);

            var activeRecording = RecordingManager.Instance!.ActiveRecording!;

            var persistentRecording = new PersistentData.PersistentRecording
            {
                DateTime = activeRecording.RecordingStart,
                LocationName = activeRecording.LocationName,
                NWRId = activeRecording.NwrId,
                NWRType = activeRecording.NwrType,
                AvgCO2 = activeRecording.MeasurementData.Average(x => x.Ppm),
                Values = string.Join(";", activeRecording.MeasurementData.Select(x=>x.Ppm))
            };

            await App.Database.SaveRecordingAsync(persistentRecording);


            //display success message and return to home
        }

        private void UpdateSubmitButtonState()
        {
            var rec = RecordingManager.Instance.ActiveRecording;

            if (TrimSlider == null) return;
            int trimStart = (int)TrimSlider.LowerValue;
            int trimEnd = (int)TrimSlider.UpperValue;

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
