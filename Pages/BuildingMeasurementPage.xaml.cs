using System;
using Microsoft.Maui.Controls;
using IndoorCO2MapAppV2.ExtensionMethods;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class BuildingMeasurementPage : AppPage
    {
        public BuildingMeasurementPage()
        {
            InitializeComponent();
        }

        private void OnCancelClicked(object sender , EventArgs e)
        {
            CancelMeasurementAsync().SafeFireAndForget();
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
                //TODO set App State instead of just going to Home (maybe going to home should also be part of the App State Change)
                await NavigateAsync("///home");
            }
        }
    }
}