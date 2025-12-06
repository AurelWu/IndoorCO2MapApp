using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Recording;

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

            // Immediately load initial data (if any)
            UpdateChart();
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
        }

        private void OnTrimChanged(object sender, EventArgs e)
        {
            UpdateChart();
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
    }
}
