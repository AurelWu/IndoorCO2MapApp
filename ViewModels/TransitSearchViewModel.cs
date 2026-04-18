using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.UIUtility;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class TransitSearchViewModel : ObservableObject
    {
        private readonly LocationStore _locationStore = LocationStore.Instance;

        private double _searchLat;
        private double _searchLon;

        public List<string> ModeFilterOptions { get; } = [Localisation.TransitModeAll, Localisation.TransitModeBus, Localisation.TransitModeTram, Localisation.TransitModeTrain, Localisation.TransitModeLightRail, Localisation.TransitModeSubway];

        public ObservableCollection<LocationData> Stations { get; } = [];
        public ObservableCollection<TransitLineData> FilteredRoutes { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartRecording))]
        private LocationData? selectedStation;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartRecording))]
        private TransitLineData? selectedRoute;

        [ObservableProperty]
        private string routeFilterText = "";

        [ObservableProperty]
        private string modeFilter = Localisation.TransitModeAll;

        [ObservableProperty]
        private string status = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SearchButtonText))]
        private bool isSearching;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRouteGeometry))]
        private RouteGeometry? selectedRouteGeometry;

        [ObservableProperty]
        private bool isLoadingRouteGeometry;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoutePreviewExpanderText))]
        private bool isRoutePreviewExpanded = true;

        // Refreshed from UserSettings on MainPage.OnAppearing so the binding updates
        // when the user toggles the setting and navigates back.
        private bool _showRoutePreview = PersistentData.UserSettings.Instance.ShowRoutePreview;
        public bool ShowRoutePreview
        {
            get => _showRoutePreview;
            set
            {
                if (_showRoutePreview == value) return;
                _showRoutePreview = value;
                OnPropertyChanged();
                if (!value) SelectedRouteGeometry = null;
            }
        }

        public bool HasRouteGeometry => SelectedRouteGeometry != null;
        public string RoutePreviewExpanderText => IsRoutePreviewExpanded ? "▲" : "▼";

        public string SearchButtonText => IsSearching ? Localisation.TransitSearchingButton : Localisation.TransitSearchButton;

        public bool CanStartRecording => SelectedStation != null && SelectedRoute != null;

        public bool IsStationFavourited =>
            SelectedStation != null &&
            UserSettings.Instance.FavouriteLocationKeys.Contains(SelectedStation.FavouriteKey);

        public Color StationStarColor => IsStationFavourited ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");

        public bool IsRouteFavourited
        {
            get
            {
                if (SelectedRoute == null) return false;
                double rLat = Math.Round(_searchLat, 2);
                double rLon = Math.Round(_searchLon, 2);
                return UserSettings.Instance.FavouriteTransitRoutes
                    .Any(f => f.RouteId == SelectedRoute.ID && f.Lat == rLat && f.Lon == rLon);
            }
        }

        public Color RouteStarColor => IsRouteFavourited ? Color.FromArgb("#512BD4") : Color.FromArgb("#BDBDBD");

        public ICommand ToggleStationFavouriteCommand => new Command(() =>
        {
            if (SelectedStation == null) return;
            var key = SelectedStation.FavouriteKey;
            var keys = new List<string>(UserSettings.Instance.FavouriteLocationKeys);
            if (!keys.Remove(key)) keys.Add(key);
            UserSettings.Instance.FavouriteLocationKeys = keys;
            OnPropertyChanged(nameof(IsStationFavourited));
            OnPropertyChanged(nameof(StationStarColor));
            RefreshStations();
        });

        public ICommand ToggleRouteFavouriteCommand => new Command(() =>
        {
            if (SelectedRoute == null) return;
            double rLat = Math.Round(_searchLat, 2);
            double rLon = Math.Round(_searchLon, 2);
            var favs = new List<TransitRouteFavourite>(UserSettings.Instance.FavouriteTransitRoutes);
            var existing = favs.FirstOrDefault(f => f.RouteId == SelectedRoute.ID && f.Lat == rLat && f.Lon == rLon);
            if (existing != null) favs.Remove(existing);
            else favs.Add(new TransitRouteFavourite { RouteId = SelectedRoute.ID, Lat = rLat, Lon = rLon });
            UserSettings.Instance.FavouriteTransitRoutes = favs;
            OnPropertyChanged(nameof(IsRouteFavourited));
            OnPropertyChanged(nameof(RouteStarColor));
            RefreshRoutes(preserveSelection: true);
        });

        public IRelayCommand<string> ModeFilterChangedCommand { get; }

        public TransitSearchViewModel()
        {
            ModeFilterChangedCommand = new RelayCommand<string>(mode =>
            {
                if (mode == null) return;
                ModeFilter = mode;
                RefreshRoutes(preserveSelection: false);
            });
        }

        [RelayCommand]
        private void ToggleRoutePreviewExpanded() => IsRoutePreviewExpanded = !IsRoutePreviewExpanded;

        partial void OnRouteFilterTextChanged(string value) => RefreshRoutes(preserveSelection: true);

        partial void OnSelectedStationChanged(LocationData? value)
        {
            OnPropertyChanged(nameof(IsStationFavourited));
            OnPropertyChanged(nameof(StationStarColor));
        }

        partial void OnSelectedRouteChanged(TransitLineData? value)
        {
            OnPropertyChanged(nameof(IsRouteFavourited));
            OnPropertyChanged(nameof(RouteStarColor));
            SelectedRouteGeometry = null;
            if (value == null || !ShowRoutePreview || UserSettings.Instance.UseLiveLocationService) return;
            IsRoutePreviewExpanded = true;
            LoadRouteGeometryAsync(value.ID).SafeFireAndForget("TransitSearchViewModel|LoadRouteGeometry");
        }

        private async Task LoadRouteGeometryAsync(long routeId)
        {
            IsLoadingRouteGeometry = true;
            try { SelectedRouteGeometry = await RT01RouteService.Instance.FetchRouteGeometryAsync(routeId); }
            catch (Exception ex) { Logger.WriteToLog($"TransitSearchViewModel|LoadRouteGeometryAsync: {ex.Message}"); }
            finally { IsLoadingRouteGeometry = false; }
        }

        public async Task SearchTransitAsync(double lat, double lon, int searchRange, CancellationToken ct = default)
        {
            IsSearching = true;
            Status = "Searching transit…";
            try
            {
                List<LocationData> stations;
                List<TransitLineData> routes;

                if (UserSettings.Instance.UseLiveLocationService)
                {
                    (stations, routes) = await SearchTransitOverpassAsync(lat, lon, searchRange, ct);
                }
                else
                {
                    var result = await PMTilesTransitService.Instance.SearchAsync(lat, lon, searchRange, ct);
                    stations = result.stations;
                    routes = result.routes;
                }

                _searchLat = lat;
                _searchLon = lon;
                TransitRouteDisplayConverter.CurrentSearchLat = lat;
                TransitRouteDisplayConverter.CurrentSearchLon = lon;

                _locationStore.SetTransportStartLocations(stations);
                _locationStore.SetTransitLines(routes);

                RefreshStations();
                RefreshRoutes(preserveSelection: false);

                Status = $"Found {stations.Count} stops, {routes.Count} routes.";
                Logger.WriteToLog($"TransitSearchViewModel|SearchTransitAsync: {stations.Count} stations, {routes.Count} routes");
            }
            catch (OperationCanceledException)
            {
                Status = "Search cancelled.";
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"TransitSearchViewModel|SearchTransitAsync failed: {ex.Message}");
                Status = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task<(List<LocationData> stations, List<TransitLineData> routes)> SearchTransitOverpassAsync(
            double lat, double lon, int searchRange, CancellationToken ct)
        {
            string query = OverpassQueryBuilder.CreateTransportOverpassQuery(lat, lon, searchRange, startLocation: true);
            string? json = await OverpassDataFetcher.Instance.FetchOverpassDataAsync(query, ct);
            if (json == null)
                return ([], []);

            var stations = OverpassDataParser.ParseTransitStopsFromOverpassResponse(json, lat, lon);
            var routes = OverpassDataParser.ParseTransitLinesFromOverpassResponse(json, lat, lon);
            return (stations, routes);
        }

        public void RefreshStations()
        {
            var favKeys = UserSettings.Instance.FavouriteLocationKeys;
            var list = _locationStore.TransportStartLocationData
                .OrderBy(s => s.Distance)
                .ToList();

            var sorted = list.Where(s => favKeys.Contains(s.FavouriteKey))
                             .Concat(list.Where(s => !favKeys.Contains(s.FavouriteKey)));

            Stations.Clear();
            foreach (var s in sorted)
                Stations.Add(s);

            SelectedStation = Stations.FirstOrDefault();
        }

        public void RefreshRoutes(bool preserveSelection)
        {
            var previous = preserveSelection ? SelectedRoute : null;

            string? osm = null;
            if (ModeFilter == Localisation.TransitModeBus)           osm = "bus";
            else if (ModeFilter == Localisation.TransitModeTram)     osm = "tram";
            else if (ModeFilter == Localisation.TransitModeTrain)    osm = "train";
            else if (ModeFilter == Localisation.TransitModeLightRail) osm = "light_rail";
            else if (ModeFilter == Localisation.TransitModeSubway)   osm = "subway";

            IEnumerable<TransitLineData> data = _locationStore.TransitLines;

            if (osm != null)
                data = data.Where(r => string.Equals(r.VehicleType, osm, StringComparison.OrdinalIgnoreCase));

            var ft = RouteFilterText?.Trim() ?? "";
            if (!string.IsNullOrEmpty(ft))
                data = data.Where(r => r.Name.Contains(ft, StringComparison.OrdinalIgnoreCase));

            data = data.OrderBy(r => r.Name);

            double rLat = Math.Round(_searchLat, 2);
            double rLon = Math.Round(_searchLon, 2);
            var favIds = UserSettings.Instance.FavouriteTransitRoutes
                .Where(f => f.Lat == rLat && f.Lon == rLon)
                .Select(f => f.RouteId)
                .ToHashSet();

            var list = data.ToList();
            var sorted = list.Where(r => favIds.Contains(r.ID))
                             .Concat(list.Where(r => !favIds.Contains(r.ID)));

            FilteredRoutes.Clear();
            foreach (var r in sorted)
                FilteredRoutes.Add(r);

            SelectedRoute = (previous != null && FilteredRoutes.Contains(previous))
                ? previous
                : FilteredRoutes.FirstOrDefault();
        }
    }
}
