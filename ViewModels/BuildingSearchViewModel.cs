using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using IndoorCO2MapAppV2.DebugTools;

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

            // React to Overpass fetch state
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
        private LocationData? selectedBuilding;

        [ObservableProperty]
        private double? latitude;

        [ObservableProperty]
        private double? longitude;

        [ObservableProperty]
        private int range;

        [ObservableProperty]
        private string status = "";

        // User-entered filter text
        [ObservableProperty]
        private string filterText = "";

        // Sorting toggle (true = alphabetical, false = by distance)
        [ObservableProperty]
        private bool sortAlphabetical;

        // Picker item source
        public ObservableCollection<LocationData> Buildings { get; } = new();

        public bool HasValidGPS =>
            Latitude is double lat &&
            Longitude is double lon &&
            lat != 0 && lon != 0;

        // ---------------------------
        // GPS
        // ---------------------------

        public async Task GetGpsAsync()
        {
#if WINDOWS
            // Fake coordinates for desktop debugging
            Latitude = 52.521006;
            Longitude = 13.404944;
            Status = $"GPS (mocked on Windows): {Latitude:F6}, {Longitude:F6}";
            OnPropertyChanged(nameof(HasValidGPS));
            return;
#endif

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

        // ---------------------------
        // Fetch buildings from Overpass
        // ---------------------------

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

            var result = OverpassDataParser.ParseBuildingLocationOverpassResponse( //that method already puts it in location store
                json,
                Latitude.Value,
                Longitude.Value
            );

            foreach (var location in result)
            {
                try
                {
                    await App.LocationCacheDb.InsertOrReplaceAsync(location); //if this is slow we might need to bundle it into a single transaction
                }
                catch (Exception ex)
                {
                    Logger.WriteToLog($"Failed to cache location {location.ID}: {ex.Message}");
                }
            }

            // Now refresh UI list using filter + sorting
            RefreshBuildings();

            Status = $"Parsed {Buildings.Count} buildings.";
        }

        // ---------------------------
        // Filtering + Sorting
        // ---------------------------

        public void RefreshBuildings()
        {
            if (_locationStore.BuildingLocationData == null)
                return;

            IEnumerable<LocationData> data = _locationStore.BuildingLocationData;

            // Sorting
            if (SortAlphabetical)
            {
                data = data.OrderBy(b => b.Name ?? "");
            }
            else
            {
                data = data.OrderBy(b => b.Distance);
            }

            // Filtering
            string ft = FilterText?.Trim() ?? "";
            if (!string.IsNullOrEmpty(ft))
            {
                ft = Helpers.RemoveDiacritics(ft);
                data = data.Where(b =>
                    !string.IsNullOrWhiteSpace(b.Name) &&
                    Helpers.RemoveDiacritics(b.Name)
                        .Contains(ft, StringComparison.OrdinalIgnoreCase));
            }

            // Update observable collection
            Buildings.Clear();
            foreach (var b in data)
                Buildings.Add(b);

            // Auto-select first entry
            SelectedBuilding = Buildings.FirstOrDefault();
        }

        // Reactive updates
        partial void OnSortAlphabeticalChanged(bool value) => RefreshBuildings();
        partial void OnFilterTextChanged(string value) => RefreshBuildings();



        public ICommand SortModeChangedCommand => new Command<string>(mode =>
        {
            SortAlphabetical = (mode == Localisation.Sort_Alphabetical);
            RefreshBuildings();
        });
    }
}
