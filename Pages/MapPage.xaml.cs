using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.ViewModels;
#if !WINDOWS
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling;
#endif

namespace IndoorCO2MapAppV2.Pages
{
    public partial class MapPage : AppPage
    {
#if !WINDOWS
        private Mapsui.UI.Maui.MapControl? _mapControl;
        private MemoryLayer? _labelLayer;
#endif
        private List<PersistentRecording> _allRecordings = new();
        private bool _showTransit = false;

        public MapPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateToggleVisuals();
#if WINDOWS
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            NoMapLabel.IsVisible = true;
#else
            _allRecordings = await App.HistoryDatabase.GetAllRecordingsAsync();
            BuildMap(FilteredRecordings());
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
#endif
        }

        private List<PersistentRecording> FilteredRecordings() =>
            _showTransit
                ? _allRecordings.Where(r => r.IsTransitRecording || r.DestinationLatitude.HasValue).ToList()
                : _allRecordings.Where(r => !r.IsTransitRecording && !r.DestinationLatitude.HasValue).ToList();

        private void OnBuildingsToggleClicked(object sender, EventArgs e)
        {
            if (_showTransit == false) return;
            _showTransit = false;
            UpdateToggleVisuals();
#if !WINDOWS
            BuildMap(FilteredRecordings());
#endif
        }

        private void OnTransitToggleClicked(object sender, EventArgs e)
        {
            if (_showTransit == true) return;
            _showTransit = true;
            UpdateToggleVisuals();
#if !WINDOWS
            BuildMap(FilteredRecordings());
#endif
        }

