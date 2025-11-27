using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class OverpassRequestDebugPage : AppPage
    {
        private static OverpassDataFetcher Fetcher => OverpassDataFetcher.Instance;
        public static OverpassFetchState FetcherState => Fetcher.State;

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

            var locations = OverpassDataParser.ParseBuildingLocationOverpassResponse(json, latitude, longitude);

            BuildingsStatusLabel.Text += $"\nParsed {locations.Count} locations.";

            return true;
        }

        private void OnSearchTransitStopsClicked(object sender, EventArgs e)
        {
            // stub
        }

        private void OnSearchTransitLinesClicked(object sender, EventArgs e)
        {
            // stub
        }
    }
}
