using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.ViewModels;
#if !WINDOWS
using Mapsui;
using Mapsui.Layers;
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

        public MapPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
#if WINDOWS
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            NoMapLabel.IsVisible = true;
#else
            var recordings = await App.HistoryDatabase.GetAllRecordingsAsync();
            BuildMap(recordings);
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
#endif
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///history");
            return true;
        }

#if !WINDOWS
        private void BuildMap(List<PersistentRecording> recordings)
        {
            // Group by NWRType+NWRId when available, else by LocationName
            static string GroupKey(PersistentRecording r) =>
                r.NWRId.HasValue ? $"{r.NWRType}:{r.NWRId}" : r.LocationName;

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
                            lat: avgLat, lon: avgLon) as (LocationGroupItem group, double lat, double lon)?;
                })
                .Where(x => x != null)
                .Select(x => x!.Value)
                .ToList();

            var map = new Mapsui.Map();
            map.Navigator.RotationLock = true;
            map.Layers.Add(OpenStreetMap.CreateTileLayer());

            if (groups.Count > 0)
            {
                var pinFeatures   = new List<IFeature>();
                var labelFeatures = new List<IFeature>();

                foreach (var (group, lat, lon) in groups)
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

                map.Layers.Add(new MemoryLayer { Name = "Pins", Features = pinFeatures, Style = null, IsMapInfoLayer = true });
                _labelLayer = new MemoryLayer  { Name = "Labels", Features = labelFeatures, Style = null };
                map.Layers.Add(_labelLayer);

                double cx = groups.Average(g => g.lon);
                double cy = groups.Average(g => g.lat);
                var (mx, my) = SphericalMercator.FromLonLat(cx, cy);
                map.Home = n =>
                {
                    n.CenterOn(new MPoint(mx, my));
                    n.ZoomTo(n.Resolutions[14]);
                };
            }

            _mapControl = new Mapsui.UI.Maui.MapControl { Map = map };
            MapContainer.Content = _mapControl;

            _mapControl.Info += (_, args) =>
            {
                var group = args.MapInfo?.Feature?["group"] as LocationGroupItem;
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
            _mapControl.Map.RefreshGraphics();
#endif
        }
    }
}
