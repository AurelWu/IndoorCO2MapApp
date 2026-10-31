using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Windows.Input;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Resources.Strings;

namespace IndoorCO2MapAppV2.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private const string DeleteEndpoint = "https://nlg29ka74k.execute-api.eu-central-1.amazonaws.com/DeleteLastSubmission";

        // Rows are added to the visible collections in chunks as the user scrolls. iOS has no
        // list virtualisation here, so showing everything at once built views for every recording.
        private const int PageSize = 30;

        private List<CO2RecordingItem> _allItems = new();
        private List<CO2RecordingItem> _filteredItems = new();
        private List<LocationGroupItem> _filteredGroups = new();
        private string _filterText = "";
        private bool _isGrouped;
        private bool _sortAlphabetical;
        private CancellationTokenSource? _filterDebounce;

        // What the list was last loaded from, so returning to the page skips an unchanged reload.
        private HistoryDatabase? _loadedFrom;
        private int _loadedVersion;
        private DateTime _loadedAt;

        public ObservableCollection<CO2RecordingItem> Recordings { get; set; } = new();
        public ObservableCollection<LocationGroupItem> GroupedRecordings { get; set; } = new();

        public bool HasNoRecordings => _allItems.Count == 0;

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterText)));
                DebounceFilterAsync().SafeFireAndForget("HistoryViewModel|DebounceFilterAsync");
            }
        }

        public bool IsGrouped
        {
            get => _isGrouped;
            set
            {
                if (_isGrouped == value) return;
                _isGrouped = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGrouped)));
                ApplyFilter();
            }
        }

        public bool SortAlphabetical
        {
            get => _sortAlphabetical;
            set
            {
                if (_sortAlphabetical == value) return;
                _sortAlphabetical = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortAlphabetical)));
                ApplyFilter();
            }
        }

        public ICommand ShowMoreCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand ToggleGroupExpandCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand DeleteCommand { get; }

        public HistoryViewModel()
        {
            ShowMoreCommand = new Command(ShowMore);

            ToggleExpandCommand = new Command<CO2RecordingItem>(item =>
            {
                item.IsExpanded = !item.IsExpanded;
            });

            ToggleGroupExpandCommand = new Command<LocationGroupItem>(group =>
            {
                group.IsExpanded = !group.IsExpanded;
            });

            ExportCommand = new Command(async () =>
            {
                bool result = await App.BackupService.ExportDatabaseAsync();
                await App.Current.MainPage.DisplayAlertAsync(
                    Localisation.HistoryExportDatabaseTitle,
                    result ? Localisation.HistoryExportDatabaseSuccess : Localisation.HistoryExportDatabaseFailure,
                    "OK"
                );
            });

            ImportCommand = new Command(async () =>
            {
                bool result = await App.BackupService.ImportDatabaseAsync();

                if (result)
                {
                    await App.Current.MainPage.DisplayAlertAsync(
                        Localisation.HistoryImportDatabaseSuccessfulTitle,
                        Localisation.HistoryImportDatabaseSuccessfulDescription,
                        "OK"
                    );
                }
                else
                {
                    await App.Current.MainPage.DisplayAlertAsync(
                        Localisation.HistoryImportDatabaseFailureTitle,
                        Localisation.HistoryImportDatabaseFailureDescription,
                        "OK"
                    );
                }
            });

            DeleteCommand = new Command<CO2RecordingItem>(async item =>
            {
                bool hasOnlineId = !string.IsNullOrEmpty(item.SubmissionId);
                string msg = hasOnlineId
                    ? Localisation.HistoryDeleteConfirmOnline
                    : Localisation.HistoryDeleteConfirmLocal;

                bool confirm = await App.Current.MainPage.DisplayAlertAsync(
                    Localisation.HistoryDeleteTitle, msg,
                    Localisation.HistoryDeleteButton, Localisation.HistoryCancelButton);
                if (!confirm) return;

                item.IsDeleting = true;
                try
                {
                    if (hasOnlineId)
                    {
                        try
                        {
                            var content = new StringContent(item.SubmissionId, Encoding.UTF8, "text/plain");
                            await _http.PostAsync(DeleteEndpoint, content);
                        }
                        catch { }
                    }

                    var db = App.HistoryDatabase;
                    bool upToDate = ReferenceEquals(db, _loadedFrom) && db.Version == _loadedVersion;
                    await db.DeleteRecordingAsync(item.Id);
                    if (upToDate) _loadedVersion = db.Version;   // the in-memory list reflects this delete
                    RemoveFromView(item);
                }
                finally
                {
                    item.IsDeleting = false;
                }
            });
        }

        public async Task ReloadRecordingsAsync()
        {
            var db = App.HistoryDatabase;
            int version = db.Version;
            // Unchanged since the last visit: keep the list. Reload anyway after a while so
            // "just now" / "today" labels don't go stale.
            if (ReferenceEquals(db, _loadedFrom) && version == _loadedVersion
                && System.DateTime.UtcNow - _loadedAt < TimeSpan.FromMinutes(15))
                return;

            var all = await db.GetAllRecordingsAsync();
            _loadedFrom = db;
            _loadedVersion = version;
            _loadedAt = System.DateTime.UtcNow;
            _allItems = all.Select(r => new CO2RecordingItem(r)).ToList();
            ApplyFilter();
        }

        // Typing rebuilds the list, so wait for a pause instead of rebuilding on every keystroke.
        private async Task DebounceFilterAsync()
        {
            _filterDebounce?.Cancel();
            var cts = _filterDebounce = new CancellationTokenSource();
            try { await Task.Delay(250, cts.Token); }
            catch (TaskCanceledException) { return; }
            ApplyFilter();
        }

        /// <summary>Recomputes the filtered, sorted result and shows its first chunk. Only the
        /// active view (flat or grouped) is filled.</summary>
        private void ApplyFilter()
        {
            var filter = _filterText?.Trim() ?? "";
            IEnumerable<CO2RecordingItem> source = string.IsNullOrEmpty(filter)
                ? _allItems
                : _allItems.Where(r => r.LocationName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            Recordings.Clear();
            GroupedRecordings.Clear();

            if (_isGrouped)
            {
                var grouped = source
                    .GroupBy(r => r.LocationName)
                    .Select(g => new LocationGroupItem(
                        g.Key,
                        g.OrderByDescending(r => r.DateTime).ToList()));

                _filteredGroups = (_sortAlphabetical
                    ? grouped.OrderBy(g => g.LocationName, StringComparer.OrdinalIgnoreCase)
                    : grouped.OrderByDescending(g => g.AllRecordings[0].DateTime)).ToList();
                _filteredItems = [];
            }
            else
            {
                _filteredItems = (_sortAlphabetical
                    ? source.OrderBy(r => r.LocationName, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.DateTime)
                    : source.OrderByDescending(r => r.DateTime)).ToList();
                _filteredGroups = [];
            }

            ShowMore();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoRecordings)));
        }

        /// <summary>Appends the next chunk of the current result; no-op once everything is shown.</summary>
        public void ShowMore()
        {
            if (_isGrouped)
                AppendNextChunk(_filteredGroups, GroupedRecordings);
            else
                AppendNextChunk(_filteredItems, Recordings);
        }

        private static void AppendNextChunk<T>(List<T> source, ObservableCollection<T> shown)
        {
            int end = Math.Min(source.Count, shown.Count + PageSize);
            for (int i = shown.Count; i < end; i++)
                shown.Add(source[i]);
        }

        /// <summary>Removes a deleted recording in place, so the list keeps its scroll position.</summary>
        private void RemoveFromView(CO2RecordingItem item)
        {
            _allItems.Remove(item);
            if (_filteredItems.Remove(item))
                Recordings.Remove(item);

            int g = _filteredGroups.FindIndex(x => x.AllRecordings.Contains(item));
            if (g >= 0)
            {
                var old = _filteredGroups[g];
                var rest = old.AllRecordings.Where(r => r != item).ToList();
                if (rest.Count == 0)
                {
                    _filteredGroups.RemoveAt(g);
                    if (g < GroupedRecordings.Count) GroupedRecordings.RemoveAt(g);
                }
                else
                {
                    var updated = new LocationGroupItem(old.LocationName, rest);
                    if (old.IsExpanded)
                    {
                        updated.IsExpanded = true;
                        updated.CurrentPage = old.CurrentPage;
                    }
                    _filteredGroups[g] = updated;
                    if (g < GroupedRecordings.Count) GroupedRecordings[g] = updated;
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoRecordings)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }


    public class CO2RecordingItem : PersistentRecording, INotifyPropertyChanged
    {
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocationNameWithChevron)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedHeight)));
            }
        }

        public double ExpandedHeight => _isExpanded ? -1 : 0;

        private bool _isDeleting;
        public bool IsDeleting
        {
            get => _isDeleting;
            set
            {
                _isDeleting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDeleting)));
            }
        }

        public string LocationNameWithChevron =>
            $"{LocationName} {(_isExpanded ? "▼" : "▶")}";

        // The values string is parsed on first use, so only rows that are actually shown pay for it.
        private List<ushort>? _ppmList;
        public List<ushort> PpmList => _ppmList ??= Values.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => ushort.TryParse(s, out var v) ? v : (ushort)0)
            .ToList();
        public ushort MinCO2 => PpmList.Count > 0 ? PpmList.Min() : (ushort)0;
        public ushort MaxCO2 => PpmList.Count > 0 ? PpmList.Max() : (ushort)0;
        public int DataPoints => PpmList.Count;
        public bool HasReadings => PpmList.Count > 0;

        private string? _statsLine;
        public string StatsLine => _statsLine ??= DataPoints > 0
            ? string.Format(Localisation.HistoryStatsLine, MinCO2, MaxCO2, DataPoints)
            : Localisation.HistoryNoData;

        private List<CO2Reading>? _ppmReadings;
        public List<CO2Reading> PpmReadings => _ppmReadings ??= PpmList
            .Select((v, i) => new CO2Reading(v, i, System.DateTime.MinValue))
            .ToList();

        public Color AvgCO2Color { get; }
        public string ContextSummary { get; }
        public string OsmId { get; }
        public bool HasOsmId { get; }
        public bool HasContext { get; }
        public string TimeAgo => ToTimeAgo(DateTime);

        public static string ToTimeAgo(long unixMs)
        {
            var diff = System.DateTime.Now - DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            if (diff.TotalHours < 1)   return Localisation.TimeAgoJustNow;
            if (diff.TotalDays  < 1)   return Localisation.TimeAgoToday;
            if (diff.TotalDays  < 2)   return Localisation.TimeAgoYesterday;
            if (diff.TotalDays  < 7)   return string.Format(Localisation.TimeAgoDaysAgo, (int)diff.TotalDays);
            if (diff.TotalDays  < 14)  return Localisation.TimeAgoOneWeekAgo;
            if (diff.TotalDays  < 30)  return string.Format(Localisation.TimeAgoWeeksAgo, (int)(diff.TotalDays / 7));
            if (diff.TotalDays  < 60)  return Localisation.TimeAgoOneMonthAgo;
            if (diff.TotalDays  < 365) return string.Format(Localisation.TimeAgoMonthsAgo, (int)(diff.TotalDays / 30));
            if (diff.TotalDays  < 730) return Localisation.TimeAgoOneYearAgo;
            return string.Format(Localisation.TimeAgoYearsAgo, (int)(diff.TotalDays / 365));
        }

        public CO2RecordingItem(PersistentRecording r)
        {
            Id = r.Id;
            DateTime = r.DateTime;
            LocationName = r.LocationName;
            AvgCO2 = r.AvgCO2;
            Values = r.Values;
            NWRId = r.NWRId;
            NWRType = r.NWRType;
            DoorWindowState = r.DoorWindowState;
            VentilationState = r.VentilationState;
            CustomNotes = r.CustomNotes;
            SensorType = r.SensorType;
            SubmissionId = r.SubmissionId;

            AvgCO2Color = AvgCO2 < 800
                ? Color.FromArgb("#4CAF50")
                : AvgCO2 < 1200
                    ? Color.FromArgb("#FF9800")
                    : Color.FromArgb("#F44336");

            var parts = new List<string>();
            if (DoorWindowState == TriState.Yes) parts.Add(Localisation.ContextWindowsOpen);
            else if (DoorWindowState == TriState.No) parts.Add(Localisation.ContextWindowsClosed);
            if (VentilationState == TriState.Yes) parts.Add(Localisation.ContextVentilated);
            else if (VentilationState == TriState.No) parts.Add(Localisation.ContextNotVentilated);
            if (!string.IsNullOrWhiteSpace(CustomNotes))
                parts.Add(CustomNotes.Length > 40 ? CustomNotes[..40] + "…" : CustomNotes);

            ContextSummary = string.Join(" · ", parts);
            HasContext = parts.Count > 0;
            OsmId = NWRId.HasValue ? $"{NWRType} {NWRId}" : "";
            HasOsmId = NWRId.HasValue;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
