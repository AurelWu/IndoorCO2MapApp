using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class OverpassRequestDebugPage : AppPage
    {
        private static OverpassDataFetcher Fetcher => OverpassDataFetcher.Instance;
        public static OverpassFetchState FetcherState => Fetcher.State;

        private bool _sortByDistance = true;

        public OverpassRequestDebugPage()
        {
            InitializeComponent();
            BindingContext = FetcherState;

            // Listen to property changes so UI updates automatically
            FetcherState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OverpassFetchState.IsFetching) ||
                    e.PropertyName == nameof(OverpassFetchState.LastFailed) ||
                    e.PropertyName == nameof(OverpassFetchState.LastError) ||
                    e.PropertyName == nameof(OverpassFetchState.UsingAlternative))
                {
                    // force binding update
                    OnPropertyChanged(nameof(FetcherState));
                }
            };
        }

        private void OnSearchBuildingsClicked(object sender, EventArgs e)
        {
            GetBuildingLocationsAsync().SafeFireAndForget();
        }

        private async Task<bool> GetBuildingLocationsAsync()
        {
            double longitude = double.Parse(LongitudeEntry.Text);
            double latitude = double.Parse(LatitudeEntry.Text);
            double range = double.Parse(RangeEntry.Text);

            string query = OverpassQueryBuilder.CreateBuildingOverpassQuery(latitude, longitude, range);

            BuildingsStatusLabel.Text = "Starting fetch...";

            string? json = await Fetcher.FetchOverpassDataAsync(query);

            if (json == null)
            {
                BuildingsStatusLabel.Text = $"Fetch failed. Using alternative: {FetcherState.UsingAlternative}\nError: {FetcherState.LastError}";
                return false;
            }

            BuildingsStatusLabel.Text = $"Fetch succeeded. Using alternative: {FetcherState.UsingAlternative}";

            List<LocationData> locations = OverpassDataParser.ParseBuildingLocationOverpassResponse(json, latitude, longitude);     

            BuildingsStatusLabel.Text += $"\nParsed {locations.Count} locations.";


            PopulatePicker();

            return true;
        }

        private void PopulatePicker(string filter="")
        {
            ResultsPicker.Items.Clear();

            var store = LocationStore.Instance;

            IEnumerable<LocationData> sorted =
                _sortByDistance == false
                    ? store.GetBuildingsSortedByName()
                    : store.GetBuildingsSortedByDistance();

            string f = filter?.Trim() ?? "";
            bool useFilter = f.Length > 0;

            foreach (var loc in sorted)
            {
                if (useFilter)
                {
                    // If the location has no name, it cannot match the filter
                    if (string.IsNullOrWhiteSpace(loc.Name))
                        continue;

                    string name = Helpers.RemoveDiacritics(loc.Name);

                    if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                ResultsPicker.Items.Add($"{loc.Name ?? "(no name)"} — {loc.Distance:F0} m");
            }

            if (ResultsPicker.Items.Count > 0)
                ResultsPicker.SelectedIndex = 0;
        }

        private void UpdateToggleButtons()
        {
            if (_sortByDistance)
            {
                DistanceButton.BackgroundColor = Color.FromArgb("#007AFF");
                DistanceButton.TextColor = Colors.White;

                AlphaButton.BackgroundColor = Color.FromArgb("#E0E0E0");
                AlphaButton.TextColor = Colors.Black;
            }
            else
            {
                AlphaButton.BackgroundColor = Color.FromArgb("#007AFF");
                AlphaButton.TextColor = Colors.White;

                DistanceButton.BackgroundColor = Color.FromArgb("#E0E0E0");
                DistanceButton.TextColor = Colors.Black;
            }
        }

        private void OnSortDistanceClicked(object sender, EventArgs e)
        {
            _sortByDistance = true;
            UpdateToggleButtons();
            PopulatePicker(FilterEntry.Text);
        }

        private void OnSortAlphaClicked(object sender, EventArgs e)
        {            
            _sortByDistance = false;
            UpdateToggleButtons();
            PopulatePicker(FilterEntry.Text);
        }

        private void OnSearchTransitStopsClicked(object sender, EventArgs e)
        {
            // stub
        }

        private void OnSearchTransitLinesClicked(object sender, EventArgs e)
        {
            // stub
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = e.NewTextValue?.Trim() ?? "";
            PopulatePicker(filter);
        }
    }
}
