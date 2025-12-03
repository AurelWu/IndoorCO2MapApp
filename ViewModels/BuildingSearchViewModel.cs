using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class BuildingSearchViewModel : ObservableObject
    {

        private readonly OverpassDataFetcher _fetcher = OverpassDataFetcher.Instance;
        private readonly ILocationService _locationService;

        private readonly LocationStore _locationStore = LocationStore.Instance;

        public BuildingSearchViewModel()
        {
            _locationService = LocationServicePlatformProvider.CreateOrUse();

            Range = 100;

            // reactive fetch state
            FetchState.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(FetchState));
            };
        }


        public OverpassFetchState FetchState => _fetcher.State;

        // ---------------------------
        // Bindable properties
        // ---------------------------

        [ObservableProperty]
        private double? latitude;

        [ObservableProperty]
        private double? longitude;

        [ObservableProperty]
        private int range;

        [ObservableProperty]
        private string status = "";

        public bool HasValidGPS =>
            Latitude is double lat &&
            Longitude is double lon &&
            lat != 0 && lon != 0;

        // This collection is ONLY for binding to the picker / UI list
        // It is NOT used for data storage anymore
        public ObservableCollection<LocationData> Buildings { get; } = new();


        // ---------------------------
        // Methods
        // ---------------------------

        public async Task GetGpsAsync()
        {
            Status = "Acquiring GPS...";

            var loc = await _locationService.GetCurrentLocationAsync();
            if (loc == null)
            {
                Status = "Unable to get GPS.";
                return;
            }

            Latitude = loc.Latitude;
            Longitude = loc.Longitude;

            Status = $"GPS OK: {Latitude:F6}, {Longitude:F6}";
            OnPropertyChanged(nameof(HasValidGPS));
        }

        public async Task SearchBuildingsAsync()
        {
            await GetGpsAsync();
            if (!HasValidGPS)
            {
                Status = "No valid GPS data yet.";
                return;
            }
            
            Status = "Fetching buildings...";

            string query = OverpassQueryBuilder.CreateBuildingOverpassQuery(
                Latitude!.Value,
                Longitude!.Value,
                Range
            );

            string? json = await _fetcher.FetchOverpassDataAsync(query);
            if (json == null)
            {
                Status = $"Fetch failed: {FetchState.LastError}";
                return;
            }

            Status = "Fetch OK, parsing...";

            OverpassDataParser.ParseBuildingLocationOverpassResponse(
                json,
                Latitude.Value,
                Longitude.Value
            ); //this updates the locationStore

            // UPDATE LOCAL OBSERVABLE COLLECTION FOR UI
            Buildings.Clear();
            foreach (var loc in _locationStore.BuildingLocationData)
            {
                if (loc != null)
                    Buildings.Add(loc);
            }

            Status = $"Parsed {Buildings.Count} buildings.";
        }
    }
}
