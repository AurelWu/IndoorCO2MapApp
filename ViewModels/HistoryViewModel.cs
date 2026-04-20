using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Windows.Input;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Resources.Strings;

namespace IndoorCO2MapAppV2.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private const string DeleteEndpoint = "https://nlg29ka74k.execute-api.eu-central-1.amazonaws.com/DeleteLastSubmission";

        private List<CO2RecordingItem> _allItems = new();
        private string _filterText = "";
        private bool _isGrouped;
        private bool _sortAlphabetical;

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
                ApplyFilter();
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
                if (_isGrouped)
                    RebuildGroups();
                else
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
                if (_isGrouped) RebuildGroups(); else ApplyFilter();
            }
        }

        public ICommand ToggleExpandCommand { get; }
        public ICommand ToggleGroupExpandCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand DeleteCommand { get; }

        public HistoryViewModel()
        {
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

                    await App.HistoryDatabase.DeleteRecordingAsync(item.Id);
                    _allItems.RemoveAll(i => i.Id == item.Id);
                    ApplyFilter();
                }
                finally
                {
                    item.IsDeleting = false;
                }
            });
        }

        public async Task ReloadRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            _allItems = all.Select(r => new CO2RecordingItem(r)).ToList();
            ApplyFilter();
        }

        private async Task LoadRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            _allItems = all.Select(r => new CO2RecordingItem(r)).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Recordings.Clear();
            var filtered = string.IsNullOrWhiteSpace(_filterText)
                ? _allItems
                : _allItems.Where(r => r.LocationName.Contains(_filterText, StringComparison.OrdinalIgnoreCase)).ToList();

            var sorted = _sortAlphabetical
                ? filtered.OrderBy(r => r.LocationName, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.DateTime)
                : (IEnumerable<CO2RecordingItem>)filtered.OrderByDescending(r => r.DateTime);

            foreach (var item in sorted)
                Recordings.Add(item);

            if (_isGrouped)
                RebuildGroups();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoRecordings)));
        }

        private void RebuildGroups()
        {
            GroupedRecordings.Clear();

            var filter = _filterText?.Trim() ?? "";
            var source = string.IsNullOrWhiteSpace(filter)
                ? _allItems
                : _allItems.Where(r => r.LocationName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var grouped = source
                .GroupBy(r => r.LocationName)
                .Select(g => new LocationGroupItem(
                    g.Key,
                    g.OrderByDescending(r => r.DateTime).ToList()));

            var sortedGroups = _sortAlphabetical
                ? grouped.OrderBy(g => g.LocationName, StringComparer.OrdinalIgnoreCase)
                : grouped.OrderByDescending(g => g.AllRecordings[0].DateTime);

            foreach (var group in sortedGroups)
                GroupedRecordings.Add(group);
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
            }
        }

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

        public List<ushort> PpmList { get; }
        public ushort MinCO2 { get; }
        public ushort MaxCO2 { get; }
        public int DataPoints { get; }
        public Color AvgCO2Color { get; }
        public string ContextSummary { get; }
        public string StatsLine { get; }
        public string OsmId { get; }
        public bool HasOsmId { get; }
        public bool HasContext { get; }
        public bool HasReadings { get; }
        public List<CO2Reading> PpmReadings { get; }
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

            PpmList = Values.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => ushort.TryParse(s, out var v) ? v : (ushort)0)
                .ToList();

            DataPoints = PpmList.Count;
            MinCO2 = PpmList.Count > 0 ? PpmList.Min() : (ushort)0;
            MaxCO2 = PpmList.Count > 0 ? PpmList.Max() : (ushort)0;

            AvgCO2Color = AvgCO2 < 800
                ? Color.FromArgb("#4CAF50")
                : AvgCO2 < 1200
                    ? Color.FromArgb("#FF9800")
                    : Color.FromArgb("#F44336");

            StatsLine = DataPoints > 0
                ? string.Format(Localisation.HistoryStatsLine, MinCO2, MaxCO2, DataPoints)
                : Localisation.HistoryNoData;

            var parts = new List<string>();
            if (DoorWindowState == TriState.Yes) parts.Add(Localisation.ContextWindowsOpen);
            else if (DoorWindowState == TriState.No) parts.Add(Localisation.ContextWindowsClosed);
            if (VentilationState == TriState.Yes) parts.Add(Localisation.ContextVentilated);
            else if (VentilationState == TriState.No) parts.Add(Localisation.ContextNotVentilated);
            if (!string.IsNullOrWhiteSpace(CustomNotes))
                parts.Add(CustomNotes.Length > 40 ? CustomNotes[..40] + "…" : CustomNotes);

            ContextSummary = string.Join(" · ", parts);
            HasContext = parts.Count > 0;
            HasReadings = PpmList.Count > 0;
            OsmId = NWRId.HasValue ? $"{NWRType} {NWRId}" : "";
            HasOsmId = NWRId.HasValue;

            PpmReadings = PpmList
                .Select((v, i) => new CO2Reading(v, i, System.DateTime.MinValue))
                .ToList();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
