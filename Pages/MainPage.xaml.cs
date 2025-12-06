using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace IndoorCO2MapAppV2.Pages
{
    //TODO: LocationPicker => picking something doesnt actually do anything yet I think, need to store

    public partial class MainPage : AppPage
    {
        private readonly MainPageViewModel _mainPageViewModel;

        private bool sortAlphabetical = false;                                                                                          

        public MainPage()
        {
            InitializeComponent();
            _mainPageViewModel = new MainPageViewModel();
            BindingContext = _mainPageViewModel;

            CO2MonitorPicker.ItemsSource = _mainPageViewModel.Sensor.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;

            sortAlphabetical = UserSettings.Instance.SortBuildingsAlphabetical;

            // Populate locations *after* constructor
            //PopulateLocationPicker(sortAlphabetical);
        }

        // Update after GPS or building search
        private async Task SearchBuildingsAsync()
        {
            await _mainPageViewModel.BuildingSearch.GetGpsAsync();
            await _mainPageViewModel.BuildingSearch.SearchBuildingsAsync();

            //PopulateLocationPicker(sortAlphabetical);
        }


        //private void PopulateLocationPicker(bool sortAlphabetical, string filter = "")
        //{
        //    LocationPicker.Items.Clear();
        //
        //    var store = LocationStore.Instance;
        //
        //    IEnumerable<LocationData> sorted =
        //        sortAlphabetical == true
        //            ? store.GetBuildingsSortedByName()
        //            : store.GetBuildingsSortedByDistance();
        //
        //    string f = filter?.Trim() ?? "";
        //    bool useFilter = f.Length > 0;
        //    if (FilterEntry.IsVisible == false) useFilter = false; // avoids leftover strings during a session if user changed settings to invisibly affect results
        //
        //    foreach (var loc in sorted)
        //    {
        //        if (useFilter)
        //        {
        //            if (string.IsNullOrWhiteSpace(loc.Name))
        //                continue;
        //
        //            string name = Helpers.RemoveDiacritics(loc.Name);
        //
        //            if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
        //                continue;
        //        }
        //
        //        LocationPicker.Items.Add($"{loc.Name ?? "(no name)"} — {loc.Distance:F0} m");
        //    }
        //
        //    if (LocationPicker.Items.Count > 0)
        //        LocationPicker.SelectedIndex = 0;
        //}


        //private void OnSortingChanged(object sender, string selected)
        //{
        //    bool sortAlphabetical = false;
        //    if (selected == Localisation.Sort_Alphabetical) sortAlphabetical = true;
        //    PopulateLocationPicker(sortAlphabetical, FilterEntry.Text);
        //}


        private void OnSearchRangeChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // only when checked

            if (sender == RadioButton100m)
                _mainPageViewModel.BuildingSearch.Range=100;

            if (sender == RadioButton250m)
                _mainPageViewModel.BuildingSearch.Range = 250;
        }


        //to be removed, just if things dont work yet to have revert
        //private void OnSortDistanceClicked(object sender, EventArgs e)
        //{
        //    _sortByDistance = true;
        //    PopulateLocationPicker(); //TODO: add filter text when implemented
        //    AlphaButton.BackgroundColor = UserSettings.Instance.NotPickedToggleButtonColor;
        //    DistanceButton.BackgroundColor = UserSettings.Instance.DefaultButtonColor;
        //}
        //
        //private void OnSortAlphaClicked(object sender, EventArgs e)
        //{
        //    _sortByDistance = false;
        //    PopulateLocationPicker(); //TODO: add filter text when implemented
        //    AlphaButton.BackgroundColor = UserSettings.Instance.DefaultButtonColor;
        //    DistanceButton.BackgroundColor = UserSettings.Instance.NotPickedToggleButtonColor; 
        //}

        //private void OnLocationFilterChanged(object sender, TextChangedEventArgs e)
        //{
        //    PopulateLocationPicker(sortAlphabetical, e.NewTextValue);
        //}

        private void DevicePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (CO2MonitorPicker.SelectedIndex >= 0)
            {
                var device = _mainPageViewModel.Sensor.Devices[CO2MonitorPicker.SelectedIndex];
                _mainPageViewModel.Sensor.SelectDeviceAsync(device).SafeFireAndForget();
            }
        }

        private void OnRefreshSensorListClicked(object sender, EventArgs e)
        {
            RefreshSensorListAsync().SafeFireAndForget();
        }

        private async Task RefreshSensorListAsync()
        {
            await _mainPageViewModel.Sensor.StartScanAsync(_mainPageViewModel.Sensor.SelectedMonitorType);

            var deviceNames = _mainPageViewModel.Sensor.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.ItemsSource = deviceNames;

            if (deviceNames.Count > 0)
            {
                CO2MonitorPicker.SelectedIndex = 0;
                var firstDevice = _mainPageViewModel.Sensor.Devices[0];
                _mainPageViewModel.Sensor.SelectDeviceAsync(firstDevice).SafeFireAndForget();
            }
        }

        private void OnSearchBuildingsClicked(object sender, EventArgs e)
        {
            SearchBuildingsAsync().SafeFireAndForget();
        }
    }
}
