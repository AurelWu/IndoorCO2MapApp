using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.CO2Monitors;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public static HistoryDatabase HistoryDatabase { get; private set; }
        public static LocationCacheDatabase LocationCacheDb { get; private set; }
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
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Destroying += async (s, e) =>
            {
                await CO2MonitorManager.Instance.DisconnectAsync();
            };

            return window;
        }

        public static void ImportDB(HistoryDatabase db)
        {
            HistoryDatabase = db;
        }
    }
}