        private void UpdateToggleVisuals()
        {
            var activeColor   = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#ac99ea") : Color.FromArgb("#512BD4");
            var inactiveColor = Colors.Transparent;
            var activeText    = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#190649") : Colors.White;
            var inactiveText  = Color.FromArgb("#888888");

            BuildingsToggleBtn.BackgroundColor = _showTransit ? inactiveColor : activeColor;
            BuildingsToggleBtn.TextColor       = _showTransit ? inactiveText  : activeText;
            TransitToggleBtn.BackgroundColor   = _showTransit ? activeColor   : inactiveColor;
            TransitToggleBtn.TextColor         = _showTransit ? activeText    : inactiveText;
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///history");
            return true;
        }

#if !WINDOWS
        private void BuildMap(List<PersistentRecording> recordings)
        {
            // Buildings: group by OSM id so multiple visits collapse to one pin.
            // Transit: each unique journey (LocationName encodes route+start+end) is its own entry;
            // grouping by route id would average positions across different start stations.
            static string GroupKey(PersistentRecording r) =>
                r.IsTransitRecording ? r.LocationName
                    : r.NWRId.HasValue ? $"{r.NWRType}:{r.NWRId}"
                    : r.LocationName;

            var groups = recordings
                .GroupBy(GroupKey)
                .Select(g =>
                {
                    var withCoords = g.Where(r => r.Latitude != 0 || r.Longitude != 0).ToList();
                    if (withCoords.Count == 0) return null;
                    double avgLat = withCoords.Average(r => r.Latitude);
                    double avgLon = withCoords.Average(r => r.Longitude);
                    var items = g.Select(r => new CO2RecordingItem(r))
                                 .OrderByDescending(r => r.DateTime).ToList();
                    return (group: new LocationGroupItem(g.First().LocationName, items),
                            lat: avgLat, lon: avgLon,
                            recs: withCoords) as (LocationGroupItem group, double lat, double lon, List<PersistentRecording> recs)?;
                })
                .Where(x => x != null)
                .Select(x => x!.Value)
                .ToList();

            var map = new Mapsui.Map();
            map.Widgets.Clear();
            map.Navigator.RotationLock = true;
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            MemoryLayer? pinLayer = null;
            if (groups.Count > 0)
            {
                var lineFeatures  = new List<IFeature>();
                var pinFeatures   = new List<IFeature>();
                var labelFeatures = new List<IFeature>();

                foreach (var (group, lat, lon, recs) in groups)
                {
                    var (x, y) = SphericalMercator.FromLonLat(lon, lat);

                    // Pin
                    var pin = new PointFeature(new MPoint(x, y));
                    var fillColor = group.MeanCO2 < 800
                        ? Mapsui.Styles.Color.FromArgb(255, 76, 175, 80)
                        : group.MeanCO2 < 1200
                            ? Mapsui.Styles.Color.FromArgb(255, 255, 152, 0)
                            : Mapsui.Styles.Color.FromArgb(255, 244, 67, 54);

                    pin.Styles.Add(new Mapsui.Styles.SymbolStyle
                    {
                        SymbolType  = Mapsui.Styles.SymbolType.Ellipse,
                        Fill        = new Mapsui.Styles.Brush(fillColor),
                        Outline     = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 1.5),
                        SymbolScale = 0.5
                    });
                    pin["group"] = group;
                    pinFeatures.Add(pin);

                    // Transit: draw destination pin + straight line if destination was recorded
                    var destRec = recs
                        .Where(r => r.DestinationLatitude.HasValue && r.DestinationLongitude.HasValue
                                    && (r.DestinationLatitude != 0 || r.DestinationLongitude != 0))
                        .OrderByDescending(r => r.DateTime)
                        .FirstOrDefault();

                    if (destRec != null)
                    {
                        var (dx, dy) = SphericalMercator.FromLonLat(
                            destRec.DestinationLongitude!.Value, destRec.DestinationLatitude!.Value);

                        var lineFeature = new GeometryFeature(
                            new NetTopologySuite.Geometries.GeometryFactory().CreateLineString(new[]
                            {
                                new NetTopologySuite.Geometries.Coordinate(x, y),
                                new NetTopologySuite.Geometries.Coordinate(dx, dy)
                            }));
                        lineFeature.Styles.Add(new Mapsui.Styles.VectorStyle
                        {
                            Line = new Mapsui.Styles.Pen(fillColor, 2)
                        });
                        lineFeatures.Add(lineFeature);

                        // Destination pin: inverted style (white fill, color outline)
                        var destPin = new PointFeature(new MPoint(dx, dy));
                        destPin.Styles.Add(new Mapsui.Styles.SymbolStyle
                        {
                            SymbolType  = Mapsui.Styles.SymbolType.Ellipse,
                            Fill        = new Mapsui.Styles.Brush(Mapsui.Styles.Color.White),
                            Outline     = new Mapsui.Styles.Pen(fillColor, 2),
                            SymbolScale = 0.5
                        });
                        pinFeatures.Add(destPin);
                    }

                    // Label (separate layer so visibility can be toggled)
                    var lbl = new PointFeature(new MPoint(x, y));
                    lbl.Styles.Add(new Mapsui.Styles.LabelStyle
                    {
                        Text      = group.LocationName,
                        ForeColor = Mapsui.Styles.Color.Black,
                        BackColor = new Mapsui.Styles.Brush(
                                        Mapsui.Styles.Color.FromArgb(180, 255, 255, 255)),
                        Offset    = new Mapsui.Styles.Offset(0, -18),
                        Font      = new Mapsui.Styles.Font { Size = 11 }
                    });
                    labelFeatures.Add(lbl);
                }

                map.Layers.Add(new MemoryLayer { Name = "Lines", Features = lineFeatures, Style = null });
                pinLayer = new MemoryLayer { Name = "Pins", Features = pinFeatures, Style = null };
                map.Layers.Add(pinLayer);
                _labelLayer = new MemoryLayer  { Name = "Labels", Features = labelFeatures, Style = null };
                map.Layers.Add(_labelLayer);

                var mostRecent = groups.OrderByDescending(g => g.recs.Max(r => r.DateTime)).First();
                var (mx, my) = SphericalMercator.FromLonLat(mostRecent.lon, mostRecent.lat);
                map.ViewportInitialized += (_, __) =>
                {
                    if (map.Navigator.Resolutions.Count > 14)
                        map.Navigator.CenterOnAndZoomTo(new MPoint(mx, my), map.Navigator.Resolutions[14]);
                };
            }

            _mapControl = new Mapsui.UI.Maui.MapControl { Map = map };
            MapContainer.Content = _mapControl;

            map.Tapped += (_, args) =>
            {
                if (pinLayer == null) return;
                var mapInfo = args.GetMapInfo([pinLayer]);
                var group = mapInfo?.Feature?["group"] as LocationGroupItem;
                if (group == null) return;
                MainThread.BeginInvokeOnMainThread(() => ShowDetailPanel(group));
            };
        }

        private void ShowDetailPanel(LocationGroupItem group)
        {
            DetailLocationName.Text = group.LocationName;
            DetailStats.Text = $"{group.TotalCount} recordings · {group.AvgCO2Range}";
            DetailLastSeen.Text = $"latest: {group.LastSeenAgo}";
            DetailChart.MultiSeriesReadings = group.PageReadings;
            DetailPanel.IsVisible = true;
        }

#endif

        private void OnDetailPanelClose(object sender, EventArgs e)
            => DetailPanel.IsVisible = false;

        private void OnShowLabelsChanged(object sender, CheckedChangedEventArgs e)
        {
#if !WINDOWS
            if (_labelLayer == null || _mapControl == null) return;
            _labelLayer.Enabled = e.Value;
            _mapControl.InvalidateCanvas();
#endif
        }
    }
}
