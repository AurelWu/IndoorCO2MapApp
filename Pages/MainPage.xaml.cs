using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class MainPage : AppPage
    {
        private readonly MainPageViewModel _mainPageViewModel;

        private bool _sortByDistance = true; // same as debug page

        public MainPage()
        {
            InitializeComponent();
            _mainPageViewModel = new MainPageViewModel();
            BindingContext = _mainPageViewModel;

            CO2MonitorPicker.ItemsSource = _mainPageViewModel.Sensor.Devices.Select(d => d.Name).ToList();
            CO2MonitorPicker.SelectedIndexChanged += DevicePicker_SelectedIndexChanged;

            // Populate locations *after* constructor
            PopulateLocationPicker();
        }

        // Update after GPS or building search
        private async Task SearchBuildingsAsync()
        {
            await _mainPageViewModel.BuildingSearch.GetGpsAsync();
            await _mainPageViewModel.BuildingSearch.SearchBuildingsAsync();

            PopulateLocationPicker();
        }


        private void PopulateLocationPicker(string? filter = "")
        {
            var list = _mainPageViewModel.BuildingSearch.Buildings;
            LocationPicker.Items.Clear();

            if (list == null || list.Count == 0)
                return;

            string f = filter?.Trim() ?? "";
            bool useFilter = f.Length > 0;

            IEnumerable<LocationData> sorted =
                _sortByDistance
                    ? list.OrderBy(b => b.Distance)
                    : list.OrderBy(b => Helpers.RemoveDiacritics(b.Name ?? "").ToLower());

            foreach (var loc in sorted)
            {
                if (useFilter)
                {
                    if (string.IsNullOrWhiteSpace(loc.Name))
                        continue;

                    string name = Helpers.RemoveDiacritics(loc.Name);

                    if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                string display = $"{loc.Name ?? "(no name)"} — {loc.Distance:F0} m";
                LocationPicker.Items.Add(display);
            }

            if (LocationPicker.Items.Count > 0)
                LocationPicker.SelectedIndex = 0;
        }


        private void OnSearchRangeChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // only when checked

            if (sender == RadioButton100m)
                _mainPageViewModel.BuildingSearch.Range=100;

            if (sender == RadioButton250m)
                _mainPageViewModel.BuildingSearch.Range = 250;
        }

        // 
        //  SORTING BUTTONS (TODO))
        // 
        private void OnSortDistanceClicked(object sender, EventArgs e)
        {
            _sortByDistance = true;
            PopulateLocationPicker(); //TODO: add filter text when implemented
            AlphaButton.BackgroundColor = SettingsManager.Instance.Settings.NotPickedToggleButtonColor;
            DistanceButton.BackgroundColor = SettingsManager.Instance.Settings.DefaultButtonColor;
        }

        private void OnSortAlphaClicked(object sender, EventArgs e)
        {
            _sortByDistance = false;
            PopulateLocationPicker(); //TODO: add filter text when implemented
            AlphaButton.BackgroundColor = SettingsManager.Instance.Settings.DefaultButtonColor;
            DistanceButton.BackgroundColor = SettingsManager.Instance.Settings.NotPickedToggleButtonColor; 
        }

        private void OnLocationFilterChanged(object sender, TextChangedEventArgs e)
        {
            PopulateLocationPicker(e.NewTextValue);
        }

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
            RefreshSensorList().SafeFireAndForget();
        }

        private async Task RefreshSensorList()
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
