using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.CO2Monitors;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public static HistoryDatabase HistoryDatabase { get; private set; }
        public static LocationCacheDatabase LocationCacheDb { get; private set; }
        public static LocationCacheDatabase TransitStationCacheDb { get; private set; }
        public static TransitLineCacheDatabase TransitLineCacheDb { get; private set; }
        public static DatabaseBackupService BackupService { get; private set; }

        public static string historyDBPath { get; private set; }
        public static string locationCacheDbPath { get; private set; }

        public App()
        {
            IndoorCO2MapAppV2.Resources.Strings.Localisation.Culture =
                new System.Globalization.CultureInfo(UserSettings.Instance.Language);

            InitializeComponent();
            _ = ViewModels.StatusViewModel.FetchAppStatusAsync();
            ViewModels.StatusViewModel.StartPeriodicRefresh();
            _ = Spatial.OverpassQueryBuilder.FetchWhitelistAsync();

            historyDBPath = Path.Combine(FileSystem.AppDataDirectory, "co2data.db3");
            locationCacheDbPath = Path.Combine(FileSystem.AppDataDirectory, "location_cache.db3");
            BackupService = new DatabaseBackupService();
            DatabaseBackupService.ApplyStagedImport();

            HistoryDatabase = new HistoryDatabase(historyDBPath);

            LocationCacheDb = new LocationCacheDatabase(locationCacheDbPath);
            TransitStationCacheDb = new LocationCacheDatabase(
                Path.Combine(FileSystem.AppDataDirectory, "transit_station_cache.db3"));
            TransitLineCacheDb = new TransitLineCacheDatabase(
                Path.Combine(FileSystem.AppDataDirectory, "transit_line_cache.db3"));
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Destroying += async (s, e) =>
            {
                ViewModels.StatusViewModel.Instance.Stop();
                await CO2MonitorManager.Instance.DisconnectAsync();
            };

#if ANDROID
            // Stopped/Resumed map to Android OnStop/OnRestart — the app genuinely going
            // away and coming back. Deliberately not Deactivated, which also fires for
            // permission dialogs and would disconnect spuriously.
            window.Stopped += async (s, e) =>
            {
                // Never while recording: the foreground service exists precisely to hold
                // this connection open in the background.
                if (Recording.RecordingManager.Instance.IsRecording) return;
                await CO2MonitorManager.Instance.SuspendConnectionAsync();
            };

            window.Resumed += async (s, e) =>
                await CO2MonitorManager.Instance.ResumeConnectionAsync();
#endif

            return window;
        }

        public static void ImportDB(HistoryDatabase db)
        {
            HistoryDatabase = db;
        }
    }
}