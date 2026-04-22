using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Resources.Strings;

namespace IndoorCO2MapAppV2.ViewModels
{
    public class LocationGroupItem : INotifyPropertyChanged
    {
        private const int PageSize = 10;

        // --- Immutable identity ---
        public string LocationName { get; }

        /// <summary>"way 123456789" or "" if no OSM data.</summary>
        public string OsmId { get; }
        public bool HasOsmId { get; }

        /// <summary>All recordings for this location, sorted newest-first.</summary>
        public List<CO2RecordingItem> AllRecordings { get; }

        // --- Aggregate stats ---
        public int TotalCount { get; }
        public string RecordingsCountText => string.Format(Localisation.HistoryRecordingsCount, TotalCount);
        public int TotalPages { get; }
        public string AvgCO2Range { get; }
        public double MeanCO2 { get; }
        public Color AvgCO2Color { get; }
        public string StdDevLabel { get; }
        public bool HasStdDev { get; }
        public string LastSeenAgo => CO2RecordingItem.ToTimeAgo(AllRecordings[0].DateTime);

        // --- Expand state ---
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; Notify(); Notify(nameof(LocationNameWithChevron)); Notify(nameof(ExpandedHeight)); }
        }

        public double ExpandedHeight => _isExpanded ? -1 : 0;

        public string LocationNameWithChevron =>
            $"{LocationName} {(_isExpanded ? "▼" : "▶")}";

        // --- Paging state ---
        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = Math.Clamp(value, 0, TotalPages - 1);

                // Mutate ObservableCollection in-place so BindableLayout sees CollectionChanged
                _currentPageRecordings.Clear();
                foreach (var r in AllRecordings.Skip(_currentPage * PageSize).Take(PageSize))
                    _currentPageRecordings.Add(r);

                NotifyMany(nameof(CurrentPage), nameof(PageReadings),
                           nameof(PageLabel), nameof(HasPrevPage), nameof(HasNextPage));
            }
        }

        private readonly ObservableCollection<CO2RecordingItem> _currentPageRecordings = new();

        /// <summary>Current page recordings — ObservableCollection so BindableLayout updates.</summary>
        public ObservableCollection<CO2RecordingItem> CurrentPageRecordings => _currentPageRecordings;

        /// <summary>PPM reading lists for all recordings on the current page — bound to MultiSeriesReadings.</summary>
        public List<List<CO2Reading>> PageReadings =>
            _currentPageRecordings.Select(r => r.PpmReadings).ToList();

        public string PageLabel => $"{_currentPage + 1} / {TotalPages}";
        public bool HasPrevPage => _currentPage > 0;
        public bool HasNextPage => _currentPage < TotalPages - 1;
        public bool HasPagination => TotalPages > 1;

        // --- Commands ---
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        // --- Constructor ---
        public LocationGroupItem(string locationName, List<CO2RecordingItem> recordings)
        {
            LocationName = locationName;
            AllRecordings = recordings;  // caller ensures newest-first
            TotalCount = recordings.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

            var withOsm = recordings.FirstOrDefault(r => r.NWRId.HasValue);
            OsmId = withOsm != null ? $"{withOsm.NWRType} {withOsm.NWRId}" : "";
            HasOsmId = !string.IsNullOrEmpty(OsmId);

            if (recordings.Count > 0)
            {
                double mean   = recordings.Average(r => r.AvgCO2);
                double stdDev = Math.Sqrt(recordings.Average(r => Math.Pow(r.AvgCO2 - mean, 2)));

                MeanCO2     = mean;
                StdDevLabel = stdDev >= 1.0 ? $"± {stdDev:F0}" : "";
                HasStdDev   = stdDev >= 1.0;
                AvgCO2Range = stdDev < 1.0
                    ? $"{mean:F0} ppm avg"
                    : $"{mean:F0} ± {stdDev:F0} ppm avg";

                AvgCO2Color = mean < 800
                    ? Color.FromArgb("#4CAF50")
                    : mean < 1200
                        ? Color.FromArgb("#FF9800")
                        : Color.FromArgb("#F44336");
            }
            else
            {
                MeanCO2     = 0;
                StdDevLabel = "";
                HasStdDev   = false;
                AvgCO2Range = "";
                AvgCO2Color = Color.FromArgb("#9E9E9E");
            }

            // Populate first page (newest)
            foreach (var r in recordings.Take(PageSize))
                _currentPageRecordings.Add(r);

            NextPageCommand = new Command(() => CurrentPage++);
            PrevPageCommand = new Command(() => CurrentPage--);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void NotifyMany(params string[] names)
        {
            foreach (var name in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
