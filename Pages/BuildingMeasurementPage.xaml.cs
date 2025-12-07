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
            MeasuredLocationLabel.Text = Localisation.RecordingLocationLabel + RecordingManager.Instance.ActiveRecording?.LocationName ?? "ID: " + RecordingManager.Instance.ActiveRecording!.NwrType + RecordingManager.Instance.ActiveRecording.NwrId.ToString();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            RecordingManager.Instance.MeasurementDataUpdated -= OnMeasurementUpdated;
        }

        private void OnMeasurementUpdated()
        {
            //MeasuredLocationLabel.Text = Localisation.RecordingLocationLabel + RecordingManager.Instance.ActiveRecording?.LocationName ?? "ID: " + RecordingManager.Instance.ActiveRecording!.NwrType + RecordingManager.Instance.ActiveRecording.NwrId.ToString();
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

        private void OnSubmitRecordingClicked(object sender, EventArgs e)
        {
            //check if 5 data points - if less then do nothing (button should be disabled anyways but should still do sanity check here - and eventually provide feedback even if button should not be in state and tell itself)
        }
    }
}
