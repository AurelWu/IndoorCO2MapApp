using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Utility;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class BuildingSearchViewModel : ObservableObject
    {
        private readonly OverpassDataFetcher _fetcher = OverpassDataFetcher.Instance;
        private readonly ILocationService _locationService;
        private readonly LocationStore _locationStore = LocationStore.Instance;
        private DateTime? _lastGpsFixTime;

        public BuildingSearchViewModel()
        {
            _locationService = LocationServicePlatformProvider.CreateOrUse();
            Range = 100;

            // React to Overpass fetch state
            FetchState.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(FetchState));
                OnPropertyChanged(nameof(CanSearch));
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

        public bool IsFavourited =>
            SelectedBuilding != null &&
            UserSettings.Instance.FavouriteLocationKeys.Contains(SelectedBuilding.FavouriteKey);

        public Color StarColor => IsFavourited ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");

        public ICommand ToggleFavouriteCommand => new Command(() =>
        {
            if (SelectedBuilding == null) return;
            var key = SelectedBuilding.FavouriteKey;
            var keys = new List<string>(UserSettings.Instance.FavouriteLocationKeys);
            if (!keys.Remove(key)) keys.Add(key);
            UserSettings.Instance.FavouriteLocationKeys = keys;
            OnPropertyChanged(nameof(IsFavourited));
            OnPropertyChanged(nameof(StarColor));
            RefreshBuildings(preserveSelection: true);
        });

        public bool HasValidGPS =>
            Latitude is double lat &&
            Longitude is double lon &&
            lat != 0 && lon != 0;

        public bool CanSearch => HasValidGPS && !FetchState.IsFetching;

        public string SearchButtonText => HasValidGPS
            ? Localisation.SearchBuildingButtonLabel_Ready
            : "Waiting for GPS...";

        // ---------------------------
        // GPS
        // ---------------------------

        public async Task GetGpsAsync()
        {
            // Skip if we already have a recent valid fix (< 60 s old)
            if (HasValidGPS && _lastGpsFixTime.HasValue &&
                (DateTime.UtcNow - _lastGpsFixTime.Value).TotalSeconds < 60)
                return;

#if WINDOWS
            // Fake coordinates for desktop debugging
            Latitude = 51.332506;
            Longitude = 12.373544;
            _lastGpsFixTime = DateTime.UtcNow;
            Status = $"GPS (mocked on Windows): {Latitude:F6}, {Longitude:F6}";
            Logger.WriteToLog(FormattableString.Invariant($"GPS (mocked on Windows): {Latitude:F6}, {Longitude:F6}"));
            OnPropertyChanged(nameof(HasValidGPS));
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(SearchButtonText));
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
            _lastGpsFixTime = DateTime.UtcNow;

            Status = $"GPS OK: {Latitude:F6}, {Longitude:F6}";
            Logger.WriteToLog(FormattableString.Invariant($"GPS OK: {Latitude:F6}, {Longitude:F6}"));
            OnPropertyChanged(nameof(HasValidGPS));
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(SearchButtonText));
        }

        // ---------------------------
        // Fetch buildings from Overpass
        // ---------------------------

        public async Task SearchBuildingsAsync()
        {
            Status = "Acquiring GPS...";
            FetchState.IsFetching = true;
            FetchState.LastFailed = false;
            FetchState.LastError = null;
            try
            {
                await GetGpsAsync();
                if (!HasValidGPS)
                {
                    FetchState.LastError = "Unable to get GPS position.";
                    FetchState.LastFailed = true;
                    Status = "No valid GPS data yet.";
                    return;
                }

                double lat = Latitude!.Value;
                double lon = Longitude!.Value;

                if (UserSettings.Instance.UseLiveLocationService)
                {
                    await SearchBuildingsOverpassAsync(lat, lon);
                }
                else
                {
                    await SearchBuildingsPMTilesAsync(lat, lon);
                }
            }
            finally
            {
                FetchState.IsFetching = false;
            }
        }

        private async Task SearchBuildingsOverpassAsync(double lat, double lon)
        {
            Status = "Fetching buildings...";

            string query = OverpassQueryBuilder.CreateBuildingOverpassQuery(lat, lon, Range);
            string? json = await _fetcher.FetchOverpassDataAsync(query);
            if (json == null)
            {
                Status = $"Fetch failed: {FetchState.LastError}";
                return;
            }

            Status = "Fetch OK, parsing...";

            var result = OverpassDataParser.ParseBuildingLocationOverpassResponse(
                json, lat, lon);

            if (UserSettings.Instance.EnableLocationCaching)
            {
                foreach (var location in result)
                {
                    try { await App.LocationCacheDb.InsertOrReplaceAsync(location); }
                    catch (Exception ex) { Logger.WriteToLog($"Failed to cache location {location.ID}: {ex.Message}"); }
                }
            }

            RefreshBuildings();
            Status = $"Parsed {Buildings.Count} buildings.";
        }

        private async Task SearchBuildingsPMTilesAsync(double lat, double lon)
        {
            Status = "Searching buildings (PMTiles)...";
            try
            {
                var results = await PMTilesLocationService.Instance.SearchAsync(lat, lon, Range);

                if (UserSettings.Instance.EnableLocationCaching)
                {
                    foreach (var location in results)
                    {
                        try { await App.LocationCacheDb.InsertOrReplaceAsync(location); }
                        catch (Exception ex) { Logger.WriteToLog($"Failed to cache location {location.ID}: {ex.Message}"); }
                    }
                }

                LocationStore.Instance.SetBuildingLocations(results.ToHashSet());
                RefreshBuildings();
                Status = $"Found {Buildings.Count} buildings.";
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"PMTiles search failed: {ex.Message}");
                FetchState.LastFailed = true;
                FetchState.LastError = ex.Message;
                Status = $"Search failed: {ex.Message}";
            }
        }

        // ---------------------------
        // Filtering + Sorting
        // ---------------------------

        public void RefreshBuildings(bool preserveSelection = false)
        {
            if (_locationStore.BuildingLocationData == null)
                return;

            var previousSelection = preserveSelection ? SelectedBuilding : null;

            IEnumerable<LocationData> data = _locationStore.BuildingLocationData;

            // Base sort
            data = SortAlphabetical
                ? data.OrderBy(b => b.Name ?? "")
                : data.OrderBy(b => b.Distance);

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

            // Favourites first, preserving base sort order within each group
            var favKeys = UserSettings.Instance.FavouriteLocationKeys;
            var list = data.ToList();
            var sorted = list.Where(b => favKeys.Contains(b.FavouriteKey))
                             .Concat(list.Where(b => !favKeys.Contains(b.FavouriteKey)));

            Buildings.Clear();
            foreach (var b in sorted)
                Buildings.Add(b);

            // Restore previous selection if requested; fall back to first otherwise
            SelectedBuilding = (previousSelection != null && Buildings.Contains(previousSelection))
                ? previousSelection
                : Buildings.FirstOrDefault();
        }

        // Reactive updates
        partial void OnSortAlphabeticalChanged(bool value) => RefreshBuildings();
        partial void OnFilterTextChanged(string value) => RefreshBuildings();
        partial void OnSelectedBuildingChanged(LocationData? value)
        {
            OnPropertyChanged(nameof(IsFavourited));
            OnPropertyChanged(nameof(StarColor));
        }



        public ICommand SortModeChangedCommand => new Command<string>(mode =>
        {
            SortAlphabetical = (mode == Localisation.Sort_Alphabetical);
            RefreshBuildings();
        });
    }
}
