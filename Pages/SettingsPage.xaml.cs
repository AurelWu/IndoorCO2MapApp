using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Resources.Strings;
using Microsoft.Maui.Controls;
using Microsoft.VisualBasic;
using IndoorCO2MapAppV2.ViewModels;
using System.Text.RegularExpressions;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class SettingsPage : AppPage
    {
        private readonly SettingsViewModel _settingsViewModel;
        private bool _roundGpsInLog = true;

        [GeneratedRegex(@"-?\d{1,3}\.\d{5,}")]
        private static partial Regex GpsCoordinateRegex();

        public SettingsPage()
        {
            InitializeComponent();
            _settingsViewModel = SettingsViewModel.Instance;
            BindingContext = _settingsViewModel; //

        }

        private void OnDeleteRecordingHistoryClicked(object sender, EventArgs e)
        {
            DeleteRecordingHistory().SafeFireAndForget("OnDeleteRecordingHistoryClicked|DeleteRecordingHistory");
        }

        private async Task DeleteRecordingHistory()
        {
            bool answer = await DisplayAlertAsync(
               "Delete History",
               "Are you sure you want to delete your entire  local Recording History?",
               "Yes",
               "No"
               );

            if (!answer)
            {
                return;
            }
            else
            {
                await App.HistoryDatabase.ClearAllRecordingsAsync();
            }
        }


        private void OnDeleteLocationCacheClicked(object sender, EventArgs e)
        {
            DeleteLocationCache().SafeFireAndForget("OnDeleteLocationCacheClicked|DeleteLocationCache");
        }

        private async Task DeleteLocationCache()
        {
            bool answer = await DisplayAlertAsync(
               "Delete Location Cache",
               "Are you sure you want to delete the location cache?",
               "Yes",
               "No"
               );

            if (!answer)
            {
                return;
            }
            else
            {
                await App.LocationCacheDb.ClearAsync();
            }
        }

        private async void OnRoundGpsToggled(object sender, ToggledEventArgs e)
        {
            if (!e.Value)
            {
                bool confirmed = await DisplayAlertAsync(
                    "Privacy Warning",
                    "Disabling GPS rounding may expose your precise location when sharing this log. Are you sure?",
                    "Yes, disable rounding",
                    "Cancel"
                );
                if (!confirmed)
                {
                    RoundGpsSwitch.IsToggled = true;
                    return;
                }
            }
            _roundGpsInLog = e.Value;
        }

        private void OnCopyDebugLogClicked(object sender, EventArgs e)
        {
            CopyDebugLog().SafeFireAndForget("OnCopyDebugLogClicked|CopyDebugLog");
        }

        private async Task CopyDebugLog()
        {
            var log = string.Join("\n", Logger.circularBuffer);

            if (_roundGpsInLog)
                log = GpsCoordinateRegex().Replace(log, m =>
                {
                    if (double.TryParse(m.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double val))
                        return (Math.Round(val / 0.5) * 0.5)
                            .ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    return m.Value;
                });

            await Clipboard.SetTextAsync(log);
            await DisplayAlertAsync("Debug Log Copied", "The debug log has been copied to your clipboard.", "OK");
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///home");
            return true;
        }

        //Select Language Dropdown
        //Default Sorting Mode (Distance, Alphabetical - for Buildings, Transit Stops, Transit Lines separate)
        //Default Search Range
        //Color Scheme
        //Export Recordings
        //Import Recordings
        //(Optional) Signature for Recordings
        //Show Sensor Selection in Main Menu (will always be displayed in Settings Menu)
        //Show Sensor Name filter in Main Menu (will always be displayed in Settings Menu)
        //Show Search Range Selection in Main Menu
        //Show Manual/Guide Button in Main Menu
        //Show Imprint in Main Menu
        //Show Map Preview on Main Menu
        //Copy Crash & Debug Log to Clipboard
        //Version Info Text
        //Optional GPS Search Time Length
        //Optional Bluetooth Search Time Length

    }
}