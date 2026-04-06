using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;
using System.Collections.ObjectModel;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class TransitSearchViewModel : ObservableObject
    {
        private readonly LocationStore _locationStore = LocationStore.Instance;

        public List<string> ModeFilterOptions { get; } = ["All", "Bus", "Tram", "Train", "LightRail", "Subway"];

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
        private string modeFilter = "All";

        [ObservableProperty]
        private string status = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SearchButtonText))]
        private bool isSearching;

        [ObservableProperty]
        private RouteGeometry? selectedRouteGeometry;

        [ObservableProperty]
        private bool isLoadingRouteGeometry;

        public string SearchButtonText => IsSearching ? "Searching…" : "Search Transit";

        public bool CanStartRecording => SelectedStation != null && SelectedRoute != null;

        public IRelayCommand<string> ModeFilterChangedCommand { get; }

        public TransitSearchViewModel()
        {
            ModeFilterChangedCommand = new RelayCommand<string>(mode =>
            {
                if (mode == null) return;
                ModeFilter = mode;
                RefreshRoutes(preserveSelection: true);
            });
        }

        partial void OnRouteFilterTextChanged(string value) => RefreshRoutes(preserveSelection: true);

        partial void OnSelectedRouteChanged(TransitLineData? value)
        {
            SelectedRouteGeometry = null;
            if (value == null) return;
            LoadRouteGeometryAsync(value.ID).SafeFireAndForget("TransitSearchViewModel|LoadRouteGeometry");
        }

        private async Task LoadRouteGeometryAsync(long routeId)
        {
            IsLoadingRouteGeometry = true;
            try { SelectedRouteGeometry = await RT01RouteService.Instance.FetchRouteGeometryAsync(routeId); }
            catch (Exception ex) { Logger.WriteToLog($"TransitSearchViewModel|LoadRouteGeometryAsync: {ex.Message}"); }
            finally { IsLoadingRouteGeometry = false; }
        }

        public async Task SearchTransitAsync(double lat, double lon, int rangeMeters, CancellationToken ct = default)
        {
            IsSearching = true;
            Status = "Searching transit…";
            try
            {
                var (stations, routes) = await PMTilesTransitService.Instance.SearchAsync(lat, lon, rangeMeters, ct);

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

        public void RefreshStations()
        {
            var data = _locationStore.TransportStartLocationData
                .OrderBy(s => s.Distance);

            Stations.Clear();
            foreach (var s in data)
                Stations.Add(s);

            SelectedStation = Stations.FirstOrDefault();
        }

        public void RefreshRoutes(bool preserveSelection)
        {
            var previous = preserveSelection ? SelectedRoute : null;

            var osm = ModeFilter switch
            {
                "Bus" => "bus",
                "Tram" => "tram",
                "Train" => "train",
                "LightRail" => "light_rail",
                "Subway" => "subway",
                _ => null
            };

            IEnumerable<TransitLineData> data = _locationStore.TransitLines;

            if (osm != null)
                data = data.Where(r => string.Equals(r.VehicleType, osm, StringComparison.OrdinalIgnoreCase));

            var ft = RouteFilterText?.Trim() ?? "";
            if (!string.IsNullOrEmpty(ft))
                data = data.Where(r => r.Name.Contains(ft, StringComparison.OrdinalIgnoreCase));

            data = data.OrderBy(r => r.Name);

            FilteredRoutes.Clear();
            foreach (var r in data)
                FilteredRoutes.Add(r);

            SelectedRoute = (previous != null && FilteredRoutes.Contains(previous))
                ? previous
                : FilteredRoutes.FirstOrDefault();
        }
    }
}
