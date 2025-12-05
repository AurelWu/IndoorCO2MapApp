using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Resources.Strings;
using Microsoft.Maui.Controls;
using Microsoft.VisualBasic;
using IndoorCO2MapAppV2.ViewModels;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class SettingsPage : AppPage
    {
        private readonly SettingsViewModel _settingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();
            _settingsViewModel = SettingsViewModel.Instance;
            BindingContext = _settingsViewModel; //

